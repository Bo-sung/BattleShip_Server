using BattleShip.Common.Network;
using BattleShip.Common.Packets;
using BattleShip.Common.Packets.Game;
using BattleShip.Common.Session;
using System.Net.Sockets;

namespace BattleShip.GameSession.Sessions
{
    public class PlayerSession : PacketSession
    {
        private readonly int _playerIndex;
        private readonly GameSessionServer _server;
        private readonly PacketDispatcher _dispatcher = new PacketDispatcher();

        public PlayerSession(int playerIndex, GameSessionServer server)
        {
            _playerIndex = playerIndex;
            _server = server;

            _dispatcher.Register<C_EnterSessionReq>(PacketId.C_EnterSessionReq, OnEnterSession);
            _dispatcher.Register<C_PlaceShipsReq>(PacketId.C_PlaceShipsReq, OnPlaceShips);
            _dispatcher.Register<C_AttackReq>(PacketId.C_AttackReq, OnAttack);
            _dispatcher.Register<C_SelectSkillsReq>(PacketId.C_SelectSkillsReq, OnSelectSkills);
            _dispatcher.Register<C_MoveReq>(PacketId.C_MoveReq, OnMove);
            _dispatcher.Register<C_SkillReq>(PacketId.C_SkillReq, OnSkill);
        }

        public async Task InitAsync(TcpClient client)
        {
            // StartAsync 호출 전 TcpClient 세팅만 먼저
            await Task.CompletedTask;
            _client = client;
            _stream = client.GetStream();
        }

        public Task StartAsync() => base.StartAsync(_client);

        protected override void OnPacketReceived(IPacket packet)
        {
            _dispatcher.Dispatch(packet);
        }

        protected override void OnDisconnected()
        {
            _ = _server.OnPlayerDisconnected(_playerIndex);
        }

        private async void OnEnterSession(C_EnterSessionReq req)
        {
            try { await _server.OnEnterSession(_playerIndex, req); }
            catch (Exception ex) { Console.WriteLine($"[Session] EnterSession 오류: {ex.Message}"); }
        }

        private async void OnPlaceShips(C_PlaceShipsReq req)
        {
            try { await _server.OnPlaceShips(_playerIndex, req); }
            catch (Exception ex) { Console.WriteLine($"[Session] PlaceShips 오류: {ex.Message}"); }
        }

        private async void OnAttack(C_AttackReq req)
        {
            try { await _server.OnAttack(_playerIndex, req); }
            catch (Exception ex) { Console.WriteLine($"[Session] Attack 오류: {ex.Message}"); }
        }

        private async void OnSelectSkills(C_SelectSkillsReq req)
        {
            try { await _server.OnSelectSkills(_playerIndex, req); }
            catch (Exception ex) { Console.WriteLine($"[Session] SelectSkills 오류: {ex.Message}"); }
        }

        private async void OnMove(C_MoveReq req)
        {
            try { await _server.OnMove(_playerIndex, req); }
            catch (Exception ex) { Console.WriteLine($"[Session] Move 오류: {ex.Message}"); }
        }

        private async void OnSkill(C_SkillReq req)
        {
            try { await _server.OnSkill(_playerIndex, req); }
            catch (Exception ex) { Console.WriteLine($"[Session] Skill 오류: {ex.Message}"); }
        }
    }
}
