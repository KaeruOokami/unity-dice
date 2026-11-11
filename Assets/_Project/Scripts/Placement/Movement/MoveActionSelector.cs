using DiceGame.Core;

namespace DiceGame.Placement
{
    /// <summary>
    /// Decision table: <see cref="MoveFacts"/> → exactly one <see cref="MoveAction"/>.
    /// Rows are exclusive within each partition; builders must not re-select on failure.
    /// </summary>
    public static class MoveActionSelector
    {
        public static MoveAction Select(in MoveFacts f) {
            if (f.HasExpandedFootprintWalk) {
                return MoveAction.ExpandedFootprintWalk;
            }

            if (f.Mode == DiceStandingMoveMode.PlayerOnly
                && f.FromLevel != SurfaceHeightLevel.Floor
                && f.StandingDice != null) {
                return SelectPlayerOnly(f);
            }

            if ((f.Mode == DiceStandingMoveMode.Slide || f.Mode == DiceStandingMoveMode.Roll)
                && !f.BlocksDiceCoupledStackEntry) {
                var coupled = SelectCoupled(f);
                if (coupled != MoveAction.ContinueToLanding) {
                    return coupled;
                }
            }

            return SelectLanding(f);
        }

        static MoveAction SelectPlayerOnly(in MoveFacts f) {
            if (f.Relation == MoveLevelRelation.BottomToTop) {
                return f.CanTierLand ? MoveAction.TierLanding : MoveAction.Blocked;
            }

            if (JumpPlayerTransferPolicy.BlocksGroundLowerLevelTransfer(
                f.IsJumping,
                f.FromLevel,
                f.TargetLevel,
                f.StandingDice)) {
                return MoveAction.Blocked;
            }

            if (f.Relation == MoveLevelRelation.Descent) {
                if (JumpPlayerTransferPolicy.CanUsePlayerOnlyLowerLevelJump(f.IsJumping, f.StandingDice)) {
                    return f.TargetLevel == SurfaceHeightLevel.Floor
                        ? MoveAction.PlayerWalkFloor
                        : MoveAction.PlayerWalk;
                }

                if (JumpPlayerTransferPolicy.BlocksPlayerOnlyJumpLowerLevelTransfer(
                    f.IsJumping,
                    f.FromLevel,
                    f.TargetLevel,
                    f.StandingDice)) {
                    return MoveAction.Blocked;
                }

                if (f.TargetLevel == SurfaceHeightLevel.Floor) {
                    return MoveAction.PlayerWalkFloor;
                }

                return f.WithinReachDescentOnly ? MoveAction.PlayerWalk : MoveAction.Blocked;
            }

            return f.WithinReachFull ? MoveAction.PlayerWalk : MoveAction.Blocked;
        }

        /// <summary>
        /// L2 table. Exclusive rows by mode; missing coupled intent → landing table.
        /// Ice with no displacement selects ContinueToLanding so HeightTransfer owns same-tier ride.
        /// </summary>
        static MoveAction SelectCoupled(in MoveFacts f) {
            return f.Mode switch {
                DiceStandingMoveMode.Slide => SelectSlide(f),
                DiceStandingMoveMode.Roll => SelectRoll(f),
                _ => MoveAction.ContinueToLanding,
            };
        }

        static MoveAction SelectSlide(in MoveFacts f) {
            if (!f.IsJumping && f.HasIceSlideDisplacement) {
                return MoveAction.IceSlide;
            }

            if (f.IsJumping && f.CanJumpGridRoll) {
                return MoveAction.CoupledJumpGrid;
            }

            if (f.ToCellIsOccupiedForCoupled && f.CanTierLand) {
                return MoveAction.TierLanding;
            }

            return MoveAction.ContinueToLanding;
        }

        static MoveAction SelectRoll(in MoveFacts f) {
            if (f.IsJumping && f.CanJumpGridRoll) {
                return MoveAction.CoupledJumpGrid;
            }

            if (!f.ToCellIsOccupiedForCoupled && f.CanTopFall) {
                return MoveAction.TopFall;
            }

            if (f.ToCellIsOccupiedForCoupled && f.CanTierLand) {
                return MoveAction.TierLanding;
            }

            if (f.CanGridRoll) {
                if (f.IsJumping && !f.Context.AllowJumpGridMove) {
                    return MoveAction.Blocked;
                }

                return f.IsJumping ? MoveAction.CoupledJumpGrid : MoveAction.GridRoll;
            }

            return MoveAction.ContinueToLanding;
        }

        static MoveAction SelectLanding(in MoveFacts f) {
            if (f.IsPlayerFloorPassable) {
                if (f.IsJumping
                    && f.FromLevel != SurfaceHeightLevel.Floor
                    && f.StandingDice != null
                    && f.StandingDice.CanJumpCoupleWithPlayer) {
                    return MoveAction.Blocked;
                }

                return MoveAction.PlayerWalkFloor;
            }

            if (f.FromLevel == SurfaceHeightLevel.Floor) {
                return f.FloorMountBottomDice != null
                    ? MoveAction.FloorToBottomMount
                    : MoveAction.Blocked;
            }

            return MoveAction.HeightTransfer;
        }
    }
}
