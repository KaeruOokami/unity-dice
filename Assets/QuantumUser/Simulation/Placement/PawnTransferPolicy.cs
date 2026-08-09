namespace Quantum
{
    using DiceGame.SimShared.Jump;
    using DiceGame.SimShared.Placement;

    /// <summary>
    /// Frame adapter over shared height / landing-tier rules
    /// (<see cref="SurfaceHeightNorms"/>, <see cref="HeightReachRules"/>, <see cref="HeightStepLimitRules"/>).
    /// </summary>
    public static unsafe class PawnTransferPolicy
    {
        public static int StandingNorm(bool isOnFloor, DiceStackTier standingTier)
        {
            return SurfaceHeightNorms.FromStanding(isOnFloor, standingTier == DiceStackTier.Top);
        }

        public static bool TryResolveLanding(
            Frame frame,
            Board board,
            int x,
            int y,
            out bool landingOnFloor,
            out DiceStackTier landingTier,
            out int landingNorm)
        {
            landingOnFloor = true;
            landingTier = DiceStackTier.Bottom;
            landingNorm = SurfaceHeightNorms.Floor;

            if (!BoardBootstrapSystem.IsInsideBoard(board, x, y))
            {
                return false;
            }

            if (CellOccupancy.HasSolidTopAt(frame, x, y))
            {
                landingOnFloor = false;
                landingTier = DiceStackTier.Top;
                landingNorm = SurfaceHeightNorms.Top;
                return true;
            }

            if (CellOccupancy.HasSolidBottomAt(frame, x, y))
            {
                landingOnFloor = false;
                landingTier = DiceStackTier.Bottom;
                landingNorm = SurfaceHeightNorms.Bottom;
                return true;
            }

            landingOnFloor = true;
            landingTier = DiceStackTier.Bottom;
            landingNorm = SurfaceHeightNorms.Floor;
            return true;
        }

        public static bool CanTransferToCell(
            Frame frame,
            Board board,
            in PlayerPawn pawn,
            in GridPose pose,
            EntityRef pawnEntity,
            int nextX,
            int nextY,
            bool isJumping)
        {
            if (!BoardBootstrapSystem.IsInsideBoard(board, nextX, nextY))
            {
                return false;
            }

            if (BoardBootstrapSystem.IsPawnOccupied(frame, nextX, nextY, pawnEntity))
            {
                return false;
            }

            if (!TryResolveLanding(
                    frame,
                    board,
                    nextX,
                    nextY,
                    out _,
                    out _,
                    out var landingNorm))
            {
                return false;
            }

            var fromNorm = StandingNorm(pawn.IsOnFloor, pawn.StandingTier);
            if (!SurfaceHeightNorms.IsLandingTierAtOrBelowStanding(fromNorm, landingNorm))
            {
                return false;
            }

            ResolveStandingCaps(
                frame,
                in pawn,
                in pose,
                out var canJumpCouple,
                out var isPlayerMovable,
                out var isSinkErasing,
                out var standingEntity,
                out var standingBlocksOther);

            if (landingNorm < fromNorm
                && HeightStepLimitRules.CanUsePlayerOnlyLowerLevelJump(
                    isJumping,
                    canJumpCouple,
                    isPlayerMovable,
                    isSinkErasing))
            {
                return true;
            }

            if (isJumping
                && landingNorm < fromNorm
                && !canJumpCouple
                && isPlayerMovable
                && !isSinkErasing)
            {
                return false;
            }

            if (isJumping
                && !pawn.IsOnFloor
                && standingEntity.IsValid
                && TryGetLandingDice(frame, nextX, nextY, landingNorm, out var landingDice)
                && JumpDiceTransferRules.ShouldBlockDiceToDiceTransfer(
                    isJumping,
                    standingBlocksOther,
                    hasStandingDice: true,
                    hasTargetDice: true,
                    targetIsSameAsStanding: landingDice == standingEntity))
            {
                return false;
            }

            // Production SelectLanding: couple-capable standing cannot floor-walk while jumping.
            if (isJumping
                && landingNorm == SurfaceHeightNorms.Floor
                && !pawn.IsOnFloor
                && standingEntity.IsValid
                && JumpPlayerTransferRules.UsesCoupledJumpStep(
                    isJumping,
                    fromNorm,
                    hasStandingDice: true,
                    canJumpCouple,
                    isSinkErasing))
            {
                return false;
            }

            var useCoupled = JumpPlayerTransferRules.UsesCoupledJumpStep(
                isJumping,
                fromNorm,
                standingEntity.IsValid,
                canJumpCouple,
                isSinkErasing);
            var maxStepPermille = HeightStepLimitRules.ResolveMaxStepPermille(
                ResolveWalk(frame),
                ResolveJumpPlayerOnly(frame),
                ResolveJumpCoupled(frame),
                isJumping,
                useCoupled);

            return HeightReachRules.CanStepBetweenNormPermille(fromNorm, landingNorm, maxStepPermille);
        }

        static void ResolveStandingCaps(
            Frame frame,
            in PlayerPawn pawn,
            in GridPose pose,
            out bool canJumpCouple,
            out bool isPlayerMovable,
            out bool isSinkErasing,
            out EntityRef standingEntity,
            out bool standingBlocksJumpTransferToOtherDice)
        {
            canJumpCouple = false;
            isPlayerMovable = false;
            isSinkErasing = false;
            standingEntity = EntityRef.None;
            standingBlocksJumpTransferToOtherDice = false;
            if (pawn.IsOnFloor)
            {
                return;
            }

            if (!CellOccupancy.TryGetAt(
                    frame,
                    pose.X,
                    pose.Y,
                    pawn.StandingTier,
                    out standingEntity,
                    out var dice))
            {
                return;
            }

            var effective = EffectiveDiceQuery.ResolveAt(frame, standingEntity, in dice, pose.X, pose.Y);
            canJumpCouple = effective.CanJumpCoupleWithPlayer;
            isPlayerMovable = effective.IsPlayerMovable;
            isSinkErasing = effective.IsSinkErasing;
            standingBlocksJumpTransferToOtherDice = effective.Capabilities.BlocksJumpTransferToOtherDice;
        }

        static bool TryGetLandingDice(Frame frame, int x, int y, int landingNorm, out EntityRef entity)
        {
            entity = EntityRef.None;
            if (landingNorm <= SurfaceHeightNorms.Floor)
            {
                return false;
            }

            var tier = landingNorm >= SurfaceHeightNorms.Top ? DiceStackTier.Top : DiceStackTier.Bottom;
            return CellOccupancy.TryGetAt(frame, x, y, tier, out entity, out _);
        }

        static int ResolveWalk(Frame frame)
        {
            var walk = frame.RuntimeConfig.MaxWalkStepPermille;
            return walk > 0 ? walk : MatchSimDefaults.MaxWalkStepPermille;
        }

        static int ResolveJumpPlayerOnly(Frame frame)
        {
            var v = frame.RuntimeConfig.MaxJumpStepPlayerOnlyPermille;
            return v > 0 ? v : MatchSimDefaults.MaxJumpStepPlayerOnlyPermille;
        }

        static int ResolveJumpCoupled(Frame frame)
        {
            var v = frame.RuntimeConfig.MaxJumpStepCoupledPermille;
            return v > 0 ? v : MatchSimDefaults.MaxJumpStepCoupledPermille;
        }
    }
}
