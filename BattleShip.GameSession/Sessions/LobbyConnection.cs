using BattleShip.Common;
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
        private readonly TaskCompletionSource<GameRuleConfig> _ruleConfigTcs = new TaskCompletionSource<GameRuleConfig>();

        public LobbyConnection(string sessionId)
        {
            _sessionId = sessionId;
        }

        // standalone 모드: Lobby 연결 전에 미리 config 설정
        public void SetRuleConfig(GameRuleConfig config)
        {
            _ruleConfigTcs.TrySetResult(config);
        }

        public Task<GameRuleConfig> WaitForRuleConfigAsync()
        {
            return _ruleConfigTcs.Task;
        }

        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();

            Console.WriteLine($"[Session:{_sessionId}] Lobby 역접속 성공");

            _ = ReceiveLoopAsync();
            _ = PingLoopAsync();
        }

        private async Task PingLoopAsync()
        {
            while (true)
            {
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
                await Task.Delay(5000);
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
                        else if (packet is SS_SessionRuleConfig ruleConfig)
                        {
                            Console.WriteLine($"[Session:{_sessionId}] 룰 수신 완료 (boardSize={ruleConfig.Config.BoardSize}, ships={ruleConfig.Config.Ships.Count})");
                            _ruleConfigTcs.TrySetResult(ruleConfig.Config);
                        }
                    }

                    recvBuffer.Compact();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session:{_sessionId}] Lobby 수신 오류: {ex.Message}");
                _ruleConfigTcs.TrySetException(ex);
            }
        }

        public async Task NotifyReadyAsync()
        {
            try
            {
                await SendAsync(new SS_SessionReady { SessionId = _sessionId });
                Console.WriteLine($"[Session:{_sessionId}] Ready 알림 전송");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session:{_sessionId}] Ready 알림 실패: {ex.Message}");
            }
        }

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
