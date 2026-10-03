using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeVehicleDesc
    {
        public NativeBodyHandle Chassis;
        public NativeVector3 Up;
        public NativeVector3 Forward;
        public NativeVector3 WheelPosition0, WheelPosition1, WheelPosition2, WheelPosition3;
        public float WheelRadius, WheelWidth, SuspensionMinLength, SuspensionMaxLength, SuspensionFrequency, SuspensionDamping;
        public float MaxSteerAngle, MaxPitchRollAngle, MaxEngineTorque;
        public uint WheelObjectLayer, Pad0;

        public static NativeVehicleDesc From(in AuraVehicleDefinition definition)
        {
            var positions = new NativeVector3[4];
            for (var index = 0; index < positions.Length && index < definition.WheelPositions.Length; index++)
                positions[index] = NativeVector3.From(definition.WheelPositions[index]);
            return new NativeVehicleDesc
            {
                Chassis = NativeBodyHandle.From(definition.Chassis), Up = NativeVector3.From(definition.Up), Forward = NativeVector3.From(definition.Forward),
                WheelPosition0 = positions[0], WheelPosition1 = positions[1], WheelPosition2 = positions[2], WheelPosition3 = positions[3],
                WheelRadius = definition.WheelRadius, WheelWidth = definition.WheelWidth, SuspensionMinLength = definition.SuspensionMinLength,
                SuspensionMaxLength = definition.SuspensionMaxLength, SuspensionFrequency = definition.SuspensionFrequency, SuspensionDamping = definition.SuspensionDamping,
                MaxSteerAngle = definition.MaxSteerAngle, MaxPitchRollAngle = definition.MaxPitchRollAngle, MaxEngineTorque = definition.MaxEngineTorque,
                WheelObjectLayer = (uint)definition.WheelObjectLayer.Value
            };
        }
    }
}
