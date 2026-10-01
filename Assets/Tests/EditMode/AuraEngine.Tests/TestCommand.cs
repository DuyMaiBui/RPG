using AuraEngine.Core;

namespace AuraEngine.Tests
{
    public sealed class TestCommand : IAuraCommand
    {
        public const ushort Id = 7;

        public TestCommand(int value) => Value = value;

        public int Value { get; }

        ushort IAuraCommand.TypeId => Id;
    }
}
