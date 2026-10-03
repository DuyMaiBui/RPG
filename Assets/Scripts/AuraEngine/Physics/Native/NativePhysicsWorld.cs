using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    public sealed class NativePhysicsWorld : IPhysicsWorld, IPhysicsJoints, IPhysicsCharacters, IPhysicsContacts, IPhysicsSerialization
    {
        private readonly NativeWorldHandle _world;
        private readonly AuraPhysicsMode _mode;
        private readonly HashSet<PhysicsBodyId> _bodies = new HashSet<PhysicsBodyId>();
        private IntPtr _collisionMasks;
        private bool _disposed;

        public NativePhysicsWorld(in AuraWorldDefinition definition)
        {
            _mode = definition.Mode;
            _collisionMasks = Marshal.AllocHGlobal(AuraPhysicsLayer.MaxLayers * sizeof(ulong));
            for (var layer = 0; layer < AuraPhysicsLayer.MaxLayers; layer++)
                Marshal.WriteInt64(_collisionMasks, layer * sizeof(ulong), unchecked((long)definition.CollisionMatrix.GetMask(new AuraPhysicsLayer(layer))));

            var desc = NativeWorldDesc.From(definition);
            desc.CollisionMasks = _collisionMasks;
            desc.CollisionMaskCount = AuraPhysicsLayer.MaxLayers;

            AuraException.ThrowIfFailed((AuraResult)NativeMethods.Aura_CreateWorld(ref desc, out _world), "Failed to create the native physics world.");
        }

        AuraPhysicsMode IPhysicsWorld.Mode => _mode;

        AuraPhysicsCapabilities IPhysicsWorld.Capabilities
        {
            get
            {
                var body = AuraPhysicsCapabilities.BodyStatic |
                           AuraPhysicsCapabilities.BodyDynamic |
                           AuraPhysicsCapabilities.BodyKinematic |
                           AuraPhysicsCapabilities.QueryRaycast |
                           AuraPhysicsCapabilities.QueryShapeCast |
                           AuraPhysicsCapabilities.QueryOverlap |
                           AuraPhysicsCapabilities.Triggers |
                           AuraPhysicsCapabilities.Contacts |
                           AuraPhysicsCapabilities.SleepWake |
                           AuraPhysicsCapabilities.Joints;

                if (_mode == AuraPhysicsMode.Plane2D)
                    return body | AuraPhysicsCapabilities.ShapeBox | AuraPhysicsCapabilities.ShapeSphere | AuraPhysicsCapabilities.Joints;

                return body | AuraPhysicsCapabilities.ShapeBox |
                       AuraPhysicsCapabilities.ShapeSphere |
                       AuraPhysicsCapabilities.ShapeCapsule |
                       AuraPhysicsCapabilities.ShapeCylinder |
                       AuraPhysicsCapabilities.ShapeConvexMesh |
                       AuraPhysicsCapabilities.ShapeTriangleMesh |
                       AuraPhysicsCapabilities.ShapePlane |
                       AuraPhysicsCapabilities.ShapeTaperedCapsule |
                       AuraPhysicsCapabilities.ShapeTaperedCylinder |
                           AuraPhysicsCapabilities.ShapeHeightField |
                           AuraPhysicsCapabilities.Characters;
            }
        }

        int IPhysicsWorld.BodyCount
        {
            get
            {
                NativeMethods.Aura_WorldBodyCount(_world, out var count);
                return (int)count;
            }
        }

        int IPhysicsEventSource.PendingEventCount
        {
            get
            {
                NativeMethods.Aura_PendingEventCount(_world, out var count);
                return (int)count;
            }
        }

        PhysicsBodyId IPhysicsWorld.CreateBody(in AuraPhysicsBodyDefinition definition)
        {
            var shapeSize = Marshal.SizeOf<NativeShapeDesc>();
            var shapes = Marshal.AllocHGlobal(shapeSize * definition.Shapes.Length);
            var pins = new List<GCHandle>();
            try
            {
                for (var index = 0; index < definition.Shapes.Length; index++)
                {
                    var shape = NativeShapeDesc.From(definition.Shapes[index]);
                    var geometry = definition.Shapes[index].Geometry;
                    if (geometry.HeightSamples != null && geometry.HeightSamples.Length > 0)
                    {
                        var pin = GCHandle.Alloc(geometry.HeightSamples, GCHandleType.Pinned);
                        pins.Add(pin);
                        shape.Vertices = pin.AddrOfPinnedObject();
                    }
                    else if (geometry.MeshVertices != null && geometry.MeshVertices.Length > 0)
                    {
                        var pin = GCHandle.Alloc(geometry.MeshVertices, GCHandleType.Pinned);
                        pins.Add(pin);
                        shape.Vertices = pin.AddrOfPinnedObject();
                    }

                    if (geometry.MeshIndices != null && geometry.MeshIndices.Length > 0)
                    {
                        var pin = GCHandle.Alloc(geometry.MeshIndices, GCHandleType.Pinned);
                        pins.Add(pin);
                        shape.Indices = pin.AddrOfPinnedObject();
                    }

                    Marshal.StructureToPtr(shape, shapes + index * shapeSize, false);
                }

                var desc = NativeBodyDesc.From(definition);
                desc.Shapes = shapes;
                var entity = new NativeEntityHandle { Index = 0, Generation = 0 };
                var result = (AuraResult)NativeMethods.Aura_AttachBody(_world, entity, ref desc, out var handle);
                if (result != AuraResult.Success)
                    return PhysicsBodyId.Invalid;

                var body = handle.ToManaged();
                _bodies.Add(body);
                return body;
            }
            finally
            {
                Marshal.FreeHGlobal(shapes);
                for (var index = 0; index < pins.Count; index++)
                    pins[index].Free();
            }
        }

        AuraResult IPhysicsWorld.DestroyBody(PhysicsBodyId body)
        {
            var handle = new NativeBodyHandle { Index = (uint)body.Index, Generation = (uint)body.Generation };
            var result = (AuraResult)NativeMethods.Aura_DestroyBody(_world, handle);
            if (result == AuraResult.Success)
                _bodies.Remove(body);

            return result;
        }

        bool IPhysicsWorld.HasBody(PhysicsBodyId body) => _bodies.Contains(body);

        void IPhysicsWorld.SetKinematicTarget(PhysicsBodyId body, in AuraPose pose)
        {
            var handle = new NativeBodyHandle { Index = (uint)body.Index, Generation = (uint)body.Generation };
            var nativePose = NativePose.From(pose);
            NativeMethods.Aura_SetKinematicTarget(_world, handle, ref nativePose);
        }

        void IPhysicsWorld.ApplyImpulse(PhysicsBodyId body, AuraVector3 impulse)
        {
        }

        AuraResult IPhysicsWorld.SetSurfaceVelocity(PhysicsBodyId body, AuraVector3 velocity)
        {
            var handle = NativeBodyHandle.From(body);
            return (AuraResult)NativeMethods.Aura_SetSurfaceVelocity(_world, handle, NativeVector3.From(velocity));
        }

        IPhysicsJoints IPhysicsWorld.Joints => this;

        IPhysicsCharacters IPhysicsWorld.Characters => this;

        IPhysicsContacts IPhysicsWorld.Contacts => this;

        IPhysicsSerialization IPhysicsWorld.Serialization => this;

        AuraJointId IPhysicsJoints.CreateJoint(in AuraJointDefinition definition)
        {
            var desc = NativeJointDesc.From(definition);
            var result = (AuraResult)NativeMethods.Aura_CreateJoint(_world, ref desc, out var handle);
            if (result != AuraResult.Success)
                return AuraJointId.Invalid;
            return new AuraJointId((int)(handle & 0xFFFFFFFFul), (int)(handle >> 32));
        }

        AuraResult IPhysicsJoints.DestroyJoint(AuraJointId joint)
        {
            if (!joint.IsValid)
                return AuraResult.InvalidHandle;
            var handle = ((ulong)(uint)joint.Generation << 32) | (uint)joint.Index;
            return (AuraResult)NativeMethods.Aura_DestroyJoint(_world, handle);
        }

        bool IPhysicsJoints.HasJoint(AuraJointId joint)
        {
            if (!joint.IsValid)
                return false;
            var handle = ((ulong)(uint)joint.Generation << 32) | (uint)joint.Index;
            NativeMethods.Aura_HasJoint(_world, handle, out var has);
            return has != 0;
        }

        AuraCharacterId IPhysicsCharacters.CreateCharacter(in AuraCharacterDefinition definition)
        {
            var desc = NativeCharacterDesc.From(definition);
            var result = (AuraResult)NativeMethods.Aura_CreateCharacter(_world, ref desc, out var handle);
            if (result != AuraResult.Success)
                return AuraCharacterId.Invalid;
            return new AuraCharacterId((int)(handle & 0xFFFFFFFFul), (int)(handle >> 32));
        }

        AuraResult IPhysicsCharacters.DestroyCharacter(AuraCharacterId character)
        {
            if (!character.IsValid)
                return AuraResult.InvalidHandle;
            return (AuraResult)NativeMethods.Aura_DestroyCharacter(_world, ToCharacterHandle(character));
        }

        bool IPhysicsCharacters.TryGetCharacterState(AuraCharacterId character, out AuraCharacterState state)
        {
            state = default;
            if (!character.IsValid)
                return false;

            var result = (AuraResult)NativeMethods.Aura_GetCharacterState(_world, ToCharacterHandle(character), out var native);
            if (result != AuraResult.Success)
                return false;

            state = new AuraCharacterState(character, native.Position.ToManaged(), native.Velocity.ToManaged(), native.IsGrounded != 0);
            return true;
        }

        void IPhysicsCharacters.MoveCharacter(AuraCharacterId character, AuraVector3 desiredTranslation, float deltaTime)
        {
            if (!character.IsValid)
                return;
            NativeMethods.Aura_MoveCharacter(_world, ToCharacterHandle(character), NativeVector3.From(desiredTranslation), deltaTime);
        }

        private static ulong ToCharacterHandle(AuraCharacterId character) =>
            ((ulong)(uint)character.Generation << 32) | (uint)character.Index;

        int IPhysicsContacts.CopyContacts(Span<AuraContact> buffer)
        {
            if (buffer.Length == 0)
                return 0;

            var size = Marshal.SizeOf<NativeContact>();
            var native = Marshal.AllocHGlobal(size * buffer.Length);
            try
            {
                var result = NativeMethods.Aura_CopyContacts(_world, native, (uint)buffer.Length, out var count);
                if ((AuraResult)result != AuraResult.Success)
                    return 0;

                var written = (int)count;
                for (var index = 0; index < written; index++)
                {
                    var contact = Marshal.PtrToStructure<NativeContact>(native + index * size);
                    buffer[index] = new AuraContact(
                        SimulationEntityId.None,
                        SimulationEntityId.None,
                        contact.BodyA.ToManaged(),
                        contact.BodyB.ToManaged(),
                        contact.Point.ToManaged(),
                        contact.Normal.ToManaged(),
                        contact.Impulse);
                }

                return written;
            }
            finally
            {
                Marshal.FreeHGlobal(native);
            }
        }

        ulong IPhysicsSerialization.ComputeStateHash()
        {
            NativeMethods.Aura_ComputeStateHash(_world, out var hash);
            return hash;
        }

        byte[] IPhysicsSerialization.SaveState()
        {
            /* Size probe with an empty capacity; the native side writes the
               required size through outSize and returns CAPACITY_EXCEEDED. The
               managed caller must stay native-free on this failure path. */
            NativeMethods.Aura_SerializeState(_world, IntPtr.Zero, 0, out var size);
            if (size == 0)
                return Array.Empty<byte>();

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                var result = (AuraResult)NativeMethods.Aura_SerializeState(_world, buffer, size, out var written);
                if (result != AuraResult.Success)
                    return Array.Empty<byte>();

                var bytes = new byte[written];
                Marshal.Copy(buffer, bytes, 0, (int)written);
                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        void IPhysicsSerialization.RestoreState(byte[] state)
        {
            if (state == null || state.Length == 0)
                return;

            var buffer = Marshal.AllocHGlobal(state.Length);
            try
            {
                Marshal.Copy(state, 0, buffer, state.Length);
                NativeMethods.Aura_DeserializeState(_world, buffer, (uint)state.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        AuraResult IPhysicsWorld.GetBodyState(PhysicsBodyId body, out AuraBodyState state)
        {
            state = default;
            if (!_bodies.Contains(body))
                return AuraResult.InvalidHandle;

            var handle = new NativeBodyHandle { Index = (uint)body.Index, Generation = (uint)body.Generation };
            var result = (AuraResult)NativeMethods.Aura_GetBodyState(_world, handle, out var nativeState);
            if (result != AuraResult.Success)
                return result;

            state = nativeState.ToManaged(SimulationEntityId.None);
            return AuraResult.Success;
        }

        int IPhysicsWorld.CopyBodyStates(Span<AuraBodyState> buffer)
        {
            if (buffer.Length == 0)
                return 0;

            var size = Marshal.SizeOf<NativeBodyState>();
            var raw = Marshal.AllocHGlobal(size * buffer.Length);
            try
            {
                var result = (AuraResult)NativeMethods.Aura_CopyBodyStates(_world, raw, (uint)buffer.Length, out var count);
                if (result != AuraResult.Success)
                    return 0;

                for (var index = 0; index < count; index++)
                {
                    var state = Marshal.PtrToStructure<NativeBodyState>(raw + index * size);
                    buffer[index] = state.ToManaged(SimulationEntityId.None);
                }

                return (int)count;
            }
            finally
            {
                Marshal.FreeHGlobal(raw);
            }
        }

        void IPhysicsWorld.Step(float deltaTime) => NativeMethods.Aura_Step(_world, 0u, deltaTime);

        int IPhysicsEventSource.CopyEvents(Span<AuraPhysicsEvent> buffer)
        {
            if (buffer.Length == 0)
                return 0;

            var size = Marshal.SizeOf<NativePhysicsEvent>();
            var raw = Marshal.AllocHGlobal(size * buffer.Length);
            try
            {
                var result = (AuraResult)NativeMethods.Aura_CopyEvents(_world, raw, (uint)buffer.Length, out var count);
                if (result != AuraResult.Success)
                    return 0;

                for (var index = 0; index < count; index++)
                    buffer[index] = Marshal.PtrToStructure<NativePhysicsEvent>(raw + index * size).ToManaged();

                return (int)count;
            }
            finally
            {
                Marshal.FreeHGlobal(raw);
            }
        }

        bool IPhysicsQuery.Raycast(in AuraRay ray, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit)
        {
            var nativeRay = NativeRay.From(ray);
            var nativeFilter = NativeQueryFilter.From(filter);
            var result = (AuraResult)NativeMethods.Aura_Raycast(_world, ref nativeRay, maxDistance, ref nativeFilter, out var nativeHit, out var hasHit);
            hit = result == AuraResult.Success && hasHit != 0 ? nativeHit.ToManaged() : default;
            return hasHit != 0;
        }

        int IPhysicsQuery.RaycastAll(in AuraRay ray, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0)
                return 0;

            var size = Marshal.SizeOf<NativeQueryHit>();
            var raw = Marshal.AllocHGlobal(size * results.Length);
            try
            {
                var nativeRay = NativeRay.From(ray);
                var nativeFilter = NativeQueryFilter.From(filter);
                var result = (AuraResult)NativeMethods.Aura_RaycastAll(_world, ref nativeRay, maxDistance, ref nativeFilter, raw, (uint)results.Length, out var count);
                if (result != AuraResult.Success)
                    return 0;

                for (var index = 0; index < count; index++)
                    results[index] = Marshal.PtrToStructure<NativeQueryHit>(raw + index * size).ToManaged();

                return (int)count;
            }
            finally
            {
                Marshal.FreeHGlobal(raw);
            }
        }

        bool IPhysicsQuery.SphereCast(AuraVector3 origin, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit)
        {
            var nativeFilter = NativeQueryFilter.From(filter);
            var result = (AuraResult)NativeMethods.Aura_SphereCast(_world, NativeVector3.From(origin), radius, NativeVector3.From(direction), maxDistance, ref nativeFilter, out var nativeHit, out var hasHit);
            hit = result == AuraResult.Success && hasHit != 0 ? nativeHit.ToManaged() : default;
            return result == AuraResult.Success && hasHit != 0;
        }

        int IPhysicsQuery.SphereCastAll(AuraVector3 origin, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0 || !((IPhysicsQuery)this).SphereCast(origin, radius, direction, maxDistance, filter, out var hit))
                return 0;
            results[0] = hit;
            return 1;
        }

        bool IPhysicsQuery.CapsuleCast(AuraVector3 pointA, AuraVector3 pointB, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit)
        {
            var nativeFilter = NativeQueryFilter.From(filter);
            var result = (AuraResult)NativeMethods.Aura_CapsuleCast(_world, NativeVector3.From(pointA), NativeVector3.From(pointB), radius, NativeVector3.From(direction), maxDistance, ref nativeFilter, out var nativeHit, out var hasHit);
            hit = result == AuraResult.Success && hasHit != 0 ? nativeHit.ToManaged() : default;
            return result == AuraResult.Success && hasHit != 0;
        }

        int IPhysicsQuery.CapsuleCastAll(AuraVector3 pointA, AuraVector3 pointB, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0 || !((IPhysicsQuery)this).CapsuleCast(pointA, pointB, radius, direction, maxDistance, filter, out var hit))
                return 0;
            results[0] = hit;
            return 1;
        }

        bool IPhysicsQuery.BoxCast(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit)
        {
            var nativeFilter = NativeQueryFilter.From(filter);
            var result = (AuraResult)NativeMethods.Aura_BoxCast(_world, NativeVector3.From(center), NativeVector3.From(halfExtents), NativeQuaternion.From(rotation), NativeVector3.From(direction), maxDistance, ref nativeFilter, out var nativeHit, out var hasHit);
            hit = result == AuraResult.Success && hasHit != 0 ? nativeHit.ToManaged() : default;
            return result == AuraResult.Success && hasHit != 0;
        }

        int IPhysicsQuery.BoxCastAll(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0 || !((IPhysicsQuery)this).BoxCast(center, halfExtents, rotation, direction, maxDistance, filter, out var hit))
                return 0;
            results[0] = hit;
            return 1;
        }

        bool IPhysicsQuery.ShapeCast(in AuraPhysicsShapeDefinition shape, AuraPose pose, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit)
        {
            switch (shape.Type)
            {
                case AuraShapeType.Sphere:
                    return ((IPhysicsQuery)this).SphereCast(pose.Position, shape.Geometry.Radius, direction, maxDistance, filter, out hit);
                case AuraShapeType.Capsule:
                    var axis = pose.Rotation.Rotate(AuraVector3.UnitY);
                    var half = Math.Max(0f, shape.Geometry.Height * 0.5f - shape.Geometry.Radius);
                    return ((IPhysicsQuery)this).CapsuleCast(pose.Position - axis * half, pose.Position + axis * half, shape.Geometry.Radius, direction, maxDistance, filter, out hit);
                case AuraShapeType.Box:
                    return ((IPhysicsQuery)this).BoxCast(pose.Position, shape.Geometry.HalfExtents, pose.Rotation, direction, maxDistance, filter, out hit);
                default:
                    hit = default;
                    return false;
            }
        }

        int IPhysicsQuery.ShapeCastAll(in AuraPhysicsShapeDefinition shape, AuraPose pose, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0 || !((IPhysicsQuery)this).ShapeCast(shape, pose, direction, maxDistance, filter, out var hit))
                return 0;
            results[0] = hit;
            return 1;
        }

        int IPhysicsQuery.OverlapPoint(AuraVector3 point, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            CopyOverlapPoint(point, filter, results);

        int IPhysicsQuery.OverlapSphere(AuraVector3 center, float radius, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0)
                return 0;

            var size = Marshal.SizeOf<NativeQueryHit>();
            var raw = Marshal.AllocHGlobal(size * results.Length);
            try
            {
                var nativeCenter = NativeVector3.From(center);
                var nativeFilter = NativeQueryFilter.From(filter);
                var result = (AuraResult)NativeMethods.Aura_OverlapSphere(_world, nativeCenter, radius, ref nativeFilter, raw, (uint)results.Length, out var count);
                if (result != AuraResult.Success)
                    return 0;

                for (var index = 0; index < count; index++)
                    results[index] = Marshal.PtrToStructure<NativeQueryHit>(raw + index * size).ToManaged();

                return (int)count;
            }
            finally
            {
                Marshal.FreeHGlobal(raw);
            }
        }

        int IPhysicsQuery.OverlapBox(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            CopyOverlapBox(center, halfExtents, rotation, filter, results);

        int IPhysicsQuery.OverlapCapsule(AuraVector3 pointA, AuraVector3 pointB, float radius, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            CopyOverlapCapsule(pointA, pointB, radius, filter, results);

        int IPhysicsQuery.OverlapShape(in AuraPhysicsShapeDefinition shape, AuraPose pose, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0)
                return 0;
            switch (shape.Type)
            {
                case AuraShapeType.Sphere:
                    return ((IPhysicsQuery)this).OverlapSphere(pose.Position, shape.Geometry.Radius, filter, results);
                case AuraShapeType.Box:
                    return ((IPhysicsQuery)this).OverlapBox(pose.Position, shape.Geometry.HalfExtents, pose.Rotation, filter, results);
                case AuraShapeType.Capsule:
                    var axis = pose.Rotation.Rotate(AuraVector3.UnitY);
                    var half = Math.Max(0f, shape.Geometry.Height * 0.5f - shape.Geometry.Radius);
                    return ((IPhysicsQuery)this).OverlapCapsule(pose.Position - axis * half, pose.Position + axis * half, shape.Geometry.Radius, filter, results);
                default:
                    return 0;
            }
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            NativeMethods.Aura_DestroyWorld(_world);
            if (_collisionMasks != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_collisionMasks);
                _collisionMasks = IntPtr.Zero;
            }
        }

        private int CopyOverlapPoint(AuraVector3 point, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0)
                return 0;
            var raw = Marshal.AllocHGlobal(Marshal.SizeOf<NativeQueryHit>() * results.Length);
            try
            {
                var nativeFilter = NativeQueryFilter.From(filter);
                var result = (AuraResult)NativeMethods.Aura_OverlapPoint(_world, NativeVector3.From(point), ref nativeFilter, raw, (uint)results.Length, out var count);
                return result == AuraResult.Success ? CopyHits(raw, count, results) : 0;
            }
            finally { Marshal.FreeHGlobal(raw); }
        }

        private int CopyOverlapBox(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0)
                return 0;
            var raw = Marshal.AllocHGlobal(Marshal.SizeOf<NativeQueryHit>() * results.Length);
            try
            {
                var nativeFilter = NativeQueryFilter.From(filter);
                var result = (AuraResult)NativeMethods.Aura_OverlapBox(_world, NativeVector3.From(center), NativeVector3.From(halfExtents), NativeQuaternion.From(rotation), ref nativeFilter, raw, (uint)results.Length, out var count);
                return result == AuraResult.Success ? CopyHits(raw, count, results) : 0;
            }
            finally { Marshal.FreeHGlobal(raw); }
        }

        private int CopyOverlapCapsule(AuraVector3 pointA, AuraVector3 pointB, float radius, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (results.Length == 0)
                return 0;
            var raw = Marshal.AllocHGlobal(Marshal.SizeOf<NativeQueryHit>() * results.Length);
            try
            {
                var nativeFilter = NativeQueryFilter.From(filter);
                var result = (AuraResult)NativeMethods.Aura_OverlapCapsule(_world, NativeVector3.From(pointA), NativeVector3.From(pointB), radius, ref nativeFilter, raw, (uint)results.Length, out var count);
                return result == AuraResult.Success ? CopyHits(raw, count, results) : 0;
            }
            finally { Marshal.FreeHGlobal(raw); }
        }

        private static int CopyHits(IntPtr raw, uint count, Span<AuraPhysicsQueryHit> results)
        {
            var size = Marshal.SizeOf<NativeQueryHit>();
            var limit = Math.Min((int)count, results.Length);
            for (var index = 0; index < limit; index++)
                results[index] = Marshal.PtrToStructure<NativeQueryHit>(raw + index * size).ToManaged();
            return limit;
        }

        private int OverlapSphereCount(AuraVector3 center, float radius, in AuraPhysicsQueryFilter filter)
        {
            var size = Marshal.SizeOf<NativeQueryHit>();
            var raw = Marshal.AllocHGlobal(size);
            try
            {
                var nativeCenter = NativeVector3.From(center);
                var nativeFilter = NativeQueryFilter.From(filter);
                var result = (AuraResult)NativeMethods.Aura_OverlapSphere(_world, nativeCenter, radius, ref nativeFilter, raw, 1u, out var count);
                return result == AuraResult.Success ? (int)count : 0;
            }
            finally
            {
                Marshal.FreeHGlobal(raw);
            }
        }
    }
}
