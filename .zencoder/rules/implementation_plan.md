---
description: PlayerStats Implementation Plan
alwaysApply: true
---

# PlayerStats Implementation Plan

## Overview
Implement a Valheim mod to track player statistics, including combat, movement, resource gathering, and hidden "2-star" mob interactions. Features include a real-time overlay, streamer-focused configuration, and a "reveal stats" end-game summary.

## 1. Data Structure & Persistence
> **Performance Note**: To avoid lag spikes, **NEVER** perform file I/O during high-frequency events (combat, movement, etc.). Data is updated in memory and persisted periodically or during specific game save triggers.

- **StatsData Class**: Define a serializable data structure.
    - Combat: `kills`, `deaths`, `dodges`, `blocks`, `perfectBlocks`, `total_damage`.
    - Movement: `runDistance`, `swimDistance`, `jumps`.
    - Gathering: `treesChopped`, `minedCount`.
    - Misc: `buildingPartsPlaced`, `fishesCaught`.
    - Complex Data: Use `List<StatEntry>` for `killed2StarMobs` and `spawned2StarAroundPlayer` to ensure `JsonUtility` compatibility.
- **JSON Storage**:
    - **Filename**: `stats_{CharacterName}_{WorldName}.json`.
    - **Path**: `BepInEx/config/PlayerStats/Stats/`.
    - **Persistence**: 
        - Save periodically (every 5 minutes via `Main.Update`).
        - Save on `OnDestroy` (plugin shutdown).
        - Optional: Hook into `ZNet.Save` for synchronized saves.

## 2. Harmony Patching (Data Collection)
- **Combat Stats**:
    - `Character.OnDeath` (Postfix):
        - If victim is local player: `deaths++`.
        - If victim is mob: `kills++`.
        - If victim level == 3 (2-star): Increment count in `killed2StarMobs`.
    - `Character.Damage` (Postfix): If `hit.GetAttacker()` is local player, add `hit.GetTotalDamage()` to `total_damage`.
    - `Humanoid.BlockAttack` (Postfix): If successful, `blocks++`. If `m_perfectBlock`, `perfectBlocks++`.
    - `Player.Dodge` (Postfix): `dodges++`.
- **Movement**:
    - `Player.Update` (Postfix): Increment `runDistance` and `swimDistance` by checking deltas of `m_runDistance` and `m_swimDistance` fields.
    - `Player.Jump` (Postfix): `jumps++`.
- **Gathering/Building**:
    - `TreeBase.OnDamaged` / `TreeLog.OnDamaged` (Postfix): If health <= 0 and hit by player, `treesChopped++`.
    - `MineRock.OnDamaged` / `MineRock5.OnDamaged` (Postfix): If health <= 0 and hit by player, `minedCount++`.
    - `Player.PlacePiece` (Postfix): `buildingPartsPlaced++`.
- **Fishing**:
    - `FishingFloat.Catch` (Postfix): `fishesCaught++`.
- **Hidden Stats**:
    - `Character.Awake` (Postfix): If level == 3 and within 50m of player, increment `spawned2StarAroundPlayer`. Use a `HashSet` to avoid double-counting the same instance.

## 3. Configuration & UI
- **BepInEx Config**:
    - Toggles for each stat in the overlay.
    - `RevealHiddenStats` (bool, default: false).
- **HUD Overlay**:
    - Unity UI `Canvas` with `Text` or `TextMeshProUGUI` (if available in Valheim).
    - Draggable or fixed position based on config.
    - Real-time updates in `Update()`.
- **Console Command**:
    - `revealstats`: Sets `RevealHiddenStats` to true and updates UI.

## 4. Implementation Steps (Completed)
1. **Initialize Project Structure** [COMPLETED]
    - Created `Data/`, `Managers/`, `Patches/`, `UI/` folders.
    - Set up `Main.cs` with Harmony initialization and basic config.
2. **Implement Core Data Logic** [COMPLETED]
    - Created `StatsData` and `StatEntry` classes for serialization.
    - Implemented `StatsManager` for JSON persistence and character/world specific files.
3. **Add patches category by category** [COMPLETED]
    - Implemented Combat patches (`OnDeath`, `Damage`, `BlockAttack`, `Dodge`).
    - Implemented Movement patches (`Update` tracking deltas for distance).
    - Implemented Gathering patches (`TreeBase`, `TreeLog`, `MineRock`).
    - Implemented Misc patches (`FishingFloat.Catch`, `Character.Awake` for 2-star mobs).
4. **Develop UI Overlay** [COMPLETED]
    - Created `StatsOverlay` using Unity UI Canvas/Text.
    - Integrated with `StatsData` and real-time updates.
5. **Finalize Configuration & Commands** [COMPLETED]
    - Added BepInEx settings for overlay toggles.
    - Implemented `revealstats` console command.
6. **Enhance Persistence & Refinement** [COMPLETED]
    - Added patches to save stats explicitly on `Player.OnLogout` (via `Game.Logout`) and `ZNet.Save`.
    - Implemented draggable overlay and configurable UI positions.
    - Added a full "Reveal Stats" summary screen (`summarystats` command).
    - Improved `MineRock` and `MineRock5` section tracking.
    - Optimized `Traverse` calls with `FieldRef` in performance-sensitive patches.

## 5. Next Steps
1. **Testing**:
    - Verify data collection and UI updates in-game across different character/world combinations.
2. **Localization Support**:
    - Ensure all displayed strings are localized using Valheim's `Localization` system.
3. **Advanced UI**:
    - Add a background/frame to the overlay for better readability in different biomes.
    - Implement a "reset stats" command or button.
