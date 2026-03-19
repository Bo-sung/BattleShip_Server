using BattleShip.Common.Packets;
using BattleShip.Common.Packets.Auth;
using BattleShip.Common.Packets.Game;
using BattleShip.Common.Packets.Lobby;
using BattleShip.Common.Packets.ServerInternal;
using System;
using System.Threading;

namespace BattleShip.Common.Network
{
    public static class PacketSerializer
    {
        private static int _sequence = 0;

        // 패킷 → 전송용 byte[]
        public static byte[] Serialize(IPacket packet)
        {
            // 1. 페이로드 직렬화
            var writer = new PacketWriter();
            packet.Serialize(writer);
            byte[] payload = writer.ToArray();

            // 2. 헤더 + 페이로드 조립
            int totalLen = PacketHeader.Size + payload.Length;
            byte[] result = new byte[totalLen];

            var header = new PacketHeader
            {
                Length = (ushort)totalLen,
                PacketId = (ushort)packet.PacketId,
                Sequence = (ushort)(Interlocked.Increment(ref _sequence) % ushort.MaxValue),
            };

            header.Write(result.AsSpan());
            Buffer.BlockCopy(payload, 0, result, PacketHeader.Size, payload.Length);

            return result;
        }

        // 수신 버퍼 → 패킷 객체
        public static IPacket Deserialize(ReadOnlyMemory<byte> buffer)
        {
            var header = PacketHeader.Read(buffer.Span);
            var reader = new PacketReader(buffer, PacketHeader.Size);

            IPacket packet = (PacketId)header.PacketId switch
            {
                // Auth
                PacketId.C_LoginReq => new C_LoginReq(),
                PacketId.S_LoginRes => new S_LoginRes(),
                PacketId.C_RegisterReq => new C_RegisterReq(),
                PacketId.S_RegisterRes => new S_RegisterRes(),

                // Lobby
                PacketId.C_EnterLobbyReq => new C_EnterLobbyReq(),
                PacketId.S_EnterLobbyRes => new S_EnterLobbyRes(),
                PacketId.C_RoomCreateReq => new C_RoomCreateReq(),
                PacketId.S_RoomCreateRes => new S_RoomCreateRes(),
                PacketId.C_RoomListReq => new C_RoomListReq(),
                PacketId.S_RoomListRes => new S_RoomListRes(),
                PacketId.C_RoomJoinReq => new C_RoomJoinReq(),
                PacketId.S_RoomJoinRes => new S_RoomJoinRes(),
                PacketId.S_RoomUserJoined => new S_RoomUserJoined(),
                PacketId.C_ReadyReq => new C_ReadyReq(),
                PacketId.S_GameStart => new S_GameStart(),

                // Game
                PacketId.C_EnterSessionReq => new C_EnterSessionReq(),
                PacketId.S_EnterSessionRes => new S_EnterSessionRes(),
                PacketId.C_PlaceShipsReq => new C_PlaceShipsReq(),
                PacketId.S_PlaceShipsRes => new S_PlaceShipsRes(),
                PacketId.S_PlacementDone => new S_PlacementDone(),
                PacketId.C_AttackReq => new C_AttackReq(),
                PacketId.S_AttackRes => new S_AttackRes(),
                PacketId.S_OpponentAttack => new S_OpponentAttack(),
                PacketId.S_TurnNotify => new S_TurnNotify(),
                PacketId.S_GameOver => new S_GameOver(),

                // 서버 간
                PacketId.SS_Ping => new SS_Ping(),
                PacketId.SS_Pong => new SS_Pong(),
                PacketId.SS_GameResultReq => new SS_GameResultReq(),
                PacketId.SS_GameResultAck => new SS_GameResultAck(),

                _ => throw new Exception($"알 수 없는 PacketId: {header.PacketId}")
            };

            packet.Deserialize(reader);
            return packet;
        }
    }
}
