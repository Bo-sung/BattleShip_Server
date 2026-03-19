using BattleShip.Common.Network;
using BattleShip.Common.Packets;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace BattleShip.Common.Session
{// Session/PacketSession.cs
    public abstract class PacketSession
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly RecvBuffer _recvBuffer = new RecvBuffer();

        public bool IsConnected => _client?.Connected ?? false;

        public async Task StartAsync(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            await ReceiveLoopAsync();
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (true)
                {
                    // 쓸 공간 부족하면 먼저 당기기
                    if (_recvBuffer.FreeSize == 0)
                        _recvBuffer.Compact();

                    int received = await _stream.ReadAsync(_recvBuffer.WriteSpan);

                    // 0바이트 = 연결 종료
                    if (received == 0)
                        break;

                    _recvBuffer.OnWritten(received);

                    // 완성된 패킷 전부 처리
                    while (_recvBuffer.TryReadPacket(out var rawPacket))
                    {
                        IPacket packet = PacketSerializer.Deserialize(rawPacket);
                        OnPacketReceived(packet);
                    }

                    _recvBuffer.Compact();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session] 수신 오류: {ex.Message}");
            }
            finally
            {
                _client?.Close();
                OnDisconnected();
            }
        }

        public async Task SendAsync(IPacket packet)
        {
            try
            {
                byte[] data = PacketSerializer.Serialize(packet);
                await _stream.WriteAsync(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session] 송신 오류: {ex.Message}");
            }
        }

        protected abstract void OnPacketReceived(IPacket packet);
        protected abstract void OnDisconnected();
    }
}
