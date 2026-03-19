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
        private readonly PacketDispatcher _dispatcher = new PacketDispatcher();

        private string _sessionId;

        public GameSessionConnection(SessionRegistry registry, GameRecordRepository gameRecord)
        {
            _registry = registry;
            _gameRecord = gameRecord;

            _dispatcher.Register<SS_Ping>(PacketId.SS_Ping, OnPing);
            _dispatcher.Register<SS_GameResultReq>(PacketId.SS_GameResultReq, OnGameResult);
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
                _sessionId = ping.SessionId;
                _registry.UpdatePing(ping.SessionId);

                await SendAsync(new SS_Pong
                {
                    Timestamp = ping.Timestamp
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] Ping 오류: {ex.Message}");
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
