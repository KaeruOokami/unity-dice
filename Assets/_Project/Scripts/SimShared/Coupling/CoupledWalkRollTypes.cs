namespace DiceGame.SimShared.Coupling
{
    using System;
    using DiceGame.SimShared.Push;

    /// <summary>
    /// Pure request for one-cell standing couple roll (production GroundParallelRoll).
    /// Occupancy is supplied via predicates — no Frame / Unity types.
    /// </summary>
    public struct CoupledWalkRollRequest
    {
        public int StandingCellX;
        public int StandingCellY;
        public int StandingTier; // 0=Bottom, 1=Top
        public int DirX;
        public int DirY;
        public float PawnWorldX;
        public float PawnWorldZ;
        public int BoardWidth;
        public int BoardHeight;
        public int DiceTopFace;
        public int DiceNorthFace;
        public int DiceEastFace;
        public bool CanGridRoll;
        public bool SlideUntilBlocked;
        public bool IsPlayerPassThrough;
        public bool DiceBusy;
        public bool DiceCarried;
        public bool DiceErasing;
        public int MotionTicks;
        public float CellSize;

        public OneCellPushPlanner.CellQuery CanPlaceBottomAt;
        public OneCellPushPlanner.CellQuery CanPlaceTopAt;
        public OneCellPushPlanner.CellQuery HasSolidBottomAt;
        public Func<int, int, bool> IsPawnOccupiedAt;
    }

    /// <summary>
    /// Ledger mutations to apply once when the session opens.
    /// </summary>
    public struct CoupledWalkRollCommit
    {
        public int DiceFromX;
        public int DiceFromY;
        public int DiceDestX;
        public int DiceDestY;
        public int LandingTier; // 0=Bottom, 1=Top
        public int NextTopFace;
        public int NextNorthFace;
        public int NextEastFace;
        public bool DemoteUnsupportedTopAtFrom;
        public int PawnCellX;
        public int PawnCellY;
        public int PawnStandingTier;
        public int MotionTicks;
    }

    /// <summary>
    /// Authoritative ride-follow lock for the duration of the couple roll.
    /// While <see cref="CoupledWalkRoll.IsBusy"/>, free surface move must not run.
    /// </summary>
    public struct CoupledWalkRollSession
    {
        public bool Active;
        public int DirX;
        public int DirY;
        public int FromX;
        public int FromY;
        public int DestX;
        public int DestY;
        public int StandingTier;
        public int TicksRemaining;
        public int TicksTotal;
        public float CellSize;
    }

    /// <summary>Session <see cref="CoupledWalkRollSession.StandingTier"/>: 0 Bottom, 1 Top, 2 Floor (partition dismount).</summary>
    public static class CoupledWalkRollStanding
    {
        public const int Bottom = 0;
        public const int Top = 1;
        public const int Floor = 2;
    }

    public struct CoupledWalkRollTickResult
    {
        public float PawnWorldX;
        public float PawnWorldZ;
        public int PawnCellX;
        public int PawnCellY;
        public int PawnStandingTier;
        public bool IsBusy;
        public bool Completed;
    }
}
