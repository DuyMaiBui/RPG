namespace AuraEngine.Core
{
    public readonly struct AuraWaterId
    {
        public AuraWaterId(ulong value) { Value = value; }
        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public static AuraWaterId Invalid => default;
    }
}
