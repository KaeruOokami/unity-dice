namespace Quantum
{
    using System.Collections.Generic;
    using DiceGame.Config;
    using DiceGame.Core;
    using DiceGame.Gameplay;
    using DiceGame.View;
    using UnityEngine;
    using CoreDiceKind = DiceGame.Core.DiceKind;
    using CoreDiceOrientation = DiceGame.Core.DiceOrientation;
    using CoreDiceStackTier = DiceGame.Core.DiceStackTier;
    using CoreDiceState = DiceGame.Core.DiceState;
    using CoreErasureKind = DiceGame.Core.ErasureKind;
    using GridBoard = DiceGame.Grid.Board;
    using QuantumDice = Quantum.Dice;
    using QuantumDiceKind = Quantum.DiceKind;
    using GameCharacterController = DiceGame.Gameplay.CharacterController;

    /// <summary>
    /// Binds Quantum Frame entities to production <see cref="DiceView"/> / Character prefabs.
    /// Placement uses <see cref="GridBoard.GridToWorld"/> (legacy Board contract).
    /// </summary>
    public sealed class DiceBoardViewBinder : MonoBehaviour
    {
        [SerializeField] float pawnScale = 0.7f;
        [SerializeField] float diceScale = 0.45f;
        [SerializeField] bool preferProductionPrefabs = true;
        [SerializeField] bool playSpawnAppearances = true;

        readonly Dictionary<EntityRef, ViewBinding> views = new();
        Transform root;
        GameBootstrap bootstrap;
        GridBoard board;
        GameObject dicePrefab;
        GameObject characterPrefab;
        DiceCatalog catalog;
        PhysicsSettings physicsSettings;
        DiceAnimationSettings animationSettings;
        DiceErasureSettings erasureSettings;
        CharacterMovementSettings movementSettings;
        bool productionReady;

        sealed class ViewBinding
        {
            public Transform Transform;
            public DiceView DiceView;
            public bool IsProductionDice;
            public bool IsProductionPawn;
            public QuantumDiceKind LastKind;
            public int LastGridX = int.MinValue;
            public int LastGridY = int.MinValue;
            public DiceStackTier LastTier;
            public bool WasErasing;
            public bool WasCarried;
            public bool SpawnStarted;
            public CoreDiceOrientation LastOrientation;
            public bool HasVisualPosition;
            public Vector3 VisualPosition;
            public float LastJumpVisualY;
        }

        void OnEnable()
        {
            root = new GameObject("DiceBoardViews").transform;
            root.SetParent(transform, false);
            ResolveProductionAssets();
            QuantumCallback.Subscribe(this, (CallbackUpdateView callback) => OnUpdateView(callback));
            QuantumCallback.Subscribe(this, (CallbackGameDestroyed callback) => ClearViews());
        }

        void OnDisable()
        {
            ClearViews();
        }

        void ResolveProductionAssets()
        {
            productionReady = false;
            if (!preferProductionPrefabs)
            {
                return;
            }

            bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                return;
            }

            board = bootstrap.Board;
            dicePrefab = bootstrap.DiceEntityPrefab;
            characterPrefab = bootstrap.CharacterPrefab;
            catalog = bootstrap.SharedDiceCatalog;
            physicsSettings = bootstrap.PhysicsSettings;
            animationSettings = bootstrap.DiceAnimationSettings;
            erasureSettings = bootstrap.DiceErasureSettings;
            movementSettings = bootstrap.CharacterMovementSettings;
            productionReady = dicePrefab != null && characterPrefab != null && board != null;
        }

        void OnUpdateView(CallbackUpdateView callback)
        {
            var frame = callback.Game.Frames.Predicted;
            if (frame == null)
            {
                return;
            }

            if (preferProductionPrefabs && !productionReady)
            {
                ResolveProductionAssets();
            }

            var alive = new HashSet<EntityRef>();
            var filter = frame.Filter<GridPose>();
            while (filter.Next(out var entity, out var pose))
            {
                alive.Add(entity);
                var binding = GetOrCreateView(frame, entity);
                if (frame.TryGet<QuantumDice>(entity, out var dice))
                {
                    UpdateDiceView(frame, entity, binding, dice, pose.X, pose.Y);
                }
                else if (frame.TryGet<PlayerPawn>(entity, out var pawn))
                {
                    UpdatePawnView(binding, pawn, pose.X, pose.Y);
                }
            }

            var stale = new List<EntityRef>();
            foreach (var pair in views)
            {
                if (!alive.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (var entity in stale)
            {
                if (views.TryGetValue(entity, out var binding) && binding.Transform != null)
                {
                    Destroy(binding.Transform.gameObject);
                }

                views.Remove(entity);
            }
        }

        void UpdateDiceView(
            Frame frame,
            EntityRef entity,
            ViewBinding binding,
            QuantumDice dice,
            int gridX,
            int gridY)
        {
            var coreKind = ToCoreKind(dice.Kind);
            var orientation = ResolveOrientation(dice);
            var tier = dice.Tier == DiceStackTier.Top
                ? CoreDiceStackTier.Top
                : CoreDiceStackTier.Bottom;
            var state = new CoreDiceState(
                new Vector2Int(gridX, gridY),
                orientation,
                tier,
                coreKind);

            if (binding.IsProductionDice && binding.DiceView != null && board != null)
            {
                UpdateProductionDice(frame, entity, binding, dice, state, gridX, gridY);
                return;
            }

            UpdateDebugDice(binding, dice, gridX, gridY);
        }

        void UpdateProductionDice(
            Frame frame,
            EntityRef entity,
            ViewBinding binding,
            QuantumDice dice,
            CoreDiceState state,
            int gridX,
            int gridY)
        {
            var diceView = binding.DiceView;
            if (binding.LastKind != dice.Kind)
            {
                GameObject mesh = null;
                catalog?.TryGetMeshPrefab(state.Kind, out mesh);
                diceView.ApplyNetworkKindPresentation(board, state.Kind, mesh);
                binding.LastKind = dice.Kind;
            }

            var gridChanged = binding.LastGridX != gridX || binding.LastGridY != gridY;
            var orientationChanged = !binding.LastOrientation.Equals(state.Orientation);
            var tierChanged = binding.LastTier != dice.Tier;
            var isAnimating = diceView.IsAnimating;
            var becameCarried = dice.IsCarried && !binding.WasCarried;
            var becamePlaced = !dice.IsCarried && binding.WasCarried;

            if (!binding.SpawnStarted)
            {
                binding.SpawnStarted = true;
                ApplyQuantumSurfaceOverride(diceView, state, gridX, gridY);
                diceView.SnapTo(state, board);
                if (playSpawnAppearances && HasGameplaySettings())
                {
                    if (dice.Tier == DiceStackTier.Bottom)
                    {
                        diceView.PlayBottomEmergenceAppear(
                            state,
                            board,
                            registry: null,
                            fallGravityScale: 1f,
                            onComplete: null);
                    }
                    else
                    {
                        diceView.PlaySpawnAppear(
                            state,
                            board,
                            registry: null,
                            enableSpawnBounce: true,
                            fallGravityScale: 1f,
                            onComplete: null);
                    }
                }

                binding.LastGridX = gridX;
                binding.LastGridY = gridY;
                binding.LastTier = dice.Tier;
                binding.LastOrientation = state.Orientation;
                binding.WasErasing = dice.IsErasing;
                binding.WasCarried = dice.IsCarried;
                return;
            }

            if (dice.IsErasing)
            {
                var progress = dice.EraseTicksTotal > 0
                    ? 1f - (dice.EraseTicksRemaining / (float)dice.EraseTicksTotal)
                    : 1f;
                progress = Mathf.Clamp01(progress);
                var visualKind = dice.Tier == DiceStackTier.Top
                    ? CoreErasureKind.Radiance
                    : CoreErasureKind.Sink;

                if (!binding.WasErasing)
                {
                    diceView.InterruptRollAnimation();
                    ApplyQuantumSurfaceOverride(diceView, state, gridX, gridY);
                    diceView.SnapTo(state, board);
                }

                diceView.ApplyNetworkVisualPresentation(
                    visualKind: (byte)visualKind,
                    progress: progress,
                    topFace: state.Orientation.Top,
                    emissionColor: null,
                    oneVanishSettings: null,
                    board: board);

                binding.WasErasing = true;
                binding.WasCarried = false;
                binding.LastGridX = gridX;
                binding.LastGridY = gridY;
                binding.LastTier = dice.Tier;
                binding.LastOrientation = state.Orientation;
                return;
            }

            if (binding.WasErasing)
            {
                diceView.ClearNetworkVisualPresentation();
                binding.WasErasing = false;
            }

            // Production Lift: DiceCarryMotion.TryBeginCarry (same FreeMove as DiceController).
            if (becameCarried && !isAnimating && HasGameplaySettings())
            {
                var fromState = BuildLastCoreState(binding, state.Kind);
                var carryWorld = ResolveCarryWorldPosition(frame, entity, gridX, gridY);
                DiceCarryMotion.TryBeginCarry(
                    diceView,
                    board,
                    registry: null,
                    fromState,
                    carryWorld,
                    onLogicalComplete: null,
                    startLogicalBusy: (_, __) => { },
                    clearLogicalBusyWithoutComplete: () => { });
                binding.WasCarried = true;
                binding.LastGridX = gridX;
                binding.LastGridY = gridY;
                binding.LastTier = dice.Tier;
                binding.LastOrientation = state.Orientation;
                binding.LastJumpVisualY = 0f;
                return;
            }

            // Production Place: DiceCarryMotion.TryPlaceAt.
            if (becamePlaced && !isAnimating && HasGameplaySettings())
            {
                var fromWorld = ResolveCarryWorldPosition(frame, entity, binding.LastGridX, binding.LastGridY);
                if (diceView.DiceTransform != null)
                {
                    fromWorld = diceView.DiceTransform.position;
                }

                ApplyQuantumSurfaceOverride(diceView, state, gridX, gridY);
                var orientState = new CoreDiceState(
                    new Vector2Int(gridX, gridY),
                    state.Orientation,
                    state.Tier,
                    state.Kind);
                DiceCarryMotion.TryPlaceAt(
                    diceView,
                    board,
                    registry: null,
                    orientState,
                    new Vector2Int(gridX, gridY),
                    state.Tier == CoreDiceStackTier.Top
                        ? CoreDiceStackTier.Top
                        : CoreDiceStackTier.Bottom,
                    fromWorld,
                    onLogicalComplete: null,
                    startLogicalBusy: (_, __) => { },
                    finishLogicalBusy: () => { },
                    commitPlacedState: null);
                binding.WasCarried = false;
                binding.LastGridX = gridX;
                binding.LastGridY = gridY;
                binding.LastTier = dice.Tier;
                binding.LastOrientation = state.Orientation;
                binding.LastJumpVisualY = 0f;
                return;
            }

            // While spawn/roll/lift/place animations run, let DiceView own the transform.
            if (diceView.IsAnimating)
            {
                return;
            }

            if (dice.IsCarried)
            {
                // Production LateUpdate carry follow (character + CarryVerticalOffset).
                diceView.SetCarryWorldPosition(ResolveCarryWorldPosition(frame, entity, gridX, gridY));
                binding.WasCarried = true;
                binding.LastGridX = gridX;
                binding.LastGridY = gridY;
                binding.LastTier = dice.Tier;
                binding.LastOrientation = state.Orientation;
                return;
            }

            binding.WasCarried = false;

            // Jump arc on standing die (production ApplyVisualYOffset), every view tick.
            var jumpY = ResolveStandingJumpOffsetY(frame, gridX, gridY, state.Tier);
            SyncJumpVisualYOffset(binding, diceView, jumpY);

            if (gridChanged || orientationChanged || tierChanged)
            {
                if (binding.LastGridX != int.MinValue && gridChanged)
                {
                    var fromState = BuildLastCoreState(binding, state.Kind);
                    ApplyQuantumSurfaceOverride(diceView, state, gridX, gridY);

                    var jumpOffsetAtMove = ResolveStandingJumpOffsetY(
                        frame,
                        binding.LastGridX,
                        binding.LastGridY,
                        binding.LastTier == DiceStackTier.Top
                            ? CoreDiceStackTier.Top
                            : CoreDiceStackTier.Bottom);
                    if (jumpOffsetAtMove <= 0f)
                    {
                        jumpOffsetAtMove = jumpY;
                    }

                    if (orientationChanged
                        && TryResolveOrthogonalMove(
                            binding.LastGridX,
                            binding.LastGridY,
                            gridX,
                            gridY,
                            out var rollDirection,
                            out var rollDistance))
                    {
                        if (jumpOffsetAtMove > 0f || IsPawnJumpingAt(frame, binding.LastGridX, binding.LastGridY, binding.LastTier))
                        {
                            diceView.PlayJumpRoll(
                                rollDirection,
                                fromState,
                                state,
                                jumpOffsetAtMove,
                                rollDistance,
                                board,
                                registry: null,
                                onComplete: null);
                        }
                        else
                        {
                            diceView.PlayRoll(
                                rollDirection,
                                fromState,
                                state,
                                board,
                                registry: null,
                                onComplete: null);
                        }
                    }
                    else if (TryResolveSlideDirection(
                                 binding.LastGridX,
                                 binding.LastGridY,
                                 gridX,
                                 gridY,
                                 out _,
                                 out var slideDistance))
                    {
                        var transition = DiceTransition.GridMove(fromState, state);
                        diceView.PlayTransition(
                            transition,
                            board,
                            registry: null,
                            onComplete: null,
                            slideCellDistance: slideDistance);
                    }
                    else
                    {
                        diceView.SnapTo(state, board);
                        SyncJumpVisualYOffset(binding, diceView, jumpY);
                    }
                }
                else
                {
                    ApplyQuantumSurfaceOverride(diceView, state, gridX, gridY);
                    diceView.SnapTo(state, board);
                    SyncJumpVisualYOffset(binding, diceView, jumpY);
                }

                binding.LastGridX = gridX;
                binding.LastGridY = gridY;
                binding.LastTier = dice.Tier;
                binding.LastOrientation = state.Orientation;
            }
        }

        static CoreDiceState BuildLastCoreState(ViewBinding binding, CoreDiceKind kind)
        {
            return new CoreDiceState(
                new Vector2Int(binding.LastGridX, binding.LastGridY),
                binding.LastOrientation,
                binding.LastTier == DiceStackTier.Top
                    ? CoreDiceStackTier.Top
                    : CoreDiceStackTier.Bottom,
                kind);
        }

        void SyncJumpVisualYOffset(ViewBinding binding, DiceView diceView, float jumpY)
        {
            if (diceView == null || board == null)
            {
                return;
            }

            if (jumpY > 0f)
            {
                diceView.ApplyVisualYOffset(board, jumpY);
                binding.LastJumpVisualY = jumpY;
                return;
            }

            if (binding.LastJumpVisualY > 0f)
            {
                diceView.ClearVisualYOffset(board);
                binding.LastJumpVisualY = 0f;
            }
        }

        Vector3 ResolveCarryWorldPosition(
            Frame frame,
            EntityRef diceEntity,
            int fallbackGridX,
            int fallbackGridY)
        {
            var carryOffset = movementSettings != null ? movementSettings.CarryVerticalOffset : board.CellSize;
            if (TryFindCarrierPawnWorld(frame, diceEntity, fallbackGridX, fallbackGridY, out var pawnWorld))
            {
                return new Vector3(pawnWorld.x, pawnWorld.y + carryOffset, pawnWorld.z);
            }

            var cell = board.GridToWorld(new Vector2Int(fallbackGridX, fallbackGridY));
            return new Vector3(cell.x, cell.y + carryOffset, cell.z);
        }

        bool TryFindCarrierPawnWorld(
            Frame frame,
            EntityRef diceEntity,
            int gridX,
            int gridY,
            out Vector3 world)
        {
            world = default;
            if (frame == null)
            {
                return false;
            }

            var hasFallback = false;
            var fallbackPawn = default(PlayerPawn);
            var fallbackPose = default(GridPose);
            var filter = frame.Filter<PlayerPawn, GridPose>();
            while (filter.Next(out _, out var pawn, out var pose))
            {
                if (!pawn.HasCarriedDice)
                {
                    continue;
                }

                if (diceEntity.IsValid && pawn.CarriedDice == diceEntity)
                {
                    return TryBuildPawnWorld(pawn, pose, out world);
                }

                if (!hasFallback && pose.X == gridX && pose.Y == gridY)
                {
                    hasFallback = true;
                    fallbackPawn = pawn;
                    fallbackPose = pose;
                }
            }

            return hasFallback && TryBuildPawnWorld(fallbackPawn, fallbackPose, out world);
        }

        bool TryBuildPawnWorld(PlayerPawn pawn, GridPose pose, out Vector3 world)
        {
            world = default;
            if (!pawn.HasWorldPose)
            {
                if (board == null)
                {
                    return false;
                }

                var cell = board.GridToWorld(new Vector2Int(pose.X, pose.Y));
                var y = ResolvePawnStandingWorldY(pawn, pose.X, pose.Y);
                if (pawn.IsJumping)
                {
                    y += pawn.JumpOffsetY.AsFloat;
                }

                world = new Vector3(cell.x, y, cell.z);
                return true;
            }

            var footY = ResolvePawnStandingWorldY(pawn, pose.X, pose.Y);
            if (pawn.IsJumping)
            {
                footY += pawn.JumpOffsetY.AsFloat;
            }

            world = new Vector3(pawn.WorldX.AsFloat, footY, pawn.WorldZ.AsFloat);
            return true;
        }

        bool IsPawnJumpingAt(Frame frame, int gridX, int gridY, DiceStackTier tier)
        {
            if (frame == null)
            {
                return false;
            }

            var filter = frame.Filter<PlayerPawn, GridPose>();
            while (filter.Next(out _, out var pawn, out var pose))
            {
                if (!pawn.IsJumping || pawn.IsOnFloor || pose.X != gridX || pose.Y != gridY)
                {
                    continue;
                }

                if (pawn.StandingTier == tier)
                {
                    return true;
                }
            }

            return false;
        }

        void ApplyQuantumSurfaceOverride(
            DiceView diceView,
            CoreDiceState state,
            int gridX,
            int gridY)
        {
            if (diceView == null || board == null)
            {
                return;
            }

            // Base surface only — jump arc uses ApplyVisualYOffset (production path).
            var surfaceY = ResolveQuantumSurfaceBaseWorldY(state, gridX, gridY);
            var grid = new Vector2Int(gridX, gridY);
            diceView.SetNetworkSurfaceOverride(surfaceY, surfaceY, grid);
        }

        float ResolveStandingJumpOffsetY(
            Frame frame,
            int gridX,
            int gridY,
            CoreDiceStackTier tier)
        {
            if (frame == null)
            {
                return 0f;
            }

            var standingTier = tier == CoreDiceStackTier.Top
                ? DiceStackTier.Top
                : DiceStackTier.Bottom;
            var filter = frame.Filter<PlayerPawn, GridPose>();
            while (filter.Next(out _, out var pawn, out var pose))
            {
                if (!pawn.IsJumping || pawn.IsOnFloor || pose.X != gridX || pose.Y != gridY)
                {
                    continue;
                }

                if (pawn.StandingTier != standingTier)
                {
                    continue;
                }

                return pawn.JumpOffsetY.AsFloat;
            }

            return 0f;
        }

        float ResolveQuantumSurfaceBaseWorldY(CoreDiceState state, int gridX, int gridY)
        {
            if (board == null)
            {
                return 0f;
            }

            if (state.Tier != CoreDiceStackTier.Top)
            {
                return board.FloorSurfaceWorldY;
            }

            // Prefer live Bottom view height (follows sink / emerge). Else one-cell stack.
            if (TryGetStandingDiceView(gridX, gridY, DiceStackTier.Bottom, out var bottom)
                && bottom != null)
            {
                return bottom.GetLogicalTopSurfaceWorldY(board);
            }

            return board.FloorSurfaceWorldY + board.CellSize;
        }

        static bool TryResolveSlideDirection(
            int fromX,
            int fromY,
            int toX,
            int toY,
            out Direction direction,
            out int distance)
        {
            direction = default;
            distance = 0;
            var dx = toX - fromX;
            var dy = toY - fromY;
            if (dx != 0 && dy != 0)
            {
                return false;
            }

            if (dx == 0 && dy == 0)
            {
                return false;
            }

            distance = Mathf.Abs(dx) + Mathf.Abs(dy);
            if (distance < 1)
            {
                return false;
            }

            if (dx == 0 && dy > 0)
            {
                direction = Direction.North;
                return true;
            }

            if (dx == 0 && dy < 0)
            {
                direction = Direction.South;
                return true;
            }

            if (dx > 0 && dy == 0)
            {
                direction = Direction.East;
                return true;
            }

            if (dx < 0 && dy == 0)
            {
                direction = Direction.West;
                return true;
            }

            return false;
        }

        static bool TryResolveOrthogonalMove(
            int fromX,
            int fromY,
            int toX,
            int toY,
            out Direction direction,
            out int distance)
        {
            direction = default;
            distance = 0;
            var dx = toX - fromX;
            var dy = toY - fromY;
            if (dx != 0 && dy != 0)
            {
                return false;
            }

            distance = Mathf.Abs(dx) + Mathf.Abs(dy);
            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance)
            {
                return false;
            }

            if (dx == 0 && dy > 0)
            {
                direction = Direction.North;
                return true;
            }

            if (dx == 0 && dy < 0)
            {
                direction = Direction.South;
                return true;
            }

            if (dx > 0 && dy == 0)
            {
                direction = Direction.East;
                return true;
            }

            if (dx < 0 && dy == 0)
            {
                direction = Direction.West;
                return true;
            }

            return false;
        }

        static bool TryResolveRollDirection(
            int fromX,
            int fromY,
            int toX,
            int toY,
            out Direction direction)
        {
            if (!TryResolveOrthogonalMove(fromX, fromY, toX, toY, out direction, out var distance))
            {
                return false;
            }

            return distance == 1;
        }

        void UpdateDebugDice(ViewBinding binding, QuantumDice dice, int gridX, int gridY)
        {
            Vector3 world;
            if (board != null)
            {
                world = board.GridToWorld(new Vector2Int(gridX, gridY));
                world.y = dice.IsCarried
                    ? world.y + board.CellSize
                    : dice.Tier == DiceStackTier.Top
                        ? world.y + board.CellSize * 0.55f
                        : world.y;
            }
            else
            {
                world = new Vector3(gridX, dice.IsCarried ? 0.9f : 0.15f, gridY);
            }

            var color = ColorForKind(dice.Kind);
            if (dice.IsErasing && dice.EraseTicksTotal > 0)
            {
                var t = dice.EraseTicksRemaining / (float)dice.EraseTicksTotal;
                color.a = Mathf.Lerp(0.15f, color.a, Mathf.Clamp01(t));
            }

            SetColor(binding.Transform, color);
            binding.Transform.localScale = Vector3.one * diceScale;
            binding.Transform.position = world;
        }

        void UpdatePawnView(ViewBinding binding, PlayerPawn pawn, int gridX, int gridY)
        {
            var target = ResolvePawnTargetWorld(pawn, gridX, gridY, binding.IsProductionPawn);
            if (!binding.IsProductionPawn)
            {
                binding.Transform.position = target;
                SetColor(binding.Transform, new Color(0.2f, 0.65f, 1f, 1f));
                binding.Transform.localScale = Vector3.one * pawnScale;
                return;
            }

            // Continuous sim pose is authoritative; only Y follows standing surface presentation.
            binding.VisualPosition = target;
            binding.HasVisualPosition = true;
            binding.Transform.position = binding.VisualPosition;
            binding.LastGridX = gridX;
            binding.LastGridY = gridY;
        }

        Vector3 ResolvePawnTargetWorld(PlayerPawn pawn, int gridX, int gridY, bool productionPawn)
        {
            if (board == null)
            {
                return new Vector3(gridX, productionPawn ? 0f : 0.35f, gridY);
            }

            float x;
            float z;
            if (pawn.HasWorldPose)
            {
                x = pawn.WorldX.AsFloat;
                z = pawn.WorldZ.AsFloat;
            }
            else
            {
                var cell = board.GridToWorld(new Vector2Int(gridX, gridY));
                x = cell.x;
                z = cell.z;
            }

            if (!productionPawn)
            {
                return new Vector3(x, 0.35f, z);
            }

            var y = ResolvePawnStandingWorldY(pawn, gridX, gridY);
            if (pawn.IsJumping)
            {
                y += pawn.JumpOffsetY.AsFloat;
            }

            return new Vector3(x, y, z);
        }

        float ResolvePawnStandingWorldY(PlayerPawn pawn, int gridX, int gridY)
        {
            if (board == null)
            {
                return 0f;
            }

            var heightOffset = movementSettings != null
                ? movementSettings.CharacterHeightOffset
                : 0f;

            if (pawn.IsOnFloor)
            {
                return board.FloorSurfaceWorldY + heightOffset;
            }

            if (TryGetStandingDiceView(gridX, gridY, pawn.StandingTier, out var diceView)
                && diceView != null)
            {
                return diceView.GetTopSurfaceWorldY(board) + heightOffset;
            }

            // Logical fallback when the standing dice view is not bound yet.
            var tiers = pawn.StandingTier == DiceStackTier.Top ? 2 : 1;
            return board.FloorSurfaceWorldY + board.CellSize * tiers + heightOffset;
        }

        bool TryGetStandingDiceView(
            int gridX,
            int gridY,
            DiceStackTier standingTier,
            out DiceView diceView)
        {
            diceView = null;
            foreach (var pair in views)
            {
                var binding = pair.Value;
                if (binding.DiceView == null || !binding.IsProductionDice)
                {
                    continue;
                }

                if (binding.LastGridX != gridX || binding.LastGridY != gridY)
                {
                    continue;
                }

                if (binding.LastTier != standingTier)
                {
                    continue;
                }

                diceView = binding.DiceView;
                return true;
            }

            return false;
        }

        static CoreDiceOrientation ResolveOrientation(QuantumDice dice)
        {
            if (DiceOrientation.IsValid(dice.TopFace, dice.NorthFace, dice.EastFace))
            {
                return new CoreDiceOrientation(dice.TopFace, dice.NorthFace, dice.EastFace);
            }

            // Fallback keeps View/SnapTo safe if sim ever emits an invalid triad.
            return CoreDiceOrientation.CreateWithTopFace(dice.TopFace);
        }

        bool HasGameplaySettings()
        {
            return physicsSettings != null && animationSettings != null && erasureSettings != null;
        }

        ViewBinding GetOrCreateView(Frame frame, EntityRef entity)
        {
            if (views.TryGetValue(entity, out var existing) && existing.Transform != null)
            {
                return existing;
            }

            var isPawn = frame.Has<PlayerPawn>(entity);
            ViewBinding binding;
            if (productionReady)
            {
                binding = isPawn ? CreateProductionPawn() : CreateProductionDice();
            }
            else
            {
                binding = CreateDebugCube(isPawn, entity);
            }

            views[entity] = binding;
            return binding;
        }

        ViewBinding CreateProductionDice()
        {
            var go = Instantiate(dicePrefab, root);
            go.name = "QuantumDiceView";
            var controller = go.GetComponent<DiceController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            var diceView = go.GetComponent<DiceView>();
            if (diceView != null)
            {
                diceView.Configure(physicsSettings, animationSettings, erasureSettings);
                if (catalog != null && catalog.TryGetMeshPrefab(CoreDiceKind.Normal, out var mesh))
                {
                    diceView.ApplyNetworkKindPresentation(board, CoreDiceKind.Normal, mesh);
                }
            }

            return new ViewBinding
            {
                Transform = go.transform,
                DiceView = diceView,
                IsProductionDice = diceView != null,
                LastKind = QuantumDiceKind.Normal,
                LastOrientation = CoreDiceOrientation.Default,
            };
        }

        ViewBinding CreateProductionPawn()
        {
            var go = Instantiate(characterPrefab, root);
            go.name = "QuantumPawnView";
            var character = go.GetComponent<GameCharacterController>();
            if (character != null)
            {
                character.enabled = false;
            }

            var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour == character)
                {
                    continue;
                }

                var typeName = behaviour.GetType().Name;
                if (typeName.Contains("Input") || typeName.Contains("Driver") || typeName.Contains("Executor"))
                {
                    behaviour.enabled = false;
                }
            }

            return new ViewBinding
            {
                Transform = go.transform,
                IsProductionPawn = true,
            };
        }

        ViewBinding CreateDebugCube(bool isPawn, EntityRef entity)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = isPawn ? $"Pawn_{entity.Index}" : $"Dice_{entity.Index}";
            go.transform.SetParent(root, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            return new ViewBinding { Transform = go.transform };
        }

        static CoreDiceKind ToCoreKind(QuantumDiceKind kind)
        {
            return (CoreDiceKind)(int)kind;
        }

        static Color ColorForKind(QuantumDiceKind kind)
        {
            switch (kind)
            {
                case QuantumDiceKind.Wood: return new Color(0.72f, 0.45f, 0.2f, 1f);
                case QuantumDiceKind.Iron: return new Color(0.55f, 0.55f, 0.6f, 1f);
                case QuantumDiceKind.Magnet: return new Color(0.85f, 0.2f, 0.35f, 1f);
                case QuantumDiceKind.Ice: return new Color(0.55f, 0.85f, 1f, 1f);
                case QuantumDiceKind.Stone: return new Color(0.35f, 0.35f, 0.35f, 1f);
                case QuantumDiceKind.Ghost: return new Color(0.75f, 0.75f, 1f, 0.55f);
                case QuantumDiceKind.Jumbo: return new Color(0.95f, 0.55f, 0.1f, 1f);
                default: return new Color(1f, 0.85f, 0.2f, 1f);
            }
        }

        static void SetColor(Transform view, Color color)
        {
            var renderer = view.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        void ClearViews()
        {
            foreach (var pair in views)
            {
                if (pair.Value.Transform != null)
                {
                    Destroy(pair.Value.Transform.gameObject);
                }
            }

            views.Clear();
            if (root != null)
            {
                Destroy(root.gameObject);
                root = null;
            }
        }
    }
}
