# Eager World Load

Eliminates the second loading screen when first opening the world map after loading a save in RimWorld 1.7.

**Package ID:** `ertzfiend.eagerworldload`  
**Target RimWorld Version:** `1.7`  
**Dependencies:** Harmony (`brrainz.harmony`)

---

## The Vanilla Mechanism

In vanilla RimWorld 1.7, the game handles world layer generation inconsistently between new games and loaded saves:

1. **New Game Creation:**
   - Generating a new world calls `RimWorld.Planet.WorldRenderer.RegenerateAllLayersNow()` (in `Page_CreateWorldParams.<CanDoNext>b__20_1`).
   - All planet meshes and terrain draw layers are fully generated upfront before gameplay starts.

2. **Save Loading:**
   - Loading an existing save (`Verse.Game.LoadGame()` / `Verse.SavedGameLoaderNow.LoadGameFromSaveFileNow()`) deserializes the world and reconstructs world components, but **does not** regenerate world draw layers.
   - All `WorldDrawLayer_Terrain` layers remain marked dirty.

3. **First World View Open:**
   - When the player first opens the world map or inspects an off-map caravan/quest site, `RimWorld.Planet.World.WorldUpdate()` calls `WorldRenderer.DrawWorldLayers()`.
   - `DrawWorldLayers()` invokes `WorldRenderer.RegenerateLayersIfDirtyInLongEvent()`.
   - The property `WorldRenderer.ShouldRegenerateDirtyLayersInLongEvent` inspects `Find.WorldGrid.PlanetLayers` and sees dirty terrain layers, returning `true`.
   - `RegenerateLayersIfDirtyInLongEvent()` sets `asynchronousRegenerationActive = true` and calls:
     ```csharp
     LongEventHandler.QueueLongEvent(RegenerateDirtyLayersNow_Async(), "GeneratingPlanet", null, false, false);
     ```
   - This halts gameplay and opens the modal "Generating planet" loading screen in the middle of a play session.

### Interaction with Loading Mods (`ilyvion.LoadingProgress`)

Loading screen enhancements such as `ilyvion.LoadingProgress` hook `WorldRenderer.RegenerateLayersIfDirtyInLongEvent` and the `GeneratingPlanet` long event to render custom progress bars and detailed status overlays. While useful during initial game launch and save loading, triggering this screen mid-game when simply pressing the world map key feels disruptive.

Eager World Load resolves the root cause in vanilla: because layer regeneration is completed during the initial save load, the dirty flag is cleared before the player enters the map, and `RegenerateLayersIfDirtyInLongEvent()` is never triggered when toggling the world view.

---

## How Eager World Load Works

Eager World Load attaches a Harmony `[HarmonyPostfix]` patch to `Verse.Game.LoadGame`:

1. At the completion of `Verse.Game.LoadGame()`, `Find.World` and its `WorldRenderer` are fully loaded and initialized, while the game loading sequence is still actively managing queued long events.
2. The patch verifies guard conditions:
   - `Find.World != null`
   - `Find.World.renderer != null`
   - `WorldRenderer.ShouldRegenerateDirtyLayersInLongEvent == true`
3. If dirty layers exist, `WorldRenderer.RegenerateLayersIfDirtyInLongEvent()` is called immediately.
4. The asynchronous generation of planet meshes and terrain layers runs as part of the save loading sequence.
5. In new games, `RegenerateAllLayersNow()` has already executed, so `ShouldRegenerateDirtyLayersInLongEvent` is false; Eager World Load does nothing and avoids double-generation.

---

## Known Limitations

- **Save Loading Time:** Save load times are slightly longer because world layer generation (planet mesh vertex baking and terrain rendering) occurs during the initial load progress bar rather than being deferred. Total load time across the session is unchanged or faster due to concurrent pipeline scheduling.

---

## Building and Testing

Prerequisites: .NET 10.0 SDK and RimWorld 1.7 managed assemblies in `/root/rimworld_libs` (or custom `-p:GameManagedDir=...`).

### Build Mod
```bash
dotnet build EagerWorldLoad.csproj -c Release
```
Outputs assembly to `bin/Release/EagerWorldLoad.dll` and copies to `1.7/Assemblies/EagerWorldLoad.dll`.

### Run Test Suite
```bash
dotnet run --project tests/EagerWorldLoad.Tests.csproj -c Release
```
The test suite validates:
- Target method `Verse.Game.LoadGame` resolves.
- Harmony postfix is registered on `Verse.Game.LoadGame`.
- `WorldRenderer.RegenerateLayersIfDirtyInLongEvent` and `ShouldRegenerateDirtyLayersInLongEvent` resolve.
- Guard conditions handle null references and uninitialized world state safely.
- Safe execution in standalone environments.
