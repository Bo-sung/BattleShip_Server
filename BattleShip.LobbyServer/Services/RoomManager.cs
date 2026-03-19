using BattleShip.Common.Packets.Lobby;
using BattleShip.LobbyServer.Sessions;
using StackExchange.Redis;

namespace BattleShip.LobbyServer.Services
{
    public class Room
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public LobbyClientSession Player1 { get; set; }  // 방장
        public LobbyClientSession Player2 { get; set; }  // 참가자
        public bool IsStarting { get; set; }

        public bool IsFull => Player2 != null;
        public bool IsBothReady() => Player1.IsReady && Player2 != null && Player2.IsReady;
    }

    public class JoinResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public LobbyClientSession Host { get; set; }
    }

    public class RoomManager
    {
        private readonly IDatabase _redis;
        private readonly Dictionary<string, Room> _rooms = new Dictionary<string, Room>();
        private readonly object _lock = new object();

        public RoomManager(IDatabase redis)
        {
            _redis = redis;
        }

        public string CreateRoom(string roomName, LobbyClientSession host)
        {
            string roomId = Guid.NewGuid().ToString("N")[..8];

            var room = new Room
            {
                RoomId = roomId,
                RoomName = roomName,
                Player1 = host
            };

            lock (_lock)
            {
                _rooms[roomId] = room;
            }

            Console.WriteLine($"[Lobby] 방 생성: {roomName} ({roomId})");
            return roomId;
        }

        public JoinResult JoinRoom(string roomId, LobbyClientSession guest)
        {
            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out var room))
                    return new JoinResult { Success = false, Message = "존재하지 않는 방입니다." };

                if (room.IsFull)
                    return new JoinResult { Success = false, Message = "방이 가득 찼습니다." };

                room.Player2 = guest;
                return new JoinResult { Success = true, Host = room.Player1 };
            }
        }

        public void LeaveRoom(string roomId, LobbyClientSession session)
        {
            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out var room))
                    return;

                // 방장이 나가면 방 삭제
                if (room.Player1 == session)
                {
                    _rooms.Remove(roomId);
                    Console.WriteLine($"[Lobby] 방 삭제 (방장 퇴장): {roomId}");
                }
                else
                {
                    room.Player2 = null;
                    room.Player1.IsReady = false;
                }
            }
        }

        public void RemoveRoom(string roomId)
        {
            lock (_lock) { _rooms.Remove(roomId); }
        }

        public Room GetRoom(string roomId)
        {
            lock (_lock)
            {
                _rooms.TryGetValue(roomId, out var room);
                return room;
            }
        }

        public List<RoomInfo> GetRoomList()
        {
            lock (_lock)
            {
                return _rooms.Values.Select(r => new RoomInfo
                {
                    RoomId = r.RoomId,
                    RoomName = r.RoomName,
                    HostUsername = r.Player1.Username,
                    PlayerCount = (byte)(r.IsFull ? 2 : 1)
                }).ToList();
            }
        }
    }
}
