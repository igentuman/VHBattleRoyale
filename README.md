# Valheim - Battle Royale

**BattleRoyale** is a server-authoritative Battle Royale game mode plugin for Valheim, built using BepInEx and HarmonyLib.

## Overview

The mod transforms Valheim into a competitive Battle Royale experience with a shrinking zone, randomized starts, and enhanced looting. It is a server-side plugin with an optional client-side HUD, and supports an optional Web API tracker for match statistics.

## Key Features

- **Server Authority**: All critical game logic (damage, zone, loot) is decided by the server.
- **Dynamic Zone**: 6 configurable shrink phases with increasing damage outside the circle.
- **Match Lifecycle**: Randomized player spawns, inventory clearing, and skill normalization at start.
- **Spectator Mode**: Players who die become spectators and can observe the match.
- **Enhanced Looting**: JSON-configurable loot tables for chests and mobs.
- **Boss Events**: Players may encounter bosses during specific match stages for extra rewards.
- **Event-Driven Architecture**: Modular system design using interfaces and a shared event bus.
- **API Integration**: Optional match event forwarding to a Web API backend.

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
- **Engine**: Unity 2022.3 (Valheim)

### Architecture

- **Singleton Pattern**: Core managers use `static Instance` properties for global access.
- **Interface-Based Systems**: All core systems implement interfaces (`IMatchSystem`, `IZoneSystem`, `ILootGenerator`) to allow implementation swaps.
- **Event-Driven**: Systems emit events via `BREventBus` (`PlayerKilled`, `ZoneUpdated`, `LootSpawned`, `MatchStarted`, `MatchEnded`) - no direct coupling between managers.
- **Client/Server Synchronization**: `ClientSync` handles RPC-like event forwarding from server to connected clients.

### Core Components

- **[./BattleRoyale/Main.cs](./BattleRoyale/Main.cs)**: BepInEx plugin entry point - lifecycle, config binding, Harmony patching, system initialization.
- **Managers** ([./BattleRoyale/Managers/](./BattleRoyale/Managers/)):
  - `MatchManager` - match state machine, player lifecycle, kill tracking (`IMatchSystem`)
  - `ZoneManager` - shrinking play area, per-tick damage logic
  - `LootManager` - randomized loot spawning from JSON loot tables
  - `SpectatorManager` - tracks dead players and manages spectator state
- **UI** ([./BattleRoyale/UI/](./BattleRoyale/UI/)):
  - `BRHud` - kill feed, match status, player count overlay
  - `ZoneRenderer` - zone boundary circles on the minimap
  - `ZoneMistEffect` - visual mist effect at zone boundary
  - `SpectatorHud` - HUD overlay for spectating players
- **Patches** ([./BattleRoyale/Patches/](./BattleRoyale/Patches/)):
  - `ChatPatches` - `!start` / `!end` chat commands
  - `GamePatches` - server/client ready hooks
  - `PlayerPatches` - kill detection and player damage hooks
  - `SpectatorPatches` - spectator mode hooks
  - `StructurePatches` - structure damage and stamina cost multipliers
- **Data** ([./BattleRoyale/Data/](./BattleRoyale/Data/)):
  - `BREventBus` - typed publish/subscribe event bus
  - `Events` - event structs (`MatchStartedEvent`, `PlayerKilledEvent`, `ZoneUpdatedEvent`, `LootSpawnedEvent`, `MatchEndedEvent`)
  - `ClientSync` - server→client RPC synchronization
  - `ApiClient` - optional HTTP event reporting to backend
  - `LootTable` - JSON-deserializable loot table model
  - `MatchState` / `ZoneConfig` - shared data models

### Configuration

All options are exposed via BepInEx config and hot-reloadable:

| Section | Key | Default | Description |
|---|---|---|---|
| Zone | PhaseWaitDuration | 480s | Time to show next zone before shrinking |
| Zone | PhaseShrinkDuration | 360s | Time to complete each shrink |
| Zone | DamagePerSecond | 1 | Base damage/s outside zone (doubles each phase) |
| Zone | PhaseRadii | `5500,4000,2000,1000,200,1` | Zone radii per phase (meters) |
| Loot | SpawnCount | 50 | Loot items to spawn per match |
| Structure | DamageMultiplier | 2× | Structure damage multiplier during match |
| Structure | StaminaCostMultiplier | 2× | Build stamina cost multiplier during match |
| Match | StartSkillLevel | 20 | Skill level for all players at match start |
| Match | StartBuffDuration | 300s | Duration of start buffs (Eikthyr, rested, etc.) |
| Match | TeleportSpawnRadius | 4000m | Radius from world center for random spawns |
| UI | RenderZoneCircles | true | Render zone boundary on the minimap |
| API | BaseUrl | `http://localhost:3000` | Backend API base URL |
| API | Enabled | false | Enable API event forwarding |
| Testing | TestingMode | false | Show debug buttons for spectator/match controls |

### Loot Tables

Default loot tables are extracted to `BepInEx/config/BattleRoyale/` on first run:
- `chest_loot_tables.json` - items spawned in chests
- `mob_loot_tables.json` - items dropped by mobs

## Project Structure

```
BattleRoyale/
├── Main.cs              # Plugin entry point
├── Managers/
│   ├── MatchManager.cs
│   ├── ZoneManager.cs
│   ├── LootManager.cs
│   └── SpectatorManager.cs
├── Patches/
│   ├── ChatPatches.cs
│   ├── GamePatches.cs
│   ├── PlayerPatches.cs
│   ├── SpectatorPatches.cs
│   └── StructurePatches.cs
├── UI/
│   ├── BRHud.cs
│   ├── ZoneRenderer.cs
│   ├── ZoneMistEffect.cs
│   └── SpectatorHud.cs
├── Data/
│   ├── BREventBus.cs
│   ├── Events.cs
│   ├── ClientSync.cs
│   ├── ApiClient.cs
│   ├── LootTable.cs
│   ├── MatchState.cs
│   └── ZoneConfig.cs
└── config/
    ├── chest_loot_tables.json
    └── mob_loot_tables.json
```

## Development

### Prerequisites

- Valheim installed at `~/.local/share/Steam/steamapps/common/Valheim/`
- .NET SDK targeting `netstandard2.1`

### Build Commands

```bash
# Build the plugin (auto-deploys to Valheim plugins folder)
dotnet build BattleRoyale.sln

# Build release version
dotnet build BattleRoyale.sln -c Release
```

The post-build step automatically copies the output DLL to (Linux):
`~/.local/share/Steam/steamapps/common/Valheim/BepInEx/plugins/`

## Gameplay Flow

1. **Lobby**: Players join the server and vote to start via `!start` chat command.
2. **Start**: Players are teleported to random locations within `TeleportSpawnRadius`, inventories are cleared, and all skills are set to `StartSkillLevel`.
3. **Buffs**: Players receive start buffs (Eikthyr, rested, corpse run, feather fall, no skill drain, sneaky) for `StartBuffDuration` seconds.
4. **Looting**: Players search for gear in chests and structures.
5. **Zone Shrink**: The zone progressively narrows across 6 phases down to a 1-meter final circle. Damage outside the zone doubles each phase.
6. **Elimination**: Dead players enter spectator mode and can watch the remaining match.
7. **End**: The last player standing wins. Match statistics are optionally reported to the API tracker.

## License

See [LICENSE.txt](./LICENSE.txt).
