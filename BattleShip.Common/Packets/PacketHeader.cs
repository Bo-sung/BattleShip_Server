using System;

namespace BattleShip.Common.Packets
{// BattleShip.Common.Packets
    public struct PacketHeader
    {
        public const int Size = 6;

        public ushort Length;    // 헤더 포함 전체 길이
        public ushort PacketId;
        public ushort Sequence;

        public static PacketHeader Read(ReadOnlySpan<byte> buffer)
        {
            return new PacketHeader
            {
                Length = BitConverter.ToUInt16(buffer[0..2]),
                PacketId = BitConverter.ToUInt16(buffer[2..4]),
                Sequence = BitConverter.ToUInt16(buffer[4..6]),
            };
        }

        public void Write(Span<byte> buffer)
        {
            BitConverter.TryWriteBytes(buffer[0..2], Length);
            BitConverter.TryWriteBytes(buffer[2..4], PacketId);
            BitConverter.TryWriteBytes(buffer[4..6], Sequence);
        }
    }
}
