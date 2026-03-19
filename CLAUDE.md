# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BattleShip Server is a multiplayer game server architecture built with ASP.NET Core. It uses a **packet-based network protocol** rather than HTTP, with client-server socket communication.

## Architecture

The system is organized as three independent microservices that communicate through a shared common library:

### Services

- **BattleShip.AuthServer** (localhost:5121) - Handles user authentication (login, registration) and token generation
- **BattleShip.LobbyServer** (localhost:5168) - Manages game lobbies and player matching
- **BattleShip.GameSession** (localhost:5118) - Manages active game sessions and game logic
- **BattleShip.Common** (netstandard2.1) - Shared library containing protocol definitions and networking utilities

### Communication Pattern

The architecture uses a **custom packet-based protocol**, not HTTP REST:

- **Packet Definition**: Located in `BattleShip.Common/Packets/` organized by domain (Auth, Lobby, Game)
- **Naming Convention**:
  - `C_*` = Client request packets (e.g., `C_LoginReq`, `C_AttackReq`)
  - `S_*` = Server response packets (e.g., `S_LoginRes`, `S_AttackRes`)
- **Packet Interface**: All packets implement `IPacket`
- **Network Utilities**:
  - `PacketReader` - Deserializes incoming packet data
  - `PacketWriter` - Serializes outgoing packet data
  - `PacketDispatcher` - Routes packets to appropriate handlers
  - `RecvBuffer` - Manages incoming data buffering

### Service Structure

Each service follows a similar pattern:
- **Program.cs** - Service startup and ASP.NET Core configuration
- **Handlers/** - Request handlers that process incoming packets (e.g., `LoginHandler`)
- **Services/** - Business logic services (e.g., `TokenService`, `UserService`)
- **Sessions/** - Client session management (e.g., `AuthClientSession`)
- **Properties/launchSettings.json** - Port and environment configuration

LobbyServer additionally has:
- **Repositories/** - Data persistence layer for lobbies/players

## Development Commands

### Build the entire solution
```bash
dotnet build BattleShip.Server.slnx
```

### Run a specific service
```bash
# AuthServer (port 5121)
dotnet run --project BattleShip.AuthServer

# LobbyServer (port 5168)
dotnet run --project BattleShip.LobbyServer

# GameSession (port 5118)
dotnet run --project BattleShip.GameSession
```

### Run all services concurrently (for development)
Start each service in separate terminals:
```bash
dotnet run --project BattleShip.AuthServer &
dotnet run --project BattleShip.LobbyServer &
dotnet run --project BattleShip.GameSession
```

### Build specific project
```bash
dotnet build --project BattleShip.AuthServer
dotnet build --project BattleShip.Common
```

### Clean build artifacts
```bash
dotnet clean BattleShip.Server.slnx
```

## Project Configuration

- **Target Framework**: .NET 10.0 (or netstandard2.1 for Common library)
- **Language Features**:
  - Nullable reference types enabled (`<Nullable>enable</Nullable>`)
  - Implicit usings enabled (for web services)
- **Solution Format**: `.slnx` (slim solution format)

## Key Packet Types

### Authentication Flow
- `C_LoginReq` / `S_LoginRes` - User login
- `C_RegisterReq` / `S_RegisterRes` - User registration

### Lobby Flow
- `C_EnterLobbyReq` - Player enters lobby
- Various lobby packets for browsing and joining games

### Game Flow
- `C_EnterSessionReq` / `S_EnterSessionRes` - Player enters game session
- `C_PlaceShipsReq` / `S_PlaceShipsRes` - Ship placement phase
- `C_AttackReq` / `S_AttackRes` - Attack action
- `S_OpponentAttack` - Notify of opponent's attack
- `S_TurnNotify` - Notify whose turn it is
- `S_GameOver` - Game completion notification

## Common Development Tasks

**Adding a new packet type**:
- Create packet class in `BattleShip.Common/Packets/{Domain}/` implementing `IPacket`
- Use naming convention (C_ for client requests, S_ for server responses)
- Add handler in respective service's `Handlers/` folder

**Adding business logic**:
- Create service class in `{Service}/Services/`
- Use dependency injection in handlers to access services

**Managing client sessions**:
- Extend or use `AuthClientSession`/service-specific session classes in `Sessions/` folders
- Sessions manage client state and connections

## Ports Reference
- AuthServer: 5121 (HTTP), 7228 (HTTPS)
- LobbyServer: 5168 (HTTP), 7157 (HTTPS)
- GameSession: 5118 (HTTP), 7026 (HTTPS)
