using System;
using System.Buffers.Binary;
using AuraEngine.Core;

namespace AuraEngine.Serialization
{
    public static class AuraStateSerializer
    {
        public const uint Magic = 0x41555241u;
        public const ushort Version = 1;
        private const int HeaderSize = 4 + 2 + 2 + 4 + 4;
        private const int BodyStateSize = 4 * 4 + 4 * 7 + 4 * 3 + 4 * 3 + 4 + 4;

        public static byte[] Serialize(AuraSimulationSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var bytes = new byte[HeaderSize + snapshot.BodyStates.Length * BodyStateSize];
            var span = new Span<byte>(bytes);
            BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4), Version);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(6), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8), snapshot.Tick.Value);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(12), snapshot.BodyStates.Length);

            var offset = HeaderSize;
            for (var index = 0; index < snapshot.BodyStates.Length; index++)
                WriteBodyState(span.Slice(offset + index * BodyStateSize), snapshot.BodyStates[index]);

            return bytes;
        }

        public static AuraSimulationSnapshot Deserialize(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var span = new ReadOnlySpan<byte>(bytes);
            if (span.Length < HeaderSize)
                throw new AuraException(AuraResult.InvalidDefinition, "State buffer is too small.");

            if (BinaryPrimitives.ReadUInt32LittleEndian(span) != Magic)
                throw new AuraException(AuraResult.InvalidDefinition, "State buffer magic mismatch.");

            var version = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(4));
            if (version != Version)
                throw new AuraException(AuraResult.InvalidDefinition, $"Unsupported state version {version}.");

            var tick = new SimulationTick(BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8)));
            var count = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(12));
            if (count < 0 || (long)HeaderSize + (long)count * BodyStateSize > span.Length)
                throw new AuraException(AuraResult.InvalidDefinition, "State buffer length is inconsistent.");

            var states = new AuraBodyState[count];
            for (var index = 0; index < count; index++)
                states[index] = ReadBodyState(span.Slice(HeaderSize + index * BodyStateSize));

            return new AuraSimulationSnapshot(tick, states);
        }

        private static void WriteBodyState(Span<byte> destination, in AuraBodyState state)
        {
            var offset = 0;
            WriteInt(destination, ref offset, state.Body.Index);
            WriteInt(destination, ref offset, state.Body.Generation);
            WriteInt(destination, ref offset, state.Entity.Index);
            WriteInt(destination, ref offset, state.Entity.Generation);
            WriteFloat(destination, ref offset, state.Pose.Position.X);
            WriteFloat(destination, ref offset, state.Pose.Position.Y);
            WriteFloat(destination, ref offset, state.Pose.Position.Z);
            WriteFloat(destination, ref offset, state.Pose.Rotation.X);
            WriteFloat(destination, ref offset, state.Pose.Rotation.Y);
            WriteFloat(destination, ref offset, state.Pose.Rotation.Z);
            WriteFloat(destination, ref offset, state.Pose.Rotation.W);
            WriteFloat(destination, ref offset, state.LinearVelocity.X);
            WriteFloat(destination, ref offset, state.LinearVelocity.Y);
            WriteFloat(destination, ref offset, state.LinearVelocity.Z);
            WriteFloat(destination, ref offset, state.AngularVelocity.X);
            WriteFloat(destination, ref offset, state.AngularVelocity.Y);
            WriteFloat(destination, ref offset, state.AngularVelocity.Z);
            WriteInt(destination, ref offset, state.IsAwake ? 1 : 0);
            WriteInt(destination, ref offset, unchecked((int)state.Flags));
        }

        private static AuraBodyState ReadBodyState(ReadOnlySpan<byte> source)
        {
            var offset = 0;
            var body = new PhysicsBodyId(ReadInt(source, ref offset), ReadInt(source, ref offset));
            var entity = new SimulationEntityId(ReadInt(source, ref offset), ReadInt(source, ref offset));
            var position = new AuraVector3(ReadFloat(source, ref offset), ReadFloat(source, ref offset), ReadFloat(source, ref offset));
            var rotation = new AuraQuaternion(
                ReadFloat(source, ref offset),
                ReadFloat(source, ref offset),
                ReadFloat(source, ref offset),
                ReadFloat(source, ref offset));
            var linear = new AuraVector3(ReadFloat(source, ref offset), ReadFloat(source, ref offset), ReadFloat(source, ref offset));
            var angular = new AuraVector3(ReadFloat(source, ref offset), ReadFloat(source, ref offset), ReadFloat(source, ref offset));
            var awake = ReadInt(source, ref offset) != 0;
            var flags = unchecked((uint)ReadInt(source, ref offset));

            return new AuraBodyState(body, entity, new AuraPose(position, rotation), linear, angular, awake, flags);
        }

        private static void WriteInt(Span<byte> destination, ref int offset, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), value);
            offset += 4;
        }

        private static void WriteFloat(Span<byte> destination, ref int offset, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), BitConverter.SingleToInt32Bits(value));
            offset += 4;
        }

        private static int ReadInt(ReadOnlySpan<byte> source, ref int offset)
        {
            var value = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset));
            offset += 4;
            return value;
        }

        private static float ReadFloat(ReadOnlySpan<byte> source, ref int offset)
        {
            var value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset)));
            offset += 4;
            return value;
        }
    }
}
