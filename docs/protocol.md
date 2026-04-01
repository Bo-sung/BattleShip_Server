# BattleShip 서버 프로토콜 명세서

본 문서는 BattleShip 멀티플레이어 게임 서버에서 사용하는 완전한 패킷 기반 네트워크 프로토콜을 설명합니다. 호환되는 클라이언트를 구현해야 하는 외부 클라이언트 개발자를 위한 문서입니다.

---

## 1. 바이너리 패킷 포맷

### 패킷 구조

모든 네트워크 통신은 고정된 헤더 뒤에 가변 길이 페이로드가 따르는 **바이너리 패킷 포맷**을 사용합니다:

```
┌─────────────────────────────────────────┐
│      패킷 헤더 (6바이트)                 │
├─────────────────────────────────────────┤
│   패킷 페이로드 (가변 길이)              │
└─────────────────────────────────────────┘
```

### 패킷 헤더 (6바이트, 리틀엔디안)

| 오프셋 | 크기 | 타입   | 필드        | 설명                             |
|--------|------|--------|-------------|----------------------------------|
| 0      | 2    | ushort | Length      | 헤더 포함 전체 패킷 길이         |
| 2      | 2    | ushort | PacketId    | 패킷 식별자 (섹션 3 참조)        |
| 4      | 2    | ushort | Sequence    | 패킷 순서 번호                   |

**예시:** ID 1001이고 페이로드 10바이트인 패킷:
- 바이트 [0-1]: `0x10 0x00` (리틀엔디안 16 = 6 헤더 + 10 페이로드)
- 바이트 [2-3]: `0xE9 0x03` (리틀엔디안 1001)
- 바이트 [4-5]: `0x00 0x00` (순서 번호)

### 데이터 타입 직렬화 규칙

모든 멀티바이트 값은 **리틀엔디안** 바이트 순서를 사용합니다:

| 타입   | 크기  | 예시 직렬화                      |
|--------|-------|----------------------------------|
| bool   | 1     | `0x00` (거짓) 또는 `0x01` (참) |
| byte   | 1     | `0x42` (66)                    |
| sbyte  | 1     | `0xFE` (-2, 2의 보수)          |
| int    | 4     | `0x78 0x56 0x34 0x12` (0x12345678) |
| ushort | 2     | `0x34 0x12` (0x1234)           |
| string | 2+n   | [길이(2)] + [utf-8 바이트]     |

**문자열 포맷:** 문자열은 2바이트 길이(ushort) 뒤에 UTF-8 인코딩 바이트가 따릅니다. 빈 문자열은 `0x00 0x00`으로 직렬화됩니다.

### 패킷 읽기 및 쓰기

구현자는 다음과 같은 유틸리티를 제공해야 합니다:
- **PacketReader**: 버퍼에서 순서대로 타입된 값을 읽음
- **PacketWriter**: 버퍼에 순서대로 타입된 값을 씀

핵심 원칙: **코드의 읽기/쓰기 순서는 패킷의 바이트 순서와 일치해야 합니다**

---

## 2. 서버 구성 및 포트

BattleShip 시스템은 세 개의 독립적인 마이크로서비스로 구성됩니다:

### 서비스 아키텍처

| 서비스          | 포트 | 프로토콜 | 역할                              |
|-----------------|------|----------|-----------------------------------|
| AuthServer      | 7001 | TCP      | 사용자 인증 & 토큰 생성           |
| LobbyServer     | 7002 | TCP      | 게임 로비 & 플레이어 매칭        |
| GameSession     | 7010-7200 | TCP | 활성 게임 세션 (게임당 생성)    |

### 연결 흐름

```
클라이언트
  │
  ├─→ AuthServer (1001-1002)
  │   로그인/회원가입
  │   ↓ Token + LobbyHost:LobbyPort 수신
  │
  ├─→ LobbyServer (2001-2011)
  │   로비 진입 → 방 검색 → 방 생성/입장 → 준비 완료
  │   ↓ GameServerHost:GameServerPort 수신
  │
  └─→ GameSession (3001-3010, 3012-3025)
      세션 진입 → 함선 배치 → [스킬 선택] → 게임 진행 → 게임 종료
```

---

## 3. 패킷 ID 참조

패킷 ID는 도메인별로 구성됩니다 (1000s=인증, 2000s=로비, 3000s=게임).

### 인증 패킷 (1xxx)

| PacketId | 방향 | 패킷명     | 목적                     |
|----------|------|-----------|--------------------------|
| 1001     | C→S  | C_LoginReq       | 사용자 로그인 요청       |
| 1002     | S→C  | S_LoginRes       | 로그인 결과 + 토큰       |
| 1003     | C→S  | C_RegisterReq    | 사용자 회원가입 요청     |
| 1004     | S→C  | S_RegisterRes    | 회원가입 결과            |

### 로비 패킷 (2xxx)

| PacketId | 방향 | 패킷명      | 목적                       |
|----------|------|-----------|----------------------------|
| 2001     | C→S  | C_EnterLobbyReq  | 토큰으로 로비 진입         |
| 2002     | S→C  | S_EnterLobbyRes  | 로비 진입 결과             |
| 2003     | C→S  | C_RoomCreateReq  | 게임 방 생성               |
| 2004     | S→C  | S_RoomCreateRes  | 방 생성됨 (roomId 반환)    |
| 2005     | C→S  | C_RoomListReq    | 개방된 방 목록 요청        |
| 2006     | S→C  | S_RoomListRes    | 사용 가능한 방 목록        |
| 2007     | C→S  | C_RoomJoinReq    | 기존 방 입장               |
| 2008     | S→C  | S_RoomJoinRes    | 입장 결과                  |
| 2009     | S→C  | S_RoomUserJoined | 브로드캐스트: 상대방 입장  |
| 2010     | C→S  | C_ReadyReq       | 게임 시작 준비 완료 신호   |
| 2011     | S→C  | S_GameStart      | 게임 시작, 포트 연결       |

### 게임 패킷 (3xxx) - 모든 모드 공통

| PacketId | 방향 | 패킷명      | 목적                       |
|----------|------|-----------|----------------------------|
| 3001     | C→S  | C_EnterSessionReq| 게임 세션 진입             |
| 3002     | S→C  | S_EnterSessionRes| 세션 진입 확인             |
| 3003     | C→S  | C_PlaceShipsReq  | 보드에 함선 배치           |
| 3004     | S→C  | S_PlaceShipsRes  | 배치 결과                  |
| 3005     | S→C  | S_PlacementDone  | 양쪽 플레이어 배치 완료    |
| 3006     | C→S  | C_AttackReq      | 상대 셀 공격               |
| 3007     | S→C  | S_AttackRes      | 공격자의 공격 결과         |
| 3008     | S→C  | S_OpponentAttack | 상대방 공격 알림           |
| 3009     | S→C  | S_TurnNotify     | 턴 변경 알림               |
| 3010     | S→C  | S_GameOver       | 게임 종료, 승자 공고       |
| 3011     | S→C  | S_GameRuleConfig | 게임 규칙 (보드, 함선, 스킬) |

### StarBattle 패킷 (3012-3025) - 스킬 모드만

| PacketId | 방향 | 패킷명      | 목적                    |
|----------|------|-----------|-------------------------|
| 3012     | C→S  | C_SelectSkillsReq   | 초기 스킬 2개 선택      |
| 3013     | S→C  | S_SelectSkillsRes   | 스킬 선택 확인          |
| 3014     | S→C  | S_BothSkillsSelected| 양쪽 선택 완료, 게임 시작 |
| 3015     | C→S  | C_MoveReq           | 함선 이동               |
| 3016     | S→C  | S_MoveRes           | 이동 결과               |
| 3018     | C→S  | C_SkillReq          | 스킬 사용               |
| 3019     | S→C  | S_SkillAttackRes    | 스킬 공격 결과 (스킬 1) |
| 3020     | S→C  | S_OpponentSkillAttack | 상대가 스킬 공격 사용   |
| 3021     | S→C  | S_SkillMoveRes      | 스킬 이동 결과 (스킬 4) |
| 3023     | S→C  | S_SkillRepairRes    | 수리 결과 (스킬 3)      |
| 3024     | S→C  | S_OpponentRepaired  | 상대가 함선 수리        |
| 3025     | S→C  | S_SkillShieldRes    | 실드 활성화 (스킬 2)    |

### 서버 내부 패킷 (9xxx)

이 패킷들은 서비스 간 통신에만 사용되며 클라이언트에서 전송되어서는 안 됩니다.

| PacketId | 패킷명             | 목적                           |
|----------|-------------------|--------------------------------|
| 9001     | SS_Ping           | LobbyServer의 헬스 체크        |
| 9002     | SS_Pong           | Ping에 대한 응답               |
| 9003     | SS_GameResultReq  | GameSession→Lobby: 승자 보고   |
| 9004     | SS_GameResultAck  | Lobby→GameSession: 확인        |
| 9005     | SS_SessionRuleConfig | Lobby→GameSession: 게임 규칙   |
| 9006     | SS_SessionReady   | GameSession→Lobby: 플레이어 준비 |

---

## 4. 패킷 상세 명세

### 4.1 인증 패킷

#### C_LoginReq (1001)

**요청:** 사용자 로그인

| 필드    | 타입   | 설명              |
|----------|--------|-------------------|
| Username | string | 사용자명 (UTF-8)  |
| PasswordHash | string | 패스워드 해시 (클라이언트 측 해시) |

**직렬화 순서:**
1. Username (string)
2. PasswordHash (string)

#### S_LoginRes (1002)

**응답:** 로그인 결과

| 필드      | 타입   | 설명                              |
|-----------|--------|-----------------------------------|
| Success   | bool   | 로그인 성공 (참/거짓)             |
| Token     | string | 일회용 UUID 토큰 (성공 시)        |
| Message   | string | 오류 메시지 (실패 시)             |
| LobbyHost | string | 로비 서버 호스트 주소             |
| LobbyPort | int    | 로비 서버 포트                    |

**직렬화 순서:**
1. Success (bool)
2. Token (string)
3. Message (string)
4. LobbyHost (string)
5. LobbyPort (int)

#### C_RegisterReq (1003)

**요청:** 사용자 회원가입

| 필드    | 타입   | 설명          |
|----------|--------|---------------|
| Username | string | 새 사용자명   |
| PasswordHash | string | 패스워드 해시 |

#### S_RegisterRes (1004)

**응답:** 회원가입 결과

| 필드    | 타입   | 설명           |
|----------|--------|----------------|
| Success | bool   | 회원가입 성공  |
| Message | string | 상태 메시지    |

---

### 4.2 로비 패킷

#### C_EnterLobbyReq (2001)

**요청:** 로비 진입

| 필드 | 타입   | 설명                      |
|------|--------|--------------------------|
| Token | string | S_LoginRes의 일회용 토큰 |

#### S_EnterLobbyRes (2002)

**응답:** 로비 진입 결과

| 필드    | 타입 | 설명          |
|---------|------|---------------|
| Success | bool | 진입 성공     |

#### C_RoomCreateReq (2003)

**요청:** 새 게임 방 생성

| 필드      | 타입   | 설명                                |
|-----------|--------|-------------------------------------|
| RoomName  | string | 인간 읽기 가능한 방 이름            |
| ConfigName | string | 게임 모드: "classic", "extended", "starbattle" |

#### S_RoomCreateRes (2004)

**응답:** 방 생성됨

| 필드    | 타입   | 설명           |
|---------|--------|----------------|
| Success | bool   | 생성 성공      |
| RoomId  | string | 고유 방 식별자 |

#### C_RoomListReq (2005)

**요청:** 개방된 방 목록 조회

*(필드 없음)*

#### S_RoomListRes (2006)

**응답:** 개방된 방 목록

| 필드 | 타입       | 설명               |
|------|------------|-------------------|
| Rooms | RoomInfo[] | 방 세부정보 배열   |

**RoomInfo 구조:**
- RoomId (string)
- RoomName (string)
- HostName (string)
- ConfigName (string)
- PlayerCount (byte) - 1 또는 2
- MaxPlayers (byte) - 항상 2

#### C_RoomJoinReq (2007)

**요청:** 방 입장

| 필드  | 타입   | 설명         |
|-------|--------|--------------|
| RoomId | string | 대상 방 ID  |

#### S_RoomJoinRes (2008)

**응답:** 입장 결과

| 필드    | 타입 | 설명        |
|---------|------|-------------|
| Success | bool | 입장 성공   |

#### S_RoomUserJoined (2009)

**브로드캐스트:** 상대방이 방에 입장

| 필드     | 타입   | 설명         |
|----------|--------|--------------|
| Username | string | 상대 이름    |

#### C_ReadyReq (2010)

**요청:** 게임 시작 준비 완료 신호

*(필드 없음)*

#### S_GameStart (2011)

**응답:** 게임 시작

| 필드           | 타입   | 설명                    |
|----------------|--------|------------------------|
| SessionId      | string | 게임 세션 식별자        |
| GameServerHost | string | GameSession 서버 주소   |
| GameServerPort | int    | GameSession 서버 포트   |

---

### 4.3 게임 패킷 - 모든 모드 공통

#### C_EnterSessionReq (3001)

**요청:** 게임 세션 진입

| 필드      | 타입   | 설명                  |
|-----------|--------|----------------------|
| SessionId | string | S_GameStart의 세션 ID |

#### S_EnterSessionRes (3002)

**응답:** 세션 진입 확인

| 필드       | 타입 | 설명                 |
|------------|------|----------------------|
| Success    | bool | 진입 성공            |
| PlayerIndex | byte | 당신의 플레이어 인덱스 (0 또는 1) |

이 응답 후 클라이언트는 S_GameRuleConfig를 수신할 것으로 예상합니다.

#### S_GameRuleConfig (3011)

**서버→클라이언트:** 게임 규칙 및 함선 정의

S_EnterSessionRes 직후 전송됩니다. 포함 내용:

| 필드   | 타입           | 설명                       |
|--------|----------------|----------------------------|
| Config | GameRuleConfig | 완전한 게임 규칙 (섹션 6 참조) |

**GameRuleConfig 구조:**
- BoardSize (byte): 보드 크기 (10 또는 12)
- GameMode (byte): 0=Basic, 1=Extended, 2=SkillMode
- Ships[] (ShipDefinition): 함선 타입 목록
  - Type (byte): 함선 타입 ID
  - Name (string): 함선명
  - Size (byte): 함선 길이 (셀)
- SkillPool[] (SkillDefinition): 사용 가능한 스킬 목록 (SkillMode만)
  - Type (byte): 스킬 ID (0-5)
  - Name (string): 스킬명
  - ManaCost (byte): 사용에 필요한 마나
  - Description (string): UI 설명

#### C_PlaceShipsReq (3003)

**요청:** 보드에 함선 배치

| 필드  | 타입             | 설명               |
|-------|------------------|-------------------|
| Ships | ShipPlacement[]  | 함선 배치 배열    |

**ShipPlacement 구조:**
- ShipType (byte): GameRuleConfig의 함선 타입 ID
- X (byte): 시작 X 좌표 (0 기반)
- Y (byte): 시작 Y 좌표 (0 기반)
- IsHorizontal (bool): true = 가로, false = 세로

#### S_PlaceShipsRes (3004)

**응답:** 배치 유효성 검사 결과

| 필드    | 타입   | 설명         |
|---------|--------|--------------|
| Success | bool   | 배치 유효    |
| Message | string | 오류 메시지  |

**유효성 검사 규칙:**
- 모든 함선은 보드 범위 내에 있어야 함
- 함선은 겹칠 수 없음
- 함선은 보드 외부에 배치될 수 없음

#### S_PlacementDone (3005)

**브로드캐스트:** 양쪽 플레이어의 함선이 배치됨

양쪽 모두 함선을 성공적으로 배치한 후 양쪽에 전송됩니다.

*(필드 없음)*

이후 게임 모드가 다음 단계를 결정합니다:
- **Classic/Extended**: S_TurnNotify 수신 (턴이 즉시 시작)
- **SkillMode**: 서버가 스킬 선택 대기

#### C_AttackReq (3006)

**요청:** 상대 셀 공격

| 필드 | 타입 | 설명      |
|------|------|-----------|
| X    | byte | 목표 X 좌표 |
| Y    | byte | 목표 Y 좌표 |

#### S_AttackRes (3007)

**응답:** 공격 결과

| 필드         | 타입   | 설명                                 |
|--------------|--------|--------------------------------------|
| X            | byte   | 공격한 X 좌표                        |
| Y            | byte   | 공격한 Y 좌표                        |
| Result       | byte   | 0=빗맞음, 1=명중, 2=침몰, 3=이미공격, 4=실드차단 |
| SunkShipName | string | 침몰했으면 함선명, 아니면 빈 문자열 |

#### S_OpponentAttack (3008)

**브로드캐스트:** 상대가 당신의 셀을 공격

| 필드         | 타입   | 설명                  |
|--------------|--------|----------------------|
| X            | byte   | 공격한 X 좌표         |
| Y            | byte   | 공격한 Y 좌표         |
| Result       | byte   | 공격 결과 코드        |
| SunkShipName | string | 침몰했으면 함선명     |

#### S_TurnNotify (3009)

**브로드캐스트:** 턴 변경

| 필드     | 타입 | 설명                                |
|----------|------|-------------------------------------|
| IsMyTurn | bool | 당신의 턴이면 true                  |
| Mana     | byte | 현재 마나 (SkillMode 0-10, 기타 0) |

**주의:** 각 성공한 행동 (공격, 이동, 스킬) 후 이 패킷이 양쪽에 전송됩니다.

#### S_GameOver (3010)

**브로드캐스트:** 게임 종료

| 필드     | 타입   | 설명                            |
|----------|--------|--------------------------------|
| WinnerId | string | 승자의 플레이어 인덱스 (문자열) |
| Reason   | byte   | 0=함선 침몰, 1=상대 연결 해제   |

---

### 4.4 StarBattle 패킷 (스킬 모드만)

#### C_SelectSkillsReq (3012)

**요청:** 초기 스킬 2개 선택 (스킬 모드만)

S_PlacementDone 후 전송됩니다. 클라이언트는 GameRuleConfig.SkillPool에서 다른 2개의 스킬을 선택해야 합니다.

| 필드  | 타입 | 설명          |
|-------|------|---------------|
| Skill1 | byte | 선택한 첫 번째 스킬 ID |
| Skill2 | byte | 선택한 두 번째 스킬 ID |

#### S_SelectSkillsRes (3013)

**응답:** 스킬 선택 확인

| 필드    | 타입 | 설명        |
|---------|------|-------------|
| Success | bool | 선택 유효   |

#### S_BothSkillsSelected (3014)

**브로드캐스트:** 양쪽 모두 스킬 선택, 게임 시작

양쪽 모두 스킬 선택을 확인했을 때 전송됩니다.

*(필드 없음)*

이후 클라이언트는 실제 게임 진행을 위해 S_TurnNotify를 기대합니다.

#### C_MoveReq (3015)

**요청:** 함선 이동 (스킬 모드만)

| 필드     | 타입  | 설명              |
|----------|-------|-------------------|
| ShipType | byte  | 이동할 함선 타입 ID |
| DirX     | sbyte | 방향 X (-1, 0, 1) |
| DirY     | sbyte | 방향 Y (-1, 0, 1) |

**규칙:**
- 한 방향으로만 1칸 이동 가능
- 함선이 손상되지 않아야 함 (손상되면 이동 불가)
- 새 위치는 보드 범위 내여야 함

#### S_MoveRes (3016)

**응답:** 이동 결과

| 필드    | 타입   | 설명        |
|---------|--------|-------------|
| Success | bool   | 이동 유효   |
| Message | string | 오류 메시지 |

#### C_SkillReq (3018)

**요청:** 스킬 사용 (스킬 모드만)

| 필드      | 타입 | 설명                          |
|-----------|------|-------------------------------|
| SkillType | byte | 스킬 ID (0-5)                 |
| TargetX   | byte | 목표 X (범위/이동 스킬용)     |
| TargetY   | byte | 목표 Y (범위/이동 스킬용)     |
| ShipType  | byte | 대상 함선 (이동/실드/수리용)  |

**스킬 타입:**
- 1: 범위공격 (3x3 영역, TargetX,TargetY 중심)
- 2: 실드 (ShipType을 2턴 동안 보호)
- 3: 수리 (ShipType의 1칸 수리)
- 4: 이동 (C_MoveReq처럼 하지만 무료 행동)
- 5: 냉동 (TargetX,TargetY에 3x3 냉동 존 3턴)
- 6: 회복 (자신에게 +3 마나)

#### S_SkillAttackRes (3019)

**응답:** 스킬 공격 결과 (스킬 1: 범위공격)

| 필드      | 타입         | 설명                |
|-----------|--------------|---------------------|
| SkillType | byte         | 스킬 타입 (echo)    |
| Cells     | CellResult[] | 영향받은 셀 배열    |
| Mana      | byte         | 스킬 후 남은 마나   |

**CellResult 구조:**
- X (byte): 셀 X 좌표
- Y (byte): 셀 Y 좌표
- Result (byte): 0=빗맞음, 1=명중, 2=침몰, 3=이미공격, 4=실드차단
- SunkShipName (string): 침몰했으면 함선명

#### S_OpponentSkillAttack (3020)

**브로드캐스트:** 상대가 범위공격 사용 (스킬 1)

| 필드      | 타입         | 설명           |
|-----------|--------------|----------------|
| SkillType | byte         | 스킬 타입 (항상 1) |
| Cells     | CellResult[] | 당신의 보드에서 영향받은 셀 |

#### S_SkillMoveRes (3021)

**응답:** 스킬 이동 결과 (스킬 4: 이동 스킬)

| 필드    | 타입   | 설명        |
|---------|--------|-------------|
| Success | bool   | 이동 유효   |
| Message | string | 오류 메시지 |
| Mana    | byte   | 남은 마나   |

#### S_SkillRepairRes (3023)

**응답:** 수리 스킬 결과 (스킬 3: 수리)

| 필드     | 타입 | 설명       |
|----------|------|-----------|
| Success  | bool | 수리 성공  |
| ShipType | byte | 수리한 함선 타입 |
| Mana     | byte | 남은 마나  |

#### S_OpponentRepaired (3024)

**브로드캐스트:** 상대가 함선을 수리

| 필드     | 타입 | 설명        |
|----------|------|------------|
| ShipType | byte | 수리한 함선 ID |

#### S_SkillShieldRes (3025)

**응답:** 실드 스킬 결과 (스킬 2: 실드)

| 필드     | 타입 | 설명            |
|----------|------|-----------------|
| Success  | bool | 실드 활성화     |
| ShipType | byte | 보호된 함선 타입 |
| Duration | byte | 실드 지속 턴 수 |
| Mana     | byte | 남은 마나       |

---

## 5. 게임 모드별 흐름

### 5.1 클래식 모드 흐름

클래식 배틀십 (10x10 보드, 5개 함선):

```
클라이언트                            서버
  │
  ├─ C_EnterSessionReq ───────────→ S_EnterSessionRes
  │                           └─→ S_GameRuleConfig
  │
  ├─ C_PlaceShipsReq ─────────────→ S_PlaceShipsRes (성공)
  │  (함선 5개 배치)
  │                      ┌─────────→ S_PlacementDone (양쪽 브로드캐스트)
  │                      │
  ├─ (대기)  ◄───────────┴─────────  (상대도 배치함)
  │
  ├─ (수신) S_TurnNotify ◄───────── (턴 할당)
  │
  │ ─┬─ 게임 끝날 때까지 반복 ───────
  │  │
  │  ├─ C_AttackReq ────────────────→ S_AttackRes
  │  │                        └────→ S_OpponentAttack (상대에게)
  │  │
  │  └─ (수신) S_TurnNotify ◄─── (턴 전환 또는 게임 종료)
  │ ─┴──────────────────────────────
  │
  └─ S_GameOver ◄────────────────── (게임 종료)
```

**핵심 포인트:**
- 이동이나 스킬 없음
- 공격이 즉시 턴을 종료
- 성공/실패 공격 후 턴 전환

---

### 5.2 확장 모드 흐름

확장 배틀십 (12x12 보드, 6개 함선):

클래식 모드와 유사하지만 더 큰 보드와 추가 함선:

```
클라이언트                            서버
  │
  ├─ C_EnterSessionReq ───────────→ S_EnterSessionRes
  │                           └─→ S_GameRuleConfig (6개 함선)
  │
  ├─ C_PlaceShipsReq ─────────────→ S_PlaceShipsRes (성공)
  │  (함선 6개 배치)
  │                      ┌─────────→ S_PlacementDone
  │
  ├─ (대기)  ◄───────────┴─────────
  │
  ├─ S_TurnNotify ◄───────────────
  │
  │ ─┬─ 게임 끝날 때까지 반복 ───────
  │  │
  │  ├─ C_AttackReq ────────────────→ S_AttackRes
  │  │                        └────→ S_OpponentAttack
  │  │
  │  └─ S_TurnNotify ◄──────────────
  │ ─┴──────────────────────────────
  │
  └─ S_GameOver ◄────────────────── 
```

**클래식과의 차이점:**
- 12x12 보드 (10x10 대신)
- 6개 함선 (5개 대신)
- 나머지는 동일한 게임플레이

---

### 5.3 StarBattle (스킬 모드) 흐름

이동, 마나, 6개 스킬이 있는 StarBattle:

```
클라이언트                            서버
  │
  ├─ C_EnterSessionReq ───────────→ S_EnterSessionRes
  │                           └─→ S_GameRuleConfig (6개 스킬 포함)
  │
  ├─ C_PlaceShipsReq ─────────────→ S_PlaceShipsRes (성공)
  │  (함선 6개 배치)
  │                      ┌─────────→ S_PlacementDone
  │
  ├─ (대기)  ◄───────────┴─────────
  │
  │ ═ 스킬 선택 단계 ══════════════
  │
  ├─ C_SelectSkillsReq ───────────→ S_SelectSkillsRes (성공)
  │  (스킬1, 스킬2 선택)
  │                      ┌─────────→ S_BothSkillsSelected
  │
  ├─ (대기)  ◄───────────┴─────────
  │
  ├─ S_TurnNotify ◄───────────────
  │  (마나=0, 내턴)
  │
  │ ═ 게임플레이 단계 ═══════════════
  │
  │ ─┬─ 게임 끝날 때까지 반복 ──────
  │  │
  │  │ 내 턴이면:
  │  │  │
  │  │  ├─ (선택 1) C_AttackReq ──→ S_AttackRes
  │  │  │                      └──→ S_OpponentAttack
  │  │  │
  │  │  ├─ (선택 2) C_MoveReq ────→ S_MoveRes
  │  │  │
  │  │  └─ (선택 3) C_SkillReq ───→ S_SkillXxxRes
  │  │                        └──→ S_OpponentXxx (해당하면)
  │  │
  │  └─ S_TurnNotify ◄──────────────
  │     (갱신된 마나, 턴 전환)
  │
  │ ─┴──────────────────────────────
  │
  └─ S_GameOver ◄────────────────── 
```

**핵심 게임 메커니즘:**
1. 배치 단계 (다른 모드와 동일)
2. **스킬 선택 단계** (스킬 모드만)
   - 각 플레이어가 풀에서 자신이 좋아하는 2개의 스킬 선택
   - 양쪽 모두 준비되면 서버가 브로드캐스트
3. **게임플레이 단계** (새로운 행동 가능)
   - **공격**: 단일 셀 공격 (턴 1회 소비, 명중 시 +1 마나)
   - **이동**: 함선을 1칸 재배치 (턴 1회 소비, 마나 획득 없음)
   - **스킬**: 선택한 스킬 중 하나 사용 (마나 소비, 실패 시 턴 미소비)

**마나 시스템:**
- 초기 마나=0
- 성공한 공격마다 +1
- 스킬 사용으로 소비
- 최대 10 마나
- 스킬 6 (회복)은 +3 마나 제공

---

## 6. 게임 설정 및 규칙

### 6.1 게임 모드

```csharp
enum GameModeType : byte
{
    Basic = 0,      // Classic 10x10, 5개 함선
    Extended = 1,   // Extended 12x12, 6개 함선
    SkillMode = 2   // StarBattle 12x12, 6개 함선 + 이동 + 마나 + 스킬
}
```

### 6.2 함선 정의

함선들은 GameRuleConfig.Ships 배열에서 정의됩니다. 각 모드별로 다른 함선 수/타입:

#### 클래식 모드 (Basic = 0)

| 타입 | 이름      | 크기 |
|------|-----------|------|
| 0    | 항공모함   | 5    |
| 1    | 전함       | 4    |
| 2    | 순양함     | 3    |
| 3    | 잠수함     | 3    |
| 4    | 구축함     | 2    |

#### 확장 모드 (Extended = 1)

| 타입 | 이름      | 크기 |
|------|-----------|------|
| 0    | 항공모함   | 5    |
| 1    | 전함       | 4    |
| 2    | 순양함     | 3    |
| 3    | 잠수함     | 3    |
| 4    | 구축함     | 2    |
| 5    | 초계함     | 1    |

#### StarBattle 모드 (SkillMode = 2)

확장 모드와 동일 (6개 함선).

### 6.3 스킬 정의 (스킬 모드만)

스킬 모드에서 사용 가능한 6개 스킬. GameRuleConfig.SkillPool에서 정의됩니다.

| 타입 | 이름     | 마나비용 | 효과                              |
|------|----------|----------|-----------------------------------|
| 1    | 범위공격 | 2        | 목표 좌표에서 3x3 영역 공격      |
| 2    | 실드     | 3        | 1개 함선을 2턴 보호 (공격 차단) |
| 3    | 수리     | 2        | 목표 함선의 1칸 수리            |
| 4    | 이동     | 1        | 함선 1칸 이동 (C_MoveReq처럼)   |
| 5    | 냉동     | 3        | 3x3 영역 3턴 냉동 (이동 차단)   |
| 6    | 회복     | 0        | +3 마나 획득 (무료 스킬)         |

### 6.4 공격 결과 코드

| 코드 | 이름            | 의미                          |
|------|-----------------|-------------------------------|
| 0    | 빗맞음          | 목표 좌표에 함선 없음         |
| 1    | 명중            | 함선 명중하지만 미침몰        |
| 2    | 침몰            | 함선 명중 및 침몰 (모든 칸 명중) |
| 3    | 이미공격함      | 그 좌표는 이미 공격했음       |
| 4    | 실드차단        | 실드로 공격 차단 (스킬 모드)  |

### 6.5 보드 좌표

- **좌표 범위**: (0,0) ~ (BoardSize-1, BoardSize-1)
- **보드 크기**:
  - 클래식: 10x10
  - 확장: 12x12
  - StarBattle: 12x12

### 6.6 함선 배치 규칙

- 함선은 가로 또는 세로로 배치됨
- 함선은 보드 범위 내에 완전히 들어가야 함
- 함선들은 겹칠 수 없음
- 플레이어당 각 타입당 1개 함선

### 6.7 이동 규칙 (스킬 모드만)

- 함선은 턴당 1칸 이동 가능 (C_MoveReq를 통해)
- 함선이 손상되면 이동 불가 (명중된 칸이 있으면)
- 새 위치는 보드 범위 내여야 함
- 다른 함선과의 겹침 불가
- 상대방은 이동을 볼 수 없음

### 6.8 실드 시스템 (스킬 모드만)

- 스킬 2 (실드)는 목표 함선에 보호 활성화
- 2턴 지속 (적용된 턴과 다음 턴에 공격 받을 수 있음)
- 공격 및 공격 스킬로부터의 피해를 차단
- 같은 함선의 여러 실드는 스택됨 (타이머 리셋/연장)
- 차단된 공격은 여전히 그 칸에 "이미 공격함"으로 계산됨

### 6.9 냉동 존 시스템 (스킬 모드만)

- 스킬 5 (냉동)는 목표 중심에 3x3 냉동 존 생성
- 3턴 동안 존으로 출입하는 함선 이동을 차단
- 이동 시도 실패: 클라이언트가 S_MoveRes (Success=false) 수신
- 여러 냉동 영역이 활성화되면 존들이 겹침

### 6.10 마나 시스템 (스킬 모드만)

- **초기 마나**: 0
- **마나 획득**: 성공한 공격마다 +1 (명중 또는 침몰)
- **마나 최대**: 10
- **스킬 소비**:
  - 스킬 1: 2 마나
  - 스킬 2: 3 마나
  - 스킬 3: 2 마나
  - 스킬 4: 1 마나
  - 스킬 5: 3 마나
  - 스킬 6: 0 마나 (제공 +3, 10을 초과할 수 없음)
- **실패한 스킬**: 마나 소비 없음, 턴 미전환

---

## 7. 클라이언트 개발자를 위한 구현 주의사항

### 7.1 토큰 처리

- **일회용 사용**: S_LoginRes의 토큰은 일회용만
- **세션 바인딩**: 토큰은 한 번의 로비 세션에 바인딩
- **만료**: C_EnterLobbyReq 성공 후 토큰 만료
- **재사용 방지**: 같은 토큰 두 번 전송하면 실패

### 7.2 패킷 구조 모범 사례

1. **헤더 파싱**: 먼저 6바이트 헤더를 읽고, Length와 PacketId 추출
2. **패킷 라우팅**: PacketId를 사용하여 올바른 역직렬화 핸들러로 라우팅
3. **길이 검증**: 수신한 바이트가 헤더 Length 필드와 일치하는지 검증
4. **버퍼링**: 스트리밍 데이터용 RecvBuffer 같은 순환 버퍼 구현
   - 모든 데이터가 한 번의 소켓 읽기에 도착하지 않을 수 있음
   - 완전한 패킷을 받을 때까지 바이트를 누적해야 함
5. **시퀀스 번호**: 디버깅용 시퀀스 추적; 서버는 순서를 강제하지 않음

### 7.3 GameRuleConfig 직렬화

**중요:** 역직렬화 순서는 이 직렬화 순서와 일치합니다:

1. BoardSize (1바이트)
2. GameMode (1바이트)
3. ShipCount (1바이트)
4. 각 함선마다:
   - Type (1바이트)
   - Name (2바이트 길이 접두사가 있는 문자열)
   - Size (1바이트)
5. SkillCount (1바이트)
6. 각 스킬마다:
   - Type (1바이트)
   - Name (2바이트 길이 접두사가 있는 문자열)
   - ManaCost (1바이트)
   - Description (2바이트 길이 접두사가 있는 문자열)

### 7.4 방 구성 이름

방을 생성할 때 (C_RoomCreateReq), 이러한 ConfigName 값을 사용합니다:

| ConfigName   | 게임 모드      | 보드 크기 | 함선 | 기능            |
|--------------|---------------|----------|------|-----------------|
| "classic"    | Basic (0)     | 10x10    | 5    | 단순 공격       |
| "extended"   | Extended (1)  | 12x12    | 6    | 큰 보드         |
| "starbattle" | SkillMode (2) | 12x12    | 6    | 이동, 스킬, 마나 |

### 7.5 게임 흐름 상태 머신

게임 단계를 추적하는 상태 머신을 구현합니다:

```
                        ┌─ 인증
                        │
인증 ─────────┼─ 로비
                        │
                        └─ 게임
                           │
                           ├─ 배치
                           ├─ 스킬선택 (스킬 모드만)
                           ├─ 진행중
                           └─ 게임종료
```

**유효한 전환:**
- 인증 → 로비 (S_LoginRes 성공 후)
- 로비 → 게임 (S_GameStart 후)
- 게임→배치 → 스킬선택 (스킬 모드) 또는 진행중 (다른 모드)
- 스킬선택 → 진행중 (스킬 모드)
- 진행중 → 게임종료 (S_GameOver 후)

### 7.6 턴 기반 행동 처리

각 턴은 활성 플레이어의 한 가지 행동으로 구성됩니다:

**클래식/확장 모드:**
- 유일한 행동: 공격
- 한 공격 = 한 턴
- 무효한 공격 (이미공격함)도 턴을 소비

**스킬 모드:**
- 세 가지 가능한 행동: 공격, 이동, 스킬
- 턴당 한 가지 행동만
- 스킬 실패 (Success=false)는 턴 미소비 및 플레이어 전환 없음
- 다시 시도하거나 다른 행동 선택 필요

### 7.7 정보 비대칭성

상대방이 수신하지 말아야 할 것:
- 이동 알림 (C_MoveReq → S_MoveRes는 개인)
- 스킬 선택 (C_SelectSkillsReq → S_SelectSkillsRes는 개인)
- 실드 활성화 세부정보 (실드 차단됨을 알지만 플레이어가 스킬을 사용했다는 것은 모름)
- 수리 세부정보 (상대가 수리했다는 것만 알고 어느 함선인지는 모름)

상대방이 수신해야 할 것:
- 공격 결과 (명중/빗맞음/침몰)
- 범위공격 결과 (스킬 1)
- 수리 알림 (S_OpponentRepaired)
- 침몰한 함선명

### 7.8 오류 처리

1. **연결 끊김**: 소켓 읽기가 0바이트를 반환하면 연결 종료
   - 서버가 연결을 종료하고 토큰 무효화
   - 클라이언트는 다시 연결하고 재로그인 필요

2. **잘못된 패킷**: 패킷 검증 실패 시:
   - 형식 오류: 연결 종료
   - 논리 오류 (예: 잘못된 좌표): 서버가 Success=false 응답
   - 순서 위반: 다시 연결

3. **타임아웃**: 60초 이상 활동 없으면:
   - 서버가 연결 종료할 수 있음
   - 필요하면 클라이언트 측 킵얼라이브 구현

### 7.9 디버깅 팁

1. **패킷 로깅 활성화**: 모든 송수신 패킷을 타임스탬프와 시퀀스로 로깅
2. **보드 상태 검증**: 상대 보드를 로컬에서 추적; 공격을 검증
3. **마나 추적**: 마나 상태 유지 및 스킬 사용 검증
4. **턴 검증**: 턴 변경을 출력하여 서버 턴 로직 검증
5. **패킷 시퀀스**: 시퀀스 번호를 모니터링하여 손실된 패킷 감지

### 7.10 일반적인 구현 패턴

**패킷 핸들러 패턴:**

```csharp
public class PacketHandler {
    private Dictionary<ushort, Action<IPacket>> handlers = new();
    
    public PacketHandler() {
        Register(PacketId.S_LoginRes, OnLoginRes);
        Register(PacketId.S_EnterLobbyRes, OnEnterLobbyRes);
        // ... 기타
    }
    
    public void Handle(IPacket packet) {
        if (handlers.TryGetValue((ushort)packet.PacketId, out var handler)) {
            handler(packet);
        }
    }
    
    private void OnLoginRes(IPacket packet) {
        var res = (S_LoginRes)packet;
        if (res.Success) {
            // 로비로 진행
        } else {
            Console.WriteLine("로그인 실패: " + res.Message);
        }
    }
}
```

**연결 패턴:**

```csharp
public class GameConnection {
    private TcpClient socket;
    private NetworkStream stream;
    
    public async Task SendPacketAsync(IPacket packet) {
        byte[] data = Serialize(packet);
        await stream.WriteAsync(data, 0, data.Length);
    }
    
    public async Task<IPacket> ReceivePacketAsync() {
        byte[] header = new byte[6];
        await stream.ReadExactlyAsync(header, 0, 6);
        
        ushort length = BitConverter.ToUInt16(header, 0);
        byte[] payload = new byte[length - 6];
        await stream.ReadExactlyAsync(payload, 0, payload.Length);
        
        return Deserialize(header, payload);
    }
}
```

---

## 부록: 빠른 참고

### 서비스 연결 순서

1. AuthServer (7001) → 토큰 획득
2. LobbyServer (7002) → 방 생성/입장 → GameServerHost:GameServerPort 획득
3. GameSession (7010-7200) → 게임 플레이

### 패킷 ID 범위

- 1001-1004: 인증
- 2001-2011: 로비
- 3001-3011: 게임 (공통)
- 3012-3025: StarBattle (스킬)
- 9001-9006: 서버 내부

### 중요한 직렬화 순서

**GameRuleConfig**: BoardSize → GameMode → Ships (수량 포함) → SkillPool (수량 포함)

**문자열 포맷**: 2바이트 길이 (ushort) + UTF-8 바이트

**리틀엔디안**: 모든 멀티바이트 값

### 마나 및 스킬 참고

| 스킬 | 타입 | 비용 | 효과 |
|------|------|------|------|
| 1    | 범위공격 | 2 | 3x3 영역 |
| 2    | 실드 | 3 | 2턴 보호 |
| 3    | 수리 | 2 | 1칸 회복 |
| 4    | 이동 | 1 | 함선 재배치 |
| 5    | 냉동 | 3 | 3x3 존, 3턴 |
| 6    | 회복 | 0 | +3 마나 |

---

**문서 버전**: 1.0  
**최종 업데이트**: 2026-04-04  
**지원 게임 버전**: BattleShip Server v1.0+
