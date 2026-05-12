
# Valheim Battle Royale Mod - MVP & Architecture Plan

## 1. Vision

Session-based Battle Royale mode for Valheim:
- Server-authoritative gameplay
- Optional client mod (PC) for better UI
- Web assistant for non-modded players (including consoles)
- External API backend (matchmaking, stats, servers)

Core principle:
> MVP must be playable first, architected for modular upgrade later without rewrite.

---

## 2. High-Level Architecture

### Component Map

```
[Valheim Dedicated Server + BattleRoyale.dll]
        │
        ├── MatchManager      (lifecycle: Lobby → Active → Ended)
        ├── ZoneManager       (shrinking poison circle, server-tick)
        ├── LootManager       (static spawn points → ZNetScene.SpawnObject)
        ├── KillTracker       (patch Character.m_onDeath, broadcast via Chat)
        └── EventBus          (decoupled in-process pub/sub)
              │
              └──► HTTP POST → Next.js API
                        │
                        ├── WebSocket/SSE → Mobile/Web clients
                        └── REST endpoints (stats, servers, leaderboard)
```

### Component 1 — Game Server (Authoritative)
- Runs Valheim dedicated server + BattleRoyale.dll BepInEx plugin
- Handles all gameplay logic: zone, damage, loot, structure rules, match lifecycle, kill feed
- Source reference: `/home/igentuman/.config/JetBrains/Rider2026.1/extensions/valheim_sources`
- **Do NOT modify files in that path** — read-only reference

### Component 2 — Backend API (Next.js)
- Server registry, matchmaking, player stats, match metadata, event relay

### Component 3 — Client Mod (Optional PC)
- UI overlay: zone indicator, player count, kill feed
- QoL: auto-connect, match browser

### Component 4 — Mobile/Web Assistant
- Zone map, match state, player count, leaderboards (non-modded players, console)

---

## 3. Core Design Principles

### 3.1 Modularity — Interface-Based Systems
```csharp
IZoneSystem      // GetCurrentRadius(), GetCenter(), GetDamagePerSecond(), Tick(float dt)
ILootGenerator   // Generate(int matchSeed, List<Vector3> spawnPoints)
IMatchSystem     // Start(), Tick(float dt), End(string winnerName)
IStructureRules  // GetDamageMultiplier(), GetStaminaCostMultiplier()
```

### 3.2 Event-Driven — No Direct Manager Coupling
In-process EventBus (static, no dependencies):
```csharp
BREventBus.Emit(new PlayerKilledEvent { KillerName, VictimName, Position })
BREventBus.Emit(new ZoneUpdatedEvent  { Radius, Center, DamagePerSecond })
BREventBus.Emit(new LootSpawnedEvent  { Position, ItemName })
BREventBus.Emit(new MatchStartedEvent { MatchId, Seed, PlayerCount })
BREventBus.Emit(new MatchEndedEvent   { WinnerName, DurationSeconds })
```
Each event is forwarded to API via `ApiClient.PostAsync()`.

### 3.3 Server Authority
- Server owns: damage, zone radius, loot table, match state
- Clients only receive visual updates via existing Valheim ZDO sync

---

## 4. Valheim Hook Points (Concrete)

### 4.1 Player Death Detection
- Hook: `[HarmonyPostfix] Player.OnDeath()` — `Player.cs:3040`
- Attacker: read from `Character.m_lastHit` (HitData has attacker reference)
- Broadcast kill via `Chat.instance.SendText(Talker.Type.Shout, $"{killer} killed {victim}")`
  - Chat.cs:472 `SendText(Talker.Type, string)`

### 4.2 Player Spawn / Inventory Wipe
- Hook: `[HarmonyPostfix] Player.OnSpawned(bool)` — `Player.cs:2301`
- On BR match start, call `player.GetInventory().RemoveAll()` — `Inventory.cs:959`
- Disable valkyrie cutscene by passing `spawnValkyrie=false` or bypassing via prefix

### 4.3 Player Respawn Control
- Hook: `[HarmonyPrefix] Game.RequestRespawn(float, bool)` — `Game.cs:535`
- During active match: block respawn (return false from prefix) → spectator mode instead
- Use `Game.m_playerInitialSpawn` static event — `Game.cs:213` — for first-spawn registration

### 4.4 Zone Damage (Poison Outside Circle)
- Each server tick in `ZoneManager.Tick(float dt)`:
  - Iterate all connected players via `ZNet.instance.GetPeers()`
  - Check `Vector3.Distance(player.transform.position, zoneCenter) > currentRadius`
  - Apply damage via `player.Damage(new HitData { m_damage = { m_blunt = dps * dt } })`
  - HitData source: `Character.cs` — `Damage(HitData hit)` at line 1883

### 4.5 Structure Damage Multiplier
- Hook: `[HarmonyPrefix] WearNTear.Damage(HitData)` — `WearNTear.cs:936`
- Multiply `hit.m_damage` values by `IStructureRules.GetDamageMultiplier()` before passing through
- MVP: simple scalar (e.g. 3x) — configurable via BepInEx config

### 4.6 Loot Spawning
- `ZNetScene.instance.SpawnObject(pos, rot, prefab)` — `ZNetScene.cs:436`
- Prefabs loaded from `ZNetScene.instance.GetPrefab(hash)` by item name hash
- LootManager holds a static list of `(Vector3 position, string prefabName)` spawn descriptors
- On `MatchStarted`: shuffle list with match seed, spawn subset

### 4.7 Structure Stamina Restriction (MVP)
- Hook: `[HarmonyPrefix] Player.HaveStamina(float amount)` (or `UseStamina`)
- During active match, multiply stamina cost for build actions by scalar
- Detect build action via `Player.m_placementGhost != null`

### 4.8 Kill Feed in Chat
- Server calls `ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ChatMessage", ...)` 
- Or simpler: patch `Chat.RPC_ChatMessage` — `Chat.cs:467` — to broadcast server-side kill messages

---

## 5. System Implementations

### 5.1 ZoneManager
```
Data/ZoneConfig.cs
  - InitialRadius: float        (config, default 500f)
  - FinalRadius: float          (config, default 50f)
  - ShrinkDuration: float       (config, default 600s)
  - DamagePerSecond: float      (config, default 5f)
  - Center: Vector3             (set on MatchStart, world origin default)

Managers/ZoneManager.cs
  - Implements IZoneSystem
  - Singleton, subscribes to MatchStarted/MatchEnded events
  - Tick(float dt): compute radius lerp, apply damage to out-of-zone players
  - Emits ZoneUpdatedEvent every N seconds (broadcast to API + clients)
  - Visual: send zone center+radius to clients via custom ZDO key or RPC
```

### 5.2 LootManager
```
Data/LootTable.cs
  - List<LootSpawnPoint> { Vector3 Position, string[] PossiblePrefabs, float Weight }

Managers/LootManager.cs
  - Implements ILootGenerator
  - Loads spawn points from config file (JSON, embedded resource)
  - Generate(matchSeed): shuffle with seed, pick N points, ZNetScene.SpawnObject each
  - Emits LootSpawnedEvent per item
  - On MatchEnded: despawn uncollected loot via ZNetScene.Destroy
```

### 5.3 MatchManager
```
Data/MatchState.cs
  - enum MatchPhase { Lobby, WaitingForPlayers, Active, Ended }
  - string MatchId (GUID)
  - int Seed
  - List<string> AlivePlayers
  - DateTime StartTime

Managers/MatchManager.cs
  - Implements IMatchSystem
  - Singleton, started from Main.Awake via Harmony + ZNet ready check
  - Start(): assign MatchId+Seed, wipe inventories, set spawn point, emit MatchStartedEvent
  - Tick(float dt): check alive count → if 1 remaining call End()
  - End(winner): emit MatchEndedEvent, POST to API, re-enable respawn, reset zone
  - Chat command: `/brstart`, `/brstop` via Terminal patch
```

### 5.4 KillTracker
```
Patches/PlayerPatches.cs
  - [HarmonyPostfix] Player.OnDeath()
  - Extract killer from ZNet peer / HitData attacker field
  - Call MatchManager.Instance.RecordKill(killer, victim)
  - MatchManager removes victim from AlivePlayers
  - Emits PlayerKilledEvent
  - Chat broadcast via Chat.instance.SendText
```

### 5.5 EventBus
```
Data/BREventBus.cs
  - Static Dictionary<Type, List<Delegate>> _handlers
  - Subscribe<T>(Action<T> handler)
  - Unsubscribe<T>(Action<T> handler)
  - Emit<T>(T evt) where T : IBREvent
  - Thread-safe (lock on _handlers)
```

### 5.6 ApiClient
```
Data/ApiClient.cs
  - HttpClient singleton
  - PostAsync<T>(string endpoint, T payload) — fire-and-forget, log errors
  - BaseUrl from BepInEx config
  - Subscribes to all IBREvent types on init, forwards as JSON POST
```

---

## 6. Main.cs Bootstrap

```csharp
private void Awake()
{
    _instance = this;
    InitConfig();
    var harmony = new Harmony(PluginGuid);
    harmony.PatchAll();
    
    // Init singletons after ZNet is ready
    ZNet.m_instance // wait via coroutine or Game.m_playerInitialSpawn event
    EventBus.Init();
    ApiClient.Init(config.ApiBaseUrl);
    MatchManager.Init();
    ZoneManager.Init();
    LootManager.Init();
}
```

Known issue: `PluginGuid = "com.igentuman.battleroayle"` — typo ("battleroayle"), fix to `"com.igentuman.battleroyale"`.

---

## 7. Patches Organization

```
Patches/
  PlayerPatches.cs     — OnDeath, OnSpawned, HaveStamina, RequestRespawn intercept
  StructurePatches.cs  — WearNTear.Damage multiplier
  ChatPatches.cs       — Terminal commands: /brstart, /brstop, /brstatus
  GamePatches.cs       — Game.Start or ZNet ready hook for init sequence
```

---

## 8. Config (BepInEx)

```
BattleRoyale.cfg
  [Zone]
  InitialRadius = 500
  FinalRadius = 50
  ShrinkDuration = 600
  DamagePerSecond = 5

  [Loot]
  SpawnCount = 50
  LootTablePath = (embedded default)

  [Structure]
  DamageMultiplier = 3.0
  StaminaCostMultiplier = 2.0

  [API]
  BaseUrl = http://localhost:3000
  Enabled = false
```

---

## 9. Backend API (Next.js)

### MVP Endpoints
```
POST /api/server/register      { serverId, address, port, version }
POST /api/server/heartbeat     { serverId, playerCount, matchPhase }
POST /api/match/start          { matchId, seed, playerCount, startedAt }
POST /api/match/end            { matchId, winnerId, durationSeconds }
POST /api/events/kill          { matchId, killerName, victimName, timestamp }
POST /api/events/zone          { matchId, radius, center, timestamp }
GET  /api/match/:id            → match metadata + kill log
GET  /api/servers              → active server list
GET  /api/leaderboard          → top winners
```

### Event Stream (WebSocket or SSE)
```
/api/stream/:matchId
  → kill feed
  → zone updates
  → player count
  → match state changes
```

---

## 10. Mobile Assistant (MVP)

Tech: PWA (React + Tailwind) — simpler than React Native, no app store needed for MVP

Features:
- Join match by code/QR
- Live zone map (circle overlay on world map image)
- Kill feed stream
- Alive player count
- Server list

Data flow: `Server → API (HTTP POST) → SSE stream → PWA`

---

## 11. Development Phases

### Phase 0 — Foundation (current)
- [x] BepInEx plugin skeleton (Main.cs)
- [ ] Fix PluginGuid typo (`battleroayle` → `battleroyale`)
- [x] EventBus implementation
- [ ] BepInEx config setup
- [x] All folder stubs → real files

### Phase 1 — MVP Playable
- [x] MatchManager (lifecycle, player tracking)
- [x] KillTracker patch (Player.OnDeath)
- [x] Inventory wipe on spawn (Player.OnSpawned patch)
- [x] ZoneManager (shrinking radius + damage)
- [x] LootManager (static spawn points)
- [x] StructurePatches (damage multiplier)
- [x] Chat commands: /brstart, /brstop
- [x] Kill feed in chat
- [ ] Fix: loot spawn positions relative to player spawn zone (issue #3)
- [ ] Fix: AlivePlayers count=0 at match start (issue #4)
- [ ] Fix: PluginGuid typo (issue #5)

### Phase 2 — External Systems
- [ ] ApiClient (HTTP POST events)
- [ ] Next.js API skeleton
- [ ] SSE event stream
- [ ] PWA mobile assistant (zone map, kill feed)
- [ ] Server browser page

### Phase 3 — Polish
- [ ] Client mod UI (zone indicator, player counter HUD)
- [ ] Matchmaking (auto server assignment)
- [ ] Stats system (per-player history)

### Phase 4 — Expansion
- [ ] New game modes (teams, last-squad)
- [ ] Ranked / MMR
- [ ] Procedural loot tables (biome-based)
- [ ] Dynamic zone shapes

---

## 12. Key Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Valheim network tick rate limits zone sync | Zone lag visible | Send zone updates every 5s, client interpolates |
| Inventory wipe race on respawn | Items survive | Wipe in OnSpawned postfix + delay 1 frame |
| `m_lastHit` attacker null (fall/zone death) | Kill unattributed | Treat as zone kill, credit zone damage |
| ZNetScene.SpawnObject on non-server | Crash | Guard all spawn calls with `ZNet.instance.IsServer()` |
| Player count check race (simultaneous deaths) | Match ends wrong | Alive list update inside lock, check count after list mutation |
| API down during match | Events lost | Fire-and-forget, log only — never block gameplay |

---

## 13. Success Criteria for MVP

- 1 full match playable end-to-end (lobby → active → winner declared)
- No manual server intervention required
- Zone shrinks correctly, deals damage outside boundary
- Loot spawns at match start, players can pick up
- Kill detection reliable, broadcast in chat
- Match ends when 1 player remains

---

## 14. Known Issues (from testing)

| # | Issue | Status | Fix |
|---|-------|--------|-----|
| 1 | Match ends instantly with 1 player — `Tick()` sees `count==1` immediately after start | **Fixed** | Track `InitialPlayerCount`; end at 1 only when `InitialPlayerCount > 1` (solo match runs until death) |
| 2 | All loot prefabs unknown (`ShieldBronzeBuckler`, `AxeBronze`, etc.) | **Fixed** | Was using manual hash; switched to `ZNetScene.instance.GetPrefab(string)` which uses Valheim's own hash |
| 3 | Loot spawns at world origin area (positions like `50,2,50`) — center of map, not in meadows biome ring where players teleport | **Open** | LootTable positions need to be relative to spawn zone or use same `FindMeadowsSpawn()` approach |
| 4 | `MatchStarted` fires with `playerCount=0` — `AlivePlayers` populated before players load into world | **Fixed** | 10s grace period in `Tick()`; re-snapshots `Player.GetAllPlayers()` after grace period if list still empty |
| 5 | `PluginGuid = "com.igentuman.battleroayle"` — typo | **Open** | Fix to `"com.igentuman.battleroyale"` |
| 6 | `ShieldDomeImageEffect.Awake()` — `ArgumentNullException: shader null` | **Open** | Shader not found on dedicated server (null GfxDevice); guard with `if (shader == null) return` |
| 7 | `LootManager.SpawnCount=0` at match start | **Open** | `playerCount=0` passed to API but spawn count also 0 — investigate `_spawnCount` config wiring |

---

## 15. Final Principle

> Build MVP that works, not MVP that is perfect. Everything else is iteration.
