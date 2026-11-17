# Insanity Interactive Reloaded: Technical Audit (Phase 1)

**Basis:** branch `audit/phase-1` at commit `aa971d7`, synthesised on 2026-11-17 from six read-only auditor reports in `.superpowers/audit/`.

**Sources**, cited in square brackets:

| Tag | Report |
|---|---|
| [01] | Architecture and build |
| [02] | Combat and weapons |
| [03] | Animation, rendering and physics |
| [04] | AI, modes and levels |
| [05] | Systems, performance and QA |
| [06] | Assets and licensing |

**Companion documents:**
- `docs/FEATURE_TRACKER.md`: the status of every feature ID.
- `docs/ASSET_REQUIREMENTS.md`: asset and licence detail.

**How to read the evidence:**
- Paths are relative to `src/MadnessInteractiveReloaded/` unless they start with `src/`, `base/` or "engine".
- "Engine" means the Walgelijk source on GitHub `master`. It may be newer than the shipped 0.30.0, but where an auditor checked the shipped binary as well, the text says so.
- **Verified** means an auditor read the code. **Inferred** means reasoned from the code but not traced or run. **Measured** means a number someone actually observed. **Suspected** means an expected cost that nobody measured.
- Only auditor [01] built the solution and ran the tests. **Nobody launched or played the game in Phase 1.**

---

## At a glance: what the game is built with

| Aspect | What it is | Source |
|---|---|---|
| **Language and runtime** | C# on .NET 9 (`net9.0`, SDK 9.0.321). Nullable reference types are on. The assembly is `IIR` version `0.51.1`. It is an `Exe` in Debug and a `WinExe` in Release. The build is framework-dependent, so players need the .NET 9 Desktop Runtime. | [01] §2.1, [05] §7 |
| **Engine** | **Walgelijk 0.30.0**, a C# 2D engine (source at `mestiez/Walgelijk`). It provides the game loop, ECS (entities, components, systems), render queue, input, audio abstraction, routines, console and logging. | [01] §2.4b |
| **Engine packages** | Walgelijk.OpenTK 0.24.0 (window, OpenGL renderer, OpenAL audio) · Walgelijk.OpenTK.MotionTK 1.2.0 (video) · Walgelijk.SimpleDrawing 1.27.0 (immediate-mode drawing and text) · Walgelijk.Onion 0.6.0 (immediate-mode UI) · Walgelijk.AssetManager 0.17.0 (`.waa` packages) · Walgelijk.CommonAssetDeserialisers 0.9.0 (OGG, QOA, WAV, `.wf` fonts) · Walgelijk.Localisation 1.2.0 · Walgelijk.Physics 0.4.0 · Walgelijk.ParticleSystem 0.3.0 · Walgelijk.PortAudio 0.5.0 (**referenced but never used**) | [01] §2.4b |
| **Rendering pipeline** | OpenGL 3.3 core through OpenTK 4.8.2 and GLFW. Almost everything is drawn in **immediate mode** with SimpleDrawing (`Draw.*`, 1,578 calls), which batches into the engine `RenderQueue`, sorted by layer and order (`Graphics/RenderOrders.cs`). There are 22 GLSL shaders in `base/shaders`, all `#version 330`. Off-screen targets: the 1024² background buffer, the corpse stamp canvas, thumbnails and the DJ screen. The HDR compositor is configured but **disabled**. There is no post-processing chain and no lighting system. | [01] §2.4, [04] §2.12, [05] §7 |
| **Physics** | Walgelijk.Physics is used **only for queries** (raycast, point and circle queries against bitmask-filtered colliders, at 30 updates/s). The engine does no collision response. Bullets are instant hitscan rays. Thrown weapons are swept point rays in `VelocitySystem` (`FixedUpdate`). Characters are **kinematic** on a floor polyline, with no gravity. Ragdolls use a **custom 200 Hz Verlet solver** written in the game. | [01] §2.2, [02] §2.3, [03] §2.7-2.8 |
| **Animation architecture** | A hybrid. Characters are **rigid cut-out sprites**: 7 parts (head, body, 2 hands, 2 feet) plus 5 apparel overlays. They are posed **procedurally** every frame by `CharacterPositionSystem`. **Keyframe clips** in a custom text format (`.anim`, 192 clips) add whole-body offsets on top, with a two-clip crossfade and gameplay constraint flags. There is no skeleton, no frame-by-frame character animation and no masked layers. Flipbooks are used only for effects. | [03] §2.1-2.4 |
| **Asset formats** | All of `base/` (2,491 packed assets, 679 MB) is packed into one `resources/base.waa` (708 MB) by running the game itself (`IIR --mode pack`). Formats: Newtonsoft **JSON** (weapons, levels, cutscenes, campaigns, factions, waves, locale); custom line-based **text** formats (`.armor`, `.hand`, `.look`, `.stats`, `.preset`, `.seq`, `.anim`); **PNG** (BigGustave decoder) and **QOI** (backgrounds, made by `src/mirbgencoder`); **WAV/OGG** audio (QOA supported); **`.wf`** fonts (a zip with an MSDF atlas); **MP4** H.264 video through FFmpeg 5.1.2. | [01] §2.5, [06] §2.1 |
| **UI library** | **Walgelijk.Onion 0.6.0**, an immediate-mode UI (1,690 `Ui.*` calls). Menus, editors and HUD widgets use it. Custom controls are in `User interface/Onion/Controls/`. | [01] §2.4, [05] §2.3 |
| **Audio backend** | **OpenAL Soft 1.23.1** (`openal32.dll`) through `OpenALAudioRenderer` in Walgelijk.OpenTK, with 128 temporary sources. Three tracks: Music, SoundEffects and UserInterface. **No positional audio.** | [05] §2.1, [06] §2.7 |
| **Input** | Walgelijk input, mapped through the game's `ControlScheme` (12 `GameAction`s). XInput controller support through P/Invoke, with menu navigation, rebinding and rumble. | [05] §2.2 |
| **Scripting and modding** | Levels can embed C# compiled at run time by Roslyn (`Microsoft.CodeAnalysis.CSharp.Scripting 4.12.0`). Data mods are `.waa` packages. Script mods are .NET assemblies that patch the game with Harmony 2.3.3. | [01] §2.7 |
| **Saves** | Separate JSON and text files under `%LOCALAPPDATA%\IIR\userdata\`. No version field and no atomic writes. | [01] §2.6, [05] §2.6 |
| **Platforms** | Windows 10/11 x64 (XInput, Win32 cursor warper, Windows FFmpeg, OpenAL and `nfd` DLLs). Linux is partial and untested. | [05] §7 |

---

## Executive summary

1. **The code base is healthy enough to build on.**
   - The Debug build succeeds with **0 errors and 110 warnings** in 13 s (warm build). **190 of 191 tests pass.**
   - The one failure is a test bug (BUG-75), not a game bug, and the fix is one line.
   - There is no CI. [01]
2. **The biggest release blocker is legal, not technical.** [06]
   - The Madness characters, factions, looks and name belong to Krinkels, and "MADNESS: Project Nexus" is a trademark of GIBBING TREE, LLC.
   - None of the 44 music tracks has a licence.
   - At least 14 sound effects come from commercial libraries.
   - Two fonts (Impact, Toxigenesis) can't ship in a game.
   - The FFmpeg DLLs are a GPL build shipped without a source offer.
   - **As it stands the game cannot be sold.** Settle the IP route before writing more content.
3. **One core runs at 100% the whole time.**
   - The engine's frame limiter busy-waits. The default cap is 300 fps, VSync is off and the process runs at High priority.
   - This was verified in the code and in the shipped 0.30.0 IL. The CPU load itself is inferred, not measured.
   - About a 50-line fix, with no engine fork (BUG-01). [01][05]
4. **Saves are fragile.** [01][05]
   - There are no version fields and no atomic writes.
   - Nothing is saved if the game crashes. The player look and arena data are saved only on a clean exit.
   - `arena.json` is wiped at every exit.
   - A corrupt campaign file makes the campaign throw every frame (BUG-02 to BUG-10).
   - Every progression feature depends on fixing this.
5. **Weapons are already data-driven.** [02]
   - One JSON schema with 37 fields, one generic `WeaponSystem`, 120 weapons (82 firearms, 38 melee) and no per-weapon classes.
   - Missing data hooks: reload, impact sounds and effects, spread patterns, rotating parts.
6. **There is no reload, by design.** You throw the empty gun and grab another. [02]
   - Reload, dual-wielding, smoothing the hop movement and hit stop all change MIR's core feel.
   - The auditors flagged each of these for Zeph's decision.
7. **Combat accuracy and responsiveness have clear gaps.** [02][03]
   - Bullets are hitscan rays that march in 10 px steps.
   - Melee is **one ray along the cursor** that ignores where the animated weapon actually is.
   - Only melee combos buffer input; other presses made during a cooldown are lost.
   - Four cheap correctness bugs exist (BUG-22, BUG-23, BUG-25, BUG-44).
8. **The animation system is a solid but limited base.** [03]
   - It has no idle or breathing motion (standing characters are frozen), no run, no masked layers and no foot channel, so kicks are impossible.
   - It has two blend bugs.
   - [03] proposes an incremental, backwards-compatible architecture: layers, procedural modifiers and a motion-profile data file whose defaults reproduce today's output exactly.
9. **Mass kills break ragdolls and decals.** [03]
   - The Verlet solver uses static 256-entry buffers that silently truncate, so from the 15th simultaneous ragdoll on, bodies fly apart.
   - Past 512 decals per renderer, new blood and bullet holes are silently dropped.
   - Wave modes will hit both (BUG-47, BUG-48).
10. **The enemy AI is one 718-line ad hoc system, and it cheats.** [04]
    - It knows where every enemy is and aims through walls.
    - It has no state machine, perception or navigation, and every archetype runs the same code.
    - There is one bespoke boss (Rich).
11. **Modes are uneven.** [04]
    - Campaign, Incident, Experiment and the tutorial work.
    - Arena is a stub.
    - There are no waves and no scoring, and objectives exist only in Campaign.
    - Cached level data is mutated at run time, which breaks two Improbability disks and Incident mode (BUG-61 to BUG-63).
12. **A shared level file can run arbitrary code.** [01][04]
    - Three routes: `TypeNameHandling.Auto` with no binder, `GameSystem` objects that add any system type, and full-trust Roslyn `LevelScript`s.
    - This must be fixed before players can share levels (BUG-87).
    - One shipped script (the boss music cue) no longer compiles (BUG-64).
13. **Replays, verifiable daily challenges and leaderboards need groundwork.** [01][05]
    - Gameplay runs on a variable timestep: 96 `Update` systems against 8 `FixedUpdate`.
    - The engine RNG is unseeded and is called from about 200 places.
    - These features need fixed-step gameplay and a seeded RNG first (ARCH-DET).
14. **Audio is non-spatial.** [05]
    - The listener and spatial multiplier are zeroed every frame, so distant gunfire is as loud as the player's own gun.
    - A suspected engine bug leaks OpenAL sources past 128 overlapping one-shots, after which sounds can stop.
15. **Controller support is real but Windows-only.** [05][01]
    - It is XInput, first pad only, with about 139 passing unit tests.
    - Gaps: pad-only players can't collect Improbability disks (BUG-71), and there are no button glyphs, deadzone settings or Linux path.
16. **Release hygiene items are cheap but visible.** [05][01]
    - Every scene shows the upstream banner "BETA BUILD … our Discord".
    - The F12 cheat console works in release builds (inferred).
    - The build needs the .NET runtime.
    - There is no UI scale, flash toggle or FPS overlay, and the built-in benchmark crashes the game.
17. **Mods and editors exist but are developer-grade.** [01]
    - Mods fail silently: a mod without `thumb.png` never activates, and one bad zip stops every later mod.
    - The level, weapon and armour editors are reachable only from the F12 console.
    - The level editor's redo is broken.

---

## 1. Project structure and architecture

### 1.1 Solution layout [01 §2.1]

`src/MIR.sln` has 5 projects:

| Project | Notes |
|---|---|
| `MadnessInteractiveReloaded/MIR.csproj` | The game. 484 `.cs` files, 50,343 lines. |
| `MadnessInteractiveReloaded.Test/MIR.Test.csproj` | xUnit 2.9.2 and FluentAssertions. 31 files, 5,528 lines. |
| `mirbgencoder` | The QOI background encoder tool. Uses ImageSharp 3.1.6. |
| `src/Harmony` | Git submodule, Harmony 2.3.3. Built for 13 target frameworks every time, which is most of a clean build's cost. |
| `src/BigGustave` | Git submodule, PNG decoder. |

`src/Directory.Build.targets` makes `dotnet build src/MIR.sln` work from the command line.

The largest game folders by line count:

| Folder | Lines |
|---|---|
| `Character/` | 9,072 |
| `Shared/` | 7,023 |
| `Level/` | 6,874 |
| `Level editor/` | 5,692 |
| `User interface/` | 5,500 |
| `Scenes/` | 2,336 |
| `Weapons/` | 1,855 |

**Build steps that run the game.** After each build, `MIR.csproj` runs `IIR --mode pack` to rebuild `resources/base.waa` (708 MB), and the test project packs it again. There is no incremental pack.

**Native binaries committed in the game folder:**
- FFmpeg 5: `avcodec-59`, `avformat-59`, `avutil-57`, `swresample-4`, `swscale-6`, `avdevice-59`. `avdevice-59` is not in the copy-to-output list.
- `openal32.dll`
- `nfd.dll` and `libnfd.so` (native file dialog)

**CI:** none. `.github/` holds only issue templates.

### 1.2 Bootstrap and game loop [01 §2.2, 05 §2.4]

**Start-up.** `Program.Main` does the following in order:
1. Registers `LegacyAssemblyRedirect`, which maps `MIR` type names to `IIR`.
2. Handles `--mode pack`.
3. Inserts the BigGustave PNG decoder.
4. Constructs `MadnessInteractiveReloaded`, which runs the whole game inside its constructor (`MadnessInteractiveReloaded.cs:118-291`).

Inside that constructor:
- The process is set to **High priority** (`:138-140`).
- `OpenALAudioRenderer` is created (`:151`).
- The MIR→IIR AppData copy runs (`:161-164`).
- `UpdateRate = 144`, `FixedUpdateRate = 60` and `VSync = false` are set (`:181-183`).
- Deserialisers and packages are registered (`:30-116`).
- The compositor is set to HDR and then disabled (`:253-254`).
- `Game.Start()` runs (`:260`) and blocks until the window closes.
- **Saves happen only after `Game.Start()` returns** (`:272-286`).

**One engine frame** (engine `Game.cs:244-317`):
1. Audio, console, and `AdditionalLoopEvents` (which include the controller layer).
2. If the console is closed: fixed steps with an accumulator, capped at 10 per frame and never clamped afterwards. Then `UpdateSystems`, then routines.
3. Render systems, render queue and compositor. Then `Window.LoopCycle`.
4. **A busy-wait limiter:** `while (clock.Elapsed < expected) {}`. `UpdateRate = 0` skips it.

**Rates actually in use.** `Settings.Apply` overrides `UpdateRate` with `Video.FramerateCap`, whose **default is 300** (`Shared/Settings.cs:53,111`). VSync defaults to off (`Settings.cs:54`).

**Most gameplay runs in the variable `Update`.** Only 8 game systems override `FixedUpdate`:
- `VelocitySystem`, `TurretSystem`, `LevelScriptSystem`, `CampaignStatsTimerSystem`, `ElevatorLevelSystem`, `PlayerDeathSequenceSystem`, `CharacterAbilitySystem` and `TelekinesisAbilityComponent`.

AI, movement, positioning, weapons and melee all integrate `Time.DeltaTime` once per rendered frame. Most of them use `Utilities.SmoothApproach`, which is frame-rate independent. Verlet ragdolls sub-step inside `Update`.

**Consequence:** the simulation is not deterministic. This blocks input-replay (G74), verifiable seeded dailies (D46) and fair leaderboards (G78) [01][05].

**`TimeScale` has no owner.** It is assigned in 26 places across 22 files, including inside the bullet ray code (`Weapons/Systems/BulletEmitter.cs:178-181`). Two slow-motion effects can overwrite each other (BUG-38).

### 1.3 Scenes, ECS and system registration [01 §2.3, 2.10]

- **Walgelijk ECS.**
  - A `Scene` holds entities, components (classes) and systems.
  - Systems run in **order of addition**. Only one game system sets `ExecutionOrder` (`BackgroundBufferSystem`, `SceneUtils.cs:197`).
  - A scene can hold only one system of each type.
  - The game has 101 system classes and 81 component classes.
- **Registration is manual and hard-coded** in `Scenes/SceneUtils.cs`.
  - `PrepareMenuScene` adds about 34 systems (`:23-100`). `PrepareGameScene` adds 61 systems in a fixed order (`:188-258`).
  - The order is implicit knowledge: for example, the player and AI systems run in opposite orders in menu and game scenes.
  - Level-specific systems get in through `GameSystem` level objects, which instantiate any system by type name.
- **Global state lives in statics.**
  - There are about 81 mutable static fields, for example `Level.CurrentLevel`, `AiCharacterSystem.DisableAI/AutoSpawn` and `IncidentSystem.CurrentConfig`.
  - Service-locator use: `Game.Main` 136 times, `Registries.*` 327 references, `UserData.Instances` 32, `SoundCache.Instance` 46.
  - Tests must reset statics by hand, and the flaky test is exactly this leaking (BUG-75).
- **Coupling hotspots:**
  - `Shared/MadnessUtils.cs`: 1,136 lines, referenced from 97 files.
  - `Prefabs/Prefabs.cs`: 1,102 lines. Every entity is built here in code, not from data templates.
  - `Scenes/SceneUtils.cs`: every mode passes through it.
  - Content IDs are hard-coded strings (`"grunt"`, `"employee_of_the_month"`, `"dbg_room"`), so renaming data breaks code at run time (BUG-20).
- **Two of everything coexist:** two asset loaders (legacy `Resources` and `Assets`), two serialisers (Newtonsoft and the custom line format) and two kinds of ID (registry string keys and `AssetId` paths).
- **Code markers:** 169 `TODO`s, and no `FIXME` or `HACK`. The most important are listed in [01] §2.10.

**What this means for new features.** Every new mode, and every mod hook, currently means editing `SceneUtils.cs` and `Prefabs.cs`. [01] proposes data-driven "mode definitions" (a system list with explicit order) and game events such as "level loaded" and "character killed".

### 1.4 Saves and persistence [01 §2.6, 05 §2.6]

All user data lives under `%LOCALAPPDATA%\IIR\userdata\` (`Shared/UserData.cs:15`). Logs go to `%LOCALAPPDATA%\IIR\logs`.

| File | Format | Written | Read | Version / atomic |
|---|---|---|---|---|
| `settings.json` | Newtonsoft | On every `Settings.Apply`, through a background `Task.Run` (`Settings.cs:129`); also at exit, without the lock | In the last loading step; a parse error falls back to defaults | No / no |
| `player.look` | `.look` text | **Only at clean exit** (`MadnessInteractiveReloaded.cs:273`) | `GameLoadingSteps.cs:81-99` | Writes a version header that is never read / no |
| `campaigns/<id>.json` | Newtonsoft (`CampaignStats`) | At level completion and at exit | `Registries.cs:127-142`; a corrupt file is skipped, then the campaign crashes (BUG-02) | No / no |
| `disks.txt` | One ID per line | On unlock and at exit | Loading step | No / no |
| `experiment/character_presets/*.preset` | Text | From the Experiment context menu | Registries | No / no |
| `arena.json` | Newtonsoft | At exit only | **Never**: the load is commented out (`GameLoadingScene.cs:45`), so each exit overwrites it with an empty default | No / no |

**Problems:**
- **No atomic writes.** There are 14 write sites and no temp-file-then-`File.Replace` anywhere.
- **No version or migration.** The text formats write a version line but `BaseDeserialiser.cs:19-23` never compares it.
- **No save on crash.**
- **Two meanings for one version number.** `GameVersion` (0.51.1) is used both as the save-file version and as the "based on MIR v…" credit (`Shared/Branding.cs:22`). The README and `Branding.DisplayVersion` say 0.1.0 (BUG-10).

**The MIR→IIR migration** (`Shared/LegacyDataMigration.cs:33-70`) copies `%LOCALAPPDATA%\MIR` when the IIR folder is missing or empty.
- A copy that fails half-way is never retried (BUG-08).
- The older `./userdata` migration is dead code (BUG-09).
- `LegacyAssemblyRedirect` keeps old `"$type": "X, MIR"` level and cutscene data loading. Levels saved from now on contain `, IIR`, so they will not load in upstream MIR (inferred).

**Cosmetics and gameplay are not separated in the data model.** Looks (`.look`) and stats (`.stats`) are separate files. But armour pieces in a look carry gameplay values (`DeflectChance`, `Detachable`, `ProceduralDamageScale`; `ArmourPiece.cs:62-82`), and the look's deflect chance feeds bullet deflection (`CharacterLook.cs:174-205`).

Does this affect the player in practice? Probably not. [02] lists "a non-cosmetic look" as a condition for deflection, and [01] notes that the player's look is created with `Cosmetic = true` (`GameLoadingSteps.cs:98`). So the player's chosen armour probably doesn't deflect bullets. This is a synthesis inference that nobody traced end to end; [05] asked for this check.

The coupling still matters for future cosmetics (F64) and armour sets (F63). [05] recommends splitting `CharacterLook` into a visual part and gameplay modifiers.

### 1.5 Modding and scripting [01 §2.7]

- **What a mod is.** A folder or `.zip` holding `meta.json`, an optional `thumb.png`, `.waa` packages and optionally one .NET assembly. Mods are found in `%LOCALAPPDATA%\IIR\mods\` and `.\mods\`.
- **Data mods** register their packages before the registries load.
  - Later registrations replace earlier ones (`Registry.cs:47-53`), so a mod can override weapons, levels, looks, armour, animations, campaigns and languages.
  - It **cannot** override the 99 textures in `Assets/Textures.cs`, which bind to `base` before mods load (inferred; BUG-92).
- **Script mods** are loaded with `Assembly.LoadFrom`. The game calls one `IModEntry.OnLoad(mod, harmony)`, then `OnReady()`.
  - There is no API beyond Harmony patching.
  - The hooks run on a thread-pool thread.
  - Mods are unsandboxed .NET code with full user rights, so the mod menu needs a warning.
- **Silent failures** (BUG-90 to BUG-93):
  - A mod without `thumb.png` never activates.
  - A broken or duplicate zip, or a colliding package ID, aborts every later mod, and the exception is never logged.
  - Dependencies are parsed but ignored, and `OnUnload` is never called.
  - Every mod is re-extracted to `%TEMP%` at each launch.
- **There are no modding docs and no in-game packer.** Building a `.waa` needs `IIR --mode pack` from a command line.
- **Level scripting (G80).** A `LevelScript` object stores C# source in the level JSON. Roslyn compiles it on a background task, with full trust (`Level/BuiltLevelScript.cs:55-124`). Five shipped levels use it.
  - A script that fails to compile is rebuilt **every frame** (BUG-15).
  - `End` is never invoked.
  - Instance fields persist across retries, because built scripts are cached by source text (BUG-17).
  - The boss-battle music script no longer compiles (BUG-64).
- **The console and convars.**
  - The engine's `DebugConsole` opens with **F12 in every build**, and opening it freezes the simulation.
  - There are 37 commands, including god mode, give weapon, time scale, unlock disk, and the level, weapon and armour editors.
  - `ConVars` are gameplay tuning values from `base/data/convars.txt`.
  - Cheats one key away are fine for single-player, but achievements, leaderboards and speedruns need a cheat flag (BUG-98).

### 1.6 Editors [01 §2.9, 03 §2.10, 04 §2.14]

| Editor | How to reach it | Saves | Notes |
|---|---|---|---|
| Level editor (41 files, 5,692 lines) | Console `LevelEditor` only | Loose JSON through a native file dialog | Palette of 20 placeable types ([04]); 22 `LevelObject` classes including 3 obsolete ([01]). Property panels, snapshot undo, test play. **Redo is broken and undo memory is unbounded** (BUG-94). The `GameSystem` dropdown lists every system (BUG-88). |
| Weapon editor (1,133 lines) | Console `WeaponEditor` | JSON | Hold points, barrel, casings, sounds, animated parts, test scene |
| Armour editor (473 lines) | Console `ArmourEditor` | `.armor` | Can't save hand armour (BUG-95) |
| Character creation (1,045 lines) | Main menu | `player.look` at exit; `.look` import and export | The only editor players are meant to see |
| Animation viewer (`anim` command) | Console | — | A viewer and debugger with scrubbing and curve graphs. It cannot edit clips. They were authored outside the game, apparently in Blender: a code comment mentions "Koof … his blender file" (`CharacterPositionSystem.cs:552`). **There is no exporter in the repo.** |

**There is no path from the editor into the game for players.** Levels are listed only from asset packages. A saved level can be played only through the editor's test mode, or after packing it into a mod.

---

## 2. Existing gameplay systems (overview)

This section is a map of the main gameplay loop and where each part lives. Sections 3-11 go into detail.

| System | Where | In one line |
|---|---|---|
| Player control | `Character/Systems/PlayerCharacterSystem.cs` | Reads `ControlScheme` actions. Aim is the raw world cursor. Sets walk, fire, block, throw, pickup and jump-dodge. |
| AI control | `Character/Systems/AiCharacterSystem.cs` (718 lines) | A "virtual controller" that writes the same "wants" flags. Omniscient targeting. |
| Movement | `Character/Systems/CharacterMovementSystem.cs` | MIR's discrete **hop** model: each hop lerps X with `Quad.InOut` over about 0.33 s. A new direction press cancels the hop instantly. |
| Posing | `Character/Systems/CharacterPositionSystem.cs` (738 lines) | Places the head, body, hands and feet every frame. Mixes in animation clips, recoil and impact offsets. |
| Firearms | `Weapons/Systems/WeaponSystem.cs`, `BulletEmitter.cs` | Data-driven firing, hitscan bullets with up to 8 recursive casts (for penetration, deflection and dodges), armour deflection rolls. |
| Melee | `Character/Systems/MeleeSequenceSystem.cs`, `Character/MeleeUtils.cs` | `.seq` combos; a single-ray hit test at hit frames; a block, parry and stun matrix. |
| Throwing and pickup | `CharacterUtilities.cs`, `Physics/Systems/VelocitySystem.cs` | No inventory. Throw the weapon: sharp boxes stab and stick in bodies. |
| Dodge | `CharacterSystem.cs`, `JumpDodgeSystem.cs`, `BulletEmitter.cs` | A dodge meter is the core defensive resource. Bullets drain it and play dodge clips automatically. The jump-dodge has keyframed i-frames. The meter regenerates **only while moving**. |
| Health | `BodyPartComponent`, `CharacterUtilities.cs:22-41` | Two pools, head and body. Either reaching 0 kills. Defaults are head 0.2 and body 0.3, against weapon damage of 0.07-10. |
| Abilities | `Character/Components/Abilities/*` | Holy Shield, Telekinesis, Teleport Dash, Flying and Spawn Menu. **Player only**: the AI can't use abilities. |
| Improbability disks | `Improbability/ImprobabilityDisks.cs` | 17 modifier disks (god mode, infinite ammo, class stats, more or fewer enemies…), unlocked by in-level pickups and saved in `disks.txt`. |
| Spawning | `Level/Systems/EnemySpawningSystem.cs` | One spawner shared by all modes. Uniform random pick from a list; no waves. |
| Progress | `Level/Systems/LevelProgressSystem.cs` | Campaign only. Win types: BodyCount, Time, Always, Explicit. |
| Ragdolls and gore | `Physics/Systems/VerletPhysicsSystem.cs`, `base/shaders/bodypart.frag` | Death-only Verlet ragdolls. Shader-driven holes and slashes; corpses stamped into the background. |
| Camera | `Camera/Systems/CameraMovementSystem.cs`, `CameraShakeSystem.cs` | Weighted targets, clamped to level bounds. Vertical-only shake that is not capped. |

---

## 3. Animation capabilities [03]

### 3.1 Clip format and inventory

- **The `.anim` format** is line-based text, parsed by `Animation/AnimationDeserialiser.cs:57-331`.
  - Headers: `fps` (default 24), `speed`, `scale`, `smooth` (parsed but ignored, BUG-50), `relative` and `#` comments.
  - Blocks: `BODY`, `HEAD` and a repeatable `HAND`.
  - Each keyframe line can set position, rotation, hand look, scale and `constraints(...)`. Each channel is optional per line, so curves are sparse.
  - Times are normalised to 0-1. Constraints hold until the next constraint key.
- **There are 192 clips in 16 folders.** The main ones are `melee` 71, `deaths` 44, `cutscenes` 20, `jumps` 13 and `stun` 11.
  - There is only one walk clip (`zed_walk`), only 2 dodge clips and only 2 throw clips.
  - A clip's folder name is its group, and its file name is its registry key. Code looks clips up 113 times and checks groups 14 times, so these names are load-bearing.
- **Parser caveats:**
  - `scale(...)` parsing depends on the system's culture (BUG-53).
  - A block with a single keyframe at t=0 yields NaN key times (inferred, BUG-54).
  - Angle keys lerp linearly, so a pair crossing ±180° spins the long way (BUG-52). Keep this as it is, because changing it would alter existing clips.

### 3.2 Playback and blending

- `PlayAnimation` appends to `character.Animations`. The last clip is the "main" one.
- **Each frame** (`CharacterPositionSystem.PrepareAnimation`, `:331-397`):
  - the last two clips crossfade with `Quad.InOut` over a fixed **0.3125 s**;
  - the procedural pose blends to the clip pose over a fixed **0.2 s**;
  - finished clips fire `OnEnd`.
- **Gameplay constraints come from clips.** `AnimationConstraint` is a 16-flag bitmask (for example `PreventDodge`, `PreventBeingShot`, `PreventWalking`). Gameplay reads it through `AnimationConstrainsAny`. I-frames are data in the clip files.
- **Blend bugs:**
  - Hand 2's scale crossfades from hand 1's scale (`CharacterUtilities.cs:476`, BUG-44).
  - Old clips are never removed, so their constraints linger until they end, and a queue of 3 or more clips pops (`AnimatedCharacterExtensions.cs:85-93`, BUG-45).
  - Hands blend out from a world position, so they trail behind a moving character (BUG-60).
- **There are no layers.** No masked layers (an upper body or arms clip over walking) and no additive layers. All 192 clips define BODY, HEAD and 2 HANDs.

### 3.3 Procedural motion

- **Locomotion is the Madness hop.**
  - Hop distance is `WalkAcceleration.X × rand(0.6..1.2) × HopAcceleration` and height is `rand(20,40)`.
  - Feet trace circles at 18 rad/s divided by scale, and the head bobs on fractal noise.
  - There is no stride cycle, no run tier and no turn animation. Flipping is instant and swaps hand-drawn Left and Right textures.
- **Aim.** Aiming drives the hand target and a torso lean of `8 + dirY × 15` degrees. The head turns only 5% toward the aim (`:447`).
- **Recoil.** It is a spring fed by weapon data (`WeaponData.cs:99-105`). The response constants are hardcoded.
- **Idle and breathing: none.** In gameplay a standing character is fully static. A grep for `breath|idle` finds only the menu clip. The only exceptions are `Look.Jitter` looks and the tired-dodge hand tremble.
- **Pickup** is the best existing example of "a clip for the body, a procedural target for the hand" (`CharacterPickupSystem.cs:37-60`).
- **Every procedural constant is code.** [03] §2.6 lists about 30 of them. The `ConVars` data mechanism holds none.

### 3.4 Capability matrix

| Capability | State | Evidence |
|---|---|---|
| Walk | Hop plus circular feet | `CharacterMovementSystem.cs:44-159` |
| Run | **None** (one `TopWalkSpeed = 500`) | `CharacterPositioning.cs:35` |
| Idle and breathing | **None** | [03] §2.4 |
| Aim | Procedural, solid | `PlayerCharacterSystem.cs:52-103` |
| Recoil | Procedural spring, data-driven input | `CharacterPositionSystem.cs:304-318,590-662` |
| Reload | **None** (no mechanic) | [02] §2.2 |
| Punch | 26 clips, 7 unarmed `.seq` files | `base/data/melee_sequences/` |
| Kick | **Impossible**: feet have no animation channel | `FootLimb.cs`; `AnimationDeserialiser.cs:20-22` |
| Melee swings | 34 swing clips; hit timing drifts (BUG-46) | `MeleeSequenceSystem.cs:42-44` |
| Dodge | 2 dodge clips plus 13 jump-dodge clips by agility tier | `CharacterUtilities.cs:318-425` |
| Slide | **None** | — |
| Jump and land | Baked clip offsets over a floor-locked centre; no vertical physics | `JumpDodgeSystem.cs:31-48` |
| Hit reactions | Additive impact offset, front/back light stuns only; **the player never flinches** | `BulletEmitter.cs:312-323,405-413`; `Prefabs.cs:952` |
| Knockdown | Baked `stun_heavy_*` fall-and-get-up (about 2.2 s). NPCs only, flat ground only; triggered only by thrown hits and unarmed finishers | `CharacterUtilities.cs:286-312` |
| Death | 44 clips chosen by hit region and facing, otherwise a ragdoll | `CharacterUtilities.cs:184-284` |
| Pickup and throw | Clip plus procedural reach; throw releases at 50% | `CharacterPickupSystem.cs:10-60`; `CharacterUtilities.cs:638-724` |

### 3.5 Recommended architecture: extend, don't replace [03 §7]

The guiding rule: **with no new data present, the output must be bit-for-bit what it is today.** Pin that with golden tests first. The proposal has five parts:

1. **Per-limb `Procedural*` slots and `IProceduralModifier`s,** evaluated after `PrepareAnimation`. The modifiers would include breathing, idle sway, stride, a hit spring, recoil (today's maths moved unchanged) and look-at.
2. **A `ClipLayerStack`** that extends `CalculateMixedAnimation`. Base is today's crossfade, with BUG-44 fixed and capped at 2 clips (which fixes BUG-45). Masked layers (UpperBody, Arms) and additive layers go on top.
3. **Optional `.anim` headers:** `mask`, `blend_in/out`, `mix`, `additive`, `events(name@frame…)` and `angle_wrap`, plus a `FOOT` block. Store `fps` per clip, so hit frames can use real frame times (fixes BUG-46).
4. **A `CharacterMotionProfile` data file** (`base/data/motion/*.motion`) whose `default.motion` holds today's constants. A unit test asserts that they equal the old literals.
5. **A knockdown state machine** with an optional *temporary live ragdoll*. It needs `TurnIntoRagdoll` split into build and recover steps, and BUG-47 fixed first.

These must stay compatible:
- the `.anim`, `.seq`, `.look` and `.armor` grammars
- the folder-as-group naming
- the Left/Right swap on flip
- the render-order constants
- the `HandPoseFunctionOverride` mod hook
- `AnimationTest.cs`

---

## 4. Character rendering and sprite composition [03 §2.1, 2.9]

### 4.1 Assembly

`Prefabs.CreateCharacter` (`Prefabs/Prefabs.cs:633-855`) builds one character from several entities:

| Part | Components |
|---|---|
| Root | `CharacterComponent` and related components |
| Body and head | `BodyPartShapeComponent` (a quad with the gore shader), a `RectangleCollider` (the head's is scaled 0.8), and health |
| 2 hands and 2 feet | Plain sprites with **no colliders** |
| 3 head and 2 body apparel overlays | `ApparelSpriteComponent` with the hole shader, locked to their parent with `TransformConstraintComponent` |

### 4.2 Draw order, flip, scale, tint

- **Draw order.** Each character gets a random layer between 20 and 199. Within it:

  | Order | Part |
  |---|---|
  | -100 | Back hand |
  | 998, 999 | Feet |
  | 2000 | Body |
  | 2100 | Body apparel. Both layers share this value, so their relative order is undefined (BUG-51). |
  | 3000 | Head |
  | 3100-3102 | Head apparel |
  | 4000 | Front hand. The weapon is drawn at the hand's order minus 100. |

  Ragdolls move every sprite to layers 1-20, or to 302 for the player.
- **Flip.** Head, body and feet use a horizontal flip. Hands use a vertical flip. On a look update the Left and Right textures are swapped: they are separate drawings, not mirrors.
- **Scale.** Sprite scale is texture size × `Positioning.Scale` × the piece's `TextureScale`. One texture pixel is one world unit, and the pivot is the texture centre ([06] §2.3).
- **Tint and jitter.** `CharacterComponent.Tint` multiplies every sprite colour. `Look.Jitter` adds noise, plus a random ±180° head snap about every 4 s.
- **What the character is made of:** a hybrid of rigid cut-out sprites, procedural posing and additive keyframe clips. There is no bone hierarchy and there are no frame sequences for characters.

### 4.3 Looks and materials

- **Looks.** A `CharacterLook` holds Head, Body, HeadLayer1-3 and BodyLayer1-2 as `ArmourPiece`s. Each piece has Left/Right textures, offsets, a deflect chance and a broken replacement. A look also has hands, optional feet, flesh and gore textures, a `BloodColour`, `Jitter` and `Cosmetic`.
- **Content.** There are 60 `.look` files and 250 `.armor` pieces (172 head, 78 body; [03] count) plus 10 glove sets.
- **Body-part materials** come from `BodyPartMaterialPool`. Each binds the skin, gore and flesh textures, a slash texture and the blood colours. **Every body part has its own material**, so nothing batches across characters (PERF-07). If the pool runs out, character creation throws "Too many body parts!" (`Prefabs.cs:252`).

### 4.4 Gore and decals

- **Skin gore** (`bodypart.frag`). Each part can hold up to 32 holes, 32 inner cut-outs and 32 slashes, with noisy edges, blood rims and stains tinted by `BloodColour`.
  - Hidden authoring convention: skin pixels with `a < 0.9` and a red bias are read as a blood mask.
- **Apparel gore** (`apparel.frag`): 32 star-edged holes. Broken armour swaps in a replacement on head hits. `Detachable` pieces can be knocked off as props. There is no dismemberment.
- **Decals.** Every bullet hole or blood splat is its own entity. They are drawn with instanced rendering, capped at 512 per renderer. **Past the cap, new decals are silently dropped** while their entities live on (BUG-48).
  - Foreground or background placement uses `GL.ReadPixels` on the background buffer for bullet holes (PERF-04).
- **Stamping.** When a death clip ends, or a ragdoll is deleted before 10 s, the corpse's 11 render tasks are drawn into a level-sized half-resolution render texture. Bodies become permanent background art. The weapon is not stamped.
- **Background buffer.** Every frame the background textures are re-rendered into a 1024² target. Its blue channel marks foreground, which feeds the decal mask and the bullet-hole test.

---

## 5. Weapon handling and combat logic [02]

### 5.1 Data model

`base/data/weapons/*.json` (120 files) deserialises to `WeaponInstructions` plus `WeaponData` (37 fields; `Weapons/WeaponInstructions.cs:11-74`, `WeaponData.cs`). `Prefabs.CreateWeapon` (`Prefabs.cs:522-631`) builds the entity.

The fields cover:
- damage, use delay, burst count, `BulletsPerShot`, `RoundsPerMagazine`
- accuracy (circle spread), recoil, rotational recoil, recoil handling, automatic fire, pump action
- shoot sounds, ejection particle and casing size
- melee: range, damage type (Firearm, Blade, Axe or Blunt) and size, deflect capability, `SpecialMelee`
- throwing: damage, heavy flag, sharp boxes
- animated parts (slide and so on) and hold points (one- or two-handed)
- `AdditionalComponents` and `EnsureSystems`, which add custom behaviour by type name (only `auditor_sword` uses them)

The **weapon census** is in [02] §5. In short:
- **82 firearms:** 12 pistols, 4 revolvers, 5 machine pistols, 25 SMGs, 10 shotguns, 20 rifles, 3 LMGs and 3 fictional weapons.
- **38 melee weapons:** 22 blades, 7 axes and 9 blunt weapons.
- **No grenades, shields or other throwables.**

**Hardcoded per-weapon behaviour that a designer cannot change:**
- casing velocity
- muzzle-flash size
- camera shake (`Damage*4` per pellet)
- the close-range damage bonus
- the pump sound (global)
- the low-ammo warning

**Unused or missing data hooks:**
- `AnimatedWeaponPart.AngleCurve` is parsed but never applied (BUG-31), so rotating parts can't be authored.
- `HitSounds` is commented out.
- There are no fields for impact VFX, spread pattern or reload.
- Variants such as `silenced_mp5` are full copies of a file, not overrides.

### 5.2 Firing, recoil and ammo

- **The firing flow** (`WeaponSystem.cs:25-207`): pump, then the fire gate (`Timer ≥ UseDelay` and rounds left), then burst closures through `DelayPausable`, then `EmitBulletFrom`.
  - `EmitBulletFrom` decrements ammo, plays the shot sound (non-spatial), spawns a pooled muzzle flash, ejects a casing, adds recoil, rumbles the pad, plays the slide animation and casts `BulletsPerShot` rays.
- **Recoil** is one scalar `CurrentRecoil`. It rotates and pushes the gun hand, tilts the body, and really moves the aim. Iron sights double recovery and halve walk speed, but **do not change spread**.
- **Reload: none, verified.** There is no reload field and no reload action; `RemainingRounds` is fixed at spawn.
  - The empty-gun loop is deliberate: a dry-fire click, the slide locking back, empty guns excluded from pickup and despawning 5 s after being dropped.
  - Adding reload changes the "throw it and grab another" loop and **needs Zeph's approval**.

### 5.3 Bullets and damage

- **Ray, not projectile.** `PhysicsSystem.Raycast` is filtered to the enemy layers and `BlockBullets`, and it ignores the shooter.
  - The engine ray marches in 10 px steps, and its own comment admits it "can possibly go through things" (inferred to miss thin colliders).
  - A miss draws a 10,000 px tracer.
- **Walls.** A wall hit places a hole and particles **only inside a `DecalZone`**. There is no wall impact sound, no ricochet and no material system.
- **Penetration.** Each body-part hit recasts from the hit point, up to 8 casts. Damage applies on exit-wound hits too, which may double-hit the same part (inferred; BUG-29: verify before changing, because it shifts balance).
- **Health.** Only the head and body take damage. Hands and feet aren't hittable.
- **Armour never reduces damage.** It only deflects, and only when all of these hold:
  - the shot is a single pellet
  - the shot comes from the front
  - the look is not cosmetic
  - a random roll succeeds
  - the victim has dodge left

  Layer chances combine with `max`. "Accurate shots" skip armour.
- **Dodge.** With dodge left, a hit drains the meter and plays a dodge clip instead of hurting.
  - A shot from behind costs more.
  - AI with an agility level may jump-dodge instead.
  - An AI "accurate shot" sets `AttacksCannotBeAutoDodged`, so only a jump-dodge's i-frames or a perfect deflect beats it.
- **Sword deflection** needs all of: blocking, a weapon that can deflect, a single pellet, and dodge left. A perfect deflect (within the 0.25 s block ramp-up) sends the bullet back to the shooter's head and runs time at 0.2x for 0.5 s, then hard-resets `TimeScale = 1` (BUG-38).

### 5.4 Melee

- **Data.** 14 `.seq` files. Each line is `anim transition_frame [hit_frames]`. The sequence is chosen by the **character's stats**, not by the weapon.
- **Hit test.** At each hit frame, **one ray from the aim origin along the cursor direction**, of length `Range*Scale + 200` (a self-described magic number). It ignores the animated weapon's position and the swing arc. This is the biggest collision-accuracy gap (PC2).
- **Hit frames are a key-count estimate**, not real frame times, so hits fire early or late (BUG-46).
- **Block and parry.** There is a perfect parry (block under 0.25 s old). Otherwise a matrix by damage type, size and `SpecialMelee` decides block or stun; it comes from an obsolete design document (BUG-81). Unarmed characters can't block.
- **Bugs:**
  - The fifth `blunt.seq` swing never hits (BUG-22).
  - The `.seq` key array is fixed at 16 keys (BUG-32).

### 5.5 Movement, pickup, throw

- **Pickup.** The nearest weapon is scored by distance **to the cursor**. The pickup is animated over 0.16 s. There is no inventory and no weapon switching, by design.
- **Throw.** The throw clip releases the weapon at 50%, at a velocity of 8000. The weapon then flies as a swept point ray in `FixedUpdate`.
  - A sharp-box hit does 80x damage and the weapon **sticks** in the body. Only the first sharp box is ever tested (BUG-25).
  - Thrown weapons **ignore god mode, dodge and `Invincible`** (BUG-26).
- **Jump dodge** (Space or pad A) dashes at up to 2000 px/s with i-frames set in the clip. The master jump allows firing mid-air. The dash cancels if *any* wall is within 150 px, even one behind the player (BUG-35).

### 5.6 Feedback

| Event | What happens |
|---|---|
| Shot fired | Muzzle flash, casing, slide animation, tracer, shot sound. Camera shake `Damage*4` for **every shooter, including off-screen enemies**. |
| Bullet hits a wall | A decal plus a flipbook **only in a DecalZone**. No sound. |
| Bullet hits flesh | A hole in the body and in *all* visible apparel (BUG-30), blood spurt and splat, impact offset, light stun. |
| Deflect | Spark and sound. A perfect deflect also gets 0.2x slow motion. |
| Melee hit | A slash decal plus spurts (blade or axe), or a punch sound. |

- **There is no hit stop anywhere.** Adding it changes the feel, so it needs approval.
- **Camera shake** is vertical only, at 70 Hz, additive and **uncapped**. A 0-200% setting exists.
- A metal-impact sound set exists, but only the train engine uses it.

### 5.7 Input path into combat

- **Order.** System order is JumpDodge → AI → Player → Abilities → Movement → Position → Velocity → Weapon. The player sets `IsFiring` before `WeaponSystem` runs, so there is no extra frame of latency (inferred).
- **Buffering.** Only melee buffers (`CanContinue`). These presses are lost if made at the wrong moment:
  - semi-auto clicks during `UseDelay`
  - jump-dodge during `PreventDodge`
  - throw during `PreventThrowing`
  - interact while a pickup is already running

  A buffer of about 120 ms would keep the controls unchanged (PC3).
- **Aim.** Mouse aim is the raw cursor, applied 1:1. Pad aim is the stick direction at a fixed distance, with no smoothing or assist.
- **Abilities** read player input for every character, including AI-owned ones (latent, BUG-37).

---

## 6. Enemy AI [04]

### 6.1 Structure

- **One system, every frame.** `AiCharacterSystem.Update` (`:25-444`) runs every AI. There is no FSM, behaviour tree or utility scoring.
- **State** is booleans and timers on `AiComponent`: docile, has target, too busy, panic level, and so on.
- **Intentions** are written as "wants" flags (`WantsToShoot`, `WantsToWalkLeft/Right`, `WantsToIronSight`, `WantsToDoAccurateShot`) and consumed the next frame. This **virtual-controller seam is worth keeping** for a future decision layer.
- **Limits and globals.**
  - `MaxAiCount = 128`. AI past that is silently skipped (inferred).
  - `DisableAI` and `AutoSpawn` are process-global statics.
  - Behaviour variety comes from a per-AI noise seed. There is no run-level RNG.
- **Every archetype runs the same code.** Grunts, agents, zealots and riot guards differ only in `.stats` numbers: aim randomness and speed, dodge, accurate-shot chance, health, melee skill, deflect. There are 21 stats files.

### 6.2 Perception: the AI cheats

- **Targeting.** `FindKillTarget` (`:546-576`) picks the nearest living hostile **anywhere in the scene**. There is no view cone, hearing, distance limit or line-of-sight test. The target is only re-chosen when lost, so an AI never switches to a closer threat (BUG-79).
- **Aim.** The AI aims at the target's true head or body position, through walls (`:310-333`), with noise scaled by `AimingRandomness` and distance.
- **Line of sight.** LOS is only a trigger gate: a barrel ray must hit the target before a non-waiting AI fires. **Waiting enemies fire whenever the ray hits anything**, at 0.75x chance. This reads as deliberate suppressive fire.
- **Melee reactions.** The AI reacts on the same frame to the player's melee animation (`:342,349`).
- **Why it matters.** In MIR's single-room levels this is rarely visible. It blocks cover, stealth and larger maps.

### 6.3 Behaviours

- **Attack turns.** The first N AI per target (`Level.MaxSimultaneousAttackingEnemies`, default 2) attack. The others "wait": they keep a safe distance, don't melee or take accurate shots, but **still shoot** at reduced chance. The slots never rotate (inferred, BUG-80).
- **Accurate shot.** A telegraphed aimed shot (a line, a closing crosshair and a warning sound; `AccurateShotSystem.cs:70-100`) with **one global cooldown** per scene. [04] calls this a good fairness device.
- **Movement** is 1-D band keeping (`WalkTowards`, `:632-660`): back off, close in, or strafe on noise. There is no Y movement and **no navigation**, and none is needed on a single floor line. Crowd separation is commented out, so enemies bunch up (inferred).
- **Melee.** A noise roll triggers an attack within range + 200. Unarmed AI fist-fights 90% of the time, or goes looking for a weapon.
- **Weapons.** **The AI never reloads.** It drops the empty gun and fetches another (C26 works). Melee wielders keep looking for a gun.
- **Dodging is passive.** The meter auto-dodges, with a jump-dodge chance for agile AI. The AI never dodges pre-emptively.
- **No retreat, flanking, squads, cover or healing.** `PanicLevel` only decays and gates shots.

### 6.4 Factions

- **Data.** Five faction files: `aahw`, `civil`, `player`, `rogue` and `zed`. Rogue is hostile to everyone, including itself.
- **Collision.** Each faction gets its own collision-layer bit. The bits come from a counter that is left-shifted from `CharacterStart = 16`, which leaves bits 5-31, so there can be at most 27 factions before the layer silently becomes 0 (inferred). Factions load once at start-up, so this only matters for heavily modded games.
- **Bugs:**
  - `GetEnemies`/`GetAllies` return the *queried* faction instead of the other one (`Faction.cs:149-179`, BUG-42). It is latent with today's data, but allies (escorts, co-op) would get friendly fire.
  - `civil.json` lists `"zeds"` instead of `zed` (BUG-43).

### 6.5 Bosses and specials

- **Specials are data plus toggle abilities:** `NoStun`, `NoWaitForAttackTurn`, `NoWeapons`. Zed and sleepwalker are the closest things to "specialists". **The AI cannot use active abilities** (`CharacterAbilityComponent.cs:38-39`, "TODO support AI").
- **Mag Rich** (`Level/Systems/RichBossSystem.cs`, 581 lines) is the only real boss.
  - It detaches its `AiComponent` and runs on bespoke code.
  - It has Sword and Gun phases, switched **at random after 9 s** (5% per 4 Hz tick), not by health thresholds.
  - It has its own health bar and a death hook. It aims straight at the player's head.
  - **There is no reusable boss, phase or attack-pattern framework.**
- **Machinist** (train) is an environmental objective, not a boss.

### 6.6 Debug tools

- `DisableAI` and `autospawn` console commands.
- `GameDebugSystem` flags for the floor line, walls and animation info. DEBUG builds only.
- **No AI debug overlay**: no state, target, LOS ray or attack slot display.

---

## 7. Collision and physics [03 §2.7-2.8, 02 §2.3]

### 7.1 Engine physics (queries only)

- **What the engine does.** Walgelijk.Physics provides raycast, `QueryPoint` and `QueryCircle` against `PhysicsBodyComponent` colliders filtered by bitmask. It does **no collision resolution**. It runs at `UpdatesPerSecond = 30` (`SceneUtils.cs:150`), and its chunk size is 1000.
- **Layers** (`Physics/CollisionLayers.cs`):

  | Bit value | Layer |
  |---|---|
  | 1 | Default |
  | 2 | BlockPhysics |
  | 4 | BlockMovement |
  | 8 | BlockBullets |
  | 16 | DecalZone |
  | From `CharacterStart = 16` upward | One bit per faction, assigned by left-shifting a static counter. [04] reads this as bits 5-31, a maximum of 27 factions; [02] describes it as "start at bit 16". [04] traced the assignment, so its reading is better evidenced. |

- **Level geometry:**
  - `RectWall` (`RectangleCollider`) and `LineWall` (`LineCollider` with a radius).
  - The legacy `AllBlocker` and `MovementBlocker`.
  - `DecalZone`.
  - **The floor is not a collider.** It is the polyline `Level.FloorLine`, sampled by `GetFloorLevelAt`. `IsFlatAt` gates death clips, stuns and jump-dodges.

### 7.2 Characters

- **Characters are kinematic.**
  - The centre rides the floor line.
  - Horizontal movement is blocked by one ray against `BlockMovement` at one third of body height.
  - There is no gravity, no vertical velocity, no step-up and no platforms or multiple floors.
  - This makes wall rebounds and aerial attacks (A7) and true jumps (PA10) a core rewrite.
- **Hit volumes.** Only the body (a `RectangleCollider`) and the head (scaled 0.8) have colliders, on the faction layer. They follow the animated transforms (inferred). **Hands, feet and apparel have none**, so a bullet through an outstretched arm misses.

### 7.3 Projectiles

- **Bullets:** one raycast per pellet, recursing up to 8 times. There are no projectiles.
- **Thrown weapons:** `VelocitySystem` (`FixedUpdate`) does a point raycast along the velocity, which is swept but has no thickness. Gravity is -10 for light weapons and -120 for heavy ones.
- **Melee:** one ray (section 5.4).
- **Particles:** casings do a point query plus a raycast per particle per frame, with a push-out loop of up to 1,200 iterations (`Graphics/WorldCollision.cs:47-99`).

### 7.4 Verlet ragdolls (custom, game-side)

- **Trigger.** `MadnessUtils.TurnIntoRagdoll` (`:729-844`) works on **dead characters only** (`:731`). It drops the weapon, re-layers the sprites, may eject head armour and builds the rig.
- **Rig.** 9 nodes (3 head, 4 body, 2 hands), about 18 links in four distance modes, and 4 transform links. Initial velocity comes from the impact offset plus measured velocity.
- **Solver** (`VerletPhysicsSystem.cs`):
  - Runs at a fixed 200 Hz through `FixedIntervalDistributor`, inside `Update`.
  - For each node, each cycle: a raycast against `BlockPhysics` and a circle query, plus a push-out loop of up to 100 iterations.
  - Then a position projection for each link, with stiffness 0.45.
  - Soft impact sounds play from inside the solver.
- **Lifetime.** A ragdoll is deleted after 10 s, or after 2 s asleep. It is stamped only if deleted before 10 s. The player's ragdoll is never deleted.
- **The buffer bug (BUG-47).**
  - The solver uses static 256-entry buffers, and `Scene.CopyAllComponentsOfType` silently truncates at the array length. [03] verified this in the engine source.
  - From the **15th** simultaneous ragdoll on, links go unsolved and bodies fly apart. From the 29th, nodes stop simulating.
  - Normal play stays under this. Mass kills (wave modes, the Konami-code kill-all) can hit it.
- **Sleeping ragdolls are still fully simulated** (PERF-02).

---

## 8. Game modes and progression [04, 05]

### 8.1 Modes

`GameMode` has only four values: `Campaign`, `Experiment`, `Editor` and `Unknown` (`Level/GameMode.cs:6-12`). Every mode builds its scene through `SceneUtils.PrepareGameScene`. **Level progress and objectives are added only for `GameMode.Campaign`** (`SceneUtils.cs:340-344`).

| Mode | State | Notes |
|---|---|---|
| **Campaign** | Works | Three campaigns: `employee_of_the_month` (42 entries, including 5 cutscene levels and the Rich boss), `mag_campaign` (19 entries, **hidden**) and `tutorial` (4 levels).<br>Win types: BodyCount (96 of the 106 level files), Always (6), Explicit (3), Time (1).<br>Retry reloads the level. There are no lives, no checkpoints and **no score**. On the last level the game drops to the main menu, with no results screen (BUG-83).<br>The equipped weapon carries into the next level. |
| **Incident** | Works | A procedural sequence of handmade levels: 1 begin, 5 shuffled middle levels and 1 end. It hits a 20-200 kill target and randomises spawns, weapons and enemies. A persistent kill counter is kept in a static.<br>The seed **can't be typed in**, and the displayed name changes every launch (BUG-67).<br>Runs are not deterministic: spawning uses unseeded RNG, mutates cached levels (BUG-62), and level pools follow registry order. |
| **Experiment** | Works | A sandbox over 7 `exp_*` levels. Edit mode freezes time. Tabs for characters, weapons, music (DJ) and game rules.<br>A drag-window **autospawn editor** acts as a live, unsaved wave editor. |
| **Tutorial** | Works | 4 levels, each with a hand-coded `Teach*System`. Its prompts adapt to the player's bindings. Nothing steers new players to it ([05]). |
| **Arena** | **Stub** | `ArenaModeSystem` is empty. `ArenaScene` has no level, so nothing spawns. Its menu entry is commented out (`MainMenuSystem.cs:246-248`).<br>A wave record exists (`ArenaModeWave`; `waves.json` has 2 waves of 10 grunts), but its keys don't match the class fields (BUG-66). |

### 8.2 Shared systems (D-ARCH)

- **Spawning.** One `EnemySpawningComponent` and `EnemySpawningSystem` serve every mode, which is good.
  - It picks uniformly at random from a list. The only way to weight a type is to list it more than once.
  - It has no waves, no difficulty over time, no budgets, no per-spawner lists and no visibility rules.
  - It is gated by the global `AutoSpawn` static.
  - Door spawns produce 1-2 enemies, never the intended 3 (BUG-65).
- **Objectives.** There are four `ProgressionType`s, handled only in Campaign. `NeedsToDieComponent` marks NPCs that must die.
- **Score:** none anywhere. A grep for `Score|Combo|HighScore` found no gameplay code.
- **Timers.** The only timer is `CampaignStatsTimerSystem`. It counts cumulative per-level time, including failed attempts, cutscenes and death screens, so it measures "time played", not run time.
- **Stats.** `CampaignStats` and `LevelStats` hold per-level kills, deaths, attempts, time and the equipped weapon. They are shown in the pause and campaign menus.
- **Cached `Level` assets are mutated at run time** (inferred from `Lazy<Level>` over the asset cache):
  - The more/fewer-enemies disks change `MaxEnemyCount` **after** the spawner has already copied it, so they have no effect, and the change accumulates on every reload (BUG-61).
  - Incident generation rewrites shared levels (BUG-62).
  - An inverted check adds a duplicate `IncidentSystem` on a second run (BUG-63).
- **Proposed by [04]:**
  - a mode-agnostic `WaveDirector` with a wave data format and a seeded per-run RNG
  - an `ObjectiveComponent` plus a `RunResults` screen
  - a `ScoreSystem` driven by kill and hit events, plus a run timer

  These unlock D37, D38, D40-D42, D45, D46 and G72 with one implementation each.

### 8.3 Progression and customisation

- **Improbability disks** (17) are the only modifier and unlock system. Disks are picked up in levels (18 level files contain them) and saved to `disks.txt`.
  - Some disk names reference outside IP: Willis, Gump, FLCL, Ghost Protocol.
- **Character customisation** offers head (base plus 3 layers), body (base plus 2), hands and blood colour, with `.look` import and export.
  - Not exposed: feet, flesh, gore, tint and skin colour.
  - There is no unlock gating. Pieces marked `hidden` are simply skipped.
- **Not present:** achievements, cosmetic unlocks, weapon mastery, skill progression, loadouts and lifetime statistics. Arena's XP and money fields are unused stubs.
- **The level format** is described in [04] §2.12:
  - Newtonsoft JSON with `TypeNameHandling.Auto` and polymorphic `LevelObject`s.
  - Objects include walls, a floor polyline, doors (spawner, progression, portal), NPCs, weapons, triggers, `GameSystem`, `LevelScript`, turrets and disks.
  - **Nothing is player-interactive or destructible** except turrets and the train engine.
  - Environments are one painted QOI background per level, with no tileset or lighting.
- **Level count.** [04] counted 106 level files and [01]/[06] count 107. [05] mentions 56 levels; that appears to count only the root `levels/` folder.

### 8.4 Local co-op (D48)

**Feasible but L-sized: about 3 weeks for same-screen 2-player** [04 §2.16].

What already generalises:
- AI targeting, factions and per-target attack turns
- the multi-target camera
- small, bound-clamped levels

What blocks it:
- `FindPlayer` has 44 call sites in 24 files
- the player-singleton convention
- global input with a single static `Gamepad` and one XInput slot
- one HUD and a global death sequence

Fix BUG-42 (factions) first.

---

## 9. Asset formats and loading [01 §2.5, 06 §2.1-2.3]

### 9.1 Packaging

- **The pack step.** `base/` (2,491 packed assets, 679 MB) is packed into `resources/base.waa` (708,632,493 bytes) by `IIR --mode pack` after each build (`Program.cs:30-49`).
  - `AssetPreprocessor` tags files by extension. Folder tags come from 9 `.tags` files.
  - Every build re-packs everything (PERF-15).
- **Where the bytes are:**

  | Content | Size |
  |---|---|
  | Music (44 OGG) | 208 MiB (217 MB) |
  | QOI backgrounds | 203-208 MB |
  | Cutscene video (7 MP4) | 196 MB |
  | PNGs (1,047) | 26 MB |
  | Data | 3.9 MB |

- **Loose files.** `resources/` outside the package holds only engine fallbacks: a font, 4 shaders and `null.wav`.

### 9.2 Registration and loading

- **Start-up registration** (`PrepareResourceInitialise`, `MadnessInteractiveReloaded.cs:30-116`):
  - engine deserialisers for OGG, QOA, WAV and `.wf`
  - 15 game deserialisers
  - 2 obsolete bridges, for `Language` (JSON) and `Video` (MP4)
  - every `resources/*.waa`
  - error fallbacks for missing textures and audio
- **Two loaders.** The legacy `Resources` loader still runs in parallel. It is used only for loose user files.
- **The API.** `Assets.Load<T>` (315 uses) and `AssetRef<T>` bind an ID to a package *at call time*. Packages are searched newest first, which is how mods override content.
- **Registries.** There is a `Registry<T>` per content type, each a `ConcurrentDictionary`. Each `LoadX()` enumerates an asset folder and logs per-file errors.
  - `Register` silently replaces an existing key, which is the mod override mechanism.
  - Enumeration order is unspecified, so lists built from it are unstable.
  - `ClearAll` has a bug (BUG-18).
- **Loading sequence.** `GameLoadingScene` runs 25-26 steps: mods, languages, weapons, animations, armour, looks, levels, audio, textures, campaigns, then settings **last**.
  - Async steps use `Task.Run` plus a wait on `IsCompleted`. **A faulted step is treated as complete and its exception is never logged** (BUG-11).
  - Each step also adds a fixed 0.05 s delay for the ticker effect. Together with a 0.5 s welcome, that is about 1.8 s of artificial boot delay (PERF-13).
- **Temp-file leak.** The video and language bridge copies each asset to `%TEMP%` and never deletes it. That is up to about 195 MB per session if all cutscenes play (BUG-16).

### 9.3 File formats

| Content | Format |
|---|---|
| Weapons (120-121), levels (106-107), cutscenes (7; [04] lists 6 cutscene definitions over 7 videos), campaigns, factions, waves, DJ, incident music, locale | Newtonsoft JSON. Levels and cutscenes use `$type` polymorphism. |
| Armour (250), hand armour (10), looks (60), stats (21), presets (16), melee sequences (14) | Line-based key/value text. The first line is a version, which is **parsed but never compared**. |
| Animations (192) | `.anim` text with **no** version line |
| Textures | PNG (1,047), decoded by BigGustave, and QOI (267, backgrounds encoded by `mirbgencoder`, with background, foreground and decal mask packed into alpha). Some exceed 4096 px (`background_mag_bridge.qoi` is 8112x3149), which may fail or be slow on older integrated GPUs (suspected). |
| Audio | WAV (277) for SFX, decoded to memory; OGG (54) for music, streamed; QOA supported |
| Fonts | `.wf` (6): a zip of `meta.json` plus a 512² MSDF atlas. Also one BMFont. |
| Video | MP4 (7), 1920x1080 H.264 + AAC, through FFmpeg 5.1.2 |

### 9.4 Authoring conventions [06 §2.3]

- **Pixels and pivots.** 1 px = 1 world unit. Sprites have a centred pivot. Every piece has separate `_left.png` and `_right.png` drawings, not mirrors.
- **Weapon points.** Weapon hold points, barrel and casing points are pixel offsets from the texture centre, with Y up (inferred from `deagle.json`).
- **Naming.** Snake_case IDs. Flipbooks encode their grid in the file name (`explosion_20_5x4.png`).
- **Tooling.** There is no sprite validator. `png2qoi.py` has a path bug (BUG-109).
- **Workflow.** [06] judges these conventions friendly to AI- and SVG-based asset production. `ASSET_REQUIREMENTS.md` covers the production workflow.

### 9.5 Localisation

- **One language only:** `en-GB`, with 146 keys and about 121-122 `Localisation.Get` calls.
- **Hardcoded English:**
  - [01] counted at least 141 hardcoded UI literals, including editors.
  - [05] counted 34 outside the editors.
  - Disk names, action names and HUD strings are also untranslated.
- **Mods** can add languages.
- **Fonts** are probably Latin-only (inferred).

---

## 10. Audio [05 §2.1]

- **Backend.** `OpenALAudioRenderer` (OpenAL Soft 1.23.1) with 128 temporary sources.
  - If construction fails, the game runs **silent**, and the only trace is a stderr line.
  - Walgelijk.PortAudio is referenced but unused.
  - The engine can enumerate and switch audio devices, but the game exposes neither.
- **Tracks and volume.** Three tracks: Music, SoundEffects and UserInterface. Master is the listener gain. There are four 0-1 sliders, with a master default of 0.5. There is no ambience or voice bus, and no mute when the window loses focus.
- **Assets.**
  - About 60 static `Sound`s are loaded eagerly by static initialisers.
  - `SoundCache` memoises `Sound` objects and logs every new entry.
  - SFX are `FixedAudioData`; music is streamed `StreamAudioData`.
  - Missing audio falls back to `error.wav` or `error.ogg`.
- **Music.**
  - `PersistentSoundHandles` holds the level, pause, menu and DJ music. Scenes swap them as **hard cuts**; the only fade is the pause music fading in.
  - The DJ has 20-29 tracks with BPM data.
  - Incident music is a flat list. Two of its file names are wrong, so those tracks never play (BUG-69).
- **No positional audio** (BUG-85, evidenced).
  - `CameraMovementSystem.cs:113-114` zeroes the listener position and the spatial multiplier every frame.
  - All SFX are non-spatial (`Sounds.DefaultSpatialParams = null`).
  - None of the 75 `PlayOnce` call sites passes a position.
  - The engine also multiplies by `SpatialMultiplier` twice, which will bite once spatial audio is enabled.
- **Voices** (BUG-86, suspected).
  - The engine creates and plays an AL source *before* it asks its 128-slot pool for a tracker. When the pool is full, the source leaks. The NuGet 0.24.0 IL has the same order.
  - Past OpenAL Soft's default of 256 sources, new sounds silently fail.
  - Bulk triggers: Verlet impacts per node per sub-step, casings, pellets.
  - The game has no voice limiting or priority.
- **Proposed audio pass** (M2-FIX-AUDIO): put the listener at the camera, add linear distance attenuation, pass positions at the main call sites, add a game-side SFX limiter, and add music crossfades.

---

## 11. Input and controller support [05 §2.2-2.3, 02 §2.9]

### 11.1 Actions and bindings

- **Actions.** There are 12 `GameAction`s. `ControlScheme` holds an `InputMap` (keyboard and mouse) and a `GamepadMap`. Gameplay reads both through `InputExtensions.ActionHeld/Pressed/Released`.
- **Binding limits.** A binding is up to 3 keys, or exactly 1 mouse button. There are no mouse-plus-key chords and no wheel bindings. The mouse constructor has a latent bug (BUG-72).
- **The rebinding UI** covers both keyboard and pad, and persists through `ControlScheme`.
  - Duplicates blink red but are allowed.
  - "Reset controls" also resets pad bindings (BUG-73).
  - Action names are hardcoded English.
- **Keys that can't be rebound:**
  - Escape (pause and skip cutscene)
  - Ctrl+wheel zoom, which clashes with the default Ability3 key (BUG-34)
  - experiment-mode Tab and A/D
  - Incident's 2/3/4 keys
  - **Disk pickup**, which needs a raw left click (BUG-71)

### 11.2 Controller

- **API.** XInput only, through P/Invoke (`xinput1_4` or `xinput9_1_0`), reading only the first connected slot.
- **Menus.** The pad drives a virtual cursor with Win32 warping, plus highlight navigation.
- **Other details.**
  - Rumble fires on damage and death.
  - Deadzones are hardcoded (`GamepadState.cs:13-15`).
  - Pad aim is the stick direction times `AimDistance`, with no smoothing or assist.
- **Tests.** About 139-140 unit tests cover this layer and pass ([01] run).
- **Gaps:**
  - **Controller-only players can't collect Improbability disks.** In gameplay, the pad never injects the left click that disk pickup needs (BUG-71, evidenced).
  - No button glyphs: `ToDisplayString` returns text.
  - No deadzone, curve, invert or vibration-strength settings.
  - **Windows only.** For Linux and Steam Deck, [05] suggests an `IGamepadSource` over GLFW gamepad state (inferred to be available in OpenTK 4) or SDL3. The `IGamepadSource` abstraction already exists.

### 11.3 UI, settings and accessibility

- **Menus** are built with Onion. Main menu: Campaigns, Improbability disks, Experiment, Incident, Character customisation, Mods, Settings, Credits and Quit. Arena is commented out.
- **The pause menu** has Resume, Settings and Quit. **Retry is commented out, and Quit has no confirmation** (BUG-99).
- **The HUD** (`PlayerUISystem.cs`, 435 lines, all inside `Render()`) shows the weapon, ammo, ability slots, crosshair and spread ring, a low-health vignette and the body-count target. It allocates strings every frame. There is no HUD scale.
- **Settings that exist:**

  | Area | Settings |
  |---|---|
  | Video | FPS cap (24-300 or uncapped; default 300); VSync (default off); windowed or borderless window; stamp ragdolls |
  | General | Screenshake 0-200% |
  | Audio | Four volume sliders |
  | Input | Pad toggles, `AimDistance`, menu cursor speed |

- **Settings that are missing:**
  - **UI or HUD scale**
  - resolution or render scale
  - exclusive fullscreen and a remembered window size
  - a **flash toggle**: there are 35 `MadnessUtils.Flash` calls
  - reduced motion
  - colour-safe crosshair and health cues
  - an FPS overlay (the profiler is forced off)
  - an audio device picker
  - pause on focus loss
  - gore and decal detail
  - a photosensitivity notice
- **The settings save bug** (BUG-07). Settings save on a thread-pool task every frame while a slider is being dragged, serialising the live object while the UI keeps changing it.
- **Onboarding.** There is no first-run prompt to the tutorial.

---

## 12. Performance bottlenecks

**Nothing was profiled in Phase 1.**
- The only *measured* numbers are the build time and the sizes on disk.
- PERF-01 is *evidenced* in the code and the shipped IL, but the CPU load it causes was not measured.
- Everything else is *suspected*, which means inferred from reading the code.

The table is roughly ordered by expected impact on a mid- or low-end PC.

| ID | Hotspot | Measured or suspected | Evidence | Fix idea | Source |
|---|---|---|---|---|---|
| PERF-01 | **Busy-wait frame limiter**: 300 fps default, VSync off, High process priority. One core runs at 100% for the whole session, menus included. On laptops it takes thermal and power budget from the GPU and drains the battery; at High priority it can starve OBS, Discord or audio threads. | **Code and IL evidenced; CPU load inferred, not measured** | engine `Game.cs:309-313`; 0.30.0 IL 0x0300-0x0312; `Settings.cs:53-54`; `MadnessInteractiveReloaded.cs:138-140` | `UpdateRate = 0` plus a sleeping `IGameLoopEvent` (`timeBeginPeriod(1)` + `Sleep` + a final spin). Default VSync on, or cap to the refresh rate. Drop High priority. (BUG-01) | [01][05] |
| PERF-02 | **Verlet ragdolls.** Each node, each 200 Hz sub-step, does 1 raycast and 1 circle query, plus up to 100 push-out queries when stuck. Sleeping ragdolls are still simulated. Estimates: about 600 queries per frame for 10 ragdolls at 60 fps ([03]); about 36,000 raycasts plus 36,000 queries per second for 20 ragdolls ([05]). [05] suspects this is the main gameplay CPU hotspot. | Suspected | `VerletPhysicsSystem.cs:43-56,115-171` | Sleep resting nodes, skip the raycast near zero velocity, run at 120 Hz, cap the push-out loop at 8. Fix BUG-47. | [03][05] |
| PERF-03 | **AI runs at the frame rate.** At 300 fps every AI decision, scan and LOS ray runs 5 times as often as at 60 Hz. Each firing AI builds a LINQ ignore list per raycast (`AiCharacterSystem.cs:484-487`, author TODO "is this... slow?"). Target, item and weapon scans are O(n) per AI (`:557,587,673`). Fine at MIR's 2-5 `MaxEnemyCount`; O(n²) with 30+ enemies in wave modes. | Suspected | as cited | Tick AI at a fixed 30-60 Hz. Cache the ignore set once per frame. Add a spatial index or cached target lists for crowds. | [04][05][01] |
| PERF-04 | **A synchronous `GL.ReadPixels` per bullet hole** in a DecalZone, which stalls the GPU pipeline and allocates `byte[4]`. The author's comment says it was never profiled. A shotgun against a wall can do several per frame. | Suspected | `Prefabs/Prefabs.cs:110-128` | A per-level foreground mask built at load, or an asynchronous readback | [02][03] |
| PERF-05 | **Text meshes are rebuilt every frame.** `Draw.CacheTextMeshes = -1` disables caching, and the menus, HUD and settings are text-heavy. | Suspected | `MadnessInteractiveReloaded.cs:251` | Try a threshold such as 2, and watch cache growth from timers. | [05] |
| PERF-06 | **Per-frame allocations and GC pressure:**<br>• 111 `GetAllComponentsOfType` and 140 `FindAnyComponent` sites, each allocating an iterator<br>• 60 LINQ calls in 14 system files<br>• spawner LINQ counts every frame (`EnemySpawningSystem.cs:124-128,141`)<br>• pause-menu stats `Sum` every frame<br>• DJ closures; HUD string interpolation (`PlayerUISystem.cs:380-398`)<br>• `QueryResult[16]` per call in `RichBossSystem.cs:485,546`; `QueryResult[1]` per dodging character (`JumpDodgeSystem.cs:309-311`)<br>• `CharacterSystem` enumerates all components per character per frame (`CharacterSystem.cs:36-40`)<br>• burst and deflect closures | Suspected (minor individually; compounds at 300 fps) | as cited | Use the span overload `GetAllComponentsOfType<T>(T[])` (as `AiCharacterSystem.cs:18,54` already does). Cache formatted strings and counts. Cache constraint-component lists on attach. | [01][02][04][05] |
| PERF-07 | **Character draw calls.** Each body part has its own material (per-part gore uniforms), so nothing batches across characters. Draw calls grow with characters × parts × armour layers. | Suspected | `Character/BodyPartMaterialPool.cs:22-26`; `Prefabs.cs:252` | Measure with RenderDoc before changing anything. | [05][03] |
| PERF-08 | **Decals.** Instance arrays are rebuilt from all decal entities on every new decal, which is O(N) with N growing through a level. The instance buffer is marked changed every frame, re-uploading about 43 KB per renderer per frame. | Suspected | `DecalSystem.cs:68-88,100` | A ring buffer, which also fixes BUG-48. Upload only when dirty. | [03][05] |
| PERF-09 | **Engine raycast cost.** The ray marches in 10 px steps and re-tests every body in the chunk at each step. A miss can cover thousands of px. Each shot costs pellets × up to 8 recursive casts. An 8-pellet shotgun into a crowd is the worst case. | Suspected | engine `Walgelijk.Physics/PhysicsSystem.cs` (`Raycast`); `BulletEmitter.cs` | Cap the length of missed rays. DDA chunk traversal upstream. | [02] |
| PERF-10 | **Animation evaluation.** `Curve<T>.Evaluate` scans keys linearly, twice per call: about 15 evaluations × 2 clips per character per frame, on baked clips with 30-80 keys. `GetAnimationConstraints` is recomputed on every `AnimationConstrainsAny` call. | Suspected (small; grows with crowds) | engine `Walgelijk/Shared/Curve.cs:33-112`; `AnimatedCharacterExtensions.cs:14-35` | Cache per frame; binary search. | [03] |
| PERF-11 | **Background and particles.** The background buffer is re-rendered into its 1024² target every frame. Each casing particle runs a point query plus a raycast per frame, with a push-out loop of up to 1,200 iterations. | Suspected, low | `Level/Systems/BackgroundBufferSystem.cs:35-56`; `Graphics/WorldCollision.cs:58-78` | Skip the re-render when the view matrix hasn't changed. Cap the push-out. | [03] |
| PERF-12 | **Asset memory and download size.** The package is 708 MB. Assets stay cached for the session (only about 12 scene-lifetime assignments), so backgrounds may accumulate in RAM and VRAM. Some textures exceed 4096 px. The music bitrate looks high (the 24 MB `contrave.ogg`). | Sizes measured on disk; memory growth suspected | `GameLoadingSteps.cs:64-75`; [06] §4 | Measure working set and VRAM after 10 levels. Give backgrounds scene lifetime. Re-encode music at about 112-128 kbps and the video at a lower bitrate. Delete the 36 MB of unused backgrounds (BUG-108). | [05][06] |
| PERF-13 | **Boot and load time.**<br>• About 1.8 s of every boot is artificial delay: 0.05 s per step plus a 0.5 s welcome (evidenced).<br>• Async steps run one at a time.<br>• The Roslyn cold compile of level scripts takes an estimated 0.5-2 s.<br>• Levels are parsed twice (cheap).<br>• Incident loads its levels synchronously on the menu thread. | Mixed (the delay is evidenced; the rest suspected) | `GameLoadingScene.cs:110`; `IncidentConfig.cs:65`; `LevelDeserialiser.cs:29-54` | Trim the ticker delay. Run independent steps in parallel. | [01][04][05] |
| PERF-14 | **Cutscene start hitch.** The first play of each cutscene copies a 16-73 MB MP4 to `%TEMP%` on the main thread. | Suspected | `TemporaryDeserialiserBridge.cs:15-22`; `GameLoadingScene.cs:35` | Extract once to a hashed cache folder (also fixes BUG-16). | [01] |
| PERF-15 | **Build time.** Every build re-packs all 2,492 non-`.tags` files under `base/` (708 MB), and the test project packs again. Harmony builds for 13 target frameworks. | **Measured:** 13 s warm Debug build | `.superpowers/audit/01-architecture.md:30` | Add an MSBuild inputs/outputs check to the pack target. | [01] |
| PERF-16 | **Mod start-up.** Every mod is re-extracted to `%TEMP%` at each launch. Folder mods are also zipped in memory and extracted to `debugout`. | Suspected | `Mod.cs:176-188`; `LocalModCollectionSource.cs:100-115` | A hash-based cache. Delete the debug copy. | [01] |
| PERF-17 | **Level editor undo.** Every action serialises the whole level to JSON and keeps every snapshot. | Suspected | `LevelEditor.LevelEditorComponent.cs:95-100,134-147` | Cap the history, or store diffs. | [01] |

**How to measure** ([05] §4, §2.8):
1. Rebuild the benchmark as a fixed-seed scene with N AI, ragdolls and decals over 60 s. Record frame-time p50, p95 and p99, GC counts and allocated bytes to a CSV.
2. Add an FPS and frame-time overlay through `Game.Profiling`.
3. Wrap the top systems in `Profiling.StartTask/EndTask`.
4. Use external tools: `dotnet-counters`, `dotnet-trace`, PresentMon and RenderDoc.

---

## 13. Bugs and technical debt

This is one consolidated, deduplicated list.

- **Severity** is the auditors' rating. Where they disagreed, both ratings are shown.
- **Group** is the fix group in `FEATURE_TRACKER.md`.
- **Source** is the auditor and their bug number, or the section if the bug came from body text.
- All bugs are verified by reading the code unless marked *inferred* or *suspected*.

**Totals by severity:**

| High | Med | Low | Debt only |
|---|---|---|---|
| 8 | 38 | 62 | 2 |

The 8 High rows: 1 performance, 1 save, 1 security, 2 release and 3 legal. Five rows carry two ratings and are counted by their higher one: BUG-06, BUG-07, BUG-13, BUG-44 and BUG-87.

| ID | Sev | Area | Where (file:line) | Description | Fix idea | Group · Source |
|---|---|---|---|---|---|---|
| BUG-01 | High | Engine loop / perf | engine `Game.cs:309-313` (same loop in 0.30.0 IL 0x0300-0x0312); `Shared/Settings.cs:53-54,111`; `MadnessInteractiveReloaded.cs:138-140` | The frame limiter busy-waits, at a 300 fps default, with VSync off and the process at High priority. One core runs at 100% in every scene, menus included. The load is inferred, not measured. (Engine-side part not re-checked in verification.) | `UpdateRate = 0` plus a sleeping `IGameLoopEvent` limiter. Default VSync on, or cap to the refresh rate. Drop High priority. | M1-FIX-PERF · [01]#1, [05]#1 |
| BUG-02 | High | Saves | `Level/CampaignStats.cs:114-116`; `Registries/Registries.cs:138-141`; callers `Shared/Systems/PauseSystem.cs:147` and `CampaignStatsTimerSystem` | A corrupt `campaigns/<id>.json` is skipped at load. `CampaignStats.CreateFor` then throws because the file exists. It throws every frame from the pause menu, and every fixed step from the stats timer. | On a parse failure, load the `.bak`, or rename the bad file to `.corrupt` and start fresh. | M1-FIX-SAVE · [05]#2 |
| BUG-03 | Med | Saves | 14 write sites, for example `Level/CampaignStats.cs:126-131` and `Shared/Settings.cs:179-190` | No atomic writes. A crash or power loss mid-write leaves a truncated file, which then silently resets to defaults: lost progress. | One `SaveFile<T>` helper: write a `.tmp`, then `File.Replace` with a `.bak`. | M1-FIX-SAVE · [01]#4, [05]#2 |
| BUG-04 | Med | Saves | `Shared/Serialisation/BaseDeserialiser.cs:19-23`; `Level/CampaignStats.cs:20-93` (fields), `:126-131` (`Save`); all JSON saves | No JSON save has a version field. The text formats' version line is parsed but never compared. There are no migrations. | Add a `FormatVersion` to every save type, plus a migration switch on load. | M1-FIX-SAVE · [01]#5, [04] §7 |
| BUG-05 | Med | Saves / crash | `MadnessInteractiveReloaded.cs:258-287` | Saves run only after `Game.Start()` returns, so if the loop throws nothing is saved. The look and arena are saved only at a clean exit. Kills and time in the current level are lost on a crash. | Call `SaveAll()` from `finally` (each save in its own `try`) and from the crash handler. Save the look when leaving character creation. | M1-FIX-SAVE · [01]#3, [05]#5 |
| BUG-06 | Low [01][04] / Med [05] | Saves | `Scenes/Menus/GameLoadingScene.cs:45`; `MadnessInteractiveReloaded.cs:274` | `arena.json` is never loaded (the load is commented out) but is overwritten with an empty default at every exit. Harmless now; it would clobber real saves later. | Stop saving it until Arena exists, or restore the load. | M1-FIX-SAVE · [01]#14, [04]#15, [05]#13 |
| BUG-07 | Med [05] / Low [01] | Settings | `Shared/Settings.cs:129-143`; `SettingsSceneSystem.cs:57-155`; `MadnessInteractiveReloaded.cs:272` | `Settings.Apply` ends in `Task.Run(Save)`. During a slider drag that is a thread-pool save every frame, serialising the live object while the UI changes it. The exit save skips the lock. | Debounce: mark the settings dirty and save on the main thread about 0.5 s after the last change. Use `Settings.Save()` at exit. | M1-FIX-SAVE · [01]#19, [05]#8 |
| BUG-08 | Low | Migration | `Shared/LegacyDataMigration.cs:43-46,57-70` | An MIR→IIR copy that fails half-way leaves the target non-empty. It is never retried, and the rest of the old data is silently left behind. | Copy into a temp sibling folder and rename at the end, or write a success marker. | M1-FIX-SAVE · [01]#15 |
| BUG-09 | Low | Migration | `MadnessInteractiveReloaded.cs:221,225,293-308` | `MigrateSaveData` (the old `./userdata` migration) can never run, because its target folder is created just before it checks for it. | Delete it, or move it before `:221`. | M1-FIX-SAVE · [01]#16 |
| BUG-10 | Low | Versioning | `Shared/GameVersion.cs:16-20`; `Shared/Branding.cs:10,22`; `MIR.csproj:9` | `GameVersion` (assembly version 0.51.1) is both the save-file version and the "based on MIR vX" credit. The README and `Branding.DisplayVersion` say 0.1.0. Bumping IIR's version will make the credit wrong. | Hard-code the MIR base version in `Branding`. Give IIR its own version and use it everywhere. | M1-FIX-SAVE · [01]#23, [06] §7 |
| BUG-11 | Med | Loading | `Scenes/Menus/GameLoadingScene.cs:115-119`; `LevelLoadingScene.cs:91-95`; `Level/Systems/LevelScriptSystem.cs:40` | `Task.Run` followed by a wait on `t.IsCompleted`. A faulted task counts as completed, so its exception is never observed or logged. This includes the mod-loading failures in BUG-91. | Check `t.IsFaulted`. Log it, and show an error screen listing the failures. | M1-FIX-STAB · [01]#6, [05]#6 |
| BUG-12 | Med | Loading | `LevelLoadingScene.cs:60-64` | A level that fails to load logs the error and stops, leaving the player on the loading screen with no way back. | Return to the menu with an error dialog. | M1-FIX-STAB · [05]#7 |
| BUG-13 | Med [05] / Low [01] | Crash handling | `Program.cs:64-65,97,120,125,143-165,207-224` (the auditors' line ranges differ slightly) | Release crash page problems:<br>• it links to a log that the commented-out `WriteLog` never writes<br>• no hardware info<br>• reflection getters are unguarded<br>• nothing is written if the crash happens before `Game` exists<br>• exception text is not HTML-escaped<br>• no `AppDomain.UnhandledException` or `UnobservedTaskException` hooks<br>• `mir_` file prefix | Copy the newest log from `%LOCALAPPDATA%\IIR\logs`. Add OS, GPU, RAM and version. Wrap the getters in `try`. Write a minimal report when `Game` is null. HTML-encode. Add global hooks. Rename the prefix. | M1-FIX-STAB · [01]#22, [05]#4, [05] §2.5 and §7 |
| BUG-14 | Med | Debug tools | `Shared/Systems/BenchmarkSystem.cs:43`; `Shared/MadnessCommands.cs:131-136` | The `Benchmark` console command loads a scene whose system throws `NotImplementedException`, which crashes the game. | Remove the command, or rebuild the benchmark ([05] §2.8). | M1-FIX-STAB · [05]#12 |
| BUG-15 | Med | Level scripts | `Level/Systems/LevelScriptSystem.cs:19-23`; `Level/BuiltLevelScript.cs:24,114-118` | A script that fails to compile sets `Script = null`, and the system starts a new build whenever `Script == null`. So it recompiles every frame and re-logs its whole source each time. | Add a `BuildFailed` flag. Rebuild only when the code changes. | M1-FIX-STAB · [01]#9 |
| BUG-16 | Med | Assets | `TemporaryDeserialiserBridge.cs:15-22` | Video and language assets are copied to a temp `.mp4` and never deleted (nor is the zero-byte temp file). That is up to about 195 MB of MP4 per session. | Delete the copy after the `Video` closes, or extract once to a hashed cache folder. Read `Language` from the stream. | M1-FIX-STAB · [01]#11 |
| BUG-17 | Low | Level scripts | `Level/Systems/LevelScriptSystem.cs:44-68`; `Level/BuiltLevelScript.cs:51,86,122`; `Level/Components/LevelScriptCache.cs` | Script lifecycle problems:<br>• `End` is bound but never invoked<br>• `FixedUpdate` and `Render` ignore pause<br>• a thread-affine `Mutex` guards an async method (suspected to throw if it resumes on another thread)<br>• built scripts are cached by source text, so instance fields persist across retries | Call `End`. Check pause. Replace the `Mutex` with a `SemaphoreSlim`. Build one instance per component, or reset it. | M1-FIX-STAB · [01] §2.7, [04]#19 |
| BUG-18 | Low | Registries | `Registries/Registries.cs:54-55` | `ClearAll` clears `Looks` twice and never clears `Stats` or `Languages`. | Replace the duplicate with `Stats.Clear(); Languages.Clear();`. | M1-FIX-STAB · [01]#20 |
| BUG-19 | Low | ConVars | `Shared/ConVars.cs:55` | The enum branch can never match (`f.FieldType == typeof(System.Enum)`). | Use `f.FieldType.IsEnum`. | M1-FIX-STAB · [01]#21 |
| BUG-20 | Low | Data robustness | `Character/Components/CharacterComponent.cs:153`; `Scenes/Menus/GameLoadingScene.cs:52`; `Shared/ArenaModeSaves.cs:32-33` | Hard-coded content IDs (`"grunt"`, `"employee_of_the_month"`) crash at run time if the data is renamed or a mod removes it. The author's TODO says: "game crash if grunt does not exist". | Move the IDs to config, with graceful fallbacks. | M1-FIX-STAB · [01] §2.10, [03] §8 |
| BUG-21 | Low | Settings | `Shared/Settings.cs:126` | An invalid language writes `null` into `Localisation.CurrentLanguage`, which is suspected to blank the UI text. | `TryGet` into a local; keep the current language or fall back to `en-GB`. | M1-FIX-STAB · [05]#15 |
| BUG-22 | Med | Melee data | `base/data/melee_sequences/blunt.seq:9`; `Character/Systems/MeleeSequenceSystem.cs:127-133` | The last blunt key has transition frame 4 and hit frame 8. The final key completes at its transition frame, so the fifth swing never does damage (*inferred* from `MeleeSequenceSystem.cs:36-48`, not run). | Data: set the transition to 9 or more. Code: don't complete a key until all its hit frames are spent. | M1-FIX-COMBAT · [02]#1 |
| BUG-23 | Med | Stats | `Character/CharacterStats.cs:146-169` | The copy constructor omits `AimingSpeed`, `WalkHopDuration`, `MeleeKnockback`, `Abilities` and `WalkAnimation`. It is used by 8 Improbability disks, by `ArenaModeSaves`, and by the tutorial's `TeachDodgeSystem` (`TeachMoveSystem.cs:167-171`). Effects: stacked disks lose Arnold's +8 knockback, custom walk and hop values reset, `AimingSpeed` falls to 40 (tutorial enemies, inferred), and abilities are lost. | Copy every field. Add a reflection test that diffs all fields. | M1-FIX-COMBAT · [02]#2, [04]#3 |
| BUG-24 | Low | Stats | `Character/CharacterStatsDeserialiser.cs:70-116` ([02]) or `:104-109` ([04]) | `Save` never writes `one_handed_gun_seq`, which `Load` reads (`:45`), so a round-trip loses it. | Add the missing line. | M1-FIX-COMBAT · [02]#12, [04]#4 |
| BUG-25 | Med | Throwing | `Physics/Systems/VelocitySystem.cs:193-220` | The `break` is outside the `if`, so only the first `ThrowableSharpBoxes` rectangle is ever tested. Weapons with 2-3 boxes (battle axe, mega hammer, tomahawk, hooked axe, billhook) stick less often than designed. | Move the `break` into the `if`. | M1-FIX-COMBAT · [02]#3 |
| BUG-26 | Med | Throwing | `Physics/Systems/VelocitySystem.cs:171-265` | Thrown weapons check only `PreventAllDamage`, so they ignore the god disk, the dodge meter and `Invincible`. | Mirror the checks in `BulletEmitter` and `MeleeUtils`. | M1-FIX-COMBAT · [02]#4 |
| BUG-27 | Low | Throwing | `Character/CharacterUtilities.cs:633-634,653,687` | `TryThrowWeapon` caches the original weapon's velocity and transform. If the weapon changed during the delay, the new weapon gets the projectile component but the old one gets the velocity. | Re-fetch inside the lambda. | M1-FIX-COMBAT · [02]#6 |
| BUG-28 | Low | Firearms | `Weapons/Systems/WeaponSystem.cs:175-177` | Burst closures keep firing after the gun is thrown or dropped, or after the wielder dies. The out-of-ammo click is evaluated before the burst's shots happen. | Check `weapon.Wielder` and `IsAlive` inside the closure, and move the out-of-ammo check into it. | M1-FIX-COMBAT · [02]#7 |
| BUG-29 | Low | Bullets | `Weapons/Systems/BulletEmitter.cs:339,392-394` | Damage applies on exit-wound hits too, which may double-hit the same part (inferred). The `cosmeticBullet` assignment is dead code. | **Verify with debug drawing first.** Changing it shifts balance and needs Zeph's approval. | M1-FIX-COMBAT (verify only) · [02]#8 |
| BUG-30 | Low | Gore | `Weapons/Systems/BulletEmitter.cs:367-381` | Every hit adds bullet holes to every visible head *and* body apparel piece. A code comment acknowledges this. | Filter by the part that was hit. | M1-FIX-COMBAT · [02]#9 |
| BUG-31 | Low | Weapon data | `Prefabs/Prefabs.cs:545-554` | `AnimatedWeaponPart.AngleCurve` is parsed but never passed on, so rotating parts (cylinders, hammers) can't be authored. | Pass the angle curve to `WeaponPartAnimationComponent`. | M1-FIX-COMBAT · [02]#10 |
| BUG-32 | Low | Melee data | `Character/MeleeSequenceDeserialiser.cs:31,107` | A fixed 16-key array with no bounds check: a 17th line throws. | Use a `List<MeleeSequenceKey>`. | M1-FIX-COMBAT · [02]#11 |
| BUG-33 | Low | Pickup | `Character/Systems/PlayerCharacterSystem.cs:288` | The pickup range compares a squared distance with squared range × an unsquared `Scale`. | Use `Scale*Scale`. | M1-FIX-COMBAT · [02]#13 |
| BUG-34 | Low | Input | `Character/Systems/PlayerCharacterSystem.cs:149`; `Shared/ControlScheme.cs:24` | Ctrl+scroll zoom is hardcoded to LeftControl, which is also the default Ability3 key, so zooming can toggle Ability3. | Make zoom an action, or use another modifier. | M1-FIX-COMBAT · [02]#14 |
| BUG-35 | Low | Dodge | `Character/Systems/JumpDodgeSystem.cs:309-313` | The dash stops if *any* `BlockMovement` collider is within 150 px, including one behind the character. | Raycast in the dash direction instead. | M1-FIX-COMBAT · [02]#15 |
| BUG-36 | Low | Abilities | `Character/Components/Abilities/NoWeapons.cs:27-33` | Calls `TryThrowWeapon` every frame until the delayed throw lands, which restarts the throw animation. | Guard with an "already throwing" check. | M1-FIX-COMBAT · [02]#16 |
| BUG-37 | Low | Abilities | `Character/Systems/CharacterAbilitySystem.cs:121-144` | All abilities read player input, including AI-owned ones. Latent, because AI abilities use slot `None`. | Gate on `PlayerComponent`. | M1-FIX-COMBAT · [02]#17 |
| BUG-38 | Low | Time scale | `Weapons/Systems/BulletEmitter.cs:178-181`; 26 `TimeScale =` assignments in 22 files | The perfect-deflect slow motion hard-resets `TimeScale = 1`, overriding Experiment-mode time scale and any other slow motion. Nothing owns `TimeScale`. | A time-scale owner or stack that restores the previous value. | M1-FIX-COMBAT · [02]#18, [01] §2.2 |
| BUG-39 | Med | AI | `Character/Systems/AiCharacterSystem.cs:192-200` | An exception inside `DoAccurateShot` `return`s out of `Update`, so every remaining AI skips its frame. | `continue` instead of `return`. | M1-FIX-COMBAT · [04]#5 |
| BUG-40 | Low | AI | `AiCharacterSystem.cs:142-147` | Dropping an empty gun `continue`s before `SyncVariables` and the reset of the wants, so stale wants carry into the next frame. | Drop, then fall through. | M1-FIX-COMBAT · [04]#6 |
| BUG-41 | Low | AI | `AiCharacterSystem.cs:708` | AI pickup completes through a `DelayPausable` closure that survives scene changes (author TODO). | Use a component timer (`CharacterPickupComponent`). | M1-FIX-COMBAT · [04]#7 |
| BUG-42 | Med | Factions | `Character/Faction.cs:149-179` (used at `:27-28`) | `GetEnemies` and `GetAllies` return the queried faction instead of the other one, so allied layers are never removed from attack masks. Latent, because shipped data only allies factions with themselves. But escorts or co-op allies would take friendly fire. | `yield return other` (and `Registries.Factions.Get(other)`). | M1-FIX-COMBAT · [04]#1 |
| BUG-43 | Low | Faction data | `base/data/factions/civil.json` | The enemies list says `"zeds"`, but the faction ID is `zed`, so civilians don't target zeds. | Rename it to `zed`. | M1-FIX-COMBAT · [04]#2 |
| BUG-44 | Med [03] / Low [02] | Animation | `Character/CharacterUtilities.cs:476` | `Hand2Scale = Lerp(previous.Hand1Scale, …)`: hand 2's scale crossfades from hand 1's. | Use `previous.Hand2Scale` (a one-token fix). | M1-FIX-ANIM · [02]#5, [03] B1 |
| BUG-45 | Med | Animation | `Character/AnimatedCharacterExtensions.cs:85-93` (constraints ORed at `:14-35`) | `PlayAnimation` never removes the previous clip, so its constraints linger until it ends. With 3 or more clips only the last two blend, so a quick re-trigger pops. | Cap the base list at 2, or take constraints from the main clip only, behind a flag. | M1-FIX-ANIM · [03] B8 |
| BUG-46 | Med | Melee / animation | `Character/Systems/MeleeSequenceSystem.cs:42-44`; `ActiveCharacterAnimation.cs` (`MaxKeyCount`) | `.seq` hit frames are compared with `ceil(t / duration × MaxKeyCount)`, a key-count estimate, not the clip's frame number. Clips with sparse keys fire hits early or late. | Compare against `frame / fps / speed`, storing fps per clip. Bump the `.seq` version so old data keeps the legacy mapping. | M1-FIX-ANIM · [03] B13 |
| BUG-47 | Med | Ragdoll physics | `Physics/Systems/VerletPhysicsSystem.cs:29-32,46-48`; engine `FilterComponentCollection.cs:152-163` | Static 256-entry buffers, and `CopyAllComponentsOfType` silently truncates (verified in the engine source). From the 15th simultaneous ragdoll some links go unsolved and bodies fly apart; from the 29th, nodes stop simulating. Mass kills trigger it, for example wave modes or the Konami-code kill-all (`PlayerCharacterSystem.cs:87-95`). (Engine-side part not re-checked in verification; the 15th/29th thresholds are estimates.) | Grow the buffers (`ArrayPool`, resized when full), or iterate `GetAllComponentsOfType`. | M1-FIX-ANIM · [03] B12, [05] §4 #2 |
| BUG-48 | Med | Decals | `Graphics/Components/DecalRendererComponent.cs:18`; `Graphics/Systems/DecalSystem.cs:30,79` | Past 512 decals per renderer, new decals are silently dropped rather than recycled, while their entities persist. Long fights stop showing new blood and bullet holes. | A ring buffer that overwrites the oldest decal and deletes its entity, or bake old decals into the stamp canvas. | M1-FIX-ANIM · [03] B14, [05] §4 #7 |
| BUG-49 | Low | Animation | `Character/ActiveCharacterAnimation.cs:52-54` | `ScaledTimer` and `ScaledDuration` are wrong or misleading: the timer already advances by `dt × Speed`. | Rename the timer to `Time`. Deprecate the properties (don't delete them). Duration should be `TotalDuration / Speed`. | M1-FIX-ANIM · [03] B2 |
| BUG-50 | Low | Animation | `Animation/AnimationDeserialiser.cs:120-125`; `CharacterPositionSystem.cs:389-391` | The `smooth` header is parsed into `DoSmoothing` but ignored. | Implement it as per-hand smoothing, or document it as a no-op. | M1-FIX-ANIM · [03] B3 |
| BUG-51 | Low | Rendering | `Character/Systems/CharacterPositionSystem.cs:232` | Both body apparel layers get `BodyDecorOrder` with no `+ i`, so their relative order is undefined. The head layers do use `+ i` (`:261`). | Use `BodyDecorOrder + i`. | M1-FIX-ANIM · [03] B4 |
| BUG-52 | Low | Animation | `Animation/AngleCurve.cs:14-15` | Rotation keys lerp linearly rather than by angle, so a key pair crossing ±180° spins the long way. | **Keep it** and document it. Add an opt-in `angle_wrap`, because changing the default would alter authored clips. | M1-FIX-ANIM (docs only) · [03] B5 |
| BUG-53 | Low | Animation | `Animation/AnimationDeserialiser.cs:261,264` | `float.TryParse` for `scale(x,y)` doesn't use the invariant culture, so it fails on comma-decimal locales. | Pass `CultureInfo.InvariantCulture`. | M1-FIX-ANIM · [03] B6 |
| BUG-54 | Low | Animation | `Animation/AnimationDeserialiser.cs:300` | A block whose only keyframe is at t=0 gives a duration of 0 and NaN key times (inferred). | Guard `duration <= 0`. | M1-FIX-ANIM · [03] B7 |
| BUG-55 | Low | Animation | `Character/Systems/CharacterMovementSystem.cs:156-157` | `foot.Offset` is smoothed and then immediately overwritten by the raw target. | Remove one line; keeping the smoothing feels better. | M1-FIX-ANIM · [03] B9 |
| BUG-56 | Low | Rendering | `Character/Stamper.cs:24` | The array from `ArrayPool.Rent` is never returned. | `ArrayPool.Shared.Return(stamps)`. | M1-FIX-ANIM · [03] B10 |
| BUG-57 | Low | Docs | `Physics/VerletLinkMode.cs`; `Graphics/RenderOrders.cs:23-28` | `KeepDistance` is documented as "Unimplemented" but is the default and works. The `RenderOrders` comments disagree with the values, and `RagdollsUpper` and `CharacterLower` share layer 20. | Fix the comments and decide the intended order. | M1-FIX-ANIM · [03] B11, [01]#27 |
| BUG-58 | Low | Character creation | `Character creation/Systems/CharacterCreationSystem.cs:500,507`; `Character/BodyPartMaterialPool.cs:47` | The preview's inner blood colour uses ×0.6522, while the game uses ×0.8, so the preview shows darker blood. | Share one constant. | M1-FIX-ANIM · [03] B15 |
| BUG-59 | Low | Animation | `Character/Systems/CharacterPositionSystem.cs:633-637` | Hand rotation special-cases the `"dodge"` and `"pickup"` groups by string (the code's own comment: "TODO what the fuck"). Every new group needs another hardcoded check. | A per-clip header flag. | M1-FIX-ANIM · [03] B16 |
| BUG-60 | Low | Animation | `Character/Systems/CharacterPositionSystem.cs:641-650` | Hands blend out from `PreviousAnimatedPosition`, which is a world position. If the character moves during the 0.2 s blend-out, the hands trail behind. | Store the position relative to `GlobalCenter`. | M1-FIX-ANIM · [03] B17 |
| BUG-61 | Med | Modes / disks | `Scenes/SceneUtils.cs:329-371` (mutation at `:353,365`) | The `fewer_enemies` and `more_enemies` disks change `level.MaxEnemyCount` *after* the spawner has copied it (`:334`), so they have no effect on this load. Because `level` is the cached asset, the change also accumulates on every reload (+4 per attempt) for the rest of the session (inferred). | Apply modifiers to the spawning and progress components, never to `Level`. | M1-FIX-LEVEL · [04]#10, [01] §2.3 |
| BUG-62 | Med | Incident | `Incident mode/IncidentConfig.cs:62-121` | Incident generation mutates the cached, shared `Level` assets: `BodyCountToWin`, music, `MaxEnemyCount`, weapons, spawn lists, `Objects.Add`. The changes leak into later incidents and other modes (inferred). | Deep-clone each level before modifying it, or keep overrides in a per-run struct. | M1-FIX-LEVEL · [04]#11 |
| BUG-63 | Med | Incident | `Incident mode/IncidentConfig.cs:99-100` | An inverted check (`!=` instead of `==`). It adds `IncidentSystem` only if the level has no *other* `GameSystem`, and adds a duplicate if it already has one, for example on a second run because of BUG-62. | `ss.SystemTypeName == sys`. | M1-FIX-LEVEL · [04]#12 |
| BUG-64 | Med | Campaign content | `base/data/levels/lvl_cutscene_bossbattle.json` (the "music thing" `LevelScript`); `Shared/PersistentSoundHandles.cs:10` | The script references `CampaignScene.LevelMusic`, which no longer exists. It fails to compile at run time and the error is only logged, so the boss-battle music cue at 93 s never plays (verified by reading; not run). By combining this with BUG-15, the synthesis infers that it also recompiles every frame during that level. | Use `PersistentSoundHandles.LevelMusic`. Add a test that compiles every shipped `LevelScript`. | M1-FIX-LEVEL · [04]#17 |
| BUG-65 | Low | Spawning | `Level/Systems/EnemySpawningSystem.cs:159` | `Utilities.RandomInt` has an exclusive maximum, so door spawns produce 1-2 enemies, never the intended 3. | `RandomInt(1, Math.Min(max, 3) + 1)`. | M1-FIX-LEVEL · [04]#13 |
| BUG-66 | Low | Wave data | `base/data/arena/waves.json`; `Level/EnemySpawnInstructions.cs:118-120` | The waves use `"Look"` and `"Stats"` keys, but the class fields are `LookKey`, `StatsKey` and `FactionKey`, so the values are silently ignored. The defaults happen to be grunt. | Fix the keys, or add `[JsonProperty]` aliases. | M1-FIX-LEVEL · [04]#14 |
| BUG-67 | Low | Incident | `Incident mode/IncidentConfig.cs:19-20` | `HashCode.Combine` is randomised per process, so the `INCIDENT:0x…` name for a seed changes on every launch, and its 16 bits collide easily. | Show the seed itself, or use a stable hash such as FNV-1a. | M1-FIX-LEVEL · [04]#20, [05]#19 |
| BUG-68 | Low | UI | `CampaignMenuSystem.cs:219`; `LevelSelectMenuSystem.cs:375`; `Shared/Systems/PauseSystem.cs:159` | The `TimeSpan` `hh`/`h` formats print the hours *component*, so totals over 24 h wrap. | Use `(int)ts.TotalHours`. | M1-FIX-LEVEL · [04]#21 |
| BUG-69 | Low | Audio data | `base/data/incident_music.json:7-8`; `Registries/Registries.cs:338` | Names `madness4.ogg` and `madness5_a.ogg`, but the files are `madness_4.ogg` and `madness_5_a.ogg`. Missing IDs are skipped silently, so two tracks never play. Both tracks are unlicensed anyway (section 14). | Fix the paths, and log a warning on a missing ID. | M1-FIX-LEVEL · [06] §3 |
| BUG-70 | Low (debt) | Spawning | `Level/Systems/EnemySpawningSystem.cs:59-61`; `Level/Components/EnemySpawningComponent.cs:63` | The dropped-weapon throttle is commented out, while `DroppedWeaponAmountThreshold` is still declared. | Delete it, or re-enable it with cached counts. | M1-FIX-LEVEL · [04]#24 |
| BUG-71 | Med | Input | `Level editor/Systems/UnlockDiskSystem.cs:38`; `Shared/Controller/ControllerLoopEvent.cs:419-424` | Improbability disks need a raw left click on the disk. The pad layer injects a click only in menus, so controller-only players can't collect disks, and the input ignores bindings. | Accept `GameAction.Interact` near the disk, or Attack while hovering it. | M1-FIX-INPUT · [05]#9 |
| BUG-72 | Low | Input | `Shared/UserInput.cs:93` | The mouse constructor checks the field `buttons` instead of the argument `m`. | Use `m.Length > 0`. | M1-FIX-INPUT · [05]#16 |
| BUG-73 | Low | Settings | `User interface/Systems/SettingsSceneSystem.cs:161-171` | "Reset controls" replaces the whole `ControlScheme`, which also resets the pad bindings. | Reset only `InputMap`. | M1-FIX-INPUT · [05]#17 |
| BUG-74 | Low | Settings | `SettingsSceneSystem.cs:308-324` | Writes to `InputMap` while enumerating it. It works on .NET Core 3+, but it is fragile. | Use `.ToArray()`, as the pad code does. | M1-FIX-INPUT · [05]#18 |
| BUG-75 | Med | Tests | `MadnessInteractiveReloaded.Test/Deserialisation/HandArmourDeserialiserTest.cs:166` (constructor at `:14-27`) | `PositiveCaseTest` fails when `base.waa` was registered earlier in the run, because it compares a package-resolved `GlobalAssetId` with a package-agnostic one (see section 15). It also replaces the `Texture` loader globally and never restores it. | Compare with `new GlobalAssetId(expected).ResolveExternal()`, or compare `Internal`. Register `base` in a shared fixture. Restore the loader. | M1-FIX-TEST · [01]#13, [05]#14 |
| BUG-76 | Low | Tests | `HandArmourDeserialiserTest.cs:16`, `TestGameUtil.cs:16`, `ArmourDeserialiserTest.cs:15`, `CharacterLookDeserialiserTest.cs:23`, `CharacterStatsDeserialiserTest.cs:12`, `MadnessUtilsTest.cs:15` | An inverted "already registered" guard is copy-pasted into 6 test setups. It re-initialises only when `base` is *already* registered, which produces the "Package … already exists" warnings. | One shared `ICollectionFixture` that registers `base` once. | M1-FIX-TEST · [01]#25 |
| BUG-77 | Low | Tests | `MadnessInteractiveReloaded.Test/GameTests/TestGameUtil.cs:36-99` | `StepGame` is a stale hand copy of the engine loop:<br>• it never calls `RoutineScheduler.StepRoutines`, so the game's 67 delay and routine uses never progress in tests<br>• it never calls `RenderSystems`<br>• it has no fixed-update cap, and computes `Interpolation` differently | Mirror `Game.Start`, including routines and optional rendering against `MockGraphics`. | M1-FIX-TEST · [01]#24, [05] §2.7 |
| BUG-78 | Low | Tests | `MadnessInteractiveReloaded.Test/GameTests/AnimationTest.cs:68-72` | The mixing test asserts a linear lerp only after the mix has finished, so it passes even if blending is broken mid-transition. It also depends on `ScaledTimer` (BUG-49). | Assert at 50% mix, using `Quad.InOut`. | M1-FIX-TEST · [03] B18 |
| BUG-79 | Med | AI | `Character/Systems/AiCharacterSystem.cs:89-93,546-576,310-333` | A target is chosen only when the AI has none, and the choice ignores line of sight: omniscient targeting and aim, with no re-targeting. | A perception component (LOS plus memory) with periodic re-evaluation. | M2-DEBT (C-ARCH) · [04]#8 |
| BUG-80 | Low | AI | `AiCharacterSystem.cs:35-51` | Attack slots never rotate: the first N AI in iteration order keep them (inferred). | Rotate on a timer, or score by distance or visibility. | M2-DEBT (C-ARCH) · [04]#9 |
| BUG-81 | Debt | Melee | `Character/MeleeUtils.cs:60-68,87`; `MeleeSequenceSystem.cs:187` | A "Welcome to hell" comment: the block matrix comes from an obsolete design document, the melee range has a `+200` magic number, and `MeleeDamageType.Firearm` on a melee weapon is handled as "wtf". | Document the matrix, or move it into data. | M2-DEBT (B-ARCH) · [02]#19 |
| BUG-82 | Debt | Combat feedback | `Weapons/Systems/BulletEmitter.cs:171-182` | The perfect-deflect sound and slow motion live inside the ray code ("TODO should be in a system"). | Emit an event and handle feedback in a `CombatFeedbackSystem`. | M2-DEBT · [02]#20 |
| BUG-83 | Low (UX) | Campaign | `Level/Systems/LevelProgressSystem.cs:96-99` | Finishing a campaign drops straight to the main menu ("TODO stats ending screen?"). | A results screen (time, kills, deaths, first-try levels). It is the natural home for scores too. | M2-DEBT (D-ARCH) · [04]#23 |
| BUG-84 | Low (debt) | Modes | `Arena/*`; `Scenes/Game/ContinuousGameplayScene.cs:7` | Dead and stub mode code: an empty `ArenaModeSystem`, an unreachable Arena scene and an obsolete continuous scene. | Remove it, or fold it into the shared wave system. | M2-DEBT (D-ARCH) · [04]#25 |
| BUG-85 | Med | Audio | `Camera/Systems/CameraMovementSystem.cs:113-114`; `Assets/Sounds.cs:13`; engine `OpenALAudioRenderer.cs:218,269` | No positional audio or distance attenuation. The listener and `SpatialMultiplier` are zeroed every frame, every SFX is non-spatial, and no call site passes a position. The engine also applies `SpatialMultiplier` twice. (Engine-side part not re-checked in verification.) | Put the listener at the camera. Use `SpatialParams` with a linear model (a reference distance of about one screen). Pass positions at the main call sites. Fix the double multiply first. | M2-FIX-AUDIO · [05]#11 |
| BUG-86 | Med (suspected) | Audio (engine) | engine `Walgelijk.OpenTK/Audio/OpenALAudioRenderer.cs:215-239`; `Walgelijk/Shared/Pool.cs:49-65` | The engine creates and plays a source *before* asking its 128-slot tracker pool. When the pool is full, the source leaks. Past OpenAL Soft's 256 sources, new sounds silently fail. The NuGet 0.24.0 IL has the same order. (Engine-side part not re-checked in verification; can't verify.) | A game-side `SfxLimiter`: cap plays per sound per frame and in total, and skip once about 100 are active. Report it upstream. | M2-FIX-AUDIO · [05]#10 |
| BUG-87 | High [01] (once levels are shared) / Med [04]#16 + High [04]#18 | Security | `Level/LevelDeserialiser.cs:24,119-127`; `Level editor/LevelObjects/GameSystem.cs:81-90`; `Level/BuiltLevelScript.cs:55-124` | **A shared level file is arbitrary code:**<br>• `TypeNameHandling.Auto` with no serialisation binder (a known Newtonsoft gadget risk)<br>• `GameSystem` instantiates any `System` by name<br>• `LevelScript` compiles full-trust, unsandboxed C#<br>Opening someone else's level runs their code. | An `ISerializationBinder` allow-list of level-object and slide types (do the MIR→IIR mapping there too). Refuse scripts and `GameSystem` in non-shipped levels, or prompt first. Longer term, a sandboxed trigger/action format. | M4-FIX-SEC · [01]#2, [04]#16, [04]#18 |
| BUG-88 | Low | Level editor | `Level editor/Components/LevelEditor.LevelEditorComponent.cs:114-122` | The `GameSystem` dropdown lists every `Walgelijk.System` in the assembly, including core ones, so a level can add duplicates. | An allow-list via a `[LevelSystem]` attribute. | M4-FIX-SEC · [04]#22 |
| BUG-89 | Low | Modding | `Modding/Mod.cs:184-186`; `Modding/ModID.cs:5-7` | `ModID` is not validated but is used in a recursive delete path. An ID like `..\..\x` would delete outside `%TEMP%\IIR` (inferred; low likelihood). | Restrict IDs to `[a-z0-9._-]`. | M4-FIX-SEC · [01]#18 |
| BUG-90 | Med | Modding | `Modding/Mod.cs:147-151`; `Modding/ModLoader.cs:145-149` | A missing `thumb.png` counts as an error, and mods with errors never activate. The only trace is a log line. | Treat it as a warning. | M4-FIX-MOD · [01]#7 |
| BUG-91 | Med | Modding | `Modding/LocalModCollectionSource.cs:74-75,84`; `ModLoader.cs:155-156` | A broken or duplicate-ID `.zip`, or a colliding package ID, throws out of loading and aborts every later mod. The async loading step swallows the exception (BUG-11). | Catch errors per mod and per package, record them in `Mod.Errors`, and show them in the mod menu. | M4-FIX-MOD · [01]#8 |
| BUG-92 | Med | Modding | `Assets/Textures.cs:11-14`; first touched at `MadnessInteractiveReloaded.cs:76` | The 99 `Textures.*` statics bind to `base` before mod packages are registered, so mods cannot override them (inferred; not tested in game). | Register packages first, or use package-agnostic IDs resolved at each use. | M4-FIX-MOD · [01]#12 |
| BUG-93 | Low | Modding | `Modding/ModLoader.cs:103-124`; `MadnessInteractiveReloaded.cs:270`; `Modding/Mod.cs:176-188`; `LocalModCollectionSource.cs:100-115` | Mod lifecycle problems:<br>• `OnLoad` and `OnReady` run on a thread-pool thread<br>• `OnUnload` is never called<br>• `Dependencies` are ignored for load order<br>• duplicates are listed twice<br>• there is no enable/disable<br>• every mod is re-extracted each launch, and folder mods are also copied to `debugout` | Marshal mod events to the main thread. Call `OnUnload` at shutdown. Sort by dependency. Cache extraction by hash. Delete the debug copy. | M4-FIX-MOD · [01]#17, [01] §2.7 |
| BUG-94 | Med | Level editor | `Level editor/Components/LevelEditor.LevelEditorComponent.cs:95-100,134-171` | Redo restores the same state as Undo, because Undo pushes the state it just restored. The undo history is unbounded full-level JSON snapshots. | Push the current level JSON before replacing it. Cap the history. | M4-FIX-MOD (G71) · [01]#10 |
| BUG-95 | Low | Armour editor | `Armour editor/ArmourEditorSystem.cs:119` | Saving is disabled for `Hand` pieces. There is no editing of broken replacements or of the `Detachable` and `Hidden` flags. | Enable hand-armour saves and add the missing fields. | M4-FIX-MOD · [01] §2.9, [03] §2.10 |
| BUG-96 | High (release) | Branding | `DisclaimerRenderSystem.cs:30`; `Scenes/SceneUtils.cs:60,188`; `Shared/Branding.cs:18` | Every scene shows the inherited banner "BETA BUILD - Found a bug? Report it on our Discord", and `Branding.DiscordUrl` is null. | Remove it, show it only in debug builds, or point it at the itch page. | M5-FIX-REL · [05]#3, [06] §7 |
| BUG-97 | High (release; rated by this synthesis) | Distribution | `MIR.csproj` (no `SelfContained` or `RuntimeIdentifier`; pack step at `:44,48`) | The build is framework-dependent, so players must install the .NET 9 Desktop Runtime. The post-publish pack runs the freshly built exe, so cross-building for Linux needs a packer that runs on the host. [05] calls this a release blocker but gives no severity. | A self-contained publish per runtime, optionally trimmed. A separate packer invocation. | M5-FIX-REL · [05] §7, §9 |
| BUG-98 | Low (inferred) | Cheats | `Shared/MadnessCommands.cs:38-431`; `MadnessInteractiveReloaded.cs:236`; `Improbability/ImprobabilityDisks.cs:129` | The F12 developer console works in release builds; only its notification is hidden. Its commands include God, Give, InfiniteAmmo, Timescale, SetLevel, WinLevel, UnlockDisk and Crash. This matters for achievements, speedruns and leaderboards. | Gate cheats behind a developer setting or a `--dev` flag, and flag runs that used them. | M5-FIX-REL · [05]#23, [01] §2.7 |
| BUG-99 | Low | UI | `Shared/Systems/PauseSystem.cs:214-229` | Retry is commented out of the pause menu, and Quit has no confirmation. | Restore Retry. Add a `ConfirmationDialogComponent`. | M5-FIX-REL · [05]#21 |
| BUG-100 | Low | Debt | Build log (12 CS0612, 5 CS0618); `MadnessInteractiveReloaded.cs:21,57-58`; `Shared/Settings.cs:75-81`; `Shared/InputExtensions.cs:21-75`; `MIR.csproj:70` | 17 uses of obsolete engine APIs that will break on an engine upgrade (`ShaderDefaults.WorldSpaceVertex`, `LegacyDoor`, `DelegateDeserialiserBridge` and others). The unused `Walgelijk.PortAudio` package. Dead `InputSettings` and display-name helpers, whose Numpad labels are swapped. | Migrate to `BuiltInShaders`. Convert legacy doors. Drop the package and the dead code. | M5-FIX-REL · [01]#26, [05]#20 |
| BUG-101 | Low | Engine | engine `Game.cs:252-253,264-283` | The scaled and unscaled scene timers are swapped, and the fixed-step accumulator is never clamped after the 10-step cap. | Report it upstream. Avoid `SecondsSinceSceneChange*` in game code. | M5-FIX-REL · [05]#22 |
| BUG-102 | Low (inferred) | Linux | `MIR.csproj:58`; `obj/project.assets.json` | `SkiaSharp.NativeAssets.Linux 3.116.1` is pinned while the managed SkiaSharp resolves to 2.88.9. The native/managed major-version mismatch may break weapon thumbnails on Linux (not tested). | Align the versions. | M5-FIX-REL · [06] §7 |
| BUG-103 | High (legal) | Notices | `NOTICE.txt:1-245` (ImageSharp entry at `:68-70`) | Lists 4 of about 30 shipped components and none of the native binaries. Mislabels ImageSharp, which uses the Split License and isn't shipped. Has no font, art or audio licences. | Generate it from a provenance manifest plus `project.assets.json`. | M5-LIC · [06] §3 |
| BUG-104 | High (legal) | Native binaries | `avcodec-59.dll` and the other FFmpeg DLLs; `MIR.csproj:92-120` | FFmpeg 5.1.2 ships as a GPL-2.0+ build with x264, xvid and LAME, with no offer of the corresponding source. The game doesn't need the encoders. | Ship an LGPL decoder-only FFmpeg build and publish its source tarball and configure line. Optionally re-encode the cutscenes to VP9/AV1 + Opus. | M5-LIC · [06] §3, §2.7 |
| BUG-105 | High (legal) | SFX | `base/sounds/weapon_drop/**`, `forcefield_off.wav`, `turret_death.wav`, `firearms/laser_rifle_*.wav`, `train_emergency_halt.ogg` | Commercial-library SFX (BOOM, PMSFX, Sound Ideas, Sound Pack Tree) are committed raw to a public GPL repo. One is tagged `~madness-project-nexus`. | Remove them from the repo, and from its history, before publicising the fork. Replace them. | M5-LIC · [06] §3 |
| BUG-106 | Med | Credits | `base/data/credits.txt:17-25` | Omits Meonly70, DJ Birdie, Arthur Smith, MonoCronic, every font author and every SFX source. 11 tracks have no author anywhere. | Generate the credits from the provenance manifest. | M5-LIC · [06] §3 |
| BUG-107 | Med | Fonts | `base/fonts/tipografia.wf`; `Scenes/Debugging/TextTestingScene.cs:84` | Unknown provenance, and used only by a debug scene. | Delete it. | M5-LIC · [06] §3 |
| BUG-108 | Med (size and IP) | Assets | `base/textures/backgrounds/madness combat/` (59 QOI, 36.1 MiB (37.8 MB)); `sounds/music/madness_4_party.ogg` (4 MB) | Unreferenced Krinkels-derived episode backgrounds and an unreferenced track ship inside `base.waa`. | Delete them, or move them out of `base/`. | M5-LIC · [06] §3, [04] §7 |
| BUG-109 | Low | Tools | `src/MadnessInteractiveReloaded/png2qoi.py:15-17` | Passes the bare file name in directory mode, so it only works when run from inside the folder. | `os.path.join(input, p)`. | M5-LIC · [06] §3 |
| BUG-110 | Low | Assets | `base/fonts` and `resources/fonts` | `roboto mono.fnt/.png` exists in both places. | Keep one copy. | M5-LIC · [06] §3 |

---

## 14. Licensing and commercial distribution concerns [06]

The full asset inventory, the per-feature asset needs and the replacement plan are in **`docs/ASSET_REQUIREMENTS.md`**. This section summarises the blockers. It is a technical reading of licences and public statements, not legal advice.

### 14.1 Bottom line

**The game as it stands cannot be sold.**

- **The GPL covers the code, not the IP.** The repo's only licence is the plain GPL-3.0 text, here and upstream, with no clause about assets.
  - At best that offers Studio Minus's *own* art, animation, levels and video under GPL-3.0. That is inferred and should be confirmed in writing.
  - It cannot license Krinkels's characters, setting and name, the guest musicians' tracks, or the commercial sound libraries.
- **itch.io requires a warranty.** Its Terms (§4) require publishers to warrant they hold the rights to their content, so selling unlicensed Madness IP or library SFX would breach that warranty.

### 14.2 Release blockers (condensed from [06] §2.8.1)

| # | Asset group | Size | Owner (best evidence) | Status for a $9.99 sale |
|---|---|---|---|---|
| 1 | **Madness Combat characters and setting:** the grunt/agent/AAHW look; Hank, Sanford, Deimos, Jebus, Tricky, Auditor, Mag Agent, Zed, Doc; Nexus; the "Madness" name. This covers the 60 looks, factions, most of the 250 armour pieces, and the boss and campaign content. | — | Matt "Krinkels" Jolly; "MADNESS: Project Nexus"™ is GIBBING TREE, LLC's | **BLOCKER.** Needs a written licence, or full replacement with an original cast and setting. A paid game with the same characters would compete with the rights holder's commercial product. |
| 2 | Madness Combat episode backgrounds | 59 QOI, 36.1 MiB (37.8 MB), unreferenced | Krinkels | **BLOCKER and dead weight.** Delete (BUG-108). |
| 3 | Studio Minus art, animation (192 `.anim`), levels (107) and video | About 200 MB of textures plus 196 MB of video | Studio Minus | GPL-3.0 by inclusion in the repo (inferred). It depicts row 1, so it is usable only if row 1 is cleared. **Ask Studio Minus in writing.** |
| 4 | **Music** | 44 OGG, 208 MiB (217 MB) | Lothyde (+MonoCronic), Cheshyre, API, Mmodule, Meonly70, DJ Birdie / Werner Thomas (1957 "Chicken Dance"), Arthur Smith, and 11 tracks with no author | **BLOCKER.** No licence was found for any track. Several are the Madness Combat episode soundtracks. |
| 5 | **SFX** | At least 14 files with commercial-library tags; about 274 with no provenance | BOOM Library, PMSFX, Sound Ideas, Sound Pack Tree, C. A. Sound. The purchaser's licence doesn't transfer. One file is tagged `~madness-project-nexus`. | **BLOCKER.** Replace every SFX (BUG-105). |
| 6 | Impact font (`impact.wf`) | — | Monotype. Microsoft's FAQ forbids bitmap conversion for games. | **BLOCKER.** Replace it with an OFL font. |
| 7 | Toxigenesis font (`toxigenesis.wf`, drives the HUD) | — | Typodermic. The free licence excludes games and glyph sprites. | **BLOCKER** unless a game licence is bought. |
| 8 | Real gun brand names in weapon `Name`s (Desert Eagle, Glock, Benelli, Beretta, Colt, HK, Uzi and others) | — | Manufacturers' trademarks | A risk, not researched. Renaming to fictional names is common practice. |
| 9 | FFmpeg GPL build | 6 DLLs, 36 MB | FFmpeg plus x264, xvid, LAME and others | Allowed, but **the source must be offered**. Better: an LGPL decoder-only build (BUG-104). |
| 10 | Credits and notices | `credits.txt`, `NOTICE.txt` | — | Must list every third-party licence and author before release (BUG-103, BUG-106). |

### 14.3 What the GPL allows a seller [06 §2.8]

- **Selling is allowed.** Buyers may then redistribute the game, and the code's source must be available to them.
- **New art can stay proprietary.** GPLv3 §5 "aggregate" lets Zeph keep his *own new* art and audio proprietary next to the GPL code. This is the id Software model.
- **The GPL never grants trademark or character rights.**

### 14.4 Third-party code and binaries [06 §2.7]

| Component | Licence and what it requires |
|---|---|
| Walgelijk core, OpenTK, ParticleSystem, Physics, SimpleDrawing, Newtonsoft, Roslyn, MonoMod, Mono.Cecil, SkiaSharp, NVorbis | MIT |
| Walgelijk sub-packages with no licence metadata (AssetManager, CommonAssetDeserialisers, Localisation, Onion, MotionTK, PortAudio) | Treat as GPL-3.0 at worst. Compatible with the game. |
| `NativeFileDialogExtendedSharp` | GPL-3.0-only. Compatible. |
| `FFmpeg.AutoGen` | LGPL-3.0 |
| OpenAL Soft | LGPL. Needs the licence text and a source offer. |
| GLFW | zlib |
| Harmony | MIT |
| BigGustave | Unlicense |
| Shader noise snippets | MIT, and need a notice |
| ImageSharp | Used only by the unshipped `mirbgencoder`. It is under the Split License, not Apache as `NOTICE.txt` claims; Zeph qualifies for its Apache-2.0 path. |

`NOTICE.txt` covers only 4 of about 30 components (BUG-103). No OFL licence texts ship for Inter, Oxanium, Cascadia or Roboto Mono, and the OFL requires them.

### 14.5 Other commercial-readiness items

- **Branding leftovers:**
  - the "BETA BUILD … our Discord" banner (BUG-96)
  - the tutorial campaign's author is listed as "Studio Minus"
  - the `mir_` crash-file prefix
  - Improbability disk names that reference films and anime (Willis, Gump, FLCL, Ghost Protocol)
  - [05] §7 and §9 list these.
- **Distribution:** the build is framework-dependent (BUG-97). The download is about 0.75 GB, with most of it in music, QOI backgrounds and video (PERF-12).
- **A demo build is feasible in about a week (M)** [05 §7]:
  - Pack a filtered `base/`, and hide the editor, mods, Incident and Experiment behind a `DEMO` constant.
  - Risks: the `Sounds` static initialisers need about 60 fixed asset paths; levels must not reference stripped content; and demo saves must be separate from, or forward-compatible with, the full game.

### 14.6 Recommended order [06 §6]

| # | Action | Effort |
|---|---|---|
| 1 | **Settle the Madness IP question before writing more content.** Either get written commercial permission from Krinkels and GIBBING TREE, or plan a total conversion with an original cast, factions, setting and a name without "Madness". | XL if reskinning |
| 2 | **Ask Studio Minus in writing** whether the GPL covers their art, animations, levels and video, and whether they object to a paid fork. Ask for music and SFX contacts at the same time. | S |
| 3 | Replace all music with a commissioned OST or CC0/CC-BY tracks. Rename the "Madness Combat N" DJ entries. | M-L |
| 4 | Remove the tagged SFX from the public repo now. Rebuild the SFX set from Sonniss GDC bundles and Freesound CC0, keeping raw files in a private asset store if needed. | M |
| 5 | Replace Impact and Toxigenesis with OFL fonts. Delete Tipografia. Ship the OFL texts. | S |
| 6 | Use an LGPL decoder-only FFmpeg with a source tarball, and regenerate `NOTICE.txt`. | S-M |
| 7 | Add a provenance manifest, a CI licence gate and a sprite validator. These also auto-generate the credits and notices. | M |
| 8 | Cut dead weight (BUG-108), fix the asset bugs (BUG-69) and rename real gun brands. | S |

---

## 15. Testing baseline [01 §2.0, 2.8; 05 §2.7]

### 15.1 Build (measured by [01])

The command was `dotnet build src/MIR.sln` (Debug, SDK 9.0.321).

- **Result:** succeeded with **0 errors and 110 warnings**.
- **Time:** 13 s wall clock (12.22 s MSBuild). This was a *warm* build: restore was up to date and `bin/` and `obj/` existed.
- **Post-build:** the step re-packed `base.waa` (2,491 assets).

110 is MSBuild's count. Removing the lines it prints twice leaves **101 distinct warnings**: 87 in `MIR.csproj`, 5 in `MIR.Test.csproj`, and the rest in `mirbgencoder`.

| Code | Count | Meaning |
|---|---|---|
| CS8618 | 12 | Non-nullable field not initialised |
| CS0612 | 12 | Use of `[Obsolete]` members (`MovementBlocker`, `AllBlocker`, `BenchmarkSystem`, `ContinuousGameplayScene`, `DelegateDeserialiserBridge`, `MadnessUtils.GetAimAccuracy`) |
| CS8625 | 11 | Null literal passed to a non-nullable |
| CS8602 | 11 | Possible null dereference (3 in `ControlSchemeTests.cs`) |
| NU1903 | 5 | **High** security advisories in `SixLabors.ImageSharp 3.1.6`. It is in the `mirbgencoder` tool only and is not shipped. |
| CS0618 | 5 | Obsolete with a message: `ShaderDefaults.WorldSpaceVertex`, `LegacyDoor`, `OLD_SpawnDoorInstructions` |
| CS0219 | 5 | Variable assigned but never used |
| CS8321 | 4 | Local function never used |
| NU1902 | 3 | Moderate advisories in the same ImageSharp package |
| CS1587, CS1574, CS1573 | 3 each | XML doc-comment problems |
| Others | — | CS0162 unreachable code ×3, CS0414 ×3, CS0649 ×3, SYSLIB0050 ×1, MSB3246 ×1, CA2022 ×1 |

Only two kinds of warning are errors in waiting:
- **CS0612 and CS0618:** obsolete engine APIs that will break on an engine upgrade (BUG-100).
- **NU190x:** advisories that affect the tool only.

### 15.2 Tests (measured by [01])

The command was `dotnet test src/MIR.sln --no-build`.

| Run | Passed | Failed | Skipped | Time |
|---|---|---|---|---|
| **191** | **190** | **1** | 0 | 7 s wall clock (5.22 s test time) |

[05] counted about 205 tests by grepping `[Fact]` and `[Theory]` without running them. [01]'s executed count of 191 is the authoritative number.

**Distribution by area:**

| Area | Tests |
|---|---|
| Controller layer (the recent XInput work) | 139 (73%) |
| Deserialisation and serialisation | 23 |
| Legacy migration | 7 |
| `MadnessUtils` | 7 |
| Routine scheduling | 5 |
| Branding | 4 |
| Animation | 2 |
| Other | 4 |

A deliberately locked-file test (`LegacyDataMigrationTests.cs:100-110`) prints an expected `IOException` in the log. It passes.

### 15.3 The flaky test and its root cause

The failing test is `MIR.Test.Deserialisation.HandArmourDeserialiserTest.PositiveCaseTest`. Its message is `Expected id.Id to be -1488260670, but found -1488260670.` Verified by reading the test and the engine source [01]:

1. The game loads hand textures with `Assets.Load<Texture>(lineValue)` (`Character/HandArmourDeserialiser.cs:49,51`).
2. `Assets.Load` calls `id.ResolveExternal()`. When the `base` package is registered, that turns a package-agnostic ID into `base:<asset>`.
3. The test compares against `new GlobalAssetId(expected)` (`HandArmourDeserialiserTest.cs:166`), which is package-agnostic. `GlobalAssetId.Equals` compares both the package and the asset, so the two IDs differ.
4. The message *looks* identical because `ToString()` prints only the internal hash for agnostic IDs *and* for IDs in the vanilla `base` package.
5. **The failure depends on test order, not chance.** The test fails whenever an earlier test in the same process registered `resources/base.waa`. Several classes in the shared `[Collection("Registry collection")]` do that, and the static `Assets` registry is never cleared between tests. ([05] independently attributed it to test order.)
6. The constructor's guard is inverted (BUG-76), and the test also replaces the `Texture` loader without restoring it.

**Fix (S, one line):** compare with `new GlobalAssetId(expected).ResolveExternal()`, or compare `id.Id.Internal` with `new AssetId(expected)`. Better still, replace the 6 copied guards with one shared fixture that registers `base` once.

### 15.4 Gaps

- **Untested areas.** Weapons and damage, melee, AI and spawning, level loading, campaign progress and saves, the settings round-trip, Improbability disks, Incident generation, mods, level scripts, the editors, the Verlet solver, audio and rendering.
- **No data validation.** There is **no "every shipped asset loads without errors" test**, even though the registry loaders swallow per-file errors.
- **The harness** (`TestGameUtil.StepGame`) diverges from the engine loop. It never runs routines or `RenderSystems` and has no fixed-step cap (BUG-77). `ControllerTestHarness` is the template for gameplay tests.
- **Infrastructure:**
  - The test build uses Windows-only `xcopy` and runs the game exe to pack assets, so it can't run on Linux CI as it is.
  - Each test build re-packs 708 MB.
  - **There is no CI.**

### 15.5 Proposed QA strategy [05 §2.8, 01 §6]

1. **Fix the test baseline (M1-FIX-TEST).** Fix the flaky test and the shared fixture, make `StepGame` mirror the engine loop, and add a GitHub Actions `windows-latest` build-and-test job.
2. **Unit and contract tests:**
   - a settings JSON round-trip, including missing keys and an invalid language
   - a campaign-stats save, load and corrupt-file recovery
   - the disk incompatibility rules
   - Incident: same seed, same levels
   - `UserInput` parsing
   - a reflection test on the `CharacterStats` copy (BUG-23)
3. **Data validation.** Every level, weapon, look, armour piece, campaign, DJ entry and incident-music path deserialises, and every asset it references exists. Also compile every shipped `LevelScript` (BUG-64). This guards mods, the demo build and future content.
4. **Headless simulation tests:**
   - load each level and step 2 s
   - N AI against an invincible player for 60 s, asserting a bounded entity count and ragdoll cleanup
   - the campaign win → save → unlock flow
   - pause and resume
5. **A benchmark scene** with fixed seed, recorded frame-time percentiles and GC counts (section 12).
6. **A manual checklist per release candidate:**
   - pads (Xbox, PlayStation through Steam Input, hot-plug, disk pickup)
   - video modes, VSync and caps, 4K at 150% scaling
   - audio device unplug
   - saves: first launch, a legacy MIR folder, a corrupt file, killing the process mid-save, read-only AppData
   - a full campaign run
   - a readable `Crash` report
   - a low-end iGPU after 30 minutes of play

---

## Appendix A: Where the auditors disagreed

| Topic | Views | Resolution |
|---|---|---|
| Busy-wait limiter evidence | [01] inferred it from the engine `master` source and the 0.30.0 member names. [05] confirmed the loop in the shipped 0.30.0 IL. | [05] is better evidenced. Neither measured the CPU load. |
| Test count | [01] ran 191 tests. [05] grepped about 205. | [01] (executed). |
| Level count | [04] 106 files; [01] and [06] 107; [05] "56 levels" | [05]'s 56 looks like the root `levels/` folder only. The 106 vs 107 gap is unexplained; treat it as about 106. |
| Level editor object types | [04] 20 placeable; [01] 22 classes including 3 obsolete | Consistent: obsolete types aren't in the palette. |
| Hardcoded UI strings | [01] at least 141 (including editors); [05] 34 outside the editors | Both stand. They measure different scopes. |
| Faction layer bits | [02] "start at bit 16"; [04] left-shifted from value 16, so bits 5-31 and at most 27 factions | [04] traced the assignment; better evidenced. |
| Cosmetics affecting gameplay | [05] flags that armour `DeflectChance` in the player's look feeds deflection and asks [02]/[03] to confirm. [02] lists "a non-cosmetic look" as a deflection requirement. [01] says the player look is created with `Cosmetic = true` (`GameLoadingSteps.cs:98`). | Combining [01] and [02], the player's custom look probably **does not** deflect bullets in practice (synthesis inference; not traced end to end). The data-model coupling still stands: the same `ArmourPiece` holds cosmetic and protective values. |
| Severity of BUG-06, BUG-07, BUG-13, BUG-44, BUG-87 | See the bug table | Both ratings are shown and counted by the higher one. |
| D46 daily seeds | [04]: M, with a per-run `Random`. [01]/[05]: needs fixed-step and a seeded engine RNG. | Different targets. Same-seed levels and spawns are M; replay-verifiable runs need ARCH-DET. |
| G71, G72, PC11 status | See `FEATURE_TRACKER.md`, "Overlaps and conflicts resolved" | — |


## Verification (2026-11-17)

An independent fact-check against the code: 83 items, 74 confirmed, 8 partly correct (fixed above), 0 wrong, and 1 that can't be verified because it's engine-internal. Details are in the audit workspace (not committed).
