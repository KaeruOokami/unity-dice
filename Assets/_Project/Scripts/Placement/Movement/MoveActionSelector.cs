using DiceGame.Core;

namespace DiceGame.Placement
{
    /// <summary>
    /// Composes dice-entry and player-entry facts into one <see cref="MoveAction"/>.
    /// Dice coupled intent and player standing transfer are evaluated as separate axes;
    /// Mode only describes how dice probes were filled, not which axis may run.
    /// </summary>
    public static class MoveActionSelector
    {
        public static MoveAction Select(in MoveFacts f) {
            if (f.HasExpandedFootprintWalk) {
                return MoveAction.ExpandedFootprintWalk;
            }

            // --- Dice axis ---
            var diceAction = MoveAction.ContinueToLanding;
            if (JumpPlayerTransferPolicy.ShouldEvaluateDiceCoupledMovement(f.IsJumping, f.StandingDice)
                && !f.BlocksDiceCoupledStackEntry) {
                diceAction = SelectCoupled(f);
            }

            if (diceAction != MoveAction.ContinueToLanding) {
                return diceAction;
            }

            // --- Player axis (independent of dice stack-entry block) ---
            if (CanSelectPlayerTierLanding(f)) {
                return MoveAction.TierLanding;
            }

            if (f.FromLevel != SurfaceHeightLevel.Floor && f.StandingDice != null) {
                return SelectPlayerStanding(f);
            }

            return SelectFloorOrigin(f);
        }

        static bool CanSelectPlayerTierLanding(in MoveFacts f) {
            return f.CanTierLand
                && f.IsJumping
                && f.FromLevel == SurfaceHeightLevel.Bottom
                && f.StandingDice != null;
        }

        /// <summary>
        /// Player leaving a standing die: walk / jump descent / same-level move / height transfer.
        /// Solo-leave surfaces (Iron, stuck Magnet, sink) use the shared descent fact.
        /// </summary>
        static MoveAction SelectPlayerStanding(in MoveFacts f) {
            if (f.Relation == MoveLevelRelation.BottomToTop) {
                return f.CanTierLand ? MoveAction.TierLanding : MoveAction.Blocked;
            }

            if (JumpPlayerTransferPolicy.BlocksPlayerDescentFromStanding(
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

                if (f.IsPlayerFloorPassable) {
                    return MoveAction.PlayerWalkFloor;
                }

                if (f.TargetLevel == SurfaceHeightLevel.Floor) {
                    return f.WithinReachDescentOnly || f.IsJumping
                        ? MoveAction.PlayerWalkFloor
                        : MoveAction.Blocked;
                }

                // Coupleable / movable standing: height transfer owns same-stack ride and steps.
                if (JumpPlayerTransferPolicy.ShouldEvaluateDiceCoupledMovement(f.IsJumping, f.StandingDice)
                    || f.Mode == DiceStandingMoveMode.None) {
                    return MoveAction.HeightTransfer;
                }

                return f.WithinReachDescentOnly ? MoveAction.PlayerWalk : MoveAction.Blocked;
            }

            if (f.IsPlayerFloorPassable) {
                if (f.IsJumping
                    && f.StandingDice.CanJumpCoupleWithPlayer
                    && !JumpPlayerTransferPolicy.IsSoloLeaveStandingSurface(f.StandingDice)) {
                    return MoveAction.Blocked;
                }

                return MoveAction.PlayerWalkFloor;
            }

            // Same / ascent while dice did not claim a coupled move.
            if (JumpPlayerTransferPolicy.ShouldEvaluateDiceCoupledMovement(f.IsJumping, f.StandingDice)
                || f.Mode == DiceStandingMoveMode.Slide
                || f.Mode == DiceStandingMoveMode.Roll) {
                return MoveAction.HeightTransfer;
            }

            return f.WithinReachFull ? MoveAction.PlayerWalk : MoveAction.Blocked;
        }

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

        static MoveAction SelectFloorOrigin(in MoveFacts f) {
            if (f.IsPlayerFloorPassable) {
                return MoveAction.PlayerWalkFloor;
            }

            return f.FloorMountBottomDice != null
                ? MoveAction.FloorToBottomMount
                : MoveAction.Blocked;
        }
    }
}
