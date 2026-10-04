namespace AuraEngine.Core
{
    public readonly struct AuraForceFieldId
    {
        public AuraForceFieldId(ulong value) { Value = value; }

        public ulong Value { get; }

        public bool IsValid => Value != 0;

        public static AuraForceFieldId Invalid => default;
    }
}
