using NUnit.Framework;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Contracts.Tests
{

public sealed class ProtocolVersionTests
{
    [Test]
    public void CompatibleVersion_RequiresSameMajorAndNoNewerMinor()
    {
        var server = new ProtocolVersion(1, 2);

        Assert.That(new ProtocolVersion(1, 0).IsCompatibleWith(server), Is.True);
        Assert.That(new ProtocolVersion(1, 3).IsCompatibleWith(server), Is.False);
        Assert.That(new ProtocolVersion(2, 0).IsCompatibleWith(server), Is.False);
    }
}
}
