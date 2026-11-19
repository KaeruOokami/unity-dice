using System;
using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public class DiceRegistry : MonoBehaviour, IDicePlacement
    {
        struct GridStack {
            public DiceController Bottom;
            public DiceController Top;
        }

        /// <summary>
        /// While set, occupancy reads treat this die as absent (AI roll-path planning).
        /// Does not mutate grid slots; nested begins for the same die are ref-counted.
        /// </summary>
        DiceController planningIgnoredDice;
        int planningIgnoreDepth;
        /// <summary>
        /// True while <see cref="BeginPlanningLayout"/> holds a temporary multi-die occupancy overlay.
        /// Nested layouts are refused. Does not mutate committed slots or CurrentState.
        /// </summary>
        bool planningLayoutActive;
        readonly Dictionary<DiceController, DiceState> planningLayoutByDie = new();
        readonly Dictionary<(Vector2Int cell, DiceStackTier tier), DiceController> planningLayoutBySlot =
            new();

        readonly Dictionary<Vector2Int, GridStack> byGrid = new();
        /// <summary>
        /// Pending fall/appear intent (not yet committed). Counts as cell occupancy for spawn
        /// lottery via <see cref="HasInFlightSpawnAt"/> together with still-IsSpawning committed
        /// dice; board updates reconcile tier via <see cref="ReconcilePendingSpawnTierAt"/>.
        /// </summary>
        readonly Dictionary<Vector2Int, GridStack> pendingOccupancy = new();
        /// <summary>
        /// Cell-level spawn claim after lottery pick, before <see cref="RegisterPendingSpawn"/>.
        /// Ensures one falling dice per cell within a multi-pick wave.
        /// </summary>
        readonly HashSet<Vector2Int> spawnCellReservations = new();
        /// <summary>
        /// Lift carry occupancy above the player's standing surface when not mounted as match Top.
        /// Floor lift mounts as committed <see cref="DiceStackTier.Top"/> instead of this map.
        /// </summary>
        readonly Dictionary<Vector2Int, DiceController> carryOccupancy = new();
        readonly List<DiceController> allDice = new();
        readonly List<Vector2Int> footprintCells = new(JumboFootprint.CellCount);
        readonly List<DiceController> footprintClearBuffer = new();
        readonly List<Vector2Int> pendingClearBuffer = new();

        Board board;

        public IReadOnlyList<DiceController> AllDice => allDice;

        public Board Board => board;

        public void Configure(Board targetBoard) {
            board = targetBoard;
        }

        /// <summary>
        /// Advance logical motion timers on all dice (lockstep tick or offline Update).
        /// </summary>
        public void TickLogicalMotions(float deltaTime) {
            if (deltaTime <= 0f) {
                return;
            }

            for (var i = 0; i < allDice.Count; i++) {
                var dice = allDice[i];
                if (dice != null) {
                    dice.TickLogicalMotion(deltaTime);
                }
            }
        }

        void Update() {
            if (GameplaySimClock.IsActive) {
                return;
            }

            TickLogicalMotions(Time.deltaTime);
        }

        public bool CanPlaceBottomDiceAt(Vector2Int gridPos) {
            if (board == null || !board.IsInside(gridPos) || board.GetCell(gridPos) != CellType.Floor) {
                return false;
            }

            // Pending spawn already claims this cell — no second Bottom while falling.
            if (HasPendingSpawnAt(gridPos)) {
                return false;
            }

            if (!TryGetBottomAt(gridPos, out var bottom) || bottom == null) {
                return true;
            }

            // Sink erasure ghost: player-solid, crushable by dice Place/Move (EvictErasingDiceAt).
            // Occupied solid Bottom is not "empty Bottom" — Top stacking uses CanPlaceTopDiceAt.
            // Interrupt promote is decided only at write time (TryPromoteCommittedBottomToTop).
            return GhostPlacementRules.IsCrushableByDicePlacement(bottom);
        }

        public bool CanPlaceTopDiceAt(Vector2Int gridPos) {
            if (board == null || !board.IsInside(gridPos) || board.GetCell(gridPos) != CellType.Floor) {
                return false;
            }

            // Pending spawn claims the whole cell (no Top onto a still-falling Bottom).
            if (HasPendingSpawnAt(gridPos)) {
                return false;
            }

            // Spawn / write capacity: any Top slot claim (including pass-through) blocks.
            if (!HasBottomAt(gridPos) || HasTopSlotClaimAt(gridPos)) {
                return false;
            }

            // Jumbo accepts stacked dice only in Bottom-only sink stage (≤ half height).
            if (TryGetBottomAt(gridPos, out var bottom) && bottom != null) {
                if (GhostPlacementRules.IsCrushableByDicePlacement(bottom)) {
                    return false;
                }

                if (bottom.Capabilities.HasExpandedFootprint
                    && bottom.WantsJumboTopOccupancy) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Pending fall/appear intent on this cell (dice already registered as spawning).
        /// </summary>
        public bool HasPendingSpawnAt(Vector2Int gridPos) {
            return HasPendingBottomAt(gridPos) || HasPendingTopAt(gridPos);
        }

        /// <summary>
        /// Cell claimed for an unfinished spawn: lottery reservation, pending fall/appear,
        /// or committed occupancy that is still <see cref="DiceController.IsSpawning"/>
        /// (e.g. BottomEmergence Place before appear completes).
        /// </summary>
        public bool HasInFlightSpawnAt(Vector2Int gridPos) {
            return spawnCellReservations.Contains(gridPos)
                || HasPendingSpawnAt(gridPos)
                || HasSpawningCommittedOccupantAt(gridPos);
        }

        /// <summary>
        /// Committed Bottom/Top still in spawn appear — blocks new spawn lottery on the cell.
        /// </summary>
        bool HasSpawningCommittedOccupantAt(Vector2Int gridPos) {
            if (TryGetBottomAt(gridPos, out var bottom)
                && bottom != null
                && bottom.IsSpawning) {
                return true;
            }

            return TryGetTopAt(gridPos, out var top)
                && top != null
                && top.IsSpawning;
        }

        /// <summary>
        /// Lottery pick claims the cell until <see cref="RegisterPendingSpawn"/> or release.
        /// </summary>
        public bool TryReserveSpawnCell(Vector2Int gridPos) {
            if (board == null || !board.IsInside(gridPos) || board.GetCell(gridPos) != CellType.Floor) {
                return false;
            }

            if (HasInFlightSpawnAt(gridPos) || HasErasingDiceAt(gridPos)) {
                return false;
            }

            spawnCellReservations.Add(gridPos);
            return true;
        }

        public void ReleaseSpawnCellReservation(Vector2Int gridPos) {
            spawnCellReservations.Remove(gridPos);
        }

        /// <summary>
        /// Top slot occupancy for writes / spawn / solid place capacity:
        /// committed Top, pending Top, or soft Top intent — including Kind Ghost.
        /// </summary>
        public bool HasTopSlotClaimAt(Vector2Int gridPos) {
            return HasTopClaimAt(gridPos, solidOnly: false);
        }

        /// <summary>
        /// Player-solid Top surface claim (standing / coupled interrupt).
        /// Pass-through Top still occupies via <see cref="HasTopSlotClaimAt"/>.
        /// </summary>
        public bool HasSolidTopClaimAt(Vector2Int gridPos) {
            return HasTopClaimAt(gridPos, solidOnly: true);
        }

        /// <summary>
        /// AI planning: treat <paramref name="dice"/> as absent in occupancy reads until disposed.
        /// </summary>
        public IDisposable BeginPlanningIgnore(DiceController dice) {
            return new PlanningIgnoreScope(this, dice);
        }

        public bool IsPlanningIgnored(DiceController dice) {
            return dice != null && planningIgnoredDice == dice;
        }

        /// <summary>
        /// AI multi-die planning: occupancy reads see participants at <paramref name="states"/>
        /// without mutating byGrid or CurrentState. Dispose clears the overlay.
        /// </summary>
        public IDisposable BeginPlanningLayout(
            IReadOnlyList<DiceController> dice,
            IReadOnlyList<DiceState> states) {
            if (dice == null || states == null || dice.Count != states.Count) {
                Debug.LogError("DiceRegistry.BeginPlanningLayout: dice/states length mismatch.");
                return NoopDisposable.Instance;
            }

            if (planningLayoutActive) {
                Debug.LogError("DiceRegistry.BeginPlanningLayout: nested layout is not supported.");
                return NoopDisposable.Instance;
            }

            if (dice.Count == 0) {
                return NoopDisposable.Instance;
            }

            planningLayoutByDie.Clear();
            planningLayoutBySlot.Clear();
            for (var i = 0; i < dice.Count; i++) {
                var die = dice[i];
                if (die == null) {
                    planningLayoutByDie.Clear();
                    planningLayoutBySlot.Clear();
                    Debug.LogError("DiceRegistry.BeginPlanningLayout: null participant.");
                    return NoopDisposable.Instance;
                }

                var state = states[i];
                var slot = (state.GridPos, state.Tier);
                if (planningLayoutBySlot.ContainsKey(slot)) {
                    planningLayoutByDie.Clear();
                    planningLayoutBySlot.Clear();
                    Debug.LogError(
                        $"DiceRegistry.BeginPlanningLayout: duplicate slot ({state.GridPos.x},{state.GridPos.y}) {state.Tier}.");
                    return NoopDisposable.Instance;
                }

                planningLayoutByDie[die] = state;
                planningLayoutBySlot[slot] = die;
            }

            planningLayoutActive = true;
            return new PlanningLayoutScope(this);
        }

        void EndPlanningLayout() {
            if (!planningLayoutActive) {
                return;
            }

            planningLayoutByDie.Clear();
            planningLayoutBySlot.Clear();
            planningLayoutActive = false;
        }

        bool TryGetPlanningLayoutAt(Vector2Int gridPos, DiceStackTier tier, out DiceController dice) {
            if (planningLayoutActive
                && planningLayoutBySlot.TryGetValue((gridPos, tier), out dice)
                && dice != null
                && !IsPlanningIgnored(dice)) {
                return true;
            }

            dice = null;
            return false;
        }

        bool IsPlanningLayoutParticipant(DiceController dice) {
            return planningLayoutActive && dice != null && planningLayoutByDie.ContainsKey(dice);
        }

        bool TryGetPlanningLayoutState(DiceController dice, out DiceState state) {
            if (planningLayoutActive && dice != null && planningLayoutByDie.TryGetValue(dice, out state)) {
                return true;
            }

            state = default;
            return false;
        }

        void BeginPlanningIgnoreInternal(DiceController dice) {
            if (dice == null) {
                return;
            }

            if (planningIgnoredDice != null && planningIgnoredDice != dice) {
                Debug.LogError(
                    $"DiceRegistry.BeginPlanningIgnore: already ignoring {planningIgnoredDice.name}, " +
                    $"cannot ignore {dice.name}");
                return;
            }

            planningIgnoredDice = dice;
            planningIgnoreDepth++;
        }

        void EndPlanningIgnoreInternal(DiceController dice) {
            if (dice == null || planningIgnoredDice != dice) {
                return;
            }

            planningIgnoreDepth--;
            if (planningIgnoreDepth <= 0) {
                planningIgnoredDice = null;
                planningIgnoreDepth = 0;
            }
        }

        readonly struct PlanningIgnoreScope : IDisposable
        {
            readonly DiceRegistry registry;
            readonly DiceController dice;

            public PlanningIgnoreScope(DiceRegistry registry, DiceController dice) {
                this.registry = registry;
                this.dice = dice;
                registry?.BeginPlanningIgnoreInternal(dice);
            }

            public void Dispose() {
                registry?.EndPlanningIgnoreInternal(dice);
            }
        }

        sealed class PlanningLayoutScope : IDisposable
        {
            readonly DiceRegistry registry;
            bool disposed;

            public PlanningLayoutScope(DiceRegistry registry) {
                this.registry = registry;
            }

            public void Dispose() {
                if (disposed) {
                    return;
                }

                disposed = true;
                registry?.EndPlanningLayout();
            }
        }

        sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();

            public void Dispose() {
            }
        }

        bool HasTopClaimAt(Vector2Int gridPos, bool solidOnly) {
            if (TryGetTopAt(gridPos, out var top)
                && top != null
                && (!solidOnly || GhostPlacementRules.CountsAsSolidTopClaim(top))) {
                return true;
            }

            if (TryGetPendingTopAt(gridPos, out var pendingTop)
                && pendingTop != null
                && !IsPlanningIgnored(pendingTop)
                && (!solidOnly || GhostPlacementRules.CountsAsSolidTopClaim(pendingTop))) {
                return true;
            }

            for (var i = 0; i < allDice.Count; i++) {
                var dice = allDice[i];
                // Carried occupancy is only committed Top (floor lift) or carryOccupancy.
                if (dice == null || dice.IsCarried || IsPlanningIgnored(dice)) {
                    continue;
                }

                var state = TryGetPlanningLayoutState(dice, out var planned)
                    ? planned
                    : dice.CurrentState;
                if (state.GridPos != gridPos || state.Tier != DiceStackTier.Top) {
                    continue;
                }

                if (solidOnly && !GhostPlacementRules.CountsAsSolidTopClaim(dice)) {
                    continue;
                }

                // Soft claim: logical Top but not this cell's committed Top slot.
                if (!TryGetTopAt(gridPos, out var committedTop) || committedTop != dice) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Elevated carry slot (standing above Bottom/Top). Not a normal Top stack claim.
        /// </summary>
        public bool TryGetCarryAt(Vector2Int gridPos, out DiceController dice) {
            return carryOccupancy.TryGetValue(gridPos, out dice) && dice != null;
        }

        public bool HasCarryAt(Vector2Int gridPos) {
            return TryGetCarryAt(gridPos, out _);
        }

        /// <summary>
        /// Register lift carry above the standing surface without claiming Bottom/Top.
        /// </summary>
        public void SetCarryOccupancy(DiceController dice, Vector2Int gridPos) {
            if (dice == null) {
                return;
            }

            ClearCarryOccupancy(dice);
            if (carryOccupancy.TryGetValue(gridPos, out var existing)
                && existing != null
                && existing != dice) {
                Debug.LogError(
                    $"DiceRegistry: carry slot occupied at ({gridPos.x},{gridPos.y}) " +
                    $"by {existing.name}; rejecting {dice.name}.");
                return;
            }

            carryOccupancy[gridPos] = dice;
        }

        public void ClearCarryOccupancy(DiceController dice) {
            if (dice == null || carryOccupancy.Count == 0) {
                return;
            }

            Vector2Int? clearKey = null;
            foreach (var pair in carryOccupancy) {
                if (pair.Value != dice) {
                    continue;
                }

                clearKey = pair.Key;
                break;
            }

            if (clearKey.HasValue) {
                carryOccupancy.Remove(clearKey.Value);
            }
        }

        /// <summary>
        /// True when an existing Bottom can be moved to Top to free the Bottom slot.
        /// </summary>
        public bool CanPromoteBottomOccupantAt(Vector2Int gridPos) {
            return !HasTopSlotClaimAt(gridPos);
        }

        public bool CanAcceptTopDiceAt(Vector2Int gridPos) {
            if (CanPlaceTopDiceAt(gridPos)) {
                return true;
            }

            // Crushable Top (Radiance / equivalent): dice Place/Move may overwrite.
            return TryGetTopAt(gridPos, out var top)
                && GhostPlacementRules.IsCrushableByDicePlacement(top);
        }

        public bool HasErasingDiceAt(Vector2Int gridPos) {
            foreach (var dice in allDice) {
                if (dice != null && dice.IsErasing && dice.CurrentState.GridPos == gridPos) {
                    return true;
                }
            }

            return false;
        }

        public void RemoveFromGrid(DiceController dice) {
            if (dice == null) {
                return;
            }

            ClearCarryOccupancy(dice);
            Remove(dice, dice.CurrentState.GridPos, dice.CurrentState.Tier);
        }

        public void EvictErasingDiceAt(Vector2Int gridPos) {
            DiceController topErasing = null;
            DiceController bottomGhost = null;
            DiceController topRadiance = null;
            DiceController bottomRadiance = null;
            DiceController emergingGhost = null;

            foreach (var dice in allDice) {
                if (dice == null || dice.CurrentState.GridPos != gridPos) {
                    continue;
                }

                if (dice.IsEmergingGhost && dice.CurrentState.Tier == DiceStackTier.Bottom) {
                    emergingGhost = dice;
                    continue;
                }

                if (!dice.IsErasing) {
                    continue;
                }

                if (dice.IsErasureGhost) {
                    if (dice.CurrentState.Tier == DiceStackTier.Top) {
                        topErasing = dice;
                    } else {
                        bottomGhost = dice;
                    }

                    continue;
                }

                if (dice.IsRadianceErasing) {
                    if (dice.CurrentState.Tier == DiceStackTier.Top) {
                        topRadiance = dice;
                    } else {
                        bottomRadiance = dice;
                    }
                }
            }

            topErasing?.CompleteErasureFromOverride();
            bottomGhost?.CompleteErasureFromOverride();
            emergingGhost?.CompleteEmergingCrushFromOverride();
            topRadiance?.CompleteErasureFromOverride();
            bottomRadiance?.CompleteErasureFromOverride();
        }

        public void RegisterPendingSpawn(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
                return;
            }

            ClearPendingOccupancy(dice);

            if (!allDice.Contains(dice)) {
                allDice.Add(dice);
            }

            if (dice.Capabilities.HasExpandedFootprint) {
                footprintCells.Clear();
                JumboFootprint.AppendCells(gridPos, footprintCells);
                for (var i = 0; i < footprintCells.Count; i++) {
                    ReleaseSpawnCellReservation(footprintCells[i]);
                    RegisterPendingAtCell(dice, footprintCells[i], DiceStackTier.Bottom);
                    RegisterPendingAtCell(dice, footprintCells[i], DiceStackTier.Top);
                }

                return;
            }

            ReleaseSpawnCellReservation(gridPos);
            RegisterPendingAtCell(dice, gridPos, tier);
        }

        public void ReleaseOccupancyReservation(DiceController dice) {
            ClearPendingOccupancy(dice);
        }

        void RegisterPendingAtCell(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (!pendingOccupancy.TryGetValue(gridPos, out var stack)) {
                stack = default;
            }

            if (tier == DiceStackTier.Top) {
                stack.Top = dice;
            } else {
                stack.Bottom = dice;
            }

            pendingOccupancy[gridPos] = stack;

            if (tier == DiceStackTier.Bottom) {
                ReconcilePendingSpawnTierAt(gridPos, excluding: dice);
            }
        }

        /// <summary>
        /// Committed Bottom support for spawn retarget / stack (excludes <paramref name="self"/>).
        /// </summary>
        bool HasCommittedBottomSupport(Vector2Int gridPos, DiceController self) {
            return TryGetBottomAt(gridPos, out var bottom)
                && bottom != null
                && bottom != self;
        }

        /// <summary>
        /// Move a spawning pending Bottom to Top when committed Bottom support appears.
        /// </summary>
        bool TryLiftSpawningPendingBottomToTop(Vector2Int gridPos) {
            if (!TryGetPendingBottomAt(gridPos, out var pending)
                || pending == null
                || !pending.IsSpawning) {
                return false;
            }

            if (HasTopSlotClaimAt(gridPos)) {
                return false;
            }

            if (!pendingOccupancy.TryGetValue(gridPos, out var pendingStack)) {
                return false;
            }

            pendingStack.Bottom = null;
            pendingStack.Top = pending;
            pendingOccupancy[gridPos] = pendingStack;
            pending.NotifyPendingSpawnRetargetedToTop();
            return true;
        }

        /// <summary>
        /// Incoming Bottom claims the slot: promote the existing committed Bottom to Top when free.
        /// </summary>
        bool TryPromoteCommittedBottomToTop(Vector2Int gridPos, DiceController incoming) {
            if (!TryGetBottomAt(gridPos, out var existing)
                || existing == null
                || existing == incoming) {
                return true;
            }

            if (!CanPromoteBottomOccupantAt(gridPos)) {
                return false;
            }

            ClearDiceAt(gridPos, existing, DiceStackTier.Bottom, notifySupportLoss: false);
            if (!byGrid.TryGetValue(gridPos, out var stack)) {
                stack = default;
            }

            stack.Top = existing;
            byGrid[gridPos] = stack;
            existing.BeginVerticalRestToState(
                new DiceState(
                    gridPos,
                    existing.CurrentState.Orientation,
                    DiceStackTier.Top,
                    existing.Kind));
            return true;
        }

        public void CommitPendingSpawn(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
                return;
            }

            ClearPendingOccupancy(dice);

            if (dice.Capabilities.HasExpandedFootprint) {
                PlaceJumbo(dice, gridPos);
                return;
            }

            var resolvedTier = ResolveSpawnCommitTier(gridPos, tier);
            if (resolvedTier != tier) {
                dice.ApplyExternalState(
                    new DiceState(
                        gridPos,
                        dice.CurrentState.Orientation,
                        resolvedTier,
                        dice.Kind),
                    snapVisual: true);
            }

            Place(dice, gridPos, resolvedTier);
        }

        /// <summary>
        /// Resolve final tier at spawn commit from committed occupancy only.
        /// </summary>
        DiceStackTier ResolveSpawnCommitTier(Vector2Int gridPos, DiceStackTier requestedTier) {
            var hasSupport = HasCommittedBottomSupport(gridPos, self: null);

            if (requestedTier == DiceStackTier.Bottom) {
                if (!hasSupport) {
                    return DiceStackTier.Bottom;
                }

                if (!HasTopAt(gridPos)) {
                    return DiceStackTier.Top;
                }

                Debug.LogError(
                    $"DiceRegistry: spawn commit cannot place at ({gridPos.x},{gridPos.y}); " +
                    "Bottom occupied and Top unavailable.");
                return DiceStackTier.Top;
            }

            if (hasSupport) {
                if (HasTopAt(gridPos)) {
                    Debug.LogError(
                        $"DiceRegistry: spawn commit Top unavailable at ({gridPos.x},{gridPos.y}).");
                }

                return DiceStackTier.Top;
            }

            if (HasTopAt(gridPos)) {
                Debug.LogError(
                    $"DiceRegistry: spawn commit Top demote blocked at ({gridPos.x},{gridPos.y}); " +
                    "Top already occupied.");
                return DiceStackTier.Top;
            }

            return DiceStackTier.Bottom;
        }

        public void Place(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
                return;
            }

            if (dice.Capabilities.HasExpandedFootprint) {
                PlaceJumbo(dice, gridPos);
                return;
            }

            ReleaseSpawnCellReservation(gridPos);

            if (!allDice.Contains(dice)) {
                allDice.Add(dice);
            }

            EvictErasingDiceAt(gridPos);

            if (tier == DiceStackTier.Top
                && TryGetBottomAt(gridPos, out var ghostBottom)
                && !HasTopAt(gridPos)
                && GhostPlacementRules.ShouldInCellPromoteOnTopPlacement(ghostBottom, dice.Kind)) {
                ClearDiceAt(gridPos, ghostBottom, DiceStackTier.Bottom, notifySupportLoss: false);
                SetDiceAt(gridPos, dice, DiceStackTier.Bottom);
                SetDiceAt(gridPos, ghostBottom, DiceStackTier.Top);
                dice.ApplyExternalState(
                    new DiceState(
                        gridPos,
                        dice.CurrentState.Orientation,
                        DiceStackTier.Bottom,
                        dice.Kind),
                    snapVisual: true);
                ghostBottom.ApplyExternalState(
                    new DiceState(
                        gridPos,
                        ghostBottom.CurrentState.Orientation,
                        DiceStackTier.Top,
                        ghostBottom.Kind),
                    snapVisual: true);
                return;
            }

            SetDiceAt(gridPos, dice, tier);
        }

        void PlaceJumbo(DiceController dice, Vector2Int anchor) {
            if (!allDice.Contains(dice)) {
                allDice.Add(dice);
            }

            ClearOccupantsForJumboLanding(anchor, excluding: dice);
            footprintCells.Clear();
            JumboFootprint.AppendCells(anchor, footprintCells);
            for (var i = 0; i < footprintCells.Count; i++) {
                var cell = footprintCells[i];
                ReleaseSpawnCellReservation(cell);
                SetDiceAt(cell, dice, DiceStackTier.Bottom);
                SetDiceAt(cell, dice, DiceStackTier.Top);
            }

            if (dice.CurrentState.GridPos != anchor
                || dice.CurrentState.Tier != DiceStackTier.Bottom) {
                dice.ApplyExternalState(
                    new DiceState(
                        anchor,
                        dice.CurrentState.Orientation,
                        DiceStackTier.Bottom,
                        dice.Kind),
                    snapVisual: true);
            }
        }

        /// <summary>
        /// Jumbo sink: progress &lt; threshold keeps Top+Bottom footprint; at/above threshold
        /// releases Top so only Bottom remains (rollback restores Top when no stacked die blocks it).
        /// </summary>
        /// <returns>True when Top occupancy changed.</returns>
        public bool SyncJumboSinkOccupancy(DiceController dice) {
            if (dice == null || !dice.Capabilities.HasExpandedFootprint) {
                return false;
            }

            var wantTop = dice.WantsJumboTopOccupancy;
            var anchor = dice.CurrentState.GridPos;
            footprintCells.Clear();
            JumboFootprint.AppendCells(anchor, footprintCells);

            // Stacked Top dice block reclaim — jumbo stays Bottom-only until they leave.
            if (wantTop) {
                for (var i = 0; i < footprintCells.Count; i++) {
                    if (TryGetTopAt(footprintCells[i], out var top)
                        && top != null
                        && top != dice) {
                        wantTop = false;
                        break;
                    }
                }
            }

            var topChanged = false;
            for (var i = 0; i < footprintCells.Count; i++) {
                var cell = footprintCells[i];
                if (wantTop) {
                    if (!TryGetTopAt(cell, out var top) || top != dice) {
                        SetDiceAt(cell, dice, DiceStackTier.Top);
                        topChanged = true;
                    }
                } else if (TryGetTopAt(cell, out var top) && top == dice) {
                    ClearDiceAt(cell, dice, DiceStackTier.Top, notifySupportLoss: false);
                    topChanged = true;
                }

                if (!TryGetBottomAt(cell, out var bottom) || bottom != dice) {
                    SetDiceAt(cell, dice, DiceStackTier.Bottom);
                }
            }

            return topChanged;
        }

        /// <summary>
        /// Sync visual height of any Top dice stacked on a jumbo footprint (or single cell).
        /// </summary>
        public void SyncStackedTopsForDice(DiceController support, Board targetBoard) {
            if (support == null || targetBoard == null) {
                return;
            }

            if (!support.Capabilities.HasExpandedFootprint) {
                SyncStackedTopAt(support.CurrentState.GridPos, targetBoard, support);
                return;
            }

            footprintCells.Clear();
            JumboFootprint.AppendCells(support.CurrentState.GridPos, footprintCells);
            for (var i = 0; i < footprintCells.Count; i++) {
                SyncStackedTopAt(footprintCells[i], targetBoard, support);
            }
        }

        void ClearOccupantsForJumboLanding(Vector2Int anchor, DiceController excluding) {
            footprintClearBuffer.Clear();
            footprintCells.Clear();
            JumboFootprint.AppendCells(anchor, footprintCells);

            for (var i = 0; i < footprintCells.Count; i++) {
                var cell = footprintCells[i];
                if (TryGetBottomAt(cell, out var bottom)
                    && bottom != null
                    && bottom != excluding
                    && !footprintClearBuffer.Contains(bottom)) {
                    footprintClearBuffer.Add(bottom);
                }

                if (TryGetTopAt(cell, out var top)
                    && top != null
                    && top != excluding
                    && !footprintClearBuffer.Contains(top)) {
                    footprintClearBuffer.Add(top);
                }
            }

            for (var i = 0; i < allDice.Count; i++) {
                var dice = allDice[i];
                if (dice == null || dice == excluding || footprintClearBuffer.Contains(dice)) {
                    continue;
                }

                if (OccupiesAnyCellInAnchorFootprint(dice, anchor)) {
                    footprintClearBuffer.Add(dice);
                }
            }

            for (var i = 0; i < footprintClearBuffer.Count; i++) {
                footprintClearBuffer[i].ForceDestroyForOverride();
            }

            footprintClearBuffer.Clear();
        }

        static bool OccupiesAnyCellInAnchorFootprint(DiceController dice, Vector2Int anchor) {
            if (dice == null) {
                return false;
            }

            if (!dice.Capabilities.HasExpandedFootprint) {
                return JumboFootprint.Contains(anchor, dice.CurrentState.GridPos);
            }

            var otherAnchor = dice.CurrentState.GridPos;
            for (var dx = 0; dx < JumboFootprint.Size; dx++) {
                for (var dy = 0; dy < JumboFootprint.Size; dy++) {
                    var cell = new Vector2Int(otherAnchor.x + dx, otherAnchor.y + dy);
                    if (JumboFootprint.Contains(anchor, cell)) {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryApplyGhostLanding(
            DiceController mover,
            DiceState moverFrom,
            DiceState moverTo,
            GhostLandingMode mode,
            DiceState ghostFrom,
            DiceState ghostTo) {
            if (mover == null || mode == GhostLandingMode.None) {
                return false;
            }

            if (mode == GhostLandingMode.CellSwapWithInCellPromote) {
                return TryApplyGhostStackTopEntry(mover, moverFrom, moverTo, ghostFrom, ghostTo);
            }

            if (!TryDeferGhostOccupant(ghostFrom, ghostTo, out var ghost)) {
                return false;
            }

            ClearDiceAt(moverFrom.GridPos, mover, moverFrom.Tier, notifySupportLoss: false);
            SetDiceAt(moverTo.GridPos, mover, moverTo.Tier);
            if (!TryCompleteDeferredGhostLanding(ghost, ghostFrom, ghostTo)) {
                return false;
            }

            TryResolveUnsupportedTopAt(moverFrom.GridPos, mover);
            TryResolveUnsupportedTopAt(ghostFrom.GridPos, ghost);
            return true;
        }

        /// <summary>
        /// Final occupancy for Top entry into ghost Bottom + ghost Top:
        /// mover → Bottom, bottom ghost → Top, exiting top ghost → <paramref name="ghostTo"/>.
        /// </summary>
        bool TryApplyGhostStackTopEntry(
            DiceController mover,
            DiceState moverFrom,
            DiceState moverTo,
            DiceState exitingTopFrom,
            DiceState exitingTopTo) {
            if (moverTo.Tier != DiceStackTier.Bottom
                || !TryDeferGhostOccupant(exitingTopFrom, exitingTopTo, out var exitingTop)) {
                return false;
            }

            if (!TryGetBottomAt(moverTo.GridPos, out var bottomGhost)
                || bottomGhost == null
                || !GhostPlacementRules.AllowsDiceSwapThrough(bottomGhost)) {
                Debug.LogError(
                    $"DiceRegistry: ghost stack entry missing Bottom ghost at ({moverTo.GridPos.x},{moverTo.GridPos.y})");
                return false;
            }

            var bottomFrom = bottomGhost.CurrentState;
            var bottomTo = new DiceState(
                moverTo.GridPos,
                bottomFrom.Orientation,
                DiceStackTier.Top,
                bottomFrom.Kind);
            if (!TryDeferGhostOccupant(bottomFrom, bottomTo, out bottomGhost)) {
                return false;
            }

            ClearDiceAt(moverFrom.GridPos, mover, moverFrom.Tier, notifySupportLoss: false);
            SetDiceAt(moverTo.GridPos, mover, DiceStackTier.Bottom);
            if (!TryCompleteDeferredGhostLanding(exitingTop, exitingTopFrom, exitingTopTo)) {
                return false;
            }

            if (!TryCompleteDeferredGhostLanding(bottomGhost, bottomFrom, bottomTo)) {
                return false;
            }

            TryResolveUnsupportedTopAt(moverFrom.GridPos, mover);
            TryResolveUnsupportedTopAt(exitingTopFrom.GridPos, exitingTop);
            return true;
        }

        /// <summary>
        /// Defer both ghosts for <see cref="GhostLandingMode.CellSwapWithInCellPromote"/> visuals.
        /// </summary>
        public bool TryDeferGhostStackTopEntry(
            DiceState moverTo,
            DiceState exitingTopFrom,
            DiceState exitingTopTo,
            out DiceState bottomGhostFrom,
            out DiceState bottomGhostTo) {
            bottomGhostFrom = default;
            bottomGhostTo = default;
            if (!TryDeferGhostOccupant(exitingTopFrom, exitingTopTo, out _)) {
                return false;
            }

            if (!TryGetBottomAt(moverTo.GridPos, out var bottomGhost)
                || bottomGhost == null
                || !GhostPlacementRules.AllowsDiceSwapThrough(bottomGhost)) {
                return false;
            }

            bottomGhostFrom = bottomGhost.CurrentState;
            bottomGhostTo = new DiceState(
                moverTo.GridPos,
                bottomGhostFrom.Orientation,
                DiceStackTier.Top,
                bottomGhostFrom.Kind);
            return TryDeferGhostOccupant(bottomGhostFrom, bottomGhostTo, out _);
        }

        public void CompleteDeferredGhostStackTopEntry(
            DiceState exitingTopFrom,
            DiceState exitingTopTo,
            DiceState bottomGhostFrom,
            DiceState bottomGhostTo,
            Action<DiceController, DiceController> onComplete = null) {
            TryCompleteDeferredGhostLanding(exitingTopFrom, exitingTopTo, topGhost => {
                TryCompleteDeferredGhostLanding(bottomGhostFrom, bottomGhostTo, bottomGhost => {
                    if (topGhost != null) {
                        ResolveUnsupportedTopAt(topGhost.CurrentState.GridPos);
                    }

                    if (bottomGhost != null) {
                        ResolveUnsupportedTopAt(bottomGhost.CurrentState.GridPos);
                    }

                    onComplete?.Invoke(topGhost, bottomGhost);
                });
            });
        }

        /// <summary>
        /// Remove ghost from the grid slot and leave <see cref="DiceController.CurrentState"/>
        /// unchanged until displace completes. Destination is not hard-reserved — occupants
        /// that arrive first are stacked up on complete via Bottom→Top promote.
        /// </summary>
        public bool TryDeferGhostOccupant(
            DiceState ghostFrom,
            DiceState ghostTo,
            out DiceController ghost) {
            ghost = null;
            if (!TryGetDiceAt(ghostFrom.GridPos, ghostFrom.Tier, out ghost) || ghost == null) {
                Debug.LogError(
                    $"DiceRegistry: deferred ghost missing at ({ghostFrom.GridPos.x},{ghostFrom.GridPos.y}) tier={ghostFrom.Tier}");
                return false;
            }

            if (!GhostPlacementRules.AllowsDiceSwapThrough(ghost)) {
                Debug.LogError($"DiceRegistry: deferred ghost is not swap-through kind={ghost.Kind}");
                ghost = null;
                return false;
            }

            ClearDiceAt(ghostFrom.GridPos, ghost, ghostFrom.Tier, notifySupportLoss: false);
            _ = ghostTo;
            return true;
        }

        /// <summary>
        /// Place a previously deferred ghost at its destination and play displace visual.
        /// </summary>
        public bool TryCompleteDeferredGhostLanding(
            DiceController ghost,
            DiceState ghostFrom,
            DiceState ghostTo,
            Action<DiceController> onComplete = null) {
            if (ghost == null) {
                onComplete?.Invoke(null);
                return false;
            }

            if (!TryWriteDiceAt(ghostTo.GridPos, ghost, ghostTo.Tier)) {
                onComplete?.Invoke(null);
                return false;
            }

            ghost.ApplyExternalState(ghostTo, snapVisual: false);
            ghost.PlayGhostDisplaceVisual(ghostFrom, ghostTo, () => onComplete?.Invoke(ghost));
            return true;
        }

        public bool TryCompleteDeferredGhostLanding(
            DiceState ghostFrom,
            DiceState ghostTo,
            Action<DiceController> onComplete = null) {
            // Ghost was deferred: still holds ghostFrom in CurrentState, not in grid.
            DiceController ghost = null;
            foreach (var dice in allDice) {
                if (dice == null || !GhostPlacementRules.AllowsDiceSwapThrough(dice)) {
                    continue;
                }

                var state = dice.CurrentState;
                if (state.GridPos == ghostFrom.GridPos && state.Tier == ghostFrom.Tier) {
                    ghost = dice;
                    break;
                }
            }

            if (ghost == null) {
                Debug.LogError(
                    $"DiceRegistry: deferred ghost not found for complete at ({ghostFrom.GridPos.x},{ghostFrom.GridPos.y}) tier={ghostFrom.Tier}");
                onComplete?.Invoke(null);
                return false;
            }

            return TryCompleteDeferredGhostLanding(ghost, ghostFrom, ghostTo, onComplete);
        }

        public void Remove(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
                return;
            }

            if (dice.Capabilities.HasExpandedFootprint) {
                footprintCells.Clear();
                JumboFootprint.AppendCells(dice.CurrentState.GridPos, footprintCells);
                for (var i = 0; i < footprintCells.Count; i++) {
                    ClearDiceAt(footprintCells[i], dice, DiceStackTier.Bottom, notifySupportLoss: false);
                    ClearDiceAt(footprintCells[i], dice, DiceStackTier.Top, notifySupportLoss: false);
                }

                return;
            }

            ClearDiceAt(gridPos, dice, tier);
        }

        public void Unregister(DiceController dice) {
            if (dice == null) {
                return;
            }

            ClearPendingOccupancy(dice);
            ClearCarryOccupancy(dice);
            Remove(dice, dice.CurrentState.GridPos, dice.CurrentState.Tier);
            allDice.Remove(dice);
        }

        /// <returns>False when the destination write was refused (die restored to <paramref name="from"/>).</returns>
        public bool MoveDice(
            DiceController dice,
            Vector2Int from,
            Vector2Int to,
            DiceStackTier fromTier,
            DiceStackTier toTier) {
            EvictErasingDiceAt(to);
            ClearDiceAt(from, dice, fromTier, notifySupportLoss: false);
            if (!TryWriteDiceAt(to, dice, toTier)) {
                // Keep the die on the board; planning should have refused this target.
                TryWriteDiceAt(from, dice, fromTier);
                return false;
            }

            TryResolveUnsupportedTopAt(from, dice);
            return true;
        }

        /// <summary>
        /// Stack support for a Top die: committed Bottom of any kind (including Kind Ghost).
        /// Distinct from player-solid checks (<see cref="GhostPlacementRules.HasSolidBottomAt"/>).
        /// </summary>
        public bool HasStackBottomSupportAt(Vector2Int gridPos) {
            return HasBottomAt(gridPos);
        }

        public bool TryGetDiceAt(Vector2Int gridPos, DiceStackTier tier, out DiceController dice) {
            return tier == DiceStackTier.Top
                ? TryGetTopAt(gridPos, out dice)
                : TryGetBottomAt(gridPos, out dice);
        }

        public bool TryGetBottomAt(Vector2Int gridPos, out DiceController dice) {
            if (TryGetPlanningLayoutAt(gridPos, DiceStackTier.Bottom, out dice)) {
                return true;
            }

            dice = null;
            if (!byGrid.TryGetValue(gridPos, out var stack) || stack.Bottom == null) {
                return false;
            }

            if (IsPlanningIgnored(stack.Bottom) || IsPlanningLayoutParticipant(stack.Bottom)) {
                return false;
            }

            dice = stack.Bottom;
            return true;
        }

        public bool TryGetBottomIncludingPending(Vector2Int gridPos, out DiceController dice) {
            if (TryGetBottomAt(gridPos, out dice)) {
                return true;
            }

            return TryGetPendingBottomAt(gridPos, out dice);
        }

        public void SyncStackedTopAt(Vector2Int gridPos, Board board, DiceController exclude = null) {
            if (board == null || !TryGetTopAt(gridPos, out var top) || top == null) {
                return;
            }

            // A jumbo occupies its own Top tier across its footprint. When syncing
            // stacked dice for that jumbo it must not sync itself, or the visual
            // update path recurses (SyncStackedSurface -> ApplyErasureVisual ->
            // NotifyErasureProgressChanged -> SyncStackedTopsForDice) indefinitely.
            if (top == exclude) {
                return;
            }

            top.View?.SyncStackedSurface(top.CurrentState, board, this);
        }

        public bool TryGetTopAt(Vector2Int gridPos, out DiceController dice) {
            if (TryGetPlanningLayoutAt(gridPos, DiceStackTier.Top, out dice)) {
                return true;
            }

            dice = null;
            if (!byGrid.TryGetValue(gridPos, out var stack) || stack.Top == null) {
                return false;
            }

            if (IsPlanningIgnored(stack.Top) || IsPlanningLayoutParticipant(stack.Top)) {
                return false;
            }

            dice = stack.Top;
            return true;
        }

        public bool TryGetAt(Vector2Int gridPos, out DiceController dice) {
            if (TryGetTopAt(gridPos, out dice)) {
                return true;
            }

            return TryGetBottomAt(gridPos, out dice);
        }

        public bool HasTopAt(Vector2Int gridPos) {
            return TryGetTopAt(gridPos, out _);
        }

        public bool HasBottomAt(Vector2Int gridPos) {
            return TryGetBottomAt(gridPos, out _);
        }

        public bool HasPendingBottomAt(Vector2Int gridPos) {
            return pendingOccupancy.TryGetValue(gridPos, out var stack) && stack.Bottom != null;
        }

        public bool HasPendingTopAt(Vector2Int gridPos) {
            return pendingOccupancy.TryGetValue(gridPos, out var stack) && stack.Top != null;
        }

        public bool TryGetPendingTopAt(Vector2Int gridPos, out DiceController dice) {
            dice = null;
            if (!pendingOccupancy.TryGetValue(gridPos, out var stack) || stack.Top == null) {
                return false;
            }

            dice = stack.Top;
            return true;
        }

        public bool TryGetPendingBottomAt(Vector2Int gridPos, out DiceController dice) {
            dice = null;
            if (!pendingOccupancy.TryGetValue(gridPos, out var stack) || stack.Bottom == null) {
                return false;
            }

            dice = stack.Bottom;
            return true;
        }

        void ClearPendingOccupancy(DiceController dice) {
            if (dice == null || pendingOccupancy.Count == 0) {
                return;
            }

            pendingClearBuffer.Clear();
            foreach (var pair in pendingOccupancy) {
                var stack = pair.Value;
                var changed = false;

                if (stack.Bottom == dice) {
                    stack.Bottom = null;
                    changed = true;
                }

                if (stack.Top == dice) {
                    stack.Top = null;
                    changed = true;
                }

                if (!changed) {
                    continue;
                }

                if (stack.Bottom == null && stack.Top == null) {
                    pendingClearBuffer.Add(pair.Key);
                } else {
                    pendingOccupancy[pair.Key] = stack;
                }
            }

            for (var i = 0; i < pendingClearBuffer.Count; i++) {
                pendingOccupancy.Remove(pendingClearBuffer[i]);
            }

            pendingClearBuffer.Clear();
        }

        public bool BlocksTraversalBetween(Vector2Int fromCell, Vector2Int toCell) {
            return board != null && board.BlocksMovement(fromCell, toCell, null);
        }

        public DiceController GetNeighbor(DiceController dice, Direction direction) {
            if (dice == null) {
                return null;
            }

            var neighborPos = dice.CurrentState.GridPos + direction.ToGridDelta();
            if (dice.CurrentState.Tier == DiceStackTier.Top) {
                TryGetTopAt(neighborPos, out var topNeighbor);
                return topNeighbor;
            }

            TryGetBottomAt(neighborPos, out var bottomNeighbor);
            return bottomNeighbor;
        }

        public DiceController GetFacingDiceAt(DiceController fromDice, Direction direction) {
            if (fromDice == null) {
                return null;
            }

            var neighborPos = fromDice.CurrentState.GridPos + direction.ToGridDelta();
            if (fromDice.CurrentState.Tier == DiceStackTier.Top) {
                TryGetTopAt(neighborPos, out var topDice);
                return topDice;
            }

            TryGetBottomAt(neighborPos, out var bottomDice);
            return bottomDice;
        }

        public DiceController GetTransferTargetAt(
            DiceController fromDice,
            Vector2Int fromCell,
            Direction direction,
            int fromLevel) {
            if (fromDice == null) {
                return null;
            }

            var anchor = fromDice.CurrentState.GridPos;
            var useFootprintOrigin = fromDice.Capabilities.HasExpandedFootprint
                && JumboFootprint.Contains(anchor, fromCell);
            var neighborPos = useFootprintOrigin
                ? fromCell + direction.ToGridDelta()
                : anchor + direction.ToGridDelta();

            if (useFootprintOrigin
                && JumboFootprint.Contains(anchor, neighborPos)) {
                return fromDice;
            }

            if (SurfaceHeightLevel.IsAtOrAboveTop(fromLevel)) {
                if (TryGetTopAt(neighborPos, out var top)
                    && top != null
                    && !GhostPlacementRules.IsPlayerPassThrough(top)) {
                    return top;
                }

                return null;
            }

            TryGetBottomAt(neighborPos, out var bottom);
            if (bottom != null && !GhostPlacementRules.IsPlayerPassThrough(bottom)) {
                return bottom;
            }

            if (TryGetPendingBottomAt(neighborPos, out var pendingBottom)
                && pendingBottom != null
                && pendingBottom.AllowsUnconditionalMount
                && !GhostPlacementRules.IsPlayerPassThrough(pendingBottom)) {
                return pendingBottom;
            }

            return null;
        }

        public DiceController ResolveSupportBottom(DiceController dice) {
            if (dice == null) {
                return null;
            }

            if (dice.CurrentState.Tier == DiceStackTier.Bottom) {
                return dice;
            }

            TryGetBottomAt(dice.CurrentState.GridPos, out var bottom);
            return bottom;
        }

        public bool AnyRolling() {
            foreach (var dice in allDice) {
                if (dice != null && dice.IsRolling) {
                    return true;
                }
            }

            return false;
        }

        public bool AnyCarried() {
            foreach (var dice in allDice) {
                if (dice != null && dice.IsCarried) {
                    return true;
                }
            }

            return false;
        }

        void SetDiceAt(Vector2Int gridPos, DiceController dice, DiceStackTier tier) {
            TryWriteDiceAt(gridPos, dice, tier);
        }

        /// <summary>
        /// Commit a die into a grid slot. Occupied Bottom is promoted to Top when free so
        /// in-flight dice can interrupt without hard cell locks.
        /// </summary>
        bool TryWriteDiceAt(Vector2Int gridPos, DiceController dice, DiceStackTier tier) {
            if (tier == DiceStackTier.Bottom) {
                TryLiftSpawningPendingBottomToTop(gridPos);
                if (!TryPromoteCommittedBottomToTop(gridPos, dice)) {
                    Debug.LogError(
                        $"DiceRegistry: cannot claim Bottom at ({gridPos.x},{gridPos.y}); " +
                        $"stack full for incoming={dice?.name}");
                    return false;
                }
            }

            if (!byGrid.TryGetValue(gridPos, out var stack)) {
                stack = default;
            }

            if (tier == DiceStackTier.Bottom) {
                if (stack.Bottom != null && stack.Bottom != dice) {
                    Debug.LogError(
                        $"DiceRegistry: refusing bottom overwrite at ({gridPos.x},{gridPos.y}) " +
                        $"existing={stack.Bottom.name} incoming={dice?.name}");
                    return false;
                }
            } else if (stack.Top != null && stack.Top != dice) {
                Debug.LogError(
                    $"DiceRegistry: refusing top overwrite at ({gridPos.x},{gridPos.y}) " +
                    $"existing={stack.Top.name} incoming={dice?.name}");
                return false;
            }

            if (tier == DiceStackTier.Top) {
                // Spawning Top intent yields when a committed Top arrives and Bottom is free.
                if (TryGetPendingTopAt(gridPos, out var pendingTop)
                    && pendingTop != null
                    && pendingTop != dice
                    && pendingTop.IsSpawning
                    && !HasBottomAt(gridPos)
                    && pendingOccupancy.TryGetValue(gridPos, out var pendingStack)) {
                    pendingStack.Top = null;
                    pendingStack.Bottom = pendingTop;
                    pendingOccupancy[gridPos] = pendingStack;
                    pendingTop.NotifyPendingSpawnRetargetedToBottom();
                }

                stack.Top = dice;
            } else {
                stack.Bottom = dice;
            }

            byGrid[gridPos] = stack;
            ReconcilePendingSpawnTierAt(gridPos, excluding: dice);
            return true;
        }

        /// <summary>
        /// On board occupancy change: align spawning pending intent to committed Bottom support.
        /// </summary>
        void ReconcilePendingSpawnTierAt(Vector2Int gridPos, DiceController excluding = null) {
            if (TryGetPendingBottomAt(gridPos, out var pendingBottom)
                && pendingBottom != null
                && pendingBottom != excluding
                && pendingBottom.IsSpawning
                && HasCommittedBottomSupport(gridPos, pendingBottom)) {
                if (HasTopSlotClaimAt(gridPos)) {
                    Debug.LogError(
                        $"DiceRegistry: cannot promote pending Bottom spawn at ({gridPos.x},{gridPos.y}); " +
                        "Top already occupied.");
                } else if (pendingOccupancy.TryGetValue(gridPos, out var promoteStack)) {
                    promoteStack.Bottom = null;
                    promoteStack.Top = pendingBottom;
                    pendingOccupancy[gridPos] = promoteStack;
                    pendingBottom.NotifyPendingSpawnRetargetedToTop();
                }
            }

            if (TryGetPendingTopAt(gridPos, out var pendingTop)
                && pendingTop != null
                && pendingTop != excluding
                && pendingTop.IsSpawning
                && !HasCommittedBottomSupport(gridPos, pendingTop)) {
                if (HasBottomAt(gridPos)) {
                    Debug.LogError(
                        $"DiceRegistry: cannot demote pending Top spawn at ({gridPos.x},{gridPos.y}); " +
                        "Bottom slot occupied.");
                    return;
                }

                if (!pendingOccupancy.TryGetValue(gridPos, out var demoteStack)) {
                    return;
                }

                demoteStack.Top = null;
                demoteStack.Bottom = pendingTop;
                pendingOccupancy[gridPos] = demoteStack;
                pendingTop.NotifyPendingSpawnRetargetedToBottom();
            }
        }

        void ClearDiceAt(
            Vector2Int gridPos,
            DiceController dice,
            DiceStackTier tier,
            bool notifySupportLoss = true) {
            if (!byGrid.TryGetValue(gridPos, out var stack)) {
                return;
            }

            DiceController removedBottom = null;
            DiceController topAfterBottomRemoved = null;

            if (tier == DiceStackTier.Top) {
                if (stack.Top == dice) {
                    stack.Top = null;
                }
            } else if (stack.Bottom == dice) {
                stack.Bottom = null;
                removedBottom = dice;
                topAfterBottomRemoved = stack.Top;
            }

            if (stack.Bottom == null && stack.Top == null) {
                byGrid.Remove(gridPos);
            } else {
                byGrid[gridPos] = stack;
            }

            if (removedBottom != null) {
                ReconcilePendingSpawnTierAt(gridPos, excluding: removedBottom);
                if (notifySupportLoss && !HasStackBottomSupportAt(gridPos)) {
                    NotifyTopAfterBottomRemoved(gridPos, removedBottom, topAfterBottomRemoved);
                }
            }
        }

        /// <summary>
        /// After a batched clear/place, demote Top only if the Bottom stack slot has no support.
        /// Kind-Ghost Bottom still counts as stack support (player pass-through does not).
        /// </summary>
        public void ResolveUnsupportedTopAt(Vector2Int gridPos) {
            TryResolveUnsupportedTopAt(gridPos, removedBottom: null);
        }

        void TryResolveUnsupportedTopAt(Vector2Int gridPos, DiceController removedBottom) {
            if (!TryGetTopAt(gridPos, out var top) || top == null) {
                return;
            }

            if (HasStackBottomSupportAt(gridPos)) {
                return;
            }

            NotifyTopAfterBottomRemoved(gridPos, removedBottom, top);
        }

        static void NotifyTopAfterBottomRemoved(
            Vector2Int gridPos,
            DiceController removedBottom,
            DiceController topDice) {
            if (topDice == null || topDice == removedBottom) {
                return;
            }

            if (topDice.CurrentState.GridPos != gridPos
                || topDice.CurrentState.Tier != DiceStackTier.Top) {
                return;
            }

            topDice.OnBottomSupportLost(removedBottom);
        }
    }
}
