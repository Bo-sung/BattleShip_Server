namespace BattleShip.Common.Packets
{// BattleShip.Common.Packets
    public enum PacketId : ushort
    {
        // Auth (1xxx)
        C_LoginReq = 1001,
        S_LoginRes = 1002,
        C_RegisterReq = 1003,
        S_RegisterRes = 1004,

        // Lobby (2xxx)
        C_EnterLobbyReq = 2001,
        S_EnterLobbyRes = 2002,
        C_RoomCreateReq = 2003,
        S_RoomCreateRes = 2004,
        C_RoomListReq = 2005,
        S_RoomListRes = 2006,
        C_RoomJoinReq = 2007,
        S_RoomJoinRes = 2008,
        S_RoomUserJoined = 2009,
        C_ReadyReq = 2010,
        S_GameStart = 2011,

        // Game (3xxx)
        C_EnterSessionReq = 3001,
        S_EnterSessionRes = 3002,
        C_PlaceShipsReq = 3003,
        S_PlaceShipsRes = 3004,
        S_PlacementDone = 3005,
        C_AttackReq = 3006,
        S_AttackRes = 3007,
        S_OpponentAttack = 3008,
        S_TurnNotify = 3009,
        S_GameOver = 3010,
        S_GameRuleConfig = 3011,

        // 서버 간 (9xxx)
        SS_Ping = 9001,
        SS_Pong = 9002,
        SS_GameResultReq = 9003,
        SS_GameResultAck = 9004,
        SS_SessionRuleConfig = 9005,
        SS_SessionReady = 9006,
    }
}
