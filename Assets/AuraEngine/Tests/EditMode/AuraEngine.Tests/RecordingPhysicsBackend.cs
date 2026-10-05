using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Tests
{
    /* Wraps a real backend and records every joint definition the world receives, so a test can assert exactly what the
       authoring handed to the kernel (for example the world-space anchors). Everything else is forwarded unchanged. */
    public sealed class RecordingPhysicsBackend : IPhysicsBackend
    {
        private readonly IPhysicsBackend _inner;
        private readonly List<AuraJointDefinition> _joints = new List<AuraJointDefinition>();

        public RecordingPhysicsBackend(IPhysicsBackend inner)
        {
            _inner = inner;
        }

        public IReadOnlyList<AuraJointDefinition> JointDefinitions => _joints;

        string IPhysicsBackend.Name => _inner.Name;

        AuraPhysicsCapabilities IPhysicsBackend.Capabilities => _inner.Capabilities;

        IPhysicsWorld IPhysicsBackend.CreateWorld(in AuraWorldDefinition definition) =>
            new RecordingPhysicsWorld(_inner.CreateWorld(definition), _joints);
    }
}
