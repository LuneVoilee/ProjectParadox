namespace GamePlay.Strategy
{
    public enum MoveRejectReason
    {
        None,
        UnitMissing,
        NoUnit,
        NoPosition,
        AlreadyThere,
        NoMapContext,
        ForbiddenByDiplomacy,
        NoPath
    }
}
