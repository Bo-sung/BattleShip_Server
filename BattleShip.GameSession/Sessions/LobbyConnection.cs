using BattleShip.Common.Network;
using BattleShip.Common.Packets;
using BattleShip.Common.Packets.ServerInternal;
using System.Net.Sockets;

namespace BattleShip.GameSession.Sessions
{
    public class LobbyConnection
    {
        private readonly string _sessionId;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private SS_GameResultAck? _pendingAck;
        private readonly SemaphoreSlim _ackSignal = new SemaphoreSlim(0, 1);

        public LobbyConnection(string sessionId)
        {
            _sessionId = sessionId;
        }

        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();

            Console.WriteLine($"[Session:{_sessionId}] Lobby 역접속 성공");

            // 수신 루프 (SS_Pong, SS_GameResultAck 처리)
            _ = ReceiveLoopAsync();

            // 핑 루프 시작
            _ = PingLoopAsync();
        }

        private async Task PingLoopAsync()
        {
            while (true)
            {
                await Task.Delay(5000);
                try
                {
                    await SendAsync(new SS_Ping
                    {
                        SessionId = _sessionId,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                }
                catch
                {
                    Console.WriteLine($"[Session:{_sessionId}] Ping 전송 실패");
                }
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var recvBuffer = new RecvBuffer();

            try
            {
                while (true)
                {
                    int received = await _stream!.ReadAsync(recvBuffer.WriteMemory);
                    if (received == 0) break;

                    recvBuffer.OnWritten(received);

                    while (recvBuffer.TryReadPacket(out var rawPacket))
                    {
                        var packet = PacketSerializer.Deserialize(rawPacket);

                        if (packet is SS_GameResultAck ack)
                        {
                            _pendingAck = ack;
                            _ackSignal.Release();
                        }
                    }

                    recvBuffer.Compact();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session:{_sessionId}] Lobby 수신 오류: {ex.Message}");
            }
        }

        // 게임 결과 전송 → ACK 대기 (최대 5초)
        public async Task<bool> SendGameResultAsync(SS_GameResultReq req)
        {
            await SendAsync(req);

            bool received = await _ackSignal.WaitAsync(TimeSpan.FromSeconds(5));
            return received && (_pendingAck?.Success ?? false);
        }

        private async Task SendAsync(IPacket packet)
        {
            byte[] data = PacketSerializer.Serialize(packet);
            await _stream!.WriteAsync(data);
        }
    }
}
