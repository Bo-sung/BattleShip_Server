using BattleShip.Common;
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
        private readonly GameRuleConfig? _standaloneConfig;

        private PlayerSession? _player1;
        private PlayerSession? _player2;
        private LobbyConnection? _lobbyConn;
        private GameRuleConfig _ruleConfig = GameRuleConfig.Default;
        private GameState _gameState = null!;
        private int _turnCount = 0;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public GameSessionServer(string sessionId, int port, string lobbyHost, int lobbyPort, GameRuleConfig? standaloneConfig = null)
        {
            _sessionId = sessionId;
            _port = port;
            _lobbyHost = lobbyHost;
            _lobbyPort = lobbyPort;
            _standaloneConfig = standaloneConfig;
        }

        public async Task RunAsync()
        {
            // 1. Lobby에 역접속
            _lobbyConn = new LobbyConnection(_sessionId);

            // standalone 모드: 파일에서 읽은 config를 미리 주입
            if (_standaloneConfig != null)
                _lobbyConn.SetRuleConfig(_standaloneConfig);

            await _lobbyConn.ConnectAsync(_lobbyHost, _lobbyPort);

            // 2. 룰 수신 대기 (standalone이면 즉시 반환)
            _ruleConfig = await _lobbyConn.WaitForRuleConfigAsync();
            _gameState = new GameState(_ruleConfig);

            Console.WriteLine($"[Session:{_sessionId}] 룰 적용 — 보드 {_ruleConfig.BoardSize}x{_ruleConfig.BoardSize}, 함선 {_ruleConfig.Ships.Count}종");

            // 3. 클라이언트 2명 수락 (타임아웃 60초)
            if (!await AcceptPlayersAsync())
            {
                Console.WriteLine($"[Session:{_sessionId}] 접속 타임아웃 — 종료");
                Environment.Exit(1);
            }

            Console.WriteLine($"[Session:{_sessionId}] 플레이어 2명 접속 완료");

            // 4. 수신 루프 시작
            _ = _player1!.StartAsync();
            _ = _player2!.StartAsync();

            // 5. 게임 종료까지 대기
            await Task.Delay(Timeout.Infinite, _cts.Token).ContinueWith(_ => { });

            Console.WriteLine($"[Session:{_sessionId}] 종료");
        }

        private async Task<bool> AcceptPlayersAsync()
        {
            var listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();
            Console.WriteLine($"[Session:{_sessionId}] 포트 {_port} 오픈");

            // 포트가 열렸음을 Lobby에 알림 → Lobby가 클라이언트에게 S_GameStart 전송
            await _lobbyConn!.NotifyReadyAsync();

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
            await session.SendAsync(new S_GameRuleConfig { Config = _ruleConfig });
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

                await BroadcastAsync(new S_PlacementDone());

                await _player1!.SendAsync(new S_TurnNotify { IsMyTurn = _gameState.CurrentTurn == 0 });
                await _player2!.SendAsync(new S_TurnNotify { IsMyTurn = _gameState.CurrentTurn == 1 });
            }
        }

        public async Task OnSelectSkills(int playerIndex, C_SelectSkillsReq req)
        {
            if (_gameState.Phase != GamePhase.SkillSelection)
                return;

            _gameState.SetSkillsSelected(playerIndex, req.Skill1, req.Skill2);

            var session = GetPlayer(playerIndex);
            await session.SendAsync(new S_SelectSkillsRes { Success = true });

            if (_gameState.AreAllSkillsSelected())
            {
                Console.WriteLine($"[Session:{_sessionId}] 스킬 선택 완료 — 선공: Player {_gameState.CurrentTurn}");
                _gameState.StartGameFromSkillSelection();

                await BroadcastAsync(new S_BothSkillsSelected());

                await _player1!.SendAsync(new S_TurnNotify
                {
                    IsMyTurn = _gameState.CurrentTurn == 0,
                    Mana = _gameState.GetMana(0)
                });
                await _player2!.SendAsync(new S_TurnNotify
                {
                    IsMyTurn = _gameState.CurrentTurn == 1,
                    Mana = _gameState.GetMana(1)
                });
            }
        }

        public async Task OnMove(int playerIndex, C_MoveReq req)
        {
            if (!_gameState.IsMyTurn(playerIndex))
                return;

            if (_gameState.Phase != GamePhase.InProgress)
                return;

            var (success, message) = _gameState.ProcessMove(playerIndex, req.ShipType, req.DirX, req.DirY);

            var session = GetPlayer(playerIndex);
            await session.SendAsync(new S_MoveRes
            {
                Success = success,
                Message = message
            });

            if (success)
            {
                _turnCount++;
                // Move consumes a turn - turn switch handled by ProcessMove (which calls ProcessAttack path)

                await _player1!.SendAsync(new S_TurnNotify
                {
                    IsMyTurn = _gameState.CurrentTurn == 0,
                    Mana = _gameState.GetMana(0)
                });
                await _player2!.SendAsync(new S_TurnNotify
                {
                    IsMyTurn = _gameState.CurrentTurn == 1,
                    Mana = _gameState.GetMana(1)
                });
            }
        }

        public async Task OnSkill(int playerIndex, C_SkillReq req)
        {
            if (!_gameState.IsMyTurn(playerIndex))
                return;

            if (_gameState.Phase != GamePhase.InProgress)
                return;

            _turnCount++;

            var (skillType, results, success) = _gameState.ProcessSkill(playerIndex, req.SkillType, req.TargetX, req.TargetY, req.ShipType);

            var attacker = GetPlayer(playerIndex);
            var defender = GetPlayer(1 - playerIndex);

            // Send appropriate response based on skill type
            switch (skillType)
            {
                case 1:  // Range Attack
                    await attacker.SendAsync(new S_SkillAttackRes
                    {
                        SkillType = 1,
                        Cells = results.Select(r => new CellResult
                        {
                            X = r.x,
                            Y = r.y,
                            Result = r.result,
                            SunkShipName = r.sunkShipName
                        }).ToList(),
                        Mana = _gameState.GetMana(playerIndex)
                    });

                    if (success)
                    {
                        await defender.SendAsync(new S_OpponentSkillAttack
                        {
                            SkillType = 1,
                            Cells = results.Select(r => new CellResult
                            {
                                X = r.x,
                                Y = r.y,
                                Result = r.result,
                                SunkShipName = r.sunkShipName
                            }).ToList()
                        });
                    }
                    break;

                case 2:  // Shield
                    await attacker.SendAsync(new S_SkillShieldRes
                    {
                        Success = success,
                        ShipType = req.ShipType,
                        Duration = 2,
                        Mana = _gameState.GetMana(playerIndex)
                    });
                    break;

                case 3:  // Repair
                    await attacker.SendAsync(new S_SkillRepairRes
                    {
                        Success = success,
                        ShipType = req.ShipType,
                        Mana = _gameState.GetMana(playerIndex)
                    });

                    if (success)
                    {
                        await defender.SendAsync(new S_OpponentRepaired
                        {
                            ShipType = req.ShipType
                        });
                    }
                    break;

                case 4:  // Movement skill
                    await attacker.SendAsync(new S_SkillMoveRes
                    {
                        Success = success,
                        Message = "",
                        Mana = _gameState.GetMana(playerIndex)
                    });
                    break;

                case 5:  // Freeze
                    await attacker.SendAsync(new S_SkillAttackRes
                    {
                        SkillType = 5,
                        Cells = new List<CellResult>(),
                        Mana = _gameState.GetMana(playerIndex)
                    });
                    break;

                case 6:  // Recovery
                    await attacker.SendAsync(new S_SkillAttackRes
                    {
                        SkillType = 6,
                        Cells = new List<CellResult>(),
                        Mana = _gameState.GetMana(playerIndex)
                    });
                    break;
            }

            if (success)
            {
                bool isGameOver = _gameState.Boards[1 - playerIndex].IsAllSunk();

                if (isGameOver)
                {
                    await FinishGameAsync(winnerId: playerIndex.ToString(), loserId: (1 - playerIndex).ToString(), reason: 0);
                    return;
                }

                await _player1!.SendAsync(new S_TurnNotify
                {
                    IsMyTurn = _gameState.CurrentTurn == 0,
                    Mana = _gameState.GetMana(0)
                });
                await _player2!.SendAsync(new S_TurnNotify
                {
                    IsMyTurn = _gameState.CurrentTurn == 1,
                    Mana = _gameState.GetMana(1)
                });
            }
        }

        public async Task OnAttack(int playerIndex, C_AttackReq req)
        {
            if (!_gameState.IsMyTurn(playerIndex))
                return;

            if (_gameState.Phase != GamePhase.InProgress)
                return;

            if (_gameState.Boards[1 - playerIndex].IsAlreadyAttacked(req.X, req.Y))
            {
                await GetPlayer(playerIndex).SendAsync(new S_AttackRes
                {
                    X = req.X,
                    Y = req.Y,
                    Result = 3,
                    SunkShipName = null
                });
                return;
            }

            _turnCount++;

            var (result, sunkShipName, isGameOver) = _gameState.ProcessAttack(playerIndex, req.X, req.Y);

            var attacker = GetPlayer(playerIndex);
            var defender = GetPlayer(1 - playerIndex);

            await attacker.SendAsync(new S_AttackRes
            {
                X = req.X,
                Y = req.Y,
                Result = result,
                SunkShipName = sunkShipName
            });

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

            await _player1!.SendAsync(new S_TurnNotify
            {
                IsMyTurn = _gameState.CurrentTurn == 0,
                Mana = _gameState.GetMana(0)
            });
            await _player2!.SendAsync(new S_TurnNotify
            {
                IsMyTurn = _gameState.CurrentTurn == 1,
                Mana = _gameState.GetMana(1)
            });
        }

        public async Task OnPlayerDisconnected(int playerIndex)
        {
            if (_gameState.Phase == GamePhase.GameOver)
                return;

            Console.WriteLine($"[Session:{_sessionId}] Player {playerIndex} 연결 끊김 → 패배 처리");

            string winnerId = (1 - playerIndex).ToString();
            string loserId = playerIndex.ToString();

            var winner = GetPlayer(1 - playerIndex);
            await winner.SendAsync(new S_GameOver { WinnerId = winnerId, Reason = 1 });

            await SendGameResultToLobbyAsync(winnerId, loserId);
        }

        private async Task FinishGameAsync(string winnerId, string loserId, byte reason)
        {
            await BroadcastAsync(new S_GameOver { WinnerId = winnerId, Reason = reason });
            await SendGameResultToLobbyAsync(winnerId, loserId);
        }

        private async Task SendGameResultToLobbyAsync(string winnerId, string loserId)
        {
            _gameState = new GameState(_ruleConfig);  // Phase = GameOver 방지용 재진입 차단

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
