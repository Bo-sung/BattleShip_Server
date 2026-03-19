using System;
using System.Text;

namespace BattleShip.Common.Network
{
    public class PacketReader
    {
        private readonly ReadOnlyMemory<byte> _buffer;
        private int _pos;

        public PacketReader(ReadOnlyMemory<byte> buffer, int startPos = 0)
        {
            _buffer = buffer;
            _pos = startPos;
        }

        public bool ReadBool()
        {
            return _buffer.Span[_pos++] != 0;
        }

        public byte ReadByte()
        {
            return _buffer.Span[_pos++];
        }

        public short ReadShort()
        {
            short value = BitConverter.ToInt16(_buffer.Span[_pos..]);
            _pos += 2;
            return value;
        }

        public ushort ReadUShort()
        {
            ushort value = BitConverter.ToUInt16(_buffer.Span[_pos..]);
            _pos += 2;
            return value;
        }

        public int ReadInt()
        {
            int value = BitConverter.ToInt32(_buffer.Span[_pos..]);
            _pos += 4;
            return value;
        }

        public long ReadLong()
        {
            long value = BitConverter.ToInt64(_buffer.Span[_pos..]);
            _pos += 8;
            return value;
        }

        public string ReadString()
        {
            ushort length = ReadUShort();
            string value = Encoding.UTF8.GetString(_buffer.Span.Slice(_pos, length));
            _pos += length;
            return value;
        }
    }
}
