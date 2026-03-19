using BattleShip.Common.Packets;
using BattleShip.Common.Packets.Game;
using BattleShip.Common.Packets.ServerInternal;
using BattleShip.GameSession.Game;
using BattleShip.GameSession.Sessions;
using System.Net;
using System.Net.Sockets;

namespace BattleShip.GameSession
{
    public class GameSessionServer
    {
        private readonly string _sessionId;
        private readonly int _port;
        private readonly string _lobbyHost;
        private readonly int _lobbyPort;

        private PlayerSession? _player1;
        private PlayerSession? _player2;
        private LobbyConnection? _lobbyConn;
        private GameState _gameState = new GameState();
        private int _turnCount = 0;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public GameSessionServer(string sessionId, int port, string lobbyHost, int lobbyPort)
        {
            _sessionId = sessionId;
            _port = port;
            _lobbyHost = lobbyHost;
            _lobbyPort = lobbyPort;
        }

        public async Task RunAsync()
        {
            // 1. Lobby에 역접속
            _lobbyConn = new LobbyConnection(_sessionId);
            await _lobbyConn.ConnectAsync(_lobbyHost, _lobbyPort);

            // 2. 클라이언트 2명 수락 (타임아웃 60초)
            if (!await AcceptPlayersAsync())
            {
                Console.WriteLine($"[Session:{_sessionId}] 접속 타임아웃 — 종료");
                Environment.Exit(1);
            }

            Console.WriteLine($"[Session:{_sessionId}] 플레이어 2명 접속 완료");

            // 3. 수신 루프 시작
            _ = _player1!.StartAsync();
            _ = _player2!.StartAsync();

            // 4. 게임 종료까지 대기
            await Task.Delay(Timeout.Infinite, _cts.Token).ContinueWith(_ => { });

            Console.WriteLine($"[Session:{_sessionId}] 종료");
        }

        private async Task<bool> AcceptPlayersAsync()
        {
            var listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            try
            {
                var client1 = await listener.AcceptTcpClientAsync(cts.Token);
                _player1 = new PlayerSession(0, this);
                await _player1.InitAsync(client1);
                Console.WriteLine($"[Session:{_sessionId}] Player 0 접속");

                var client2 = await listener.AcceptTcpClientAsync(cts.Token);
                _player2 = new PlayerSession(1, this);
                await _player2.InitAsync(client2);
                Console.WriteLine($"[Session:{_sessionId}] Player 1 접속");

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                listener.Stop();
            }
        }

        // ── 게임 로직 진입점 (PlayerSession에서 호출) ─────────────

        public async Task OnEnterSession(int playerIndex, C_EnterSessionReq req)
        {
            var session = GetPlayer(playerIndex);
            await session.SendAsync(new S_EnterSessionRes
            {
                Success = true,
                PlayerIndex = (byte)playerIndex
            });
        }

        public async Task OnPlaceShips(int playerIndex, C_PlaceShipsReq req)
        {
            var board = _gameState.Boards[playerIndex];
            bool ok = board.PlaceShips(req.Ships);

            var session = GetPlayer(playerIndex);

            if (!ok)
            {
                await session.SendAsync(new S_PlaceShipsRes
                {
                    Success = false,
                    Message = "배치가 올바르지 않습니다."
                });
                return;
            }

            await session.SendAsync(new S_PlaceShipsRes { Success = true, Message = "" });

            bool bothDone = _gameState.SetPlacementDone(playerIndex);

            if (bothDone)
            {
                Console.WriteLine($"[Session:{_sessionId}] 배치 완료 — 선공: Player {_gameState.CurrentTurn}");

                // 양쪽에 배치 완료 알림
                await BroadcastAsync(new S_PlacementDone());

                // 선공 통보
                await _player1!.SendAsync(new S_TurnNotify { IsMyTurn = _gameState.CurrentTurn == 0 });
                await _player2!.SendAsync(new S_TurnNotify { IsMyTurn = _gameState.CurrentTurn == 1 });
            }
        }

        public async Task OnAttack(int playerIndex, C_AttackReq req)
        {
            // 내 턴이 아님 → 무시
            if (!_gameState.IsMyTurn(playerIndex))
                return;

            // 게임 진행 중이 아님 → 무시
            if (_gameState.Phase != GamePhase.InProgress)
                return;

            _turnCount++;

            var (result, sunkShipName, isGameOver) = _gameState.ProcessAttack(playerIndex, req.X, req.Y);

            var attacker = GetPlayer(playerIndex);
            var defender = GetPlayer(1 - playerIndex);

            // 공격자에게 결과
            await attacker.SendAsync(new S_AttackRes
            {
                X = req.X,
                Y = req.Y,
                Result = result,
                SunkShipName = sunkShipName
            });

            // 수비자에게 피격 알림
            await defender.SendAsync(new S_OpponentAttack
            {
                X = req.X,
                Y = req.Y,
                Result = result,
                SunkShipName = sunkShipName
            });

            if (isGameOver)
            {
                await FinishGameAsync(winnerId: playerIndex.ToString(), loserId: (1 - playerIndex).ToString(), reason: 0);
                return;
            }

            // 턴 전환 통보
            await _player1!.SendAsync(new S_TurnNotify { IsMyTurn = _gameState.CurrentTurn == 0 });
            await _player2!.SendAsync(new S_TurnNotify { IsMyTurn = _gameState.CurrentTurn == 1 });
        }

        public async Task OnPlayerDisconnected(int playerIndex)
        {
            if (_gameState.Phase == GamePhase.GameOver)
                return;

            Console.WriteLine($"[Session:{_sessionId}] Player {playerIndex} 연결 끊김 → 패배 처리");

            string winnerId = (1 - playerIndex).ToString();
            string loserId = playerIndex.ToString();

            // 남은 플레이어에게 게임 종료 알림
            var winner = GetPlayer(1 - playerIndex);
            await winner.SendAsync(new S_GameOver { WinnerId = winnerId, Reason = 1 });

            await SendGameResultToLobbyAsync(winnerId, loserId);
        }

        private async Task FinishGameAsync(string winnerId, string loserId, byte reason)
        {
            // 양쪽에 게임 종료 알림
            await BroadcastAsync(new S_GameOver { WinnerId = winnerId, Reason = reason });
            await SendGameResultToLobbyAsync(winnerId, loserId);
        }

        private async Task SendGameResultToLobbyAsync(string winnerId, string loserId)
        {
            _gameState = new GameState();  // Phase = GameOver 방지용 재진입 차단

            for (int retry = 0; retry < 3; retry++)
            {
                bool acked = await _lobbyConn!.SendGameResultAsync(new SS_GameResultReq
                {
                    SessionId = _sessionId,
                    WinnerId = winnerId,
                    LoserId = loserId,
                    TotalTurns = _turnCount
                });

                if (acked)
                {
                    Console.WriteLine($"[Session:{_sessionId}] 결과 전송 완료");
                    break;
                }

                Console.WriteLine($"[Session:{_sessionId}] ACK 미수신, 재시도 ({retry + 1}/3)");
                await Task.Delay(5000);
            }

            _cts.Cancel();
        }

        // ── 헬퍼 ─────────────────────────────────────────────────

        private PlayerSession GetPlayer(int index) => index == 0 ? _player1! : _player2!;

        private async Task BroadcastAsync(IPacket packet)
        {
            await _player1!.SendAsync(packet);
            await _player2!.SendAsync(packet);
        }
    }
}
