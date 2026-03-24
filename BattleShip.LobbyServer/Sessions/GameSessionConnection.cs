using BattleShip.Common;
using BattleShip.Common.Network;
using BattleShip.Common.Packets;
using BattleShip.Common.Packets.ServerInternal;
using BattleShip.Common.Session;
using BattleShip.LobbyServer.Repositories;
using BattleShip.LobbyServer.Services;

namespace BattleShip.LobbyServer.Sessions
{
    public class GameSessionConnection : PacketSession
    {
        private readonly SessionRegistry _registry;
        private readonly GameRecordRepository _gameRecord;
        private readonly Dictionary<string, GameRuleConfig> _configs;
        private readonly PacketDispatcher _dispatcher = new PacketDispatcher();

        private string _sessionId = "";

        public GameSessionConnection(
            SessionRegistry registry,
            GameRecordRepository gameRecord,
            Dictionary<string, GameRuleConfig> configs)
        {
            _registry = registry;
            _gameRecord = gameRecord;
            _configs = configs;

            _dispatcher.Register<SS_Ping>(PacketId.SS_Ping, OnPing);
            _dispatcher.Register<SS_GameResultReq>(PacketId.SS_GameResultReq, OnGameResult);
            _dispatcher.Register<SS_SessionReady>(PacketId.SS_SessionReady, OnSessionReady);
        }

        protected override void OnPacketReceived(IPacket packet)
        {
            _dispatcher.Dispatch(packet);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"[Lobby] GameSession 연결 끊김: {_sessionId}");
        }

        private async void OnPing(SS_Ping ping)
        {
            try
            {
                bool isFirstPing = string.IsNullOrEmpty(_sessionId);
                _sessionId = ping.SessionId;
                _registry.UpdatePing(ping.SessionId);

                await SendAsync(new SS_Pong { Timestamp = ping.Timestamp });

                if (isFirstPing)
                {
                    string configName = _registry.GetConfigName(ping.SessionId);
                    var config = _configs.TryGetValue(configName, out var found) ? found : GameRuleConfig.Default;

                    await SendAsync(new SS_SessionRuleConfig { Config = config });
                    Console.WriteLine($"[Lobby] 룰 전송: {ping.SessionId} → {configName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] Ping 오류: {ex.Message}");
            }
        }

        private async void OnSessionReady(SS_SessionReady ready)
        {
            try
            {
                var info = _registry.GetSession(ready.SessionId);
                if (info?.PendingGameStart == null)
                {
                    Console.WriteLine($"[Lobby] SessionReady: 세션 없음 또는 이미 처리됨 — {ready.SessionId}");
                    return;
                }

                await info.Player1!.SendAsync(info.PendingGameStart);
                await info.Player2!.SendAsync(info.PendingGameStart);

                info.Player1 = null;
                info.Player2 = null;
                info.PendingGameStart = null;

                Console.WriteLine($"[Lobby] S_GameStart 전송 완료: {ready.SessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] SessionReady 오류: {ex.Message}");
            }
        }

        private async void OnGameResult(SS_GameResultReq req)
        {
            try
            {
                await _gameRecord.SaveAsync(req.SessionId, req.WinnerId, req.LoserId, req.TotalTurns);
                _registry.Remove(req.SessionId);

                Console.WriteLine($"[Lobby] 게임 결과 저장: {req.SessionId} — 승자 {req.WinnerId}");

                await SendAsync(new SS_GameResultAck { Success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] GameResult 오류: {ex.Message}");
                await SendAsync(new SS_GameResultAck { Success = false });
            }
        }
    }
}
