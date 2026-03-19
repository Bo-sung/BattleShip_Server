using BattleShip.Common.Packets;
using System;

namespace BattleShip.Common.Network
{
    public class RecvBuffer
    {
        private readonly byte[] _buffer;
        private int _readPos;
        private int _writePos;

        public RecvBuffer(int capacity = 65535)
        {
            _buffer = new byte[capacity];
        }

        // 아직 처리 안 한 데이터 크기
        public int DataSize => _writePos - _readPos;

        // 남은 쓰기 공간
        public int FreeSize => _buffer.Length - _writePos;

        // 소켓 수신 데이터를 여기에 씀
        public Memory<byte> WriteSpan => _buffer.AsMemory(_writePos);

        public void OnWritten(int numBytes)
        {
            _writePos += numBytes;
        }

        // 완성된 패킷 하나씩 꺼내기
        public bool TryReadPacket(out ReadOnlyMemory<byte> packet)
        {
            packet = default;

            // 헤더조차 안 왔으면 대기
            if (DataSize < PacketHeader.Size)
                return false;

            // 헤더에서 전체 길이 확인
            ushort totalLen = BitConverter.ToUInt16(_buffer, _readPos);

            // 패킷이 아직 완성 안 됐으면 대기
            if (DataSize < totalLen)
                return false;

            packet = new ReadOnlyMemory<byte>(_buffer, _readPos, totalLen);
            _readPos += totalLen;

            return true;
        }

        // 읽은 데이터 앞으로 당기기 (공간 확보)
        public void Compact()
        {
            if (_readPos == 0)
                return;

            if (DataSize > 0)
                Buffer.BlockCopy(_buffer, _readPos, _buffer, 0, DataSize);

            _writePos = DataSize;
            _readPos = 0;
        }
    }
}
