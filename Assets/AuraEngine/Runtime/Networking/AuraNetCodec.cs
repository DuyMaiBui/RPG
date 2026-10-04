using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AuraEngine.Networking
{
    public static class AuraNetCodec
    {
        public static byte[] EncodeInput(uint clientId, uint sequence, int moveX, int moveY)
        {
            var payload = new byte[1 + 4 + 4 + 2 + 2];
            payload[0] = (byte)AuraNetMessageType.Input;
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(1), clientId);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(5), sequence);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(9), (short)moveX);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(11), (short)moveY);
            return payload;
        }

        public static bool TryDecodeInput(byte[] payload, out AuraInputCommand command)
        {
            command = default;
            if (payload == null || payload.Length < 13 || payload[0] != (byte)AuraNetMessageType.Input)
                return false;

            var clientId = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(1));
            var sequence = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(5));
            var moveX = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(9));
            var moveY = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(11));
            command = new AuraInputCommand(clientId, sequence, moveX, moveY);
            return true;
        }

        public static byte[] EncodeSnapshot(uint serverTick, byte[] worldState)
        {
            worldState ??= Array.Empty<byte>();
            var payload = new byte[1 + 4 + 4 + worldState.Length];
            payload[0] = (byte)AuraNetMessageType.Snapshot;
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(1), serverTick);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(5), worldState.Length);
            worldState.CopyTo(payload, 9);
            return payload;
        }

        public static bool TryDecodeSnapshot(byte[] payload, out uint serverTick, out byte[] worldState)
        {
            serverTick = 0;
            worldState = null;
            if (payload == null || payload.Length < 9 || payload[0] != (byte)AuraNetMessageType.Snapshot)
                return false;

            serverTick = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(1));
            var length = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(5));
            if (length < 0 || 9 + length > payload.Length)
                return false;

            worldState = new byte[length];
            Array.Copy(payload, 9, worldState, 0, length);
            return true;
        }

        public static byte[] EncodeAck(uint clientId, uint sequence)
        {
            var payload = new byte[1 + 4 + 4];
            payload[0] = (byte)AuraNetMessageType.Ack;
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(1), clientId);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(5), sequence);
            return payload;
        }

        public static bool TryDecodeAck(byte[] payload, out uint clientId, out uint sequence)
        {
            clientId = 0;
            sequence = 0;
            if (payload == null || payload.Length < 9 || payload[0] != (byte)AuraNetMessageType.Ack)
                return false;

            clientId = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(1));
            sequence = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(5));
            return true;
        }

        public static List<AuraInputCommand> DecodeInputBatch(byte[] payload)
        {
            var commands = new List<AuraInputCommand>();
            if (payload == null)
                return commands;

            var offset = 0;
            while (offset < payload.Length)
            {
                var frame = new byte[13];
                var copy = Math.Min(13, payload.Length - offset);
                Array.Copy(payload, offset, frame, 0, copy);
                if (TryDecodeInput(frame, out var command))
                    commands.Add(command);
                offset += 13;
            }

            return commands;
        }
    }
}
