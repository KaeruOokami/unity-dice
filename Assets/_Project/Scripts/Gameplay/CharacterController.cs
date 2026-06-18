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
        [SerializeField] float characterPushRadius = 0.25f;
        [SerializeField] float characterPushHeight = 0.6f;
        [SerializeField] float characterPushBottom = 0.05f;
        [SerializeField] bool debugPushContact;

        DiceRegistry registry;
        DiceController currentDice;
        Transform characterTransform;
        CapsuleCollider characterPushCollider;
        Vector2 facePosition;
        Vector2 floorWorldPosition;
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
        public Vector2 FacePosition => facePosition;
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
            facePosition = Vector2.zero;
            currentSpeed = 0f;
            isInitialized = true;
            UpdateCharacterWorldPosition();
        }

        public void OnStandingDiceDissolved(DiceController dissolvedDice) {
            if (!isInitialized || currentDice != dissolvedDice) {
                return;
            }

            MoveToFloorAtCurrentWorldPosition();
        }

        void OnDisable() {
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

            UpdateCharacterWorldPosition();
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

            var edgeLimit = GetEdgeLimit();
            var move = input * (currentSpeed * Time.deltaTime);
            var nextPosition = facePosition + move;

            if (TryTransferToAdjacentDiceAtEdge(nextPosition, edgeLimit, move)) {
                return;
            }

            if (TryRollAtEdge(nextPosition, edgeLimit, move)) {
                return;
            }

            if (TryTransferToFloorAtEdge(nextPosition, edgeLimit, move)) {
                return;
            }

            facePosition = ClampToFace(nextPosition, edgeLimit);
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
            var nextPosition = ApplyDiceMovementBlock(floorWorldPosition, move);

            if (TryStepOntoDiceFromFloor(nextPosition, move)) {
                if (debugPushContact) {
                    LogPushDebug(
                        "StepOntoDice",
                        $"乗り移りで押しを中断 floor={FormatVector2(floorWorldPosition)} move={FormatVector2(move)}");
                }

                ResetPushState();
                return;
            }

            floorWorldPosition = ClampFloorPosition(nextPosition);
            UpdatePushContact(input);
        }

        void UpdateDuringRoll(Vector2 input) {
            if (input.sqrMagnitude > 0f) {
                input.Normalize();
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxMoveSpeed, moveAcceleration * Time.deltaTime);
                var move = input * (currentSpeed * Time.deltaTime);
                facePosition = ClampToFace(facePosition + move, GetEdgeLimit());
                return;
            }

            currentSpeed = 0f;
            facePosition = Vector2.MoveTowards(facePosition, Vector2.zero, rollCenterPullSpeed * Time.deltaTime);
        }

        void OnDiceStateChanged(DiceState state) {
            if (!isInitialized) {
                return;
            }

            currentSpeed = 0f;
        }

        void SetCurrentDice(DiceController dice) {
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
            if (characterTransform == null) {
                SetCurrentDice(null);
                return;
            }

            var position = characterTransform.position;
            SetCurrentDice(null);
            floorWorldPosition = new Vector2(position.x, position.z);
        }

        void EnsureCharacterInstance() {
            if (characterTransform != null) {
                return;
            }

            if (characterObject != null) {
                var instance = Instantiate(characterObject, transform);
                instance.name = "CharacterVisual";
                characterTransform = instance.transform;
                return;
            }

            characterTransform = transform;
        }

        void EnsureCharacterPushCollider() {
            if (characterPushCollider != null || characterTransform == null) {
                return;
            }

            characterPushCollider = characterTransform.GetComponent<CapsuleCollider>();
            if (characterPushCollider == null) {
                characterPushCollider = characterTransform.gameObject.AddComponent<CapsuleCollider>();
            }

            characterPushCollider.isTrigger = true;
            characterPushCollider.radius = characterPushRadius;
            characterPushCollider.height = characterPushHeight;
            characterPushCollider.center = new Vector3(
                0f,
                characterPushBottom + characterPushHeight * 0.5f,
                0f);
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
            Debug.Log($"overlapHitCount: {overlapHitCount}, pushBodyHitCount: {pushBodyHitCount}");
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
            if (pushFollowDice == null || board == null) {
                return;
            }

            var diceTransform = pushFollowDice.View.DiceTransform;
            if (diceTransform == null) {
                return;
            }

            var diceCenter = diceTransform.position;
            var half = board.CellSize * 0.5f;
            var contactOffset = half + characterPushRadius;

            floorWorldPosition = pushFollowDirection switch {
                Direction.East => new Vector2(diceCenter.x - contactOffset, floorWorldPosition.y),
                Direction.West => new Vector2(diceCenter.x + contactOffset, floorWorldPosition.y),
                Direction.North => new Vector2(floorWorldPosition.x, diceCenter.z - contactOffset),
                Direction.South => new Vector2(floorWorldPosition.x, diceCenter.z + contactOffset),
                _ => floorWorldPosition
            };
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

            var characterCenter = new Vector3(
                floorWorldPosition.x,
                board.FloorSurfaceWorldY + characterPushBottom + characterPushHeight * 0.5f,
                floorWorldPosition.y);
            var halfHeight = characterPushHeight * 0.5f - characterPushRadius;
            var bottom = characterCenter - Vector3.up * halfHeight;
            var top = characterCenter + Vector3.up * halfHeight;
            var hits = Physics.OverlapCapsule(
                bottom,
                top,
                characterPushRadius,
                ~0,
                QueryTriggerInteraction.Collide);
            overlapHitCount = hits.Length;

            foreach (var hit in hits) {
                var pushBody = hit.GetComponent<DicePushBody>();
                if (pushBody == null || pushBody.Dice == null || pushBody.Collider == null) {
                    continue;
                }

                pushBodyHitCount++;

                if (pushBody.Dice.IsDissolving || pushBody.Dice.IsBusy) {
                    skippedBusyCount++;
                    continue;
                }

                var bounds = pushBody.Collider.bounds;
                TryAddPushCandidate(candidates, pushBody.Dice, bounds, input, Direction.East);
                TryAddPushCandidate(candidates, pushBody.Dice, bounds, input, Direction.West);
                TryAddPushCandidate(candidates, pushBody.Dice, bounds, input, Direction.North);
                TryAddPushCandidate(candidates, pushBody.Dice, bounds, input, Direction.South);
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
                $"input={FormatVector2(input)} floor={FormatVector2(floorWorldPosition)} " +
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
                $"input={FormatVector2(input)} floor={FormatVector2(floorWorldPosition)} " +
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
            Direction direction) {
            if (!TryEvaluatePushCandidate(
                bounds,
                floorWorldPosition,
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
            var characterBottom = board.FloorSurfaceWorldY + characterPushBottom;
            var characterTop = characterBottom + characterPushHeight;

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

                resultMove = BlockMoveAgainstDiceBounds(currentPosition, resultMove, bounds, characterPushRadius);
            }

            return currentPosition + resultMove;
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

        void UpdateCharacterWorldPosition() {
            if (characterTransform == null || board == null) {
                return;
            }

            var worldY = GetCurrentSurfaceWorldY() + characterHeightOffset;

            if (IsOnFloor) {
                characterTransform.position = new Vector3(
                    floorWorldPosition.x,
                    worldY,
                    floorWorldPosition.y);
            } else {
                var diceTransform = currentDice.View.DiceTransform;
                if (diceTransform == null) {
                    return;
                }

                var dicePosition = diceTransform.position;
                characterTransform.position = new Vector3(
                    dicePosition.x + facePosition.x,
                    worldY,
                    dicePosition.z + facePosition.y);
            }

            characterTransform.rotation = Quaternion.identity;
        }

        float GetCurrentSurfaceWorldY() {
            if (IsOnFloor) {
                return board.FloorSurfaceWorldY;
            }

            return currentDice.GetTopSurfaceWorldY();
        }

        bool CanStepBetween(float fromSurfaceY, float toSurfaceY) {
            return Mathf.Abs(fromSurfaceY - toSurfaceY) <= maxStepHeight;
        }

        bool TryTransferToAdjacentDiceAtEdge(Vector2 nextPosition, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextPosition, edgeLimit, move, out var direction)) {
                return false;
            }

            var neighbor = registry.GetNeighbor(currentDice, direction);
            if (neighbor == null) {
                return false;
            }

            if (!CanStepBetween(currentDice.GetTopSurfaceWorldY(), neighbor.GetTopSurfaceWorldY())) {
                return false;
            }

            facePosition = RemapFacePositionForTransfer(nextPosition, edgeLimit, direction);
            SetCurrentDice(neighbor);
            return true;
        }

        bool TryTransferToFloorAtEdge(Vector2 nextPosition, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextPosition, edgeLimit, move, out var direction)) {
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
            var worldPosition = GetWorldPositionAtDiceEdge(diceCenter, nextPosition, edgeLimit, direction);
            SetCurrentDice(null);
            floorWorldPosition = new Vector2(worldPosition.x, worldPosition.z);
            return true;
        }

        bool TryRollAtEdge(Vector2 nextPosition, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextPosition, edgeLimit, move, out var direction)) {
                return false;
            }

            if (currentDice.IsDissolving) {
                return false;
            }

            var targetPos = currentDice.CurrentState.GridPos + direction.ToGridDelta();
            if (registry.TryGetAt(targetPos, out _)) {
                return false;
            }

            facePosition = ClampToFace(nextPosition, edgeLimit);

            if (!board.CanDiceRollInto(targetPos)) {
                return false;
            }

            if (!currentDice.TryRoll(direction)) {
                return false;
            }

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
            var currentGrid = board.WorldToGrid(new Vector3(floorWorldPosition.x, 0f, floorWorldPosition.y));
            var diceGrid = currentGrid + direction.ToGridDelta();

            if (!registry.TryGetAt(diceGrid, out var targetDice)) {
                return false;
            }

            var diceCenter = board.GridToWorld(diceGrid);
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
                floorWorldPosition = ClampFloorPosition(nextPosition);
                return true;
            }

            facePosition = RemapFacePositionForTransfer(offset, edgeLimit, direction.Opposite());
            SetCurrentDice(targetDice);
            return true;
        }

        static Vector2 WorldOffsetFromDiceCenter(Vector3 diceCenter, Vector2 worldPosition) {
            return new Vector2(worldPosition.x - diceCenter.x, worldPosition.y - diceCenter.z);
        }

        static Vector3 GetWorldPositionAtDiceEdge(
            Vector3 diceCenter,
            Vector2 nextPosition,
            float edgeLimit,
            Direction direction) {
            return direction switch {
                Direction.East => new Vector3(
                    diceCenter.x + Mathf.Min(nextPosition.x, edgeLimit),
                    0f,
                    diceCenter.z + nextPosition.y),
                Direction.West => new Vector3(
                    diceCenter.x + Mathf.Max(nextPosition.x, -edgeLimit),
                    0f,
                    diceCenter.z + nextPosition.y),
                Direction.North => new Vector3(
                    diceCenter.x + nextPosition.x,
                    0f,
                    diceCenter.z + Mathf.Min(nextPosition.y, edgeLimit)),
                Direction.South => new Vector3(
                    diceCenter.x + nextPosition.x,
                    0f,
                    diceCenter.z + Mathf.Max(nextPosition.y, -edgeLimit)),
                _ => new Vector3(diceCenter.x + nextPosition.x, 0f, diceCenter.z + nextPosition.y)
            };
        }

        Vector2 ClampFloorPosition(Vector2 position) {
            var minX = 0f;
            var minZ = 0f;
            var maxX = (board.Width - 1) * board.CellSize;
            var maxZ = (board.Height - 1) * board.CellSize;
            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minZ, maxZ));
        }

        static bool TryGetCrossingDirection(Vector2 nextPosition, float edgeLimit, Vector2 move, out Direction direction) {
            direction = default;

            if (move.x > 0f && nextPosition.x > edgeLimit) {
                direction = Direction.East;
                return true;
            }

            if (move.x < 0f && nextPosition.x < -edgeLimit) {
                direction = Direction.West;
                return true;
            }

            if (move.y > 0f && nextPosition.y > edgeLimit) {
                direction = Direction.North;
                return true;
            }

            if (move.y < 0f && nextPosition.y < -edgeLimit) {
                direction = Direction.South;
                return true;
            }

            return false;
        }

        static Vector2 RemapFacePositionForTransfer(Vector2 nextPosition, float edgeLimit, Direction direction) {
            return direction switch {
                Direction.East => new Vector2(
                    -edgeLimit + (nextPosition.x - edgeLimit),
                    Mathf.Clamp(nextPosition.y, -edgeLimit, edgeLimit)),
                Direction.West => new Vector2(
                    edgeLimit + (nextPosition.x + edgeLimit),
                    Mathf.Clamp(nextPosition.y, -edgeLimit, edgeLimit)),
                Direction.North => new Vector2(
                    Mathf.Clamp(nextPosition.x, -edgeLimit, edgeLimit),
                    -edgeLimit + (nextPosition.y - edgeLimit)),
                Direction.South => new Vector2(
                    Mathf.Clamp(nextPosition.x, -edgeLimit, edgeLimit),
                    edgeLimit + (nextPosition.y + edgeLimit)),
                _ => nextPosition
            };
        }

        static Vector2 ClampToFace(Vector2 position, float edgeLimit) {
            return new Vector2(
                Mathf.Clamp(position.x, -edgeLimit, edgeLimit),
                Mathf.Clamp(position.y, -edgeLimit, edgeLimit));
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
