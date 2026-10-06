namespace RPG.Core.Actors
{
    public enum PresentationSignalKind : byte
    {
        AttackStarted = 1,
        Damaged = 2,
        Died = 3,
        OrderRejected = 4,
        AbilityCast = 5,
    }
}
