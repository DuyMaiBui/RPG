using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AuraEngine.Core;

namespace AuraEngine.Networking
{
    /* Quantised entity delta codec. Instead of shipping a full body-state blob
       each tick, the server sends per-entity position/velocity deltas against
       the client's acknowledged baseline. Entities are keyed by body index;
       unchanged entities are omitted by the caller. */
    public static class AuraSnapshotDelta
    {
        public const uint Magic = 0x41554444u;
        private const int EntrySize = 4 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2;

        public static byte[] Encode(uint serverTick, IReadOnlyList<AuraDeltaEntry> entries)
        {
            entries ??= Array.Empty<AuraDeltaEntry>();
            var bytes = new byte[4 + 4 + 2 + 2 + entries.Count * EntrySize];
            var span = new Span<byte>(bytes);
            BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4), serverTick);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(8), (ushort)entries.Count);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(10), 0);

            var offset = 12;
            for (var index = 0; index < entries.Count; index++)
                WriteEntry(span.Slice(offset + index * EntrySize), entries[index]);

            return bytes;
        }

        public static bool TryDecode(byte[] payload, out uint serverTick, out List<AuraDeltaEntry> entries)
        {
            serverTick = 0;
            entries = null;
            if (payload == null || payload.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(payload) != Magic)
                return false;

            serverTick = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4));
            var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(8));
            if (12 + count * EntrySize > payload.Length)
                return false;

            entries = new List<AuraDeltaEntry>(count);
            for (var index = 0; index < count; index++)
                entries.Add(ReadEntry(payload.AsSpan(12 + index * EntrySize)));
            return true;
        }

        public static AuraDeltaEntry FromBodyState(in AuraBodyState state, in AuraBodyState baseline, float positionQuantum, float velocityQuantum)
        {
            return new AuraDeltaEntry(
                (uint)state.Body.Index,
                Quantise(state.Pose.Position.X - baseline.Pose.Position.X, positionQuantum),
                Quantise(state.Pose.Position.Y - baseline.Pose.Position.Y, positionQuantum),
                Quantise(state.Pose.Position.Z - baseline.Pose.Position.Z, positionQuantum),
                Quantise(state.LinearVelocity.X - baseline.LinearVelocity.X, velocityQuantum),
                Quantise(state.LinearVelocity.Y - baseline.LinearVelocity.Y, velocityQuantum),
                Quantise(state.LinearVelocity.Z - baseline.LinearVelocity.Z, velocityQuantum),
                state.IsAwake ? (ushort)1 : (ushort)0,
                (ushort)(state.Body.Generation & 0xFFFF));
        }

        public static AuraBodyState Apply(in AuraBodyState baseline, in AuraDeltaEntry entry, float positionQuantum, float velocityQuantum, SimulationEntityId entity)
        {
            var position = baseline.Pose.Position + new AuraVector3(
                entry.PositionX * positionQuantum,
                entry.PositionY * positionQuantum,
                entry.PositionZ * positionQuantum);
            var linear = baseline.LinearVelocity + new AuraVector3(
                entry.VelocityX * velocityQuantum,
                entry.VelocityY * velocityQuantum,
                entry.VelocityZ * velocityQuantum);

            return new AuraBodyState(
                baseline.Body,
                entity,
                new AuraPose(position, baseline.Pose.Rotation),
                linear,
                baseline.AngularVelocity,
                entry.Awake != 0,
                baseline.Flags);
        }

        private static short Quantise(float value, float quantum)
        {
            var scaled = value / (quantum <= 0f ? 1f : quantum);
            if (scaled > short.MaxValue)
                return short.MaxValue;
            if (scaled < short.MinValue)
                return short.MinValue;
            return (short)Math.Round(scaled);
        }

        private static void WriteEntry(Span<byte> destination, in AuraDeltaEntry entry)
        {
            var offset = 0;
            BinaryPrimitives.WriteUInt32LittleEndian(destination, entry.BodyIndex);
            offset += 4;
            WriteShort(destination, ref offset, entry.PositionX);
            WriteShort(destination, ref offset, entry.PositionY);
            WriteShort(destination, ref offset, entry.PositionZ);
            WriteShort(destination, ref offset, entry.VelocityX);
            WriteShort(destination, ref offset, entry.VelocityY);
            WriteShort(destination, ref offset, entry.VelocityZ);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset), entry.Awake);
            offset += 2;
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset), entry.Generation);
        }

        private static AuraDeltaEntry ReadEntry(ReadOnlySpan<byte> source)
        {
            var offset = 0;
            var bodyIndex = BinaryPrimitives.ReadUInt32LittleEndian(source);
            offset += 4;
            var px = ReadShort(source, ref offset);
            var py = ReadShort(source, ref offset);
            var pz = ReadShort(source, ref offset);
            var vx = ReadShort(source, ref offset);
            var vy = ReadShort(source, ref offset);
            var vz = ReadShort(source, ref offset);
            var awake = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset));
            offset += 2;
            var generation = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset));
            return new AuraDeltaEntry(bodyIndex, px, py, pz, vx, vy, vz, awake, generation);
        }

        private static void WriteShort(Span<byte> destination, ref int offset, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(offset), value);
            offset += 2;
        }

        private static short ReadShort(ReadOnlySpan<byte> source, ref int offset)
        {
            var value = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(offset));
            offset += 2;
            return value;
        }
    }
}
