using System.Collections.Generic;
using System.Text;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.View;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class CharacterController : MonoBehaviour
    {
        const float EdgeEpsilon = 0.001f;

        [SerializeField] Board board;
        [SerializeField] GameObject characterObject;
        [SerializeField] float characterHeightOffset = 0.15f;
        [SerializeField] float faceStepRatio = 0.85f;
        [SerializeField] float maxMoveSpeed = 2.5f;
        [SerializeField] float moveAcceleration = 10f;
        [SerializeField] float rollCenterPullSpeed = 2.5f;
        [SerializeField] float maxStepHeight = 1.5f;
        [SerializeField] float pushHoldDuration = 0.25f;
        [SerializeField] float pushInputAlignment = 0.7f;
        [SerializeField] bool debugPushContact;

        DiceRegistry registry;
        DiceController currentDice;
        Transform characterMount;
        Transform characterTransform;
        CapsuleCollider characterPushCollider;
        bool isTrackingDiceRoll;
        Vector3 rollStartCharacterPosition;
        Vector3 rollStartDiceCenter;
        float rollFixedWorldY;
        float currentSpeed;
        float pushContactTime;
        DiceController pushTargetDice;
        Direction pushDirection;
        bool hasPushDirection;
        DiceController pushFollowDice;
        Direction pushFollowDirection;
        bool isPushFollowing;
        bool isInitialized;
        readonly List<PushContactCandidate> pushCandidates = new();
        readonly StringBuilder pushDebugBuilder = new();
        int debugLastOverlapHitCount = -1;
        int debugLastPushBodyHitCount = -1;
        int debugLastSkippedBusyCount = -1;
        string debugLastCandidateSummary;
        DiceController debugLastPushTarget;
        Direction debugLastPushDirection;
        bool debugLastHadPushDirection;
        float debugLastLoggedContactTime = -1f;
        bool debugLastAnyRolling;

        struct PushContactCandidate {
            public DiceController Dice;
            public Direction Direction;
            public float InputAlignment;
            public float FaceDistance;
        }

        public bool IsOnFloor => currentDice == null;
        public bool IsBusy => currentDice != null && currentDice.IsRolling;
        public Vector2 FacePosition => GetOffsetFromDiceCenter(currentDice, characterTransform != null ? characterTransform.position : Vector3.zero);
        public DiceController CurrentDice => currentDice;

        public void Configure(Board targetBoard, DiceRegistry targetRegistry, DiceController startDice) {
            board = targetBoard;
            registry = targetRegistry;
            SetCurrentDice(startDice);
            Initialize();
        }

        public void Initialize() {
            if (board == null || registry == null) {
                Debug.LogError("CharacterController: Board or DiceRegistry is not assigned.");
                return;
            }

            if (currentDice != null) {
                currentDice.View.EnsureDiceInstance();
                if (currentDice.View.DiceTransform == null) {
                    Debug.LogError("CharacterController: Dice visual is not available.");
                    return;
                }
            }

            EnsureCharacterInstance();
            EnsureCharacterPushCollider();
            currentSpeed = 0f;
            isInitialized = true;

            if (currentDice != null) {
                var center = currentDice.View.DiceTransform.position;
                ApplyWorldPosition(new Vector3(center.x, 0f, center.z));
            } else {
                SnapYToSurface();
            }
        }

        public void OnStandingDiceDissolved(DiceController dissolvedDice) {
            if (!isInitialized || currentDice != dissolvedDice) {
                return;
            }

            MoveToFloorAtCurrentWorldPosition();
        }

        void OnDisable() {
            EndRollTracking();
            UnsubscribeCurrentDice();
            EndPushFollow();
        }

        void Update() {
            if (!isInitialized) {
                return;
            }

            if (isPushFollowing) {
                currentSpeed = 0f;
                return;
            }

            if (currentDice != null && isTrackingDiceRoll && !currentDice.IsRolling) {
                EndRollTracking();
            }

            var input = GetInputDirection();
            var isRolling = currentDice != null && currentDice.IsRolling;

            if (isRolling) {
                UpdateDuringRoll(input);
            } else if (IsOnFloor) {
                UpdateFloorMovement(input);
            } else {
                UpdateDiceMovement(input);
            }
        }

        void LateUpdate() {
            if (!isInitialized) {
                return;
            }

            if (isPushFollowing) {
                 SyncPositionToPushingDice();
                if (pushFollowDice == null || !pushFollowDice.IsRolling) {
                    EndPushFollow();
                }
            }

            if (currentDice != null && currentDice.IsRolling) {
                SyncPositionDuringRoll();
            } else if (currentDice != null) {
                SnapYToSurface();
            }
        }

        void UpdateDiceMovement(Vector2 input) {
            if (input.sqrMagnitude <= 0f) {
                currentSpeed = 0f;
                return;
            }

            input.Normalize();
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxMoveSpeed, moveAcceleration * Time.deltaTime);

            if (currentSpeed <= 0f) {
                return;
            }

            var diceTransform = currentDice.View.DiceTransform;
            if (diceTransform == null) {
                return;
            }

            var edgeLimit = GetEdgeLimit();
            var move = input * (currentSpeed * Time.deltaTime);
            var position = characterTransform.position;
            var nextWorld = position + new Vector3(move.x, 0f, move.y);
            var diceCenter = diceTransform.position;
            var nextOffset = new Vector2(nextWorld.x - diceCenter.x, nextWorld.z - diceCenter.z);

            if (TryTransferToAdjacentDiceAtEdge(nextOffset, edgeLimit, move)) {
                return;
            }

            if (TryRollAtEdge(nextOffset, edgeLimit, move)) {
                return;
            }

            if (TryTransferToFloorAtEdge(nextOffset, edgeLimit, move)) {
                return;
            }

            nextOffset = ClampToFace(nextOffset, edgeLimit);
            ApplyWorldPosition(new Vector3(diceCenter.x + nextOffset.x, 0f, diceCenter.z + nextOffset.y));
        }

        void UpdateFloorMovement(Vector2 input) {
            if (input.sqrMagnitude <= 0f) {
                currentSpeed = 0f;
                ResetPushState();
                return;
            }

            input.Normalize();
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxMoveSpeed, moveAcceleration * Time.deltaTime);

            if (currentSpeed <= 0f) {
                return;
            }

            var move = input * (currentSpeed * Time.deltaTime);
            var currentXZ = GetWorldXZ();
            var nextXZ = ApplyDiceMovementBlock(currentXZ, move);

            if (TryStepOntoDiceFromFloor(nextXZ, move)) {
                if (debugPushContact) {
                    LogPushDebug(
                        "StepOntoDice",
                        $"乗り移りで押しを中断 pos={FormatVector2(GetWorldXZ())} move={FormatVector2(move)}");
                }

                ResetPushState();
                return;
            }

            ApplyWorldPosition(new Vector3(nextXZ.x, 0f, nextXZ.y));
            UpdatePushContact(input);
        }

        void UpdateDuringRoll(Vector2 input) {
            if (!isTrackingDiceRoll) {
                BeginRollTracking();
            }

            currentSpeed = 0f;
        }

        void OnDiceStateChanged(DiceState state) {
            if (!isInitialized) {
                return;
            }

            currentSpeed = 0f;
        }

        void SetCurrentDice(DiceController dice) {
            EndRollTracking();
            UnsubscribeCurrentDice();
            currentDice = dice;
            if (currentDice != null) {
                currentDice.StateChanged += OnDiceStateChanged;
            }
        }

        void UnsubscribeCurrentDice() {
            if (currentDice != null) {
                currentDice.StateChanged -= OnDiceStateChanged;
            }
        }

        void MoveToFloorAtCurrentWorldPosition() {
            EndRollTracking();
            SetCurrentDice(null);
            SnapYToSurface();
        }

        void EnsureCharacterInstance() {
            characterMount = transform;

            if (characterTransform != null) {
                return;
            }

            if (characterObject != null) {
                var instance = Instantiate(characterObject, characterMount);
                instance.name = "CharacterVisual";
                characterTransform = instance.transform;
                return;
            }

            characterTransform = characterMount;
        }

        void EnsureCharacterPushCollider() {
            if (characterPushCollider != null || characterTransform == null) {
                return;
            }

            characterPushCollider = characterTransform.GetComponent<CapsuleCollider>();
            if (characterPushCollider == null) {
                Debug.LogWarning("CharacterController: CapsuleCollider is not assigned on the character prefab.");
                characterPushCollider = characterTransform.gameObject.AddComponent<CapsuleCollider>();
                characterPushCollider.isTrigger = true;
            }
        }

        float GetPushHorizontalRadius() {
            if (characterPushCollider == null) {
                return 0f;
            }

            var bounds = characterPushCollider.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        void GetPushWorldVerticalRange(out float bottomY, out float topY) {
            if (characterPushCollider == null) {
                bottomY = 0f;
                topY = 0f;
                return;
            }

            var bounds = characterPushCollider.bounds;
            bottomY = bounds.min.y;
            topY = bounds.max.y;
        }

        Vector2 GetWorldXZ() {
            if (characterTransform == null) {
                return Vector2.zero;
            }

            var position = characterTransform.position;
            return new Vector2(position.x, position.z);
        }

        float GetSurfaceWorldY() {
            if (IsOnFloor) {
                return board.FloorSurfaceWorldY;
            }

            return currentDice.GetTopSurfaceWorldY();
        }

        void ApplyWorldPosition(Vector3 worldPos) {
            if (characterTransform == null || board == null) {
                return;
            }

            worldPos.y = GetSurfaceWorldY() + characterHeightOffset;
            characterTransform.position = worldPos;
            characterTransform.rotation = Quaternion.identity;
        }

        void SnapYToSurface() {
            if (characterTransform == null || isTrackingDiceRoll) {
                return;
            }

            var position = characterTransform.position;
            position.y = GetSurfaceWorldY() + characterHeightOffset;
            characterTransform.position = position;
            characterTransform.rotation = Quaternion.identity;
        }

        Vector3 ClampToWalkBounds(Vector3 worldPos) {
            if (IsOnFloor) {
                var minX = 0f;
                var minZ = 0f;
                var maxX = (board.Width - 1) * board.CellSize;
                var maxZ = (board.Height - 1) * board.CellSize;
                worldPos.x = Mathf.Clamp(worldPos.x, minX, maxX);
                worldPos.z = Mathf.Clamp(worldPos.z, minZ, maxZ);
                return worldPos;
            }

            if (currentDice?.View.DiceTransform == null) {
                return worldPos;
            }

            var center = currentDice.View.DiceTransform.position;
            var limit = GetEdgeLimit();
            worldPos.x = Mathf.Clamp(worldPos.x, center.x - limit, center.x + limit);
            worldPos.z = Mathf.Clamp(worldPos.z, center.z - limit, center.z + limit);
            return worldPos;
        }

        static Vector2 GetOffsetFromDiceCenter(DiceController dice, Vector3 worldPos) {
            if (dice?.View.DiceTransform == null) {
                return Vector2.zero;
            }

            var center = dice.View.DiceTransform.position;
            return new Vector2(worldPos.x - center.x, worldPos.z - center.z);
        }

        void BeginRollTracking() {
            if (characterTransform == null || currentDice?.View.DiceTransform == null) {
                return;
            }

            rollStartCharacterPosition = characterTransform.position;
            rollStartDiceCenter = currentDice.View.DiceTransform.position;
            rollFixedWorldY = rollStartCharacterPosition.y;
            isTrackingDiceRoll = true;
        }

        void BeginRollTracking(Vector3 characterAnchor, Vector3 diceCenterAnchor) {
            rollStartCharacterPosition = characterAnchor;
            rollStartDiceCenter = diceCenterAnchor;
            rollFixedWorldY = characterAnchor.y;
            isTrackingDiceRoll = true;
        }

        void EndRollTracking() {
            if (!isTrackingDiceRoll) {
                return;
            }

            SyncPositionDuringRoll();
            isTrackingDiceRoll = false;
            SnapYToSurface();
            if (characterTransform != null) {
                characterTransform.rotation = Quaternion.identity;
            }
        }

        void SyncPositionDuringRoll() {
            if (!isTrackingDiceRoll || characterTransform == null || currentDice?.View.DiceTransform == null) {
                return;
            }

            var diceCenter = currentDice.View.DiceTransform.position;
            var delta = diceCenter - rollStartDiceCenter;
            var worldPosition = rollStartCharacterPosition + delta;
            worldPosition.y = rollFixedWorldY;
            characterTransform.position = worldPosition;
            characterTransform.rotation = Quaternion.identity;
        }

        void ResetPushState() {
            pushContactTime = 0f;
            pushTargetDice = null;
            hasPushDirection = false;
        }

        void UpdatePushContact(Vector2 input) {
            var anyRolling = registry == null || registry.AnyRolling();
            if (debugPushContact && anyRolling != debugLastAnyRolling) {
                debugLastAnyRolling = anyRolling;
                LogPushDebug("AnyRolling", anyRolling ? "true（押し無効）" : "false");
            }

            if (anyRolling) {
                ResetPushState();
                return;
            }

            CollectPushCandidates(input, pushCandidates, out var overlapHitCount, out var pushBodyHitCount, out var skippedBusyCount);
            if (pushCandidates.Count == 0) {
                LogPushDebugNoCandidates(input, overlapHitCount, pushBodyHitCount, skippedBusyCount);
                ResetPushState();
                return;
            }

            LogPushDebugCandidates(input, overlapHitCount, pushBodyHitCount, skippedBusyCount);

            var best = pushCandidates[0];
            var targetChanged = pushTargetDice != best.Dice || !hasPushDirection || pushDirection != best.Direction;
            if (targetChanged) {
                LogPushDebugTargetChange(best, pushContactTime);
                pushTargetDice = best.Dice;
                pushDirection = best.Direction;
                hasPushDirection = true;
                pushContactTime = 0f;
            }

            pushContactTime += Time.deltaTime;
            LogPushDebugHoldProgress();
            if (pushContactTime < pushHoldDuration) {
                return;
            }

            LogPushDebug(
                "HoldComplete",
                $"hold={pushContactTime:F3}s candidates={FormatCandidates(pushCandidates)}");

            var slideSucceeded = false;
            foreach (var candidate in pushCandidates) {
                if (candidate.Dice.TrySlide(candidate.Direction)) {
                    slideSucceeded = true;
                    LogPushDebug("TrySlide", $"OK {FormatCandidate(candidate)}");
                    BeginPushFollow(candidate.Dice, candidate.Direction);
                    break;
                }

                LogPushDebug("TrySlide", $"FAIL {FormatCandidate(candidate)}");
            }

            if (!slideSucceeded) {
                LogPushDebug("TrySlide", "全候補が失敗");
            }

            ResetPushState();
        }

        void BeginPushFollow(DiceController dice, Direction direction) {
            EndPushFollow();
            pushFollowDice = dice;
            pushFollowDirection = direction;
            isPushFollowing = true;
            currentSpeed = 0f;
            pushFollowDice.StateChanged += OnPushFollowDiceStateChanged;
            SyncPositionToPushingDice();
        }

        void EndPushFollow() {
            if (pushFollowDice != null) {
                pushFollowDice.StateChanged -= OnPushFollowDiceStateChanged;
            }

            if (isPushFollowing && pushFollowDice != null) {
                SyncPositionToPushingDice();
            }

            isPushFollowing = false;
            pushFollowDice = null;
        }

        void OnPushFollowDiceStateChanged(DiceState state) {
            if (!isPushFollowing) {
                return;
            }

            EndPushFollow();
        }

        void SyncPositionToPushingDice() {
            if (pushFollowDice == null || board == null || characterTransform == null) {
                return;
            }

            var diceTransform = pushFollowDice.View.DiceTransform;
            if (diceTransform == null) {
                return;
            }

            EndRollTracking();
            SetCurrentDice(null);

            var diceCenter = diceTransform.position;
            var half = board.CellSize * 0.5f;
            var contactOffset = half + GetPushHorizontalRadius();
            var position = characterTransform.position;

            position = pushFollowDirection switch {
                Direction.East => new Vector3(diceCenter.x - contactOffset, position.y, position.z),
                Direction.West => new Vector3(diceCenter.x + contactOffset, position.y, position.z),
                Direction.North => new Vector3(position.x, position.y, diceCenter.z - contactOffset),
                Direction.South => new Vector3(position.x, position.y, diceCenter.z + contactOffset),
                _ => position
            };

            ApplyWorldPosition(position);
        }

        void CollectPushCandidates(
            Vector2 input,
            List<PushContactCandidate> candidates,
            out int overlapHitCount,
            out int pushBodyHitCount,
            out int skippedBusyCount) {
            candidates.Clear();
            overlapHitCount = 0;
            pushBodyHitCount = 0;
            skippedBusyCount = 0;

            if (characterPushCollider == null) {
                return;
            }

            var bounds = characterPushCollider.bounds;
            var halfHeight = characterPushCollider.height * 0.5f - characterPushCollider.radius;
            var bottom = bounds.center - Vector3.up * halfHeight;
            var top = bounds.center + Vector3.up * halfHeight;
            var hits = Physics.OverlapCapsule(
                bottom,
                top,
                characterPushCollider.radius,
                ~0,
                QueryTriggerInteraction.Collide);
            overlapHitCount = hits.Length;

            var characterXZ = GetWorldXZ();

            foreach (var hit in hits) {
                if (hit == characterPushCollider) {
                    continue;
                }

                var pushBody = hit.GetComponent<DicePushBody>();
                if (pushBody == null || pushBody.Dice == null || pushBody.Collider == null) {
                    continue;
                }

                pushBodyHitCount++;

                if (pushBody.Dice.IsDissolving || pushBody.Dice.IsBusy) {
                    skippedBusyCount++;
                    continue;
                }

                var pushBounds = pushBody.Collider.bounds;
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.East);
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.West);
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.North);
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.South);
            }

            candidates.Sort(ComparePushCandidates);
        }

        void LogPushDebugNoCandidates(Vector2 input, int overlapHitCount, int pushBodyHitCount, int skippedBusyCount) {
            if (!debugPushContact || input.sqrMagnitude <= 0f) {
                return;
            }

            if (overlapHitCount == debugLastOverlapHitCount
                && pushBodyHitCount == debugLastPushBodyHitCount
                && skippedBusyCount == debugLastSkippedBusyCount
                && debugLastCandidateSummary == string.Empty) {
                return;
            }

            debugLastOverlapHitCount = overlapHitCount;
            debugLastPushBodyHitCount = pushBodyHitCount;
            debugLastSkippedBusyCount = skippedBusyCount;
            debugLastCandidateSummary = string.Empty;
            debugLastPushTarget = null;
            debugLastHadPushDirection = false;
            debugLastLoggedContactTime = -1f;

            LogPushDebug(
                "NoCandidates",
                $"input={FormatVector2(input)} pos={FormatVector2(GetWorldXZ())} " +
                $"overlapHits={overlapHitCount} pushBodies={pushBodyHitCount} skippedBusy={skippedBusyCount}");
        }

        void LogPushDebugCandidates(Vector2 input, int overlapHitCount, int pushBodyHitCount, int skippedBusyCount) {
            if (!debugPushContact) {
                return;
            }

            var summary = FormatCandidates(pushCandidates);
            if (summary == debugLastCandidateSummary
                && overlapHitCount == debugLastOverlapHitCount
                && pushBodyHitCount == debugLastPushBodyHitCount
                && skippedBusyCount == debugLastSkippedBusyCount) {
                return;
            }

            debugLastCandidateSummary = summary;
            debugLastOverlapHitCount = overlapHitCount;
            debugLastPushBodyHitCount = pushBodyHitCount;
            debugLastSkippedBusyCount = skippedBusyCount;

            LogPushDebug(
                "Candidates",
                $"input={FormatVector2(input)} pos={FormatVector2(GetWorldXZ())} " +
                $"overlapHits={overlapHitCount} pushBodies={pushBodyHitCount} skippedBusy={skippedBusyCount} list={summary}");
        }

        void LogPushDebugTargetChange(PushContactCandidate best, float previousContactTime) {
            if (!debugPushContact) {
                return;
            }

            var previousTarget = debugLastHadPushDirection
                ? FormatDiceRef(debugLastPushTarget, debugLastPushDirection)
                : "(none)";
            var nextTarget = FormatCandidate(best);
            var reason = !hasPushDirection
                ? "初回接触"
                : pushDirection != best.Direction
                    ? "方向変更"
                    : "ダイス切替";

            LogPushDebug(
                "TargetChange",
                $"{reason} {previousTarget} -> {nextTarget} timerReset from {previousContactTime:F3}s");

            debugLastPushTarget = best.Dice;
            debugLastPushDirection = best.Direction;
            debugLastHadPushDirection = true;
            debugLastLoggedContactTime = -1f;
        }

        void LogPushDebugHoldProgress() {
            if (!debugPushContact || !hasPushDirection) {
                return;
            }

            var tenth = Mathf.FloorToInt(pushContactTime * 10f);
            var previousTenth = debugLastLoggedContactTime < 0f
                ? -1
                : Mathf.FloorToInt(debugLastLoggedContactTime * 10f);
            if (tenth == previousTenth) {
                return;
            }

            debugLastLoggedContactTime = pushContactTime;
            LogPushDebug(
                "Hold",
                $"{FormatDiceRef(pushTargetDice, pushDirection)} {pushContactTime:F2}/{pushHoldDuration:F2}s");
        }

        void LogPushDebug(string category, string message) {
            Debug.Log($"[PushDebug:{category}] {message}");
        }

        static string FormatVector2(Vector2 value) {
            return $"({value.x:F3}, {value.y:F3})";
        }

        string FormatCandidates(List<PushContactCandidate> candidates) {
            if (candidates.Count == 0) {
                return "(none)";
            }

            pushDebugBuilder.Clear();
            for (var i = 0; i < candidates.Count; i++) {
                if (i > 0) {
                    pushDebugBuilder.Append(" | ");
                }

                pushDebugBuilder.Append('#');
                pushDebugBuilder.Append(i);
                pushDebugBuilder.Append(' ');
                pushDebugBuilder.Append(FormatCandidate(candidates[i]));
            }

            return pushDebugBuilder.ToString();
        }

        static string FormatCandidate(PushContactCandidate candidate) {
            var grid = candidate.Dice.CurrentState.GridPos;
            return $"Grid({grid.x},{grid.y}) {candidate.Direction} align={candidate.InputAlignment:F3} face={candidate.FaceDistance:F3}";
        }

        static string FormatDiceRef(DiceController dice, Direction direction) {
            if (dice == null) {
                return "(none)";
            }

            var grid = dice.CurrentState.GridPos;
            return $"Grid({grid.x},{grid.y}) {direction}";
        }

        void TryAddPushCandidate(
            List<PushContactCandidate> candidates,
            DiceController dice,
            Bounds bounds,
            Vector2 input,
            Vector2 characterPosition,
            Direction direction) {
            if (!TryEvaluatePushCandidate(
                bounds,
                characterPosition,
                input,
                direction,
                pushInputAlignment,
                out var inputAlignment,
                out var faceDistance)) {
                return;
            }

            candidates.Add(new PushContactCandidate {
                Dice = dice,
                Direction = direction,
                InputAlignment = inputAlignment,
                FaceDistance = faceDistance
            });
        }

        static int ComparePushCandidates(PushContactCandidate a, PushContactCandidate b) {
            var alignmentCompare = b.InputAlignment.CompareTo(a.InputAlignment);
            if (alignmentCompare != 0) {
                return alignmentCompare;
            }

            return a.FaceDistance.CompareTo(b.FaceDistance);
        }

        static bool TryEvaluatePushCandidate(
            Bounds bounds,
            Vector2 characterPosition,
            Vector2 input,
            Direction direction,
            float minInputAlignment,
            out float inputAlignment,
            out float faceDistance) {
            inputAlignment = Vector2.Dot(input, GetDirectionInputVector(direction));
            if (inputAlignment < minInputAlignment) {
                faceDistance = 0f;
                return false;
            }

            var charX = characterPosition.x;
            var charZ = characterPosition.y;

            switch (direction) {
                case Direction.East:
                    if (charX > bounds.center.x + EdgeEpsilon) {
                        faceDistance = 0f;
                        return false;
                    }

                    faceDistance = Mathf.Abs(charX - bounds.min.x);
                    break;
                case Direction.West:
                    if (charX < bounds.center.x - EdgeEpsilon) {
                        faceDistance = 0f;
                        return false;
                    }

                    faceDistance = Mathf.Abs(charX - bounds.max.x);
                    break;
                case Direction.North:
                    if (charZ > bounds.center.z + EdgeEpsilon) {
                        faceDistance = 0f;
                        return false;
                    }

                    faceDistance = Mathf.Abs(charZ - bounds.min.z);
                    break;
                case Direction.South:
                    if (charZ < bounds.center.z - EdgeEpsilon) {
                        faceDistance = 0f;
                        return false;
                    }

                    faceDistance = Mathf.Abs(charZ - bounds.max.z);
                    break;
                default:
                    faceDistance = 0f;
                    return false;
            }

            return true;
        }

        static Vector2 GetDirectionInputVector(Direction direction) {
            return direction switch {
                Direction.East => Vector2.right,
                Direction.West => Vector2.left,
                Direction.North => Vector2.up,
                Direction.South => Vector2.down,
                _ => Vector2.zero
            };
        }

        Vector2 ApplyDiceMovementBlock(Vector2 currentPosition, Vector2 move) {
            if (registry == null || move.sqrMagnitude <= 0f) {
                return currentPosition + move;
            }

            var resultMove = move;
            GetPushWorldVerticalRange(out var characterBottom, out var characterTop);

            foreach (var dice in registry.AllDice) {
                if (dice == null || dice.IsDissolving) {
                    continue;
                }

                var pushBody = dice.GetComponentInChildren<DicePushBody>();
                if (pushBody == null || pushBody.Collider == null) {
                    continue;
                }

                var bounds = pushBody.Collider.bounds;
                if (characterTop < bounds.min.y || characterBottom > bounds.max.y) {
                    continue;
                }

                resultMove = BlockMoveAgainstDiceBounds(currentPosition, resultMove, bounds, GetPushHorizontalRadius());
            }

            var next = currentPosition + resultMove;
            var clamped = ClampToWalkBounds(new Vector3(next.x, 0f, next.y));
            return new Vector2(clamped.x, clamped.z);
        }

        static Vector2 BlockMoveAgainstDiceBounds(Vector2 position, Vector2 move, Bounds bounds, float radius) {
            var result = move;

            if (OverlapsAxis(position.y, radius, bounds.min.z, bounds.max.z)) {
                if (result.x > 0f && position.x < bounds.center.x) {
                    var limitX = bounds.min.x - radius;
                    if (position.x + result.x > limitX) {
                        result.x = Mathf.Max(0f, limitX - position.x);
                    }
                }

                if (result.x < 0f && position.x > bounds.center.x) {
                    var limitX = bounds.max.x + radius;
                    if (position.x + result.x < limitX) {
                        result.x = Mathf.Min(0f, limitX - position.x);
                    }
                }
            }

            if (OverlapsAxis(position.x, radius, bounds.min.x, bounds.max.x)) {
                if (result.y > 0f && position.y < bounds.center.z) {
                    var limitZ = bounds.min.z - radius;
                    if (position.y + result.y > limitZ) {
                        result.y = Mathf.Max(0f, limitZ - position.y);
                    }
                }

                if (result.y < 0f && position.y > bounds.center.z) {
                    var limitZ = bounds.max.z + radius;
                    if (position.y + result.y < limitZ) {
                        result.y = Mathf.Min(0f, limitZ - position.y);
                    }
                }
            }

            return result;
        }

        static bool OverlapsAxis(float center, float radius, float min, float max) {
            return center + radius > min && center - radius < max;
        }

        bool CanStepBetween(float fromSurfaceY, float toSurfaceY) {
            return Mathf.Abs(fromSurfaceY - toSurfaceY) <= maxStepHeight;
        }

        bool TryTransferToAdjacentDiceAtEdge(Vector2 nextOffset, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextOffset, edgeLimit, move, out var direction)) {
                return false;
            }

            var neighbor = registry.GetNeighbor(currentDice, direction);
            if (neighbor == null) {
                return false;
            }

            if (!CanStepBetween(currentDice.GetTopSurfaceWorldY(), neighbor.GetTopSurfaceWorldY())) {
                return false;
            }

            var remapped = RemapFacePositionForTransfer(nextOffset, edgeLimit, direction);
            var neighborCenter = neighbor.View.DiceTransform.position;
            SetCurrentDice(neighbor);
            ApplyWorldPosition(new Vector3(neighborCenter.x + remapped.x, 0f, neighborCenter.z + remapped.y));
            return true;
        }

        bool TryTransferToFloorAtEdge(Vector2 nextOffset, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextOffset, edgeLimit, move, out var direction)) {
                return false;
            }

            var targetGrid = currentDice.CurrentState.GridPos + direction.ToGridDelta();
            if (!board.IsInside(targetGrid) || registry.TryGetAt(targetGrid, out _)) {
                return false;
            }

            if (!CanStepBetween(currentDice.GetTopSurfaceWorldY(), board.FloorSurfaceWorldY)) {
                return false;
            }

            var diceCenter = currentDice.View.DiceTransform.position;
            var worldPosition = GetWorldPositionAtDiceEdge(diceCenter, nextOffset, edgeLimit, direction);
            SetCurrentDice(null);
            ApplyWorldPosition(worldPosition);
            return true;
        }

        bool TryRollAtEdge(Vector2 nextOffset, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextOffset, edgeLimit, move, out var direction)) {
                return false;
            }

            if (currentDice.IsDissolving) {
                return false;
            }

            var targetPos = currentDice.CurrentState.GridPos + direction.ToGridDelta();
            if (registry.TryGetAt(targetPos, out _)) {
                return false;
            }

            var clamped = ClampToFace(nextOffset, edgeLimit);
            var diceCenter = currentDice.View.DiceTransform.position;
            ApplyWorldPosition(new Vector3(diceCenter.x + clamped.x, 0f, diceCenter.z + clamped.y));

            if (!board.CanDiceRollInto(targetPos)) {
                return false;
            }

            var characterAnchor = characterTransform.position;
            var diceCenterAnchor = diceCenter;

            if (!currentDice.TryRoll(direction)) {
                return false;
            }

            BeginRollTracking(characterAnchor, diceCenterAnchor);
            return true;
        }

        bool TryStepOntoDiceFromFloor(Vector2 nextPosition, Vector2 move) {
            if (Mathf.Abs(move.x) >= Mathf.Abs(move.y)) {
                if (move.x > 0f && TryStepOntoDiceFromFloorDirection(nextPosition, Direction.East)) {
                    return true;
                }

                if (move.x < 0f && TryStepOntoDiceFromFloorDirection(nextPosition, Direction.West)) {
                    return true;
                }
            } else {
                if (move.y > 0f && TryStepOntoDiceFromFloorDirection(nextPosition, Direction.North)) {
                    return true;
                }

                if (move.y < 0f && TryStepOntoDiceFromFloorDirection(nextPosition, Direction.South)) {
                    return true;
                }
            }

            return false;
        }

        bool TryStepOntoDiceFromFloorDirection(Vector2 nextPosition, Direction direction) {
            var currentXZ = GetWorldXZ();
            var currentGrid = board.WorldToGrid(new Vector3(currentXZ.x, 0f, currentXZ.y));
            var diceGrid = currentGrid + direction.ToGridDelta();

            if (!registry.TryGetAt(diceGrid, out var targetDice)) {
                return false;
            }

            var diceCenter = targetDice.View.DiceTransform.position;
            var edgeLimit = GetEdgeLimit();
            var offset = WorldOffsetFromDiceCenter(diceCenter, nextPosition);

            var crossed = direction switch {
                Direction.East => offset.x >= -edgeLimit - EdgeEpsilon,
                Direction.West => offset.x <= edgeLimit + EdgeEpsilon,
                Direction.North => offset.y >= -edgeLimit - EdgeEpsilon,
                Direction.South => offset.y <= edgeLimit + EdgeEpsilon,
                _ => false
            };

            if (!crossed) {
                return false;
            }

            if (!CanStepBetween(board.FloorSurfaceWorldY, targetDice.GetTopSurfaceWorldY())) {
                ApplyWorldPosition(ClampToWalkBounds(new Vector3(nextPosition.x, 0f, nextPosition.y)));
                return true;
            }

            var remapped = RemapFacePositionForTransfer(offset, edgeLimit, direction.Opposite());
            SetCurrentDice(targetDice);
            ApplyWorldPosition(new Vector3(diceCenter.x + remapped.x, 0f, diceCenter.z + remapped.y));
            return true;
        }

        static Vector2 WorldOffsetFromDiceCenter(Vector3 diceCenter, Vector2 worldPosition) {
            return new Vector2(worldPosition.x - diceCenter.x, worldPosition.y - diceCenter.z);
        }

        static Vector3 GetWorldPositionAtDiceEdge(
            Vector3 diceCenter,
            Vector2 nextOffset,
            float edgeLimit,
            Direction direction) {
            return direction switch {
                Direction.East => new Vector3(
                    diceCenter.x + Mathf.Min(nextOffset.x, edgeLimit),
                    0f,
                    diceCenter.z + nextOffset.y),
                Direction.West => new Vector3(
                    diceCenter.x + Mathf.Max(nextOffset.x, -edgeLimit),
                    0f,
                    diceCenter.z + nextOffset.y),
                Direction.North => new Vector3(
                    diceCenter.x + nextOffset.x,
                    0f,
                    diceCenter.z + Mathf.Min(nextOffset.y, edgeLimit)),
                Direction.South => new Vector3(
                    diceCenter.x + nextOffset.x,
                    0f,
                    diceCenter.z + Mathf.Max(nextOffset.y, -edgeLimit)),
                _ => new Vector3(diceCenter.x + nextOffset.x, 0f, diceCenter.z + nextOffset.y)
            };
        }

        static bool TryGetCrossingDirection(Vector2 nextOffset, float edgeLimit, Vector2 move, out Direction direction) {
            direction = default;

            if (move.x > 0f && nextOffset.x > edgeLimit) {
                direction = Direction.East;
                return true;
            }

            if (move.x < 0f && nextOffset.x < -edgeLimit) {
                direction = Direction.West;
                return true;
            }

            if (move.y > 0f && nextOffset.y > edgeLimit) {
                direction = Direction.North;
                return true;
            }

            if (move.y < 0f && nextOffset.y < -edgeLimit) {
                direction = Direction.South;
                return true;
            }

            return false;
        }

        static Vector2 RemapFacePositionForTransfer(Vector2 nextOffset, float edgeLimit, Direction direction) {
            return direction switch {
                Direction.East => new Vector2(
                    -edgeLimit + (nextOffset.x - edgeLimit),
                    Mathf.Clamp(nextOffset.y, -edgeLimit, edgeLimit)),
                Direction.West => new Vector2(
                    edgeLimit + (nextOffset.x + edgeLimit),
                    Mathf.Clamp(nextOffset.y, -edgeLimit, edgeLimit)),
                Direction.North => new Vector2(
                    Mathf.Clamp(nextOffset.x, -edgeLimit, edgeLimit),
                    -edgeLimit + (nextOffset.y - edgeLimit)),
                Direction.South => new Vector2(
                    Mathf.Clamp(nextOffset.x, -edgeLimit, edgeLimit),
                    edgeLimit + (nextOffset.y + edgeLimit)),
                _ => nextOffset
            };
        }

        static Vector2 ClampToFace(Vector2 offset, float edgeLimit) {
            return new Vector2(
                Mathf.Clamp(offset.x, -edgeLimit, edgeLimit),
                Mathf.Clamp(offset.y, -edgeLimit, edgeLimit));
        }

        float GetEdgeLimit() {
            return board.CellSize * 0.5f * faceStepRatio;
        }

        static Vector2 GetInputDirection() {
            var input = Vector2.zero;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) {
                input.x += 1f;
            }

            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) {
                input.x -= 1f;
            }

            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) {
                input.y += 1f;
            }

            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) {
                input.y -= 1f;
            }

            return input;
        }
    }
}
