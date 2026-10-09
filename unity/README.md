# Last Match (Unity)

Native port of the reverse match-3 prototype for Android and iOS. Everything is
generated in code: sprites, sounds, UI and the 250-level campaign, so the
project has no art or audio assets to maintain.

## Open it

Unity 6000.4.0f1 (or newer Unity 6). Open the `unity` folder from Unity Hub.
On first open, run **Last Match > Set up project** from the menu bar once. It
creates `Assets/LastMatch/Scenes/Main.unity`, registers it in Build Settings
and applies portrait orientation plus the Android and iOS player settings.
Then press Play.

The same step from a terminal, without opening the editor:

```
Unity.exe -batchmode -quit -projectPath <path>\unity -executeMethod LastMatch.EditorTools.ProjectSetup.Setup
```

## Layout

| Folder | What it holds |
| --- | --- |
| `Assets/LastMatch/Scripts/Core` | Engine-free game logic: `Board` (rules, specials, gravity), `LevelGen` (tiers and the deterministic level generator), `GameSim` (the round: AI, player, power-ups, timers). |
| `Assets/LastMatch/Scripts/View` | Unity side: `GameController` (board drawing, HUD, menus, input), `GemView`, `SpriteFactory` (procedural sprites), `ProcAudio` (synthesized sounds), `UiKit`, `SaveData`. |
| `Assets/LastMatch/Editor` | `ProjectSetup`, the scene and player-settings builder. |
| `Assets/LastMatch/Tests/EditMode` | NUnit tests over the core: every level playable, tier boundaries, blaster and gravity rules, and a headless simulation of a shielded player across all tiers. |

## Tests

Window > General > Test Runner > EditMode > Run All, or from a terminal:

```
Unity.exe -batchmode -nographics -projectPath <path>\unity -runTests -testPlatform EditMode -testResults results.xml
```

## Build

Android: File > Build Settings > Android > Switch Platform, then Build. The
setup script already selects IL2CPP and ARM64 with minimum API 24.
iOS: switch to iOS, Build, then open the generated Xcode project on a Mac.

## Tuning

Pacing lives in `Tuning` (top of `GameSim.cs`): `AiSpeed` is the single global
multiplier, the rest are the base timings in seconds. Tier boundaries and
mechanics are the `Tiers` table in `LevelGen.cs`; each tier has a `From` level.
