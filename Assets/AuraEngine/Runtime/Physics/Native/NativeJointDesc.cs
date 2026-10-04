using System;
using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeJointDesc
    {
        public int Type;
        public NativeBodyHandle BodyA;
        public NativeBodyHandle BodyB;
        public NativeVector3 AnchorA;
        public NativeVector3 AnchorB;
        public NativeVector3 AxisA;
        public NativeVector3 AxisB;
        public NativeVector3 NormalAxisA;
        public NativeVector3 NormalAxisB;
        public NativeVector3 FixedPoint;
        public float Distance;
        public float MinLimit;
        public float MaxLimit;
        public float SwingLimit;
        public float MotorTargetVelocity;
        public float MaxMotorForce;
        public float SpringFrequency;
        public float SpringDamping;
        public byte EnableLimit;
        public byte MotorEnabled;
        public byte Pad0;
        public byte Pad1;

        /* v11 appendix, in lockstep with AuraJointDesc (sizeof 280). */
        public NativeVector3 FixedPointB;
        public float Ratio;
        public float PlaneSwingLimit;
        public float MaxFriction;
        public NativeJointRef JointRefA;
        public NativeJointRef JointRefB;
        public NativeJointAxisLimit Axis0;
        public NativeJointAxisLimit Axis1;
        public NativeJointAxisLimit Axis2;
        public NativeJointAxisLimit Axis3;
        public NativeJointAxisLimit Axis4;
        public NativeJointAxisLimit Axis5;
        public byte PyramidSwing;
        public byte Pad2;
        public byte Pad3;
        public byte Pad4;

        public static NativeJointDesc From(in AuraJointDefinition definition) =>
            new NativeJointDesc
            {
                Type = (int)definition.Type,
                BodyA = NativeBodyHandle.From(definition.BodyA),
                BodyB = NativeBodyHandle.From(definition.BodyB),
                AnchorA = NativeVector3.From(definition.AnchorA),
                AnchorB = NativeVector3.From(definition.AnchorB),
                AxisA = NativeVector3.From(definition.AxisA),
                AxisB = NativeVector3.From(definition.AxisB),
                NormalAxisA = NativeVector3.From(definition.NormalAxisA),
                NormalAxisB = NativeVector3.From(definition.NormalAxisB),
                FixedPoint = NativeVector3.From(definition.FixedPointA),
                Distance = definition.Distance,
                MinLimit = definition.MinLimit,
                MaxLimit = definition.MaxLimit,
                SwingLimit = definition.SwingLimit,
                MotorTargetVelocity = definition.MotorTargetVelocity,
                MaxMotorForce = definition.MaxMotorForce,
                SpringFrequency = definition.SpringFrequency,
                SpringDamping = definition.SpringDamping,
                EnableLimit = definition.EnableLimit ? (byte)1 : (byte)0,
                MotorEnabled = definition.MotorEnabled ? (byte)1 : (byte)0,
                FixedPointB = NativeVector3.From(definition.FixedPointB),
                Ratio = definition.Ratio,
                PlaneSwingLimit = definition.PlaneSwingLimit,
                MaxFriction = definition.MaxFriction,
                JointRefA = NativeJointRef.From(definition.JointA),
                JointRefB = NativeJointRef.From(definition.JointB),
                Axis0 = NativeJointAxisLimit.From(definition.SixDof.TranslationX),
                Axis1 = NativeJointAxisLimit.From(definition.SixDof.TranslationY),
                Axis2 = NativeJointAxisLimit.From(definition.SixDof.TranslationZ),
                Axis3 = NativeJointAxisLimit.From(definition.SixDof.RotationX),
                Axis4 = NativeJointAxisLimit.From(definition.SixDof.RotationY),
                Axis5 = NativeJointAxisLimit.From(definition.SixDof.RotationZ),
                PyramidSwing = definition.PyramidSwing ? (byte)1 : (byte)0,
            };
    }
}
