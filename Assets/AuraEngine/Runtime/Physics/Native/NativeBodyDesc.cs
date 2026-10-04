using System;
using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeBodyDesc
    {
        public int Type;
        public uint Layer;
        public ulong CollisionMask;
        public int GroupIndex;
        public float Mass;
        public float GravityScale;
        public float Friction;
        public float Restitution;
        public float Density;
        public NativePose InitialPose;
        public NativeVector3 InitialLinearVelocity;
        public NativeVector3 InitialAngularVelocity;
        public IntPtr Shapes;
        public uint ShapeCount;
        public uint Pad0;
        public float LinearDamping;
        public float AngularDamping;
        public float MaxLinearVelocity;
        public float MaxAngularVelocity;
        public NativeVector3 CenterOfMass;
        public float InertiaMultiplier;
        public uint FreezeFlags;
        public int CollisionDetection;
        public byte AllowSleeping;
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;

        public static NativeBodyDesc From(in AuraPhysicsBodyDefinition definition) =>
            new NativeBodyDesc
            {
                Type = (int)definition.Type,
                Layer = (uint)definition.Layer.Value,
                CollisionMask = definition.CollisionMask.Bits,
                GroupIndex = definition.GroupIndex,
                Mass = definition.Mass,
                GravityScale = definition.GravityScale,
                Friction = ResolveFriction(definition),
                Restitution = ResolveRestitution(definition),
                Density = definition.Material.Density,
                InitialPose = NativePose.From(definition.InitialPose),
                InitialLinearVelocity = NativeVector3.From(definition.InitialLinearVelocity),
                InitialAngularVelocity = NativeVector3.From(definition.InitialAngularVelocity),
                ShapeCount = (uint)definition.Shapes.Length,
                LinearDamping = definition.LinearDamping,
                AngularDamping = definition.AngularDamping,
                MaxLinearVelocity = definition.MaxLinearVelocity,
                MaxAngularVelocity = definition.MaxAngularVelocity,
                CenterOfMass = NativeVector3.From(definition.CenterOfMass),
                InertiaMultiplier = definition.InertiaMultiplier,
                FreezeFlags = (uint)definition.Freeze,
                CollisionDetection = (int)definition.CollisionDetection,
                AllowSleeping = definition.AllowSleeping ? (byte)1 : (byte)0,
            };

        private static float ResolveFriction(in AuraPhysicsBodyDefinition definition)
        {
            if (definition.Material.Friction > 0f)
                return definition.Material.Friction;

            var friction = 0f;
            for (var index = 0; index < definition.Shapes.Length; index++)
            {
                if (definition.Shapes[index].Material.Friction > friction)
                    friction = definition.Shapes[index].Material.Friction;
            }

            return friction;
        }

        private static float ResolveRestitution(in AuraPhysicsBodyDefinition definition)
        {
            if (definition.Material.Restitution > 0f)
                return definition.Material.Restitution;

            var restitution = 0f;
            for (var index = 0; index < definition.Shapes.Length; index++)
            {
                if (definition.Shapes[index].Material.Restitution > restitution)
                    restitution = definition.Shapes[index].Material.Restitution;
            }

            return restitution;
        }
    }
}
