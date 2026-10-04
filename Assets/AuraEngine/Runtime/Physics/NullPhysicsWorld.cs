using System;
using System.Collections.Generic;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public sealed class NullPhysicsWorld : IPhysicsWorld, IPhysicsJoints, IPhysicsCharacters, IPhysicsVehicles, IPhysicsSoftBodies, IPhysicsRagdolls, IPhysicsHair, IPhysicsWater, IPhysicsContacts, IPhysicsSerialization
    {
        private readonly List<NullBodySlot> _slots;
        private readonly List<int> _freeSlots = new List<int>();

        public NullPhysicsWorld(in AuraWorldDefinition definition)
        {
            Mode = definition.Mode;
            _slots = new List<NullBodySlot>(Math.Max(1, definition.InitialBodyCapacity));
        }

        AuraPhysicsMode IPhysicsWorld.Mode => Mode;

        AuraPhysicsCapabilities IPhysicsWorld.Capabilities =>
            AuraPhysicsCapabilities.BodyStatic |
            AuraPhysicsCapabilities.BodyDynamic |
            AuraPhysicsCapabilities.BodyKinematic;

        public AuraPhysicsMode Mode { get; }

        int IPhysicsWorld.BodyCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _slots.Count; index++)
                {
                    if (_slots[index].Occupied)
                        count++;
                }

                return count;
            }
        }

        PhysicsBodyId IPhysicsWorld.CreateBody(in AuraPhysicsBodyDefinition definition)
        {
            if (definition.Validate() != AuraResult.Success)
                return PhysicsBodyId.Invalid;

            if (_freeSlots.Count > 0)
            {
                var index = _freeSlots[_freeSlots.Count - 1];
                _freeSlots.RemoveAt(_freeSlots.Count - 1);
                var slot = _slots[index];
                slot.Occupied = true;
                slot.Pose = definition.InitialPose;
                slot.IsAwake = definition.Type != AuraBodyType.Static;
                _slots[index] = slot;
                return new PhysicsBodyId(index, slot.Generation);
            }

            var generation = 0;
            _slots.Add(new NullBodySlot
            {
                Generation = generation,
                Occupied = true,
                Pose = definition.InitialPose,
                IsAwake = definition.Type != AuraBodyType.Static,
            });

            return new PhysicsBodyId(_slots.Count - 1, generation);
        }

        AuraResult IPhysicsWorld.DestroyBody(PhysicsBodyId body)
        {
            if (!TryGetSlot(body, out var index))
                return AuraResult.InvalidHandle;

            var slot = _slots[index];
            slot.Occupied = false;
            slot.Generation++;
            _slots[index] = slot;
            _freeSlots.Add(index);
            return AuraResult.Success;
        }

        bool IPhysicsWorld.HasBody(PhysicsBodyId body) => TryGetSlot(body, out _);

        void IPhysicsWorld.SetKinematicTarget(PhysicsBodyId body, in AuraPose pose)
        {
            if (!TryGetSlot(body, out var index))
                return;

            var slot = _slots[index];
            slot.Pose = pose;
            _slots[index] = slot;
        }

        void IPhysicsWorld.ApplyImpulse(PhysicsBodyId body, AuraVector3 impulse)
        {
        }

        AuraResult IPhysicsWorld.SetSurfaceVelocity(PhysicsBodyId body, AuraVector3 velocity) =>
            TryGetSlot(body, out _) ? AuraResult.Success : AuraResult.InvalidHandle;

        IPhysicsJoints IPhysicsWorld.Joints => this;

        IPhysicsBodyControl IPhysicsWorld.BodyControl => NullPhysicsBodyControl.Instance;

        IPhysicsJointControl IPhysicsWorld.JointControl => NullPhysicsJointControl.Instance;

        IPhysicsCharacters IPhysicsWorld.Characters => this;
        IPhysicsVehicles IPhysicsWorld.Vehicles => this;
        IPhysicsSoftBodies IPhysicsWorld.SoftBodies => this;
        IPhysicsRagdolls IPhysicsWorld.Ragdolls => this;
        IPhysicsHair IPhysicsWorld.Hair => this;
        IPhysicsWater IPhysicsWorld.Water => this;

        IPhysicsContacts IPhysicsWorld.Contacts => this;

        IPhysicsSerialization IPhysicsWorld.Serialization => this;

        AuraJointId IPhysicsJoints.CreateJoint(in AuraJointDefinition definition) => AuraJointId.Invalid;

        AuraResult IPhysicsJoints.DestroyJoint(AuraJointId joint) => AuraResult.InvalidHandle;

        bool IPhysicsJoints.HasJoint(AuraJointId joint) => false;

        AuraCharacterId IPhysicsCharacters.CreateCharacter(in AuraCharacterDefinition definition) => AuraCharacterId.Invalid;

        AuraResult IPhysicsCharacters.DestroyCharacter(AuraCharacterId character) => AuraResult.InvalidHandle;

        bool IPhysicsCharacters.TryGetCharacterState(AuraCharacterId character, out AuraCharacterState state)
        {
            state = default;
            return false;
        }

        void IPhysicsCharacters.MoveCharacter(AuraCharacterId character, AuraVector3 desiredTranslation, float deltaTime)
        {
        }

        AuraVehicleId IPhysicsVehicles.CreateVehicle(in AuraVehicleDefinition definition) => AuraVehicleId.Invalid;
        AuraResult IPhysicsVehicles.DestroyVehicle(AuraVehicleId vehicle) => AuraResult.InvalidHandle;
        AuraResult IPhysicsVehicles.SetVehicleInput(AuraVehicleId vehicle, float forward, float steering, float brake, float handBrake) => AuraResult.InvalidHandle;
        bool IPhysicsVehicles.TryGetWheelState(AuraVehicleId vehicle, int wheelIndex, out AuraVehicleWheelState state) { state = default; return false; }
        AuraSoftBodyId IPhysicsSoftBodies.CreateSoftBody(in AuraSoftBodyDefinition definition) => AuraSoftBodyId.Invalid;
        AuraResult IPhysicsSoftBodies.DestroySoftBody(AuraSoftBodyId softBody) => AuraResult.InvalidHandle;
        bool IPhysicsSoftBodies.TryGetState(AuraSoftBodyId softBody, out AuraSoftBodyState state) { state = default; return false; }
        AuraRagdollId IPhysicsRagdolls.CreateRagdoll(in AuraRagdollDefinition definition) => AuraRagdollId.Invalid;
        AuraResult IPhysicsRagdolls.DestroyRagdoll(AuraRagdollId ragdoll) => AuraResult.InvalidHandle;
        AuraResult IPhysicsRagdolls.GetPose(AuraRagdollId ragdoll, Span<AuraPose> poses) => AuraResult.InvalidHandle;
        AuraResult IPhysicsRagdolls.SetPose(AuraRagdollId ragdoll, ReadOnlySpan<AuraPose> poses) => AuraResult.InvalidHandle;

        AuraHairId IPhysicsHair.CreateHair(in AuraHairDefinition definition) => AuraHairId.Invalid;
        AuraResult IPhysicsHair.DestroyHair(AuraHairId hair) => AuraResult.UnsupportedShape;
        bool IPhysicsHair.TryGetState(AuraHairId hair, out AuraHairState state) { state = default; return false; }

        int IPhysicsContacts.CopyContacts(Span<AuraContact> buffer) => 0;

        ulong IPhysicsSerialization.ComputeStateHash() => 0ul;

        byte[] IPhysicsSerialization.SaveState() => Array.Empty<byte>();

        void IPhysicsSerialization.RestoreState(byte[] state)
        {
        }

        AuraResult IPhysicsWorld.GetBodyState(PhysicsBodyId body, out AuraBodyState state)
        {
            if (!TryGetSlot(body, out var index))
            {
                state = default;
                return AuraResult.InvalidHandle;
            }

            var slot = _slots[index];
            state = new AuraBodyState(
                new PhysicsBodyId(index, slot.Generation),
                SimulationEntityId.None,
                slot.Pose,
                AuraVector3.Zero,
                AuraVector3.Zero,
                slot.IsAwake,
                0u);
            return AuraResult.Success;
        }

        int IPhysicsWorld.CopyBodyStates(Span<AuraBodyState> buffer)
        {
            var written = 0;
            for (var index = 0; index < _slots.Count && written < buffer.Length; index++)
            {
                var slot = _slots[index];
                if (!slot.Occupied)
                    continue;

                buffer[written++] = new AuraBodyState(
                    new PhysicsBodyId(index, slot.Generation),
                    SimulationEntityId.None,
                    slot.Pose,
                    AuraVector3.Zero,
                    AuraVector3.Zero,
                    slot.IsAwake,
                    0u);
            }

            return written;
        }

        void IPhysicsWorld.Step(float deltaTime)
        {
        }

        AuraWaterId IPhysicsWater.CreateWater(in AuraWaterDefinition definition) => definition.Density > 0f ? new AuraWaterId(1) : AuraWaterId.Invalid;
        AuraResult IPhysicsWater.DestroyWater(AuraWaterId water) => water.IsValid ? AuraResult.Success : AuraResult.InvalidHandle;
        AuraResult IPhysicsWater.SetWaterParameters(AuraWaterId water, in AuraWaterDefinition definition) => water.IsValid && definition.Density > 0f ? AuraResult.Success : AuraResult.InvalidHandle;
        AuraResult IPhysicsWater.ApplyWaterStep(AuraWaterId water, float deltaTime) => water.IsValid && deltaTime >= 0f ? AuraResult.Success : AuraResult.InvalidHandle;

        AuraWaterId IPhysicsWorld.CreateWater(in AuraWaterDefinition definition) => definition.Density > 0f ? new AuraWaterId(1) : AuraWaterId.Invalid;
        AuraResult IPhysicsWorld.DestroyWater(AuraWaterId water) => water.IsValid ? AuraResult.Success : AuraResult.InvalidHandle;
        AuraResult IPhysicsWorld.SetWaterParameters(AuraWaterId water, in AuraWaterDefinition definition) => water.IsValid && definition.Density > 0f ? AuraResult.Success : AuraResult.InvalidHandle;
        AuraResult IPhysicsWorld.ApplyWaterStep(AuraWaterId water, float deltaTime) => water.IsValid && deltaTime >= 0f ? AuraResult.Success : AuraResult.InvalidHandle;

        bool IPhysicsQuery.Raycast(
            in AuraRay ray,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            return false;
        }

        int IPhysicsQuery.RaycastAll(
            in AuraRay ray,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        bool IPhysicsQuery.SphereCast(
            AuraVector3 origin,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            return false;
        }

        int IPhysicsQuery.SphereCastAll(
            AuraVector3 origin,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        bool IPhysicsQuery.BoxCast(
            AuraVector3 center,
            AuraVector3 halfExtents,
            AuraQuaternion rotation,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            return false;
        }

        int IPhysicsQuery.BoxCastAll(
            AuraVector3 center,
            AuraVector3 halfExtents,
            AuraQuaternion rotation,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        bool IPhysicsQuery.CapsuleCast(
            AuraVector3 pointA,
            AuraVector3 pointB,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            return false;
        }

        int IPhysicsQuery.CapsuleCastAll(
            AuraVector3 pointA,
            AuraVector3 pointB,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        bool IPhysicsQuery.ShapeCast(
            in AuraPhysicsShapeDefinition shape,
            AuraPose pose,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            return false;
        }

        int IPhysicsQuery.ShapeCastAll(
            in AuraPhysicsShapeDefinition shape,
            AuraPose pose,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsQuery.OverlapPoint(
            AuraVector3 point,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsQuery.OverlapSphere(
            AuraVector3 center,
            float radius,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsQuery.OverlapBox(
            AuraVector3 center,
            AuraVector3 halfExtents,
            AuraQuaternion rotation,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsQuery.OverlapCapsule(
            AuraVector3 pointA,
            AuraVector3 pointB,
            float radius,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsQuery.OverlapShape(
            in AuraPhysicsShapeDefinition shape,
            AuraPose pose,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsEventSource.PendingEventCount => 0;

        int IPhysicsEventSource.CopyEvents(Span<AuraPhysicsEvent> buffer) => 0;

        void IDisposable.Dispose()
        {
            _slots.Clear();
            _freeSlots.Clear();
        }

        private bool TryGetSlot(PhysicsBodyId body, out int index)
        {
            index = body.Index;
            return body.IsValid &&
                   index < _slots.Count &&
                   _slots[index].Occupied &&
                   _slots[index].Generation == body.Generation;
        }
    }
}
