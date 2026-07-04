# BattleShip Server

C# / .NET 10 기반의 **멀티플레이어 배틀십(Battleship) 게임 서버** — Auth / Lobby / GameSession 3개의 독립 서버 프로세스가 커스텀 바이너리 TCP 프로토콜로 통신하는 분산 게임 서버 아키텍처 학습·포트폴리오 프로젝트입니다.

## 기술 스택

| 구분 | 내용 |
|---|---|
| 언어 / 런타임 | C#, .NET 10 (공용 라이브러리는 netstandard2.1) |
| 네트워크 | 순수 TCP 소켓 (`TcpListener` / `TcpClient`), 커스텀 바이너리 패킷 프로토콜 (HTTP 미사용) |
| DB | MySQL (`MySqlConnector`) — 유저 계정, 전적 저장 |
| 캐시 / 토큰 저장소 | Redis (`StackExchange.Redis`) — 로그인 토큰 (TTL 30초, 1회용) |
| 배포 | `deploy.py` (Python + paramiko) — 크로스 플랫폼 publish 및 SSH 업로드 |
| 솔루션 형식 | `.slnx` (슬림 솔루션 포맷) |

## 아키텍처

```
클라이언트
   │ ① 로그인/회원가입 (TCP :7001)
   ▼
AuthServer ──── MySQL(users) 조회, Redis에 1회용 토큰 발급(TTL 30s)
   │ ② 토큰 + Lobby 주소 전달
   ▼
LobbyServer (클라이언트 :7002 / 내부 :8002)
   │  - Redis 토큰 검증(1회 사용 후 즉시 삭제)
   │  - 방 생성/목록/입장/Ready 관리 (RoomManager)
   │  - 2명 Ready 시 GameSession 프로세스를 동적으로 spawn
   │    (GameSessionLauncher: 포트 풀 7010~7200 할당)
   │  - SessionWatchdog: 30초 무핑 세션 프로세스 Kill (좀비 정리)
   │  - 게임 결과 수신 → MySQL(game_records) 저장
   ▼
GameSession (세션당 1프로세스, 동적 포트)
   - 기동 직후 Lobby 내부 포트로 역접속(SS_Ping) → 게임 룰 수신
   - 플레이어 2명 수락(60초 타임아웃) → 함선 배치 → 턴제 공격 진행
   - 승패 판정 후 결과를 Lobby로 전송(ACK 미수신 시 3회 재시도) 후 종료
```

### 폴더 구조

```
BattleShip.Server.slnx
├── BattleShip.Common/          # 서버·클라이언트 공유 라이브러리 (netstandard2.1)
│   ├── Network/                # PacketReader/Writer, PacketSerializer, PacketDispatcher, RecvBuffer
│   ├── Session/PacketSession.cs# TCP 수신 루프 + 패킷 조립 공통 베이스 클래스
│   ├── Packets/                # 패킷 정의 (Auth / Lobby / Game / ServerInternal)
│   └── GameRuleConfig.cs       # 보드 크기·함선 구성 룰 (JSON으로 외부화)
├── BattleShip.AuthServer/      # 인증 서버 (:7001)
├── BattleShip.LobbyServer/     # 로비 서버 (:7002, 내부 :8002) + configs/classic.json
├── BattleShip.GameSession/     # 게임 세션 서버 (동적 포트, 세션당 1프로세스)
├── BattleShip.TestClient/      # CLI 테스트 클라이언트 (로그인→로비→게임 전체 흐름)
├── database.sql                # MySQL 스키마 (users, game_records) + 테스트 계정
├── config.env.example          # .env 예시 (DB/Redis 연결, 호스트/포트)
└── deploy.py                   # 대화형 배포 스크립트 (dotnet publish + SSH 업로드)
```

### 커스텀 패킷 프로토콜

- 헤더 6바이트: `Length(2) + PacketId(2) + Sequence(2)` — length-prefix 방식으로 TCP 스트림에서 패킷 경계 처리 (`RecvBuffer`)
- 패킷 ID 대역: Auth `1xxx` / Lobby `2xxx` / Game `3xxx` / 서버 간 `9xxx`
- 명명 규칙: `C_*` 클라이언트→서버, `S_*` 서버→클라이언트, `SS_*` 서버 간 내부 패킷
- 모든 패킷은 `IPacket` 구현체이며 `PacketSerializer`가 ID 기반으로 역직렬화, `PacketDispatcher`가 핸들러로 라우팅

`BattleShip.Common`은 서버 5개 프로젝트가 모두 참조하는 공유 프로토콜 라이브러리입니다. (별도 게임 클라이언트 저장소에서 동일 DLL을 공유하는지는 이 저장소만으로는 **확인 필요** — 본 저장소에는 CLI 기반 `BattleShip.TestClient`가 포함되어 있습니다.)

## 구현된 기능 (코드 기준)

**인증 (AuthServer)**
- 회원가입 / 로그인 (MySQL `users` 테이블, parameterized query)
- 로그인 성공 시 Redis에 1회용 토큰 발급 (GUID, TTL 30초) + Lobby 접속 정보 전달
- ※ 비밀번호는 현재 해시 없이 문자열 비교 (테스트 계정도 평문 저장 — `database.sql` 주석에 명시)

**로비 (LobbyServer)**
- 토큰 검증 후 로비 입장 (토큰은 1회 사용 후 즉시 삭제)
- 방 생성 / 방 목록 조회 / 방 입장 / 퇴장 처리 (방장 퇴장 시 방 삭제)
- 양측 Ready 시 GameSession 프로세스 자동 실행 → 포트 풀(7010~7200)에서 포트 할당
- 세션 준비 완료(`SS_SessionReady`) 수신 후에야 클라이언트에 `S_GameStart` 전송 (레이스 방지)
- SessionWatchdog: 10초 주기 검사, 30초 이상 핑 없는 세션 프로세스 강제 종료
- 게임 결과를 MySQL `game_records`에 저장 (승자/패자/총 턴 수)

**게임 (GameSession)**
- 세션당 독립 프로세스, 기동 시 Lobby로 역접속하여 게임 룰(`GameRuleConfig`) 수신
- 게임 룰 외부화: 보드 크기·함선 종류/크기를 `configs/*.json`으로 정의 (기본 classic: 10x10, 함선 5종)
- 함선 배치 검증 (범위, 겹침, 함선 종류/개수 일치)
- 턴제 공격 처리: 명중/빗나감/격침 판정, 중복 공격 방지, 랜덤 선공
- 승패 판정(전 함선 격침) 및 상대 접속 종료 시 부전승 처리
- 결과 전송 ACK 재시도(3회) 후 프로세스 종료, 플레이어 2명 미접속 시 60초 타임아웃 자동 종료

**공통 인프라 (BattleShip.Common)**
- 링 버퍼 방식 수신 버퍼(`RecvBuffer`)로 TCP 패킷 분할/병합 처리
- 비동기 수신 루프 베이스 클래스(`PacketSession`), 타입 안전 핸들러 등록(`PacketDispatcher`)

**테스트 클라이언트 (BattleShip.TestClient)**
- 콘솔 기반 CLI로 로그인 → 로비 → 방 생성/입장 → 함선 배치 → 대전까지 전체 플로우 플레이 가능

**배포 (deploy.py)**
- 서비스/타깃 OS(win/linux/macOS, x64/ARM64) 선택 → `dotnet publish` → SSH(paramiko) 업로드 자동화

## 실행 방법

### 사전 준비
```bash
# 1. MySQL 스키마 생성 (테스트 계정 포함: aaaa/1111, bbbb/2222 등)
mysql -u root -p < database.sql

# 2. Redis 실행 (기본 localhost:6379)

# 3. 환경 설정
cp config.env.example .env   # DB_CONNECTION, REDIS_CONNECTION, GAME_SESSION_EXE_PATH 등 수정
```

### 서버 실행 (각각 별도 터미널)
```bash
dotnet build BattleShip.Server.slnx

dotnet run --project BattleShip.AuthServer    # :7001
dotnet run --project BattleShip.LobbyServer   # :7002 (내부 :8002)
# GameSession은 LobbyServer가 자동 실행 (.env의 GAME_SESSION_EXE_PATH 필요 — 먼저 빌드해 둘 것)
```

### 테스트 클라이언트
```bash
dotnet run --project BattleShip.TestClient
# 클라이언트 2개를 띄워 방 생성/입장 후 대전
```

GameSession 단독 실행(개발용):
```bash
dotnet run --project BattleShip.GameSession -- --session test --port 7010 --config classic
```

> 참고: 저장소 내 `CLAUDE.md`에 기재된 포트(5121/5168/5118)는 초기 ASP.NET Core 템플릿 기준이며, 실제 코드는 위의 TCP 포트(7001/7002/8002/7010~7200)를 사용합니다.
