using System;
using System.Buffers.Binary;
using AuraEngine.Core;

namespace AuraEngine.Serialization
{
    public static class AuraReplayFile
    {
        public const uint Magic = 0x41555252u;
        public const ushort Version = 1;

        public static byte[] Serialize(AuraReplay replay)
        {
            if (replay == null)
                throw new ArgumentNullException(nameof(replay));

            var stateBytes = AuraStateSerializer.Serialize(replay.InitialState);
            var size = 4 + 2 + 2 + 4 + stateBytes.Length + 4 + replay.FrameCount * 12;
            var bytes = new byte[size];
            var span = new Span<byte>(bytes);
            var offset = 0;

            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset), Magic);
            offset += 4;
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset), Version);
            offset += 2;
            offset += 2;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), stateBytes.Length);
            offset += 4;
            stateBytes.CopyTo(span.Slice(offset));
            offset += stateBytes.Length;

            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), replay.FrameCount);
            offset += 4;
            for (var index = 0; index < replay.FrameCount; index++)
            {
                var frame = replay[index];
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset), frame.Tick.Value);
                offset += 4;
                BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(offset), frame.StateHash);
                offset += 8;
            }

            return bytes;
        }

        public static AuraReplay Deserialize(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var span = new ReadOnlySpan<byte>(bytes);
            if (span.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(span) != Magic)
                throw new AuraException(AuraResult.InvalidDefinition, "Replay buffer magic mismatch.");

            var version = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(4));
            if (version != Version)
                throw new AuraException(AuraResult.InvalidDefinition, $"Unsupported replay version {version}.");

            var offset = 8;
            var stateLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;
            if (stateLength < 0 || offset + stateLength + 4 > span.Length)
                throw new AuraException(AuraResult.InvalidDefinition, "Replay buffer length is inconsistent.");

            var stateBytes = new byte[stateLength];
            span.Slice(offset, stateLength).CopyTo(stateBytes);
            offset += stateLength;

            var snapshot = AuraStateSerializer.Deserialize(stateBytes);
            var frameCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;
            if (frameCount < 0 || offset + frameCount * 12 > span.Length)
                throw new AuraException(AuraResult.InvalidDefinition, "Replay frame count is inconsistent.");

            var frames = new AuraReplayFrame[frameCount];
            for (var index = 0; index < frameCount; index++)
            {
                var tick = new SimulationTick(BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset)));
                offset += 4;
                var hash = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset));
                offset += 8;
                frames[index] = new AuraReplayFrame(tick, hash);
            }

            return new AuraReplay(snapshot, frames);
        }
    }
}
