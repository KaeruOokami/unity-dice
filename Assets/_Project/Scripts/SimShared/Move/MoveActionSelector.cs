namespace DiceGame.SimShared.Move
{
    using DiceGame.Core;
    using DiceGame.SimShared.Jump;
    using DiceGame.SimShared.Placement;

    /// <summary>
    /// Copied from production <c>MoveActionSelector</c> (Domain JumpPlayerTransferRules).
    /// </summary>
    public static class MoveActionSelector
    {
        public static MoveAction Select(in MoveFacts f)
        {
            if (f.HasExpandedFootprintWalk)
            {
                return MoveAction.ExpandedFootprintWalk;
            }

            if (f.Mode == DiceStandingMoveMode.PlayerOnly
                && f.FromLevel != SurfaceHeightNorms.Floor
                && f.HasStandingDice)
            {
                return SelectPlayerOnly(in f);
            }

            if ((f.Mode == DiceStandingMoveMode.Slide || f.Mode == DiceStandingMoveMode.Roll)
                && !f.BlocksDiceCoupledStackEntry)
            {
                var coupled = SelectCoupled(in f);
                if (coupled != MoveAction.ContinueToLanding)
                {
                    return coupled;
                }
            }

            return SelectLanding(in f);
        }

        static MoveAction SelectPlayerOnly(in MoveFacts f)
        {
            if (f.Relation == MoveLevelRelation.BottomToTop)
            {
                return f.CanTierLand ? MoveAction.TierLanding : MoveAction.Blocked;
            }

            if (JumpPlayerTransferRules.BlocksGroundLowerLevelTransfer(
                    f.IsJumping,
                    f.FromLevel,
                    f.TargetLevel,
                    f.IsSinkErasing,
                    f.CanJumpCoupleWithPlayer,
                    f.IsPlayerMovable))
            {
                return MoveAction.Blocked;
            }

            if (f.Relation == MoveLevelRelation.Descent)
            {
                if (JumpPlayerTransferRules.CanUsePlayerOnlyLowerLevelJump(
                        f.IsJumping,
                        f.IsSinkErasing,
                        f.CanJumpCoupleWithPlayer,
                        f.IsPlayerMovable))
                {
                    return f.TargetLevel == SurfaceHeightNorms.Floor
                        ? MoveAction.PlayerWalkFloor
                        : MoveAction.PlayerWalk;
                }

                if (JumpPlayerTransferRules.BlocksPlayerOnlyJumpLowerLevelTransfer(
                        f.IsJumping,
                        f.FromLevel,
                        f.TargetLevel,
                        f.Mode,
                        f.IsSinkErasing,
                        f.CanJumpCoupleWithPlayer,
                        f.IsPlayerMovable))
                {
                    return MoveAction.Blocked;
                }

                if (f.TargetLevel == SurfaceHeightNorms.Floor)
                {
                    return MoveAction.PlayerWalkFloor;
                }

                return f.WithinReachDescentOnly ? MoveAction.PlayerWalk : MoveAction.Blocked;
            }

            return f.WithinReachFull ? MoveAction.PlayerWalk : MoveAction.Blocked;
        }

        static MoveAction SelectCoupled(in MoveFacts f)
        {
            return f.Mode switch
            {
                DiceStandingMoveMode.Slide => SelectSlide(in f),
                DiceStandingMoveMode.Roll => SelectRoll(in f),
                _ => MoveAction.ContinueToLanding,
            };
        }

        static MoveAction SelectSlide(in MoveFacts f)
        {
            if (!f.IsJumping && f.HasIceSlideDisplacement)
            {
                return MoveAction.IceSlide;
            }

            if (f.IsJumping && f.CanJumpGridRoll)
            {
                return MoveAction.CoupledJumpGrid;
            }

            if (f.ToCellIsOccupiedForCoupled && f.CanTierLand)
            {
                return MoveAction.TierLanding;
            }

            return MoveAction.ContinueToLanding;
        }

        static MoveAction SelectRoll(in MoveFacts f)
        {
            if (f.IsJumping && f.CanJumpGridRoll)
            {
                return MoveAction.CoupledJumpGrid;
            }

            if (!f.ToCellIsOccupiedForCoupled && f.CanTopFall)
            {
                return MoveAction.TopFall;
            }

            if (f.ToCellIsOccupiedForCoupled && f.CanTierLand)
            {
                return MoveAction.TierLanding;
            }

            if (f.CanGridRoll)
            {
                if (f.IsJumping && !f.AllowJumpGridMove)
                {
                    return MoveAction.Blocked;
                }

                return f.IsJumping ? MoveAction.CoupledJumpGrid : MoveAction.GridRoll;
            }

            return MoveAction.ContinueToLanding;
        }

        static MoveAction SelectLanding(in MoveFacts f)
        {
            if (f.IsPlayerFloorPassable)
            {
                if (f.IsJumping
                    && f.FromLevel != SurfaceHeightNorms.Floor
                    && f.HasStandingDice
                    && f.CanJumpCoupleWithPlayer)
                {
                    return MoveAction.Blocked;
                }

                return MoveAction.PlayerWalkFloor;
            }

            if (f.FromLevel == SurfaceHeightNorms.Floor)
            {
                return f.HasFloorMountBottom
                    ? MoveAction.FloorToBottomMount
                    : MoveAction.Blocked;
            }

            return MoveAction.HeightTransfer;
        }
    }
}
