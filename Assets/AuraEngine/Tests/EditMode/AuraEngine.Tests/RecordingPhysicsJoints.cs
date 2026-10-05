using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Tests
{
    public sealed class RecordingPhysicsJoints : IPhysicsJoints
    {
        private readonly IPhysicsJoints _inner;
        private readonly List<AuraJointDefinition> _definitions;

        public RecordingPhysicsJoints(IPhysicsJoints inner, List<AuraJointDefinition> definitions)
        {
            _inner = inner;
            _definitions = definitions;
        }

        AuraJointId IPhysicsJoints.CreateJoint(in AuraJointDefinition definition)
        {
            _definitions.Add(definition);
            return _inner.CreateJoint(definition);
        }

        AuraResult IPhysicsJoints.DestroyJoint(AuraJointId joint) => _inner.DestroyJoint(joint);

        bool IPhysicsJoints.HasJoint(AuraJointId joint) => _inner.HasJoint(joint);
    }
}
