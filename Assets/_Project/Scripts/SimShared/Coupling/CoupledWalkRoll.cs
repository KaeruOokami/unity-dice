namespace DiceGame.SimShared.Coupling
{
    using DiceGame.SimShared.Dice;
    using DiceGame.SimShared.Move;
    using DiceGame.SimShared.Placement;
    using DiceGame.SimShared.Push;
    using DiceGame.SimShared.Slide;

    /// <summary>
    /// Standing couple GridRoll / Ice slide + character ride lock.
    /// Production: CoupledDiceMove → GroundParallelRoll / GroundIceSlide → IsTrackingRoll.
    /// </summary>
    public static class CoupledWalkRoll
    {
        public static bool TryBegin(
            in CoupledWalkRollRequest request,
            out CoupledWalkRollCommit commit,
            out CoupledWalkRollSession session)
        {
            commit = default;
            session = default;

            if (request.DirX == 0 && request.DirY == 0)
            {
                return false;
            }

            if ((request.DirX != 0 && request.DirY != 0)
                || (request.DirX != 0 && request.DirX != 1 && request.DirX != -1)
                || (request.DirY != 0 && request.DirY != 1 && request.DirY != -1))
            {
                return false;
            }

            if (request.DiceBusy || request.DiceCarried || request.DiceErasing)
            {
                return false;
            }

            if (request.MotionTicks <= 0 || request.CellSize <= 0f)
            {
                return false;
            }

            var mode = StandingMoveModeRules.Resolve(
                request.CanGridRoll,
                request.SlideUntilBlocked,
                request.IsPlayerPassThrough);

            if (StandingMoveModeRules.AllowsWalkCoupleSlide(mode))
            {
                return TryBeginSlide(in request, out commit, out session);
            }

            if (!StandingMoveModeRules.AllowsWalkCoupleRoll(mode))
            {
                return false;
            }

            return TryBeginRoll(in request, out commit, out session);
        }

        static bool TryBeginRoll(
            in CoupledWalkRollRequest request,
            out CoupledWalkRollCommit commit,
            out CoupledWalkRollSession session)
        {
            commit = default;
            session = default;

            if (!OneCellPushPlanner.TryPlan(
                    request.StandingCellX,
                    request.StandingCellY,
                    request.StandingTier,
                    request.DirX,
                    request.DirY,
                    request.BoardWidth,
                    request.BoardHeight,
                    request.CanPlaceBottomAt,
                    request.CanPlaceTopAt,
                    request.HasSolidBottomAt,
                    out var destX,
                    out var destY,
                    out var landingTier,
                    out var demoteTop))
            {
                return false;
            }

            if (request.IsPawnOccupiedAt != null
                && request.IsPawnOccupiedAt(destX, destY))
            {
                return false;
            }

            var top = request.DiceTopFace;
            var north = request.DiceNorthFace;
            var east = request.DiceEastFace;
            if (DiceFaceOrientation.TryRoll(
                    request.DirX,
                    request.DirY,
                    top,
                    north,
                    east,
                    out var nextTop,
                    out var nextNorth,
                    out var nextEast))
            {
                top = nextTop;
                north = nextNorth;
                east = nextEast;
            }

            return FinishBegin(
                in request,
                destX,
                destY,
                landingTier,
                top,
                north,
                east,
                demoteTop,
                request.MotionTicks,
                out commit,
                out session);
        }

        static bool TryBeginSlide(
            in CoupledWalkRollRequest request,
            out CoupledWalkRollCommit commit,
            out CoupledWalkRollSession session)
        {
            commit = default;
            session = default;

            if (!IceSlideUntilBlockedPlanner.TryPlan(
                    request.StandingCellX,
                    request.StandingCellY,
                    request.StandingTier,
                    request.DirX,
                    request.DirY,
                    request.BoardWidth,
                    request.BoardHeight,
                    request.CanPlaceBottomAt,
                    request.CanPlaceTopAt,
                    request.HasSolidBottomAt,
                    out var destX,
                    out var destY,
                    out var landingTier,
                    out var stepCount,
                    out var demoteTop))
            {
                return false;
            }

            if (request.IsPawnOccupiedAt != null
                && request.IsPawnOccupiedAt(destX, destY))
            {
                return false;
            }

            var motionTicks = IceSlideUntilBlockedPlanner.ResolveMotionTicks(
                request.MotionTicks,
                stepCount);

            return FinishBegin(
                in request,
                destX,
                destY,
                landingTier,
                request.DiceTopFace,
                request.DiceNorthFace,
                request.DiceEastFace,
                demoteTop,
                motionTicks,
                out commit,
                out session);
        }

        static bool FinishBegin(
            in CoupledWalkRollRequest request,
            int destX,
            int destY,
            int landingTier,
            int top,
            int north,
            int east,
            bool demoteTop,
            int motionTicks,
            out CoupledWalkRollCommit commit,
            out CoupledWalkRollSession session)
        {
            commit = new CoupledWalkRollCommit
            {
                DiceFromX = request.StandingCellX,
                DiceFromY = request.StandingCellY,
                DiceDestX = destX,
                DiceDestY = destY,
                LandingTier = landingTier,
                NextTopFace = top,
                NextNorthFace = north,
                NextEastFace = east,
                DemoteUnsupportedTopAtFrom = demoteTop,
                PawnCellX = destX,
                PawnCellY = destY,
                PawnStandingTier = landingTier,
                MotionTicks = motionTicks,
            };

            session = new CoupledWalkRollSession
            {
                Active = true,
                DirX = request.DirX,
                DirY = request.DirY,
                FromX = request.StandingCellX,
                FromY = request.StandingCellY,
                DestX = destX,
                DestY = destY,
                StandingTier = landingTier,
                TicksRemaining = motionTicks,
                TicksTotal = motionTicks,
                CellSize = request.CellSize,
            };
            return true;
        }

        public static bool IsBusy(in CoupledWalkRollSession session)
        {
            return session.Active && session.TicksRemaining > 0;
        }

        public static void Tick(
            ref CoupledWalkRollSession session,
            out CoupledWalkRollTickResult tick)
        {
            tick = default;
            if (!session.Active)
            {
                tick.Completed = true;
                return;
            }

            session.TicksRemaining -= 1;
            var total = session.TicksTotal;
            var remaining = session.TicksRemaining;
            var t01 = total > 0 ? 1f - (remaining / (float)total) : 1f;
            if (t01 < 0f)
            {
                t01 = 0f;
            }
            else if (t01 > 1f)
            {
                t01 = 1f;
            }

            CellSurfaceMotion.CellCenter(
                session.FromX,
                session.FromY,
                session.CellSize,
                out var fromX,
                out var fromZ);
            CellSurfaceMotion.CellCenter(
                session.DestX,
                session.DestY,
                session.CellSize,
                out var toX,
                out var toZ);
            PushFollowMotion.Lerp(fromX, fromZ, toX, toZ, t01, out var pawnX, out var pawnZ);

            tick.PawnWorldX = pawnX;
            tick.PawnWorldZ = pawnZ;
            tick.PawnCellX = session.DestX;
            tick.PawnCellY = session.DestY;
            tick.PawnStandingTier = session.StandingTier;
            tick.IsBusy = session.TicksRemaining > 0;
            tick.Completed = session.TicksRemaining <= 0;

            if (tick.Completed)
            {
                session.Active = false;
                session.TicksRemaining = 0;
            }
        }
    }
}
