namespace AuraEngine.Core
{
    public enum AuraResult
    {
        Success = 0,
        InvalidHandle = 1,
        InvalidDefinition = 2,
        UnsupportedShape = 3,
        UnsupportedQuery = 4,
        OutOfMemory = 5,
        InvalidWorld = 6,
        AbiMismatch = 7,
        CapacityExceeded = 8,
        BackendFailure = 9,

        /* ABI v10: the body is removed from the simulation. */
        BodyDisabled = 10,

        /* ABI v10: valid request the body/joint type or backend cannot perform. */
        UnsupportedOperation = 11,
    }
}
