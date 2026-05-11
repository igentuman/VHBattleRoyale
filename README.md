# Valheim - Battle Royale

**BattleRoyale** is a server-authoritative Battle Royale game mode plugin for Valheim, built using BepInEx and HarmonyLib.

## Overview

The mod transforms Valheim into a competitive Battle Royale experience with a shrinking zone, randomized starts, and enhanced looting. It consists of a server-side mod, an optional client-side mod for UI enhancements, and a planned Web API tracker for global statistics and server discovery.

## Key Features

- **Server Authority**: Critical game logic (damage, zone, loot) is decided by the server.
- **Dynamic Zone**: 6 stages of zone shrinkage, with increasing damage outside the circle.
- **Match Lifecycle**: Randomized player spawns, inventory clearing, and skill normalization at start.
- **Enhanced Looting**: Focus on exploration and looting over traditional base building.
- **Boss Events**: Players may encounter bosses during specific match stages for extra rewards.
- **Event-Driven Architecture**: Modular system design using interfaces and events.

## Technical Details

### Plugin Identity
- **GUID**: `com.igentuman.battleroyale`
- **Name**: `Battle Royale`
- **Version**: `1.0.0`
- **Namespace**: `BattleRoyale`

### Technology Stack
- **Target Framework**: `.NET Standard 2.1`
- **Modding Framework**: [BepInEx 5.x](https://github.com/BepInEx/BepInEx)
- **Hooking Library**: [HarmonyLib](https://github.com/pardeike/HarmonyTranspiler)
- **Engine**: Unity 2022.3 (Valheim version)

### Architecture
- **Singleton Pattern**: Core managers and the main plugin entry point use `Instance` properties for global access.
- **Interface-Based Systems**: (Planned) All core systems (`IZoneSystem`, `ILootGenerator`, etc.) are designed to be swappable.
- **Event-Driven**: Systems communicate via events (`PlayerKilled`, `ZoneUpdated`) to maintain low coupling.
- **Client/Server Synchronization**: `ClientSync` handles RPC-like event forwarding between the server and connected clients.
- **API Integration**: `ApiClient` for optional match data reporting to a Web API.

### Core Components
- **Main Plugin**: Handles BepInEx lifecycle, configuration binding, and Harmony patching.
- **Managers**:
  - `MatchManager`: Controls match state machine and player lifecycle.
  - `ZoneManager`: Manages the shrinking play area and damage logic.
  - `LootManager`: Handles randomized loot spawning across the map.
- **UI & Rendering**:
  - `BRHud`: Custom HUD component for kill feeds and match status.
  - `ZoneRenderer`: Visualizes the zone boundaries and mini-map indicators.
- **Patches**: HarmonyLib prefix/postfix/transpiler hooks in `BattleRoyale.Patches`.

### Configuration
The mod exposes extensive configuration options via BepInEx:
- **Zone**: Phase radii, wait/shrink durations, and base damage.
- **Loot**: Spawn counts and item distribution settings.
- **Structure**: Damage multipliers and stamina costs for building during matches.
- **Match**: Starting skill levels and teleportation logic.
- **API**: Backend URL and event forwarding toggles.

## Project Structure

- **[./Main.cs](./Main.cs)**: Plugin entry point and configuration.
- **[./Managers/](./Managers/)**: Core game systems (Match, Zone, Loot managers).
- **[./Patches/](./Patches/)**: HarmonyLib patches for game hooks.
- **[./UI/](./UI/)**: In-game HUD elements like kill feeds and zone indicators.
- **[./Data/](./Data/)**: Data models and configuration types.

## Development

### Prerequisites

- Valheim installed at `~/.local/share/Steam/steamapps/common/Valheim/`.
- .NET SDK targeting `netstandard2.1`.

### Build Commands

```bash
# Build the plugin (auto-deploys to Valheim plugins folder)
dotnet build BattleRoyale.sln

# Build release version
dotnet build BattleRoyale.sln -c Release
```

The post-build step automatically copies the output DLL to:
`~/.local/share/Steam/steamapps/common/Valheim/BepInEx/plugins/`

## Gameplay Flow

1. **Wait Phase**: Players join the server and vote to start via `!start` or UI button.
2. **Start**: Players are teleported to random locations (Meadows/Black Forest), inventories are cleared, and skills are set to 20.
3. **Looting**: Players search for gear in chests and structures.
4. **Shrink**: The zone progressively narrows down to a final 1-meter circle.
5. **End**: The last player standing wins, and match statistics are reported to the tracker.

## License

This project is currently in early development.
