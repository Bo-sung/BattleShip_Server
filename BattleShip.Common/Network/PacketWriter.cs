using System;
using System.Collections.Generic;
using System.Text;

namespace BattleShip.Common.Network
{// BattleShip.Common.Network
    public class PacketWriter
    {
        private readonly List<byte> _buffer = new List<byte>();

        public void Write(bool value)
        {
            _buffer.Add(value ? (byte)1 : (byte)0);
        }

        public void Write(byte value)
        {
            _buffer.Add(value);
        }

        public void Write(short value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        public void Write(ushort value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        public void Write(int value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        public void Write(long value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        public void Write(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Write((ushort)bytes.Length);
            _buffer.AddRange(bytes);
        }

        public byte[] ToArray()
        {
            return _buffer.ToArray();
        }
    }
}
