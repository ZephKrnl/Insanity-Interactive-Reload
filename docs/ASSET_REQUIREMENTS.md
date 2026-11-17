# Asset requirements: Insanity Interactive Reloaded

Draft from the Phase 1 audit (branch `audit/phase-1`, HEAD `aa971d7`), 2026-11-17. Sources: `.superpowers/audit/06-assets-licensing.md` (inventory, licences, IP, sourcing, manifest), plus the "Assets needed" column in `02-combat.md`, `03-animation-rendering.md`, `04-ai-modes-levels.md` and `05-systems-perf-qa.md`. Feature IDs are from `.superpowers/audit/FEATURES.md`.

Paths are relative to `src/MadnessInteractiveReloaded/` unless they start with `src/` or the repo root. Every licence claim carries the auditor's source URL; a claim without one is marked **unverified**. This is a planning document, not legal advice.

**Priority key.** The audit defines no milestones, so this document proposes them:

| Milestone | Goal |
| --- | --- |
| M1 Legal clean-up | Remove or quarantine every blocker, swap the fonts, add silent or placeholder audio, make the binaries compliant, add the manifest gate |
| M2 Original identity | Original cast, setting and name; a base SFX set; core music. This is the minimum sellable game |
| M3 Combat content | Groups A and B, and Phase 2 combat and animation polish |
| M4 Enemies, modes, maps | Groups C, D and E |
| M5 Progression and launch | Groups F and G, store page and launch art |
| Post-launch | Large or approval-gated items |

---

## 1. Release blockers (read first)

> **GPL-3.0 code availability does not grant rights to the art, characters, trademarks, music or sounds.** The repo's only licence is the plain GPL-3.0 text with no asset clause, here and upstream (06 §2.8). At best it offers Studio Minus's *own* art under GPL-3.0, and even that is inferred, not confirmed. It cannot license Krinkels's characters or name, the guest musicians' tracks or commercial sound libraries. Selling GPL code is allowed (https://www.gnu.org/licenses/gpl-faq.en.html#DoesTheGPLAllowMoney), but the GPL never grants trademark or character rights. itch.io's Terms §4 make you warrant that you "own or have the rights, licenses, permissions and consents" for everything you upload (https://itch.io/docs/legal/terms).

**Bottom line: the game cannot be sold as it stands.** Plan a total conversion with an original cast, setting and name. Switch back only if written permission arrives.

| # | Blocker group | Size | Default action | When |
| --- | --- | --- | --- | --- |
| BL-1 | Madness characters, looks, factions, armour, bosses, campaigns | 60 looks, most of 250 `.armor`, 3 campaigns | Replace with an original cast | M1 hide, M2 replace |
| BL-2 | Madness Combat episode backgrounds | 59 QOI, 36.1 MiB (37.8 MB) | Delete | M1 |
| BL-3 | Names and trademarks ("Madness", track titles, gun brands, disk names) | text only | Rename | M1 |
| BL-4 | Studio Minus art, animation, levels, cutscenes (conditional) | ~200 MB textures, 192 `.anim`, 106 level `.json` files, 7 videos | Ask Studio Minus; reuse only generic items if confirmed | M1 ask |
| BL-5 | Music | 44 OGG, 208 MiB (217 MB) | Remove; commission or source CC0 | M1 remove, M2 replace |
| BL-6 | SFX with commercial-library tags | 13 SFX with library tags, plus 1 music file (`Lothyde/strenuous_end.ogg`) | Delete from repo and history; replace | M1 |
| BL-7 | SFX with unknown provenance | ~274 files | Full replacement pass | M1 placeholders, M2 replace |
| BL-8 | Impact font | 1 `.wf` | Replace with an OFL font | M1 |
| BL-9 | Toxigenesis font (plus Tipografia) | 2 `.wf` | Replace with OFL; delete Tipografia | M1 |
| BL-10 | FFmpeg 5.1.2 GPL build, no source offer | 6 DLLs, 36 MB | LGPL decoder-only build with source, or drop video | M1 |
| BL-11 | NOTICE.txt and credits gaps | `NOTICE.txt`, `credits.txt` | Regenerate from the manifest | M1, then every release |

### BL-1 Madness Combat characters, looks, factions, armour, bosses and campaigns
- **What:** the grunt, agent and AAHW looks; named characters (Hank, Sanford, Deimos, Jebus, Tricky, Auditor, Mag Agent, Zed, Doc); Nexus; and the boss and campaign content built on them.
- **Evidence:** `base/data/looks/*.look` (60, e.g. `hank3.look`, `sanford9.look`, `tricky6.look`, `auditor.look`); `data/factions/aahw.json`; armour such as `bodies/body1/hank_bandages`, `sanford_bandages`, `tricky6`, `auditor`, `mag_agent`, `nexus_*`, `zed`; the default grunt parts in `textures/bodies/default/` (19 files); the Rich boss; 3 campaigns.
- **Problem:** krinkels.net says "Madness Combat is a creation by me, Matthew "Krinkels" Jolly. Copyright 2002-2026" and has no fan-game policy (https://www.krinkels.net/). "MADNESS: Project Nexus™ is a trademark of GIBBING TREE, LLC", and that game is sold on Steam (https://store.steampowered.com/app/488860/MADNESS_Project_Nexus). No commercial-use policy was found (https://newgrounds.wiki.gg/wiki/Madness_Combat_(series), https://madnesscombat.fandom.com/wiki/Madness_Combat). A paid game with these characters competes with the owner's product. Fan tolerance is not a licence.
- **Permission:** Matt "Krinkels" Jolly (via https://www.krinkels.net/) and GIBBING TREE, LLC. Ask for a written, non-exclusive commercial licence for the characters, setting and name. Asking is free, but a grant for a competing paid game is unlikely, so do not plan on it.
- **Replacement:** an original cast and factions. **Real art is unavoidable here.** You need a new base character (head, body, 7 hand poses front and back, feet, left and right) and 30–60 original armour and clothing pairs (§4, core rows). The cheapest route is a deliberately simple geometric house style that Zeph builds in SVG from templates (§6.4), with a commissioned artist for the base character and the hero pieces.
- **Removal breaks:** character-creator presets, every spawn that names a look, factions, the Rich boss, all 3 campaigns and Incident enemy sets. In M1, hide the MC looks and armour (the armour `hidden` flag already exists, `Character creation/ArmourPiece.cs:57`) and delete them before release. Keep the data formats.

### BL-2 Madness Combat episode backgrounds
- **Evidence:** `textures/backgrounds/madness combat/`, 59 QOI, 36.1 MiB (37.8 MB) (`background_mc1_1` … `background_mc7_2`, `boombox.qoi`, `mc2_1_verdwijnlayer.qoi`). No level, campaign or `.cs` file references them (06 §2.1).
- **Problem:** these are Krinkels's episode scenes by name (https://www.krinkels.net/).
- **Options:** removal only. Nothing breaks, and it saves 36.1 MiB (37.8 MB). Also never use `background_mc5_5_collapsed.qoi` for E56.

### BL-3 Names and trademarks
- **What:** the "Madness" name; `dj.json` track titles "Madness Combat 4/5/6/6.5/7/9.5"; real gun brand names; Improbability disk names that reference outside IP; MC character names in item names (e.g. `auditor` sword, `mag_railgun`).
- **Evidence:** `data/weapons/*.json` `Name` (Desert Eagle, Glock, Benelli, Mossberg, Beretta, Colt, S&W, FN, HK, Walther, Uzi, …); disk names Willis, Gump, FLCL and Ghost Protocol (05 §7); `base/data/dj.json`.
- **Problem:** the Steam page states "MADNESS: Project Nexus™ is a trademark of GIBBING TREE, LLC" (https://store.steampowered.com/app/488860/MADNESS_Project_Nexus). Gun brands are manufacturers' trademarks: **unverified** (the auditor did not research it). Disk-name references: **unverified**. The working title "Insanity Interactive Reloaded" echoes "Madness Interactive Reloaded"; trademark similarity was not researched (**unverified**).
- **Options:** rename (S, cheap). Use fictional weapon and disk names, keep "Madness" out of the title, store page and tags, and state the lineage factually in the credits with a non-affiliation line (§8). Changing display names breaks nothing; if you change ids, migrate saves.

### BL-4 Studio Minus art, animations, levels and cutscenes (conditional)
- **Evidence:** the rest of `textures/` (~200 MB), 192 `.anim` files, 106 level `.json` files, 7 cutscenes (196 MB), 18 modifier cards and the UI icons.
- **Problem:** they sit in a GPL-3.0 repo with no asset clause, so "GPL-3.0 by inclusion" is the most defensible reading (inferred). The GPL covers non-software only "as long as it is clear what constitutes the 'source code' for the work" (https://www.gnu.org/licenses/gpl-faq.en.html#GPLOtherThanSoftware), and no layered art sources ship. Much of it depicts BL-1. The Studio Minus site's terms are **unverified** (HTTP 402 to the fetcher). The videos are Studio Minus campaign cutscenes (inferred) with no credits.
- **Permission (do first, effort S):** Studio Minus (GitHub org `studio-minus`; Dikkiedik and Koof per 06). Ask in writing: (1) do the repo's GPL-3.0 terms cover your art, animations, levels and videos; (2) do you object to a paid fork; (3) what are the sources and contacts for the music and SFX? Their answer decides how much of §3 survives.
- **If confirmed:** generic items are reusable under GPL-3.0. Ship the GPL text, keep the PNGs available as "source", and treat recolours as GPL too.
- **If not:** replace everything visual. Removing the cutscenes breaks the `VideoSlide` entries in `base/data/cutscenes/*.json`. Under a total conversion they depict the old cast anyway, so drop them and use in-engine slide cutscenes.

### BL-5 Music: 44 tracks, 208 MiB (217 MB)
- **Evidence:** `sounds/music/` holds 44 OGG files. `base/data/dj.json` lists 29 with an author: Lothyde (with MonoCronic), Cheshyre, API, Mmodule, Meonly70, DJ Birdie and Arthur Smith. 11 have no author anywhere: `contrave.ogg`, `jebus.ogg`, `the_pied_piper.ogg`, `the_whistler.ogg`, `giant.ogg`, `experiment.ogg`, `incident_1000a.ogg`, `madness_6.ogg`, `madness_2.ogg`, `death.ogg` and `madness_4_party.ogg`. The credits (`base/data/credits.txt:17-20`) omit Meonly70, DJ Birdie, Arthur Smith and MonoCronic.
- **Problem:** no licence or permission exists for any track. The "Madness Combat N" tracks (Cheshyre, API) are the soundtracks of Krinkels's episodes. "Chicken Dance" (`madness_1.ogg`) is Werner Thomas's 1957 "Der Ententanz", so both composition and recording rights apply (https://en.wikipedia.org/wiki/Chicken_Dance). Cheshyre's SoundCloud mentions only a free YouTube release (search summary of https://soundcloud.com/cheshyre-1). Lothyde's OST was written for Studio Minus's game, and no licence for it was found.
- **Permission:** ask each composer in writing. Start with Lothyde and MonoCronic (the most reusable OST), then Mmodule and Meonly70. Don't pursue the "Madness Combat N" tracks (tied to BL-1), "Chicken Dance" or the uncredited tracks.
- **Replacement:** commission an OST under a written non-exclusive, perpetual, worldwide game-sync licence that includes streaming and video use (06 §2.10). Alternatively use CC0 or CC-BY music from OpenGameArt or Freesound (§5). **A real OST needs money**; CC0 music is free but generic.
- **Removal breaks:** the DJ system (`dj.json`, E57 club), Incident music (`incident_music.json`), level music (e.g. `levels/tutorial/tut_move.json:8`), the death sting (`Assets/Sounds.cs:56`) and boss music. Incident skips missing ids silently (`Registries/Registries.cs:338`); other paths are **unverified**, so ship silent placeholder OGGs in M1.

### BL-6 SFX with commercial-library tags (13 SFX with library tags, plus 1 music file)
- **Evidence (embedded tags):**
  - BOOM Library: `forcefield_off.wav`, `weapon_drop/firearm/drop-01..03.wav`, `weapon_drop/sword/drop-01..04.wav`.
  - PMSFX: `firearms/laser_rifle_1..3.wav` and `turret_death.wav`. The last also carries a Sound Ideas tag and the album tag `~madness-project-nexus`.
  - Heiko Lohmann / The Sound Pack Tree plus BOOM: `train_emergency_halt.ogg`.
  - C. A. Sound "Cymbal Flash" inside `music/Lothyde/strenuous_end.ogg`.
- **Problem:** a library licence belongs to its purchaser and does not pass to Zeph. BOOM bars "Standalone Redistribution: Reselling, trading, or sharing raw or processed files" (https://www.boomlibrary.com/support/faq/what-am-i-allowed-to-do-with-your-sounds/). These files sit raw in a public repo.
- **Permission:** not practical. Buying your own licence still would not let the raw files sit in the public GPL repo.
- **Removal (M1):** delete them from the repo and its git history before publicising the fork (06 §3). This breaks weapon-drop, laser-rifle, turret-death, forcefield and train-halt sounds, so add placeholders.
- **Replacement:** Sonniss GDC bundles and Freesound CC0 (§5), kept out of the public repo where the licence requires it.

### BL-7 SFX with unknown provenance (~274 files)
- **Evidence:** 287 SFX (277 WAV and 10 non-music OGG) in 19 folders; firearms alone is 20 MB. About 274 have no metadata at all (06 §2.8).
- **Problem:** the provenance is unknown. With 14 confirmed library files (13 SFX plus 1 music file), assume others are library sounds with stripped tags. Studio Minus's GPL cannot relicense sounds it does not own (06 §2.8).
- **Permission:** request the source list from Studio Minus (BL-4 letter). A file may stay only with a documented, transferable licence.
- **Replacement:** plan a **full SFX replacement pass** (M2, §4 core rows). Removal breaks every weapon, melee, impact and UI sound. `resources/null.wav` exists as an engine fallback; whether missing paths fall back to it is **unverified**.

### BL-8 Impact font
- **Evidence:** `fonts/impact.wf`, loaded by `Assets/Fonts.cs:17-23`. Used in the pause menu (`Shared/Systems/PauseSystem.cs:149,203`), cutscene text (`Level/Systems/CutsceneSystem.cs:76`), Incident mode and the benchmark.
- **Problem:** "© 2011 The Monotype Corporation" (https://learn.microsoft.com/en-us/typography/font-list/impact). Microsoft's FAQ says that converting the font into a bitmap font for a game is not allowed (https://learn.microsoft.com/en-us/typography/fonts/font-faq). The `.wf` file is exactly such an MSDF-atlas conversion.
- **Permission:** buy a Monotype game licence (costs money; price **unverified**).
- **Replacement (recommended, S):** an OFL condensed display face such as Anton (https://fonts.google.com/specimen/Anton) or Bebas Neue (licence **unverified**; check its `OFL.txt`). Regenerate the atlas with `Walgelijk.FontGenerator` (https://github.com/mestiez/Walgelijk/tree/master/Walgelijk.FontGenerator).
- **Removal breaks:** text on those screens.

### BL-9 Toxigenesis font (and Tipografia)
- **Evidence:** `fonts/toxigenesis.wf` drives the HUD (`User interface/Systems/PlayerUISystem.cs:65,231`), the death screen (`Character/Systems/PlayerDeathSequenceSystem.cs:103`), boss UI, tutorial, spawn menu and disk unlocks.
- **Problem:** Typodermic's free Desktop EULA excludes "Embedding in websites, software, apps, games, devices". It also excludes distributing individual letters or sprites that software can recombine, which a glyph atlas is (https://typodermicfonts.com/license/). The family is sold via MyFonts (https://www.myfonts.com/collections/toxigenesis-font-typodermic).
- **Permission:** a custom PC-game licence from Typodermic or Monotype (costs money; price **unverified**).
- **Replacement:** an OFL font. It is a visible HUD change, so choose it early. Removal breaks HUD text.
- **Tipografia:** unknown provenance and used only in a debug scene (`Scenes/Debugging/TextTestingScene.cs:84`). Delete it.

### BL-10 FFmpeg 5.1.2 GPL build with no source offer
- **Evidence:** `avcodec-59.dll` (30.6 MB), `avformat-59`, `avutil-57`, `avdevice-59`, `swresample-4` and `swscale-6` (36 MB total), copied by `MIR.csproj:92-120`. The configure line includes `--enable-gpl --enable-libx264 --enable-libxvid --enable-libmp3lame`.
- **Problem:** this is a GPL-2.0-or-later build. Each sale must offer the complete corresponding source of FFmpeg and every linked library, and nothing does. FFmpeg's legal page says "If those parts get used the GPL applies to all of FFmpeg" and advises "Make sure your program is not using any GPL libraries (notably libx264)". It also warns about MPEG LA patent collection (https://ffmpeg.org/legal.html).
- **Options:**
  - Ship an LGPL decoder-only FFmpeg 5.1.x (h264 or vp9, aac or opus, mp4 or webm demuxers) and publish its exact source tarball and configure line. Optionally re-encode the videos to VP9/AV1 with Opus.
  - If the cutscenes are dropped (BL-4), remove FFmpeg and the video player entirely. This is the cheapest option. Whether anything else needs FFmpeg is **unverified**; check before deleting.

### BL-11 NOTICE.txt and credits gaps
- **Evidence:** `NOTICE.txt:1-246` lists only OpenTK, Newtonsoft.Json, FastNoise Lite and ImageSharp. ImageSharp is mislabelled as Apache-2.0: it uses the Six Labors Split License and is not shipped. `credits.txt:17-25` omits Meonly70, DJ Birdie, Arthur Smith, MonoCronic, every font author and every SFX source.
- **Missing from NOTICE:**
  - Walgelijk.
  - FFmpeg and its codec libraries.
  - OpenAL Soft (LGPL, https://github.com/kcat/openal-soft/blob/master/COPYING).
  - nativefiledialog-extended (zlib) and NativeFileDialogExtendedSharp (GPL-3.0-only, https://github.com/Crauzer/NativeFileDialogSharp).
  - GLFW, SkiaSharp, NVorbis and PortAudio (https://github.com/PortAudio/portaudio/blob/master/LICENSE.txt).
  - Harmony, MonoMod, Mono.Cecil, Roslyn, TextCopy, System.CommandLine and BigGustave.
  - The OFL texts for Inter, Oxanium, Cascadia Mono and Roboto Mono.
  - The shader snippet notices.
- **Options:** regenerate both files from the provenance manifest plus `obj/project.assets.json` (§6.2, §8). No permission is needed. Effort S–M.

---

## 2. Inventory summary

Measured on disk by the auditor (06 §2.1). `base/` has **2,500 files, 679 MB** and is packed into `resources/base.waa` at build time (`MIR.csproj:43-49`). `resources/` holds 11 files (265 KB) of engine fallbacks: `error.png`, `white.png`, `null.wav`, `icon.ico`/`icon.png`, `roboto mono.fnt/.png` and 4 shaders.

**By type:**

| Type | Files | MB | Content |
| --- | --- | --- | --- |
| `.qoi` | 267 | 208.45 | Backgrounds, level thumbnails, large overlays |
| `.ogg` | 54 | 208.34 | Music (44) and long SFX or ambience (10) |
| `.mp4` | 7 | 195.45 | Cutscenes, 1920x1080 H.264 + AAC |
| `.wav` | 277 | 32.04 | SFX |
| `.png` | 1,047 | 26.28 | Character parts, weapons, UI, FX |
| `.json` | 244 | 1.16 | Levels, weapons, campaigns, config |
| `.anim` | 192 | 0.73 | Character animation clips |
| `.wf` | 6 | 0.71 | Fonts (zip of `meta.json` + 512x512 MSDF atlas) |
| `.armor` | 250 | 0.06 | Armour pieces (36 Body, 42 BodyAccessory, 32 Head, 140 HeadAccessory) |
| other data | 130 | <0.1 | 60 `.look`, 16 `.preset`, 21 `.stats`, 14 `.seq`, 10 `.hand`, 9 `.tags` |

**By folder:** `sounds/` 242 MB (music 208 MiB, 217 MB); `textures/` 238 MB (backgrounds 203 MB in 160 files, `bodies/` 649 files 16.2 MB, `weapons/` 162 files 3.2 MB, `ui/` 98 files 1.8 MB, `level_thumbnails/` 90 files); `video/` 196 MB; `data/` 3.9 MB; `fonts/` 852 KB; `shaders/` 67 KB (18 `.frag`, 4 `.vert`); `locale/` 9 KB (`en-GB.json` only).

**Biggest files:**

| File | Size |
| --- | --- |
| `video/cutscene_bossbattle.mp4` | 73.0 MB |
| `video/cutscene_magmachine.mp4` | 41.9 MB |
| `sounds/music/contrave.ogg` | 24.0 MB |
| `video/cutscene_mag_intro.mp4` | 20.7 MB |
| `video/cutscene_credits.mp4` | 19.3 MB |
| `video/cutscene_note.mp4` | 17.3 MB |
| `video/cutscene_begin_part1.mp4` / `part2.mp4` | 16.5 / 16.3 MB |
| `sounds/music/trainmadness.ogg` | 14.5 MB |
| `sounds/music/Lothyde/come_to_blows.ogg` | 13.8 MB |
| `backgrounds/mag_campaign/background_mag_bridge.qoi` | 9.2 MB (8112x3149, ~102 MB in VRAM) |

Removing the blocked music, videos and MC backgrounds alone drops about 440 MB.

**Character parts** (`textures/bodies/`, 649 files): `body1/` 35 sets, `body2/` 42, `hats/` 42, `eyes/` 53, `mouth/` 27, `skin/` 31, `hands/` 9 sets × 7 poses × front/back, `broken/` 5 sets, `gore/` 10 files, `default/` 19, `feet/` 2.

### Asset conventions (the spec new assets must meet)
- **Scale:** 1 px = 1 world unit. `Prefabs.CreateSprite` builds a centred quad sized to the texture (`Prefabs/Prefabs.cs:191-209`). A character sprite's scale is `texture.Size × Positioning.Scale × piece.TextureScale`, with `TextureScale` 1 by default (`Character creation/ArmourPiece.cs:72`; 03 §2.1).
- **Pivots:** the default pivot is the texture centre. Rotation pivots are `HeadRotationPivot = (-0.2, -0.3)` and `BodyRotationPivot = (0, -0.5)` in normalised sprite space, X-flipped on facing (`CharacterConstants.cs:17-18`). The weapon pivot is `-HoldPoints[0]`.
- **Weapon anchors:** `HoldPoints`, `BarrelEndPoint` and `CasingEjectionPoint` are pixel offsets from the texture centre, Y up (deagle: `BarrelEndPoint {125,55}` on 246x142). Guns are drawn horizontal and melee weapons vertical (pike 128x711).
- **Facing:** every part has a `_left.png` and a `_right.png`. They are **separate drawings, not mirrors**, so each new piece is two drawings. Hands flip vertically because their axis runs along the arm (03 §2.1).
- **Layers and draw order:** within a character layer (20–199), the back hand is at -100, feet at 998–999, body at 2000, body apparel at 2100 (2 layers), head at 3000, head apparel at 3100–3102 (3 layers) and the front hand at 4000. The weapon is drawn 100 below its hand (`Graphics/RenderOrders.cs`, `CharacterPositionSystem.cs`).
- **Armour data:** `.armor` is versioned key/value text: `name`, `type` (Body/BodyAccessory/Head/HeadAccessory), `category` (All/Eyes/Hat/Mask), `left`/`right`, `hidden`, `detach`, `broken`, `offset X Y` (pixels) and `order`. A `.hand` maps 7 poses (Fist, HoldPistol, HoldUnderside, HoldRifle, Open, Point, HoldStock) to `front`/`back` textures.
- **Sizes (sampled):**
  - body1 189–573 × 256–907 px (agent suit 194x351); body2 241x362; hats 128–418 × 62–755; hands 75–298 px.
  - Weapons from 41x201 to 981x188.
  - UI icons 32/64 px; modifier cards 500x500; level thumbnails 260x200; loading strip 2048x256.
  - Textures are trimmed to content, with no power-of-two rule. **Keep new textures ≤ 4096 px** (several backgrounds exceed this; 06 §4).
- **Backgrounds:** one composite QOI per level. Background, foreground and a decal mask are packed into alpha by `src/mirbgencoder`.
- **Animation:** `.anim` is line-based text with BODY, HEAD and 2 HAND blocks and sparse keys: `<frame|Ns> [Xpx Ypx] [Ddeg] [HandLook] [scale(x,y)] [constraints(...)]` (03 §2.2). Characters are rigid cut-out sprites with procedural posing and additive clips. There is no skeleton and no frame-by-frame art. Flipbooks are used for FX only.
- **Naming:** snake_case ids; `<id>_left.png` / `<id>_right.png`; `textures/weapons/<id>/base.png`; flipbooks encode their grid in the name (`explosion_20_5x4.png` = 20 frames on a 5x4 grid). Some legacy names are Dutch (`vuurtje`, `verdwijnlayer`). Use English for new assets.

## 3. Reusable assets

"Reusable" means technically reusable (06 §2.9). Each tier carries a different licence caveat.

**Tier 1: reusable now, with notices.**

| Asset | Reuse for | Caveat |
| --- | --- | --- |
| Inter, Oxanium, Cascadia Mono, Roboto Mono fonts | All UI | OFL-1.1 (`gh api repos/rsms/inter`, `gh api repos/sevmeyer/oxanium`, https://github.com/microsoft/cascadia-code/blob/main/LICENSE, https://github.com/googlefonts/RobotoMono). Ship each `OFL.txt` and copyright line; don't use "Cascadia Code" (RFN) as the atlas name (https://openfontlicense.org/ofl-faq/ FAQ 1.20, 2.2) |
| Shaders and shader-driven effects: `apparel.frag` tint and damage, `bodypart.frag`, `door.frag`, `train-background.frag`, particle system, `MadnessUtils.Flash` | B24, F62 recolours, R19, R27, R35, R87 | Code is GPL-3.0. Noise snippets need MIT notices: glsl-voronoi-noise (`gh api repos/MaxBittker/glsl-voronoi-noise`), stegu webgl-noise (`gh api repos/stegu/webgl-noise`), Inigo Quilez (licence inline in `Assets/Materials.cs:56-58`) |
| Tools: `src/mirbgencoder`, armour editor, `AnimationTestingSystem`, `WeaponThumbnailCache`, `Walgelijk.FontGenerator` | Backgrounds, armour, clips, thumbnails, fonts | Code only; GPL-3.0 or MIT. `mirbgencoder` uses ImageSharp under the Six Labors Split License, and Zeph qualifies for its Apache-2.0 path (06 §2.7). It is not shipped |
| Data formats and schemas (`.armor`, `.hand`, `.look`, `.anim`, `.seq`, weapon JSON, level JSON) | All new content | A format is not art; new files written in them are Zeph's |

**Tier 2: reusable only if Studio Minus confirms GPL-3.0 in writing (BL-4) and the item does not depict BL-1.** If reused, the item and any recolour are GPL-3.0 and must stay redistributable. Whether art "derived from Krinkels's style" (06 §2.8.1 row 3) carries style-similarity risk was not researched (**unverified**).

| Existing asset | Path | Reuse for |
| --- | --- | --- |
| Revolvers `colt_revolver`, `sw_500`, `enfield_mk2`, `m_73` | `data/weapons/`, `textures/weapons/` | B13 (rename brands) |
| Shotguns: `mossberg_500`, `_compact`, `spas_12`, `benelli_m4`, `saiga_12`, `sawed_off_shotgun`, `norinco_97k`, `semi_auto_shotgun`, `usas_12` | same | B14 (data only) |
| Heavy MGs `m60`, `mg_42`, `bren_gun` | same | B15 |
| Knives: `throwing_knife`, `bowie_knife`, `tanto`, `switchblade`, `carbon_knife`, `iron_knife` | same | B16 |
| 38 melee weapons; 71 melee `.anim`; 14 `.seq` | `data/animations/melee`, `data/melee_sequences` | B17, A10, PA8 |
| `streetsign`, `iron_pipe`, `baseball_bat`, `cigarette` | weapons | B18 |
| `laser_rifle`, `railgun`, `plasma_cutter`, `binary_sword`, `1337_sword` | weapons | B21. Rename or drop `mag_railgun` and the `auditor` sword (BL-1); their SFX are blocked (BL-6) |
| Silenced variants (`silenced_mp5`, `silenced_tmp`, `silenced_mac_10`, `silenced_beretta_92`, `tactical_deagle`) | weapons | B22 reference only |
| Generic armour: `coat1–11`, `jacket1–2`, `vest1`, `bandolier1–3`, `tactical_backpack1–3`, `body_armor1–2`, `combat_armor1`, `power_armor`, `metal_armor`, `hats/bandana*`, `eyes/aviators` | `textures/bodies/`, `data/armour/` | F62, F63, C30 (06 §2.9) |
| Hand sets (the generic ones), gore and flesh layers | `bodies/hands/`, `bodies/gore/` | R04, all characters. Check that no glove set is an MC character's |
| FX flipbooks: `blood_impact`, `bullet_impact`, `bullet_deflect`, `blade_impact`, `muzzle_flashes`, `bullet_casings`, `decals`, `engine_spark`, explosions, fire | `textures/` | R12, B19, B23, PC7 |
| Props (28), doors (9 frames), `elevator_details` (9), turrets (3) | `textures/props`, `doors`, `turrets` | E60, B18 |
| Industrial backgrounds: `background_boiler`, `_electricity`, `_tubes`, `_shipping_receiving`, `_boxes`, `debarkationFire1_16_4x4.png` | `textures/backgrounds/` | E49 |
| Lab and bunker backgrounds: `mag_specimen`, `_servers`, `_vaults`, `mag_storage_1/2`, `experiment_1..6` | `backgrounds/mag_campaign/`, root | E50, E54 |
| `background_roof`; train set (`background_train_a/b/c`, `train_machinist(_b)`, `transportation_locomotive`, `transportation_wagon`) | `backgrounds/` | E52, E53 |
| Club level and props (`exp_club.json`, `clubM_poster.png`, `clubN_poster.png`, 4 dancing clips) | `data/levels`, `props/` | E57. Music blocked |
| `background_mag_procedural.qoi`, `exp_*` arenas | `backgrounds/mag_campaign/`, `data/levels` | E59, D37, D38 |
| Stun, death, jump, dodge, pickup and throw clips (192 `.anim` in total) | `data/animations/` | PA8, PA9, PA12, PA13, A2 (`stun_gun_stolen.anim`) |
| 18 modifier cards, UI icons, loading strip | `textures/ui/` | D47, F70. Review the names and images for outside IP first |
| 106 levels' geometry | `data/levels/` | New levels, as layout references. Their backgrounds and enemy sets depend on BL-1 |

**Tier 3: never ship.**
- MC looks and named armour (BL-1).
- `backgrounds/madness combat/` (BL-2).
- All 44 music tracks (BL-5).
- All 287 SFX (BL-6, BL-7). Treat even "probably fine" sounds as replaced until a licence is documented.
- Impact, Toxigenesis and Tipografia (BL-8, BL-9).
- The 7 cutscenes, which depict the old cast (BL-4).

## 4. Required assets by feature

Assets needed by the feature rows in audits 02–06. A **Core** group comes first because every feature depends on it. Rows are numbered R01–R97 for reference.

**Licence-requirement codes** (full rules in §5 and §7):
- **CC0/BY:** CC0-1.0 preferred. CC-BY-4.0 is allowed with credit. Never NC or ND.
- **Sonniss:** Sonniss GDC bundle terms; store privately (`public_repo: false`).
- **OFL:** OFL-1.1. Ship `OFL.txt` and the copyright line, and don't use the Reserved Font Name in the converted atlas.
- **Own:** Zeph's own work (`Proprietary-Zeph`, or GPL by choice), including code-generated output.
- **Comm:** a written commission contract that assigns copyright or grants a perpetual, worldwide, non-exclusive licence covering modification, sale, streaming and trailers.
- **SM-GPL:** reuse only if Studio Minus confirms GPL-3.0 in writing (BL-4) **and** the item does not depict BL-1.

**Source-plan codes:** Procedural (code or shader), SVG (Inkscape, exported by script), CC0 source, Commission, Reuse existing.

**Status** reflects licence state today. "Have-unlicensed" means the file exists but cannot ship as is.

| # | Asset | Type | Needed for | Source plan | Licence requirement | Status | Priority |
| --- | --- | --- | --- | --- | --- | --- | --- |
| | **Core: blocks every feature** | | | | | | |
| R01 | Original base character: head, body, hands (7 poses × front/back), feet, all left/right | art | all; F61; C25–C36 | **Commission** the base set (real art unavoidable); Zeph makes SVG variants from the template (§6.4) | Comm | Have-unlicensed (`bodies/default/` grunt, BL-1) | M2 |
| R02 | Original faction looks (4–6: rank-and-file, elite, suit, heavy, civilian) as `.look` | art | C25–C36, D37–D45, E55 | Compose from R01 + R03; no new drawing | Own | Have-unlicensed (60 MC looks, BL-1) | M2 |
| R03 | Original armour and clothing kit, first 30 pairs (hats, eyes, masks, body layers 1–2) | art | F62, F63, F64, C27–C36 | SVG by Zeph for simple items (caps, visors, vests, bandanas); **commission** hero pieces | Own / Comm | Have-unlicensed (generic pieces SM-GPL; named pieces BL-1) | M2 |
| R04 | Gore, flesh and slash textures | art | all combat, A12 | Reuse if cleared; else a procedural noise texture baked to PNG | SM-GPL / Own | Have-unlicensed | M2 |
| R05 | Base SFX set (~150–200): firearms per class, melee swish/clash/blade/blunt, punch, bullet impact, near-miss, casings, physics, doors, footsteps, jump | audio | all A, B, C; PC7, PC8 | CC0 source: Sonniss GDC, Freesound CC0, Kenney; layer and pitch per weapon | Sonniss / CC0/BY | Have-unlicensed (BL-6, BL-7) | M1 placeholder, M2 |
| R06 | UI SFX (click, hover, back, toast, error) | audio | all menus, F64, F68, G71 | Kenney CC0 audio; sfxr-family generator | CC0/BY / Own | Have-unlicensed (`sounds/ui`) | M2 |
| R07 | Music: menu, 6–10 combat loops, 2–4 boss, 1–2 ambient, death and results stingers | audio | D37–D47, C36, D39, E57 | **Commission** (preferred) or CC0/CC-BY | Comm (sync licence) / CC0/BY | Have-unlicensed (BL-5) | M1 silence, M2 |
| R08 | Display font (Impact replacement) | font | pause, cutscenes, Incident | OFL Google Font → `.wf` via Walgelijk.FontGenerator | OFL | Missing | M1 |
| R09 | HUD font (Toxigenesis replacement) | font | HUD, death screen, boss UI | OFL Google Font → `.wf` | OFL | Missing | M1 |
| R10 | Inter, Oxanium, Cascadia Mono, Roboto Mono (keep) | font | all UI | Reuse; add `OFL.txt` and copyright lines; rename the Cascadia atlas (RFN) | OFL | Have-licensed (texts missing) | M1 |
| R11 | Cutscene replacements (campaign intro and outro) | animation | D39 | In-engine slides with static art, or drop them | Own / Comm | Have-unlicensed (7 MP4, BL-4) | Post-launch |
| R12 | FX flipbooks: blood, bullet impact and deflect, muzzle flash, casings, decals, fire, explosions | VFX | all combat; B19; PC7 | Reuse if cleared; else render procedurally (§6.3) | SM-GPL / Own | Have-unlicensed | M2 |
| R13 | Fictional names: 120 weapons, 17 disks, game title | UI | B13–B24, D47, F70 | Write them | Own | Missing | M1 |
| R14 | Logo, app icon, store capsule, screenshots | UI | store page, launcher | SVG wordmark in the OFL font (cheap); **commission** capsule art | Own / Comm | Have-unlicensed (`icon.ico/png` provenance **unverified**) | M5 |
| | **A: Combat and movement** | | | | | | |
| R15 | Dual-wield hand poses and clips (aim, fire, throw, pickup) | animation | A1 | `.anim` from presets; reuse hand sprites | Own | Missing | Post-launch (L, needs approval) |
| R16 | Steal and grab clip | animation | A2, A9 | Extend `stun_gun_stolen.anim`; new `.anim` | Own (+ SM-GPL base) | Missing | M3 |
| R17 | Takedown hazard props (fuse box, vent fan, railing) and paired clips | art / animation | A3, E60 | SVG props; `.anim` | Own | Missing | Post-launch |
| R18 | Execution paired clips, 3–6 per weapon class | animation | A4 | `.anim` from presets (§6.4); start with 1 per class | Own | Missing | M3 |
| R19 | Counter flash | VFX | A5 | Procedural (`MadnessUtils.Flash` plus tint) | Own | Missing | M3 |
| R20 | Slide clips (start, loop, exit) | animation | A6, PA9 | `.anim` | Own | Missing | M3 |
| R21 | Wall-rebound and aerial clips | animation | A7 | `.anim` | Own | Missing | Post-launch (TECH_DIFFICULT) |
| R22 | Grab, shove and throw-character clips | animation | A9, A2 | `.anim`; reuse `StunHeavy` reactions | Own | Missing | M3 |
| R23 | Combo swings (branching, charged, launcher) | animation | A10, PA8 | Extend the 71 melee clips | Own (+ SM-GPL base) | Missing | M3 |
| R24 | Jam icon and jam/clear click | UI / audio | A11 | SVG icon; Freesound CC0 or Kenney | Own / CC0/BY | Missing | M3 |
| R25 | Combat SFX extras: counter sting, ricochet, body slam, grab/shove whoosh | audio | A5, A9, A12 | Freesound CC0, Sonniss | CC0/BY / Sonniss | Missing | M3 |
| R26 | Broken variants for new armour | art | A12, C30 | Procedural damage via `apparel.frag`; hand-made broken sprites only for hero pieces | Own | Missing (5 old sets are BL-1/BL-4) | M3 |
| R27 | Ricochet sparks | VFX | A12, PC8 | Procedural particles (copy the `engine_spark` pattern) | Own | Have-unlicensed (`engine_spark`) | M3 |
| | **B: Weapons and equipment** | | | | | | |
| R28 | Weapon sprites, 120 (`base.png` plus slide, mag, glow) | art | B13–B17, B21 | Reuse if cleared (rename MC-named items); else redraw in SVG (§6.3) | SM-GPL / Own | Have-unlicensed | M2 (decide) |
| R29 | Revolver cylinder part | art | B13 | SVG part (needs the AngleCurve bug #10 fix) | Own | Missing | M3 |
| R30 | Revolver cylinder and speed-loader SFX | audio | B13 | Freesound CC0, Sonniss | CC0/BY / Sonniss | Missing | M3 |
| R31 | Heavy-MG bipod deploy pose | animation | B15 | `.anim` | Own | Missing | Post-launch |
| R32 | Throwing-knife spin and stick decal | VFX | B16 | Procedural rotation; decal reuses the knife sprite | Own | Placeholder | Post-launch |
| R33 | Improvised props: chair, bottle, fire extinguisher | art | B18, A3, E60 | SVG flat props; Kenney CC0 as a base | Own / CC0/BY | Missing | M3 |
| R34 | Grenade and flashbang sprites | art | B19 | SVG | Own | Missing | M3 |
| R35 | Explosion and flash VFX | VFX | B19, E56, E60 | Procedural flash overlay; explosion flipbook reused or rendered (§6.3) | SM-GPL / Own | Have-unlicensed | M3 |
| R36 | Grenade bang, bounce and tinnitus ring | audio | B19 | Sonniss, Freesound CC0; the ring is a generated sine | Sonniss / CC0/BY / Own | Missing | M3 |
| R37 | Hand-held riot shield (front/back, left/right) | art | B20, C27 | SVG (a slab with a viewport) | Own | Missing | M3 |
| R38 | Shield block poses and bash clip | animation | B20, C27 | `.anim` with an off-hand pose | Own | Missing | M3 |
| R39 | Shield impact SFX | audio | B20, C27 | Freesound CC0, Sonniss | CC0/BY / Sonniss | Missing | M3 |
| R40 | Laser, railgun and plasma SFX | audio | B21 | Synth (sfxr-family) layered with CC0 | Own / CC0/BY | Have-unlicensed (PMSFX, BL-6) | M2 |
| R41 | Attachment sprites with anchor markers (suppressor, optic, laser, grip, extended mag) | art | B22 | SVG with marker circles → anchor JSON (§6.3) | Own | Missing | Post-launch |
| R42 | Suppressed-shot SFX | audio | B22 | Sonniss, Freesound CC0 | Sonniss / CC0/BY | Missing | Post-launch |
| R43 | Ammo-type icons and coloured tracers | UI / VFX | B23 | SVG icons; tint the existing tracer | Own | Missing | M4 |
| R44 | Skin patterns (5–10 at 256 px) and tint presets | art | B24, F64 | Procedural noise (camo, carbon, rust) baked to PNG, or CC0 textures | Own / CC0/BY | Missing | M5 |
| | **C: Enemy AI** | | | | | | |
| R45 | Shield-bearer look | art | C27 | Compose from R03 + R37 | Own | Missing | M4 |
| R46 | Sniper look and laser line | art / VFX | C28 | Kit look; the laser is a line draw | Own | Missing | M4 |
| R47 | Fast-melee look and lunge/dash clip | art / animation | C29 | Kit look; `.anim` | Own | Missing | M4 |
| R48 | Heavy armour plates and heavy look | art | C30 | **Commission** (bulky plates read badly as simple SVG) | Comm | Missing (`power_armor` if cleared) | M4 |
| R49 | Armour-break VFX and SFX | VFX / audio | C30, A12 | Procedural debris; Freesound CC0 metal | Own / CC0/BY | Missing | M4 |
| R50 | Medic look, heal clip, heal VFX and SFX | art / animation / VFX / audio | C31 | Kit look; `.anim`; procedural ring; Kenney or Freesound CC0 | Own / CC0/BY | Missing | M4 |
| R51 | Commander look and barks | art / audio | C32 | Kit look; barks optional (own voice or commissioned VO) | Own / Comm | Missing | M4 (barks Post-launch) |
| R52 | Crouch, peek and vault clips; cover props | animation / art | C34, E60 | `.anim`; SVG props | Own | Missing | Post-launch |
| R53 | Elite looks, unique-attack clips and VFX | art / animation / VFX | C35 | Kit looks + 1–2 commissioned hero pieces; `.anim`; procedural VFX | Own / Comm | Missing | M4 |
| R54 | Bosses (2–4): unique look, phase-change states, boss clips | art / animation | C36, D40 | **Commission** boss art (real art unavoidable); `.anim` by Zeph | Comm / Own | Missing (Rich is BL-1) | M4 |
| R55 | Boss music and phase stingers | audio | C36, D40 | **Commission** (part of the R07 OST) | Comm | Missing | M4 |
| | **D: Game modes** | | | | | | |
| R56 | Mode icons (64 px) | UI | D37–D47 | SVG | Own | Missing | M4 |
| R57 | Modifier and disk cards (500x500) and disk icons | UI | D43, D44, D47, F70 | SVG template + text; replace the 18 existing cards | Own | Have-unlicensed (18 cards; some reference outside IP) | M4 |
| R58 | Score, combo, timer and results HUD elements | UI | D41, D42, G77 | SVG + OFL font | Own | Missing | M4 |
| R59 | Mode stingers: countdown, wave start, score tick, results | audio | D37, D38, D41, D42 | Kenney CC0, Freesound CC0, sfxr | CC0/BY / Own | Missing | M4 |
| R60 | Arena backgrounds | art | D37, D38 | Reuse the `exp_*` arenas if cleared; else E rows | SM-GPL | Have-unlicensed | M4 |
| R61 | Upgrade icons and shop UI | UI | D45, F66 | SVG icon set | Own | Missing | Post-launch |
| R62 | Player-2 colour, look and HUD | art / UI | D48 | Tint shader + kit look | Own | Missing | Post-launch (TECH_DIFFICULT) |
| | **E: Maps and environments** | | | | | | |
| R63 | Industrial backgrounds ("abandoned" variant) | art | E49 | Reuse if cleared; add a grime/damage overlay pass | SM-GPL / Own | Have-unlicensed | M4 |
| R64 | Lab and bunker backgrounds | art | E50, E54 | Reuse Mag-campaign art if cleared (`mag_specimen`, `_servers`, `_vaults`, `mag_storage_1/2`) | SM-GPL | Have-unlicensed | M4 |
| R65 | Rooftop and train backgrounds | art | E52, E53 | Reuse if cleared (`background_roof`, `train_*`) | SM-GPL | Have-unlicensed | M4 |
| R66 | Desert outpost background (bg, fg, decal mask) and props | art | E51 | **Commission** a painting, or compose CC0 tiles in Krita | Comm / CC0/BY | Missing | M4 |
| R67 | Snow base background and snow particles | art / VFX | E58 | Commission or CC0 composite; snow is procedural particles | Comm / CC0/BY / Own | Missing | M4 |
| R68 | Prison background and prisoner looks | art | E55 | Commission or CC0 composite; looks from the kit | Comm / CC0/BY / Own | Missing | Post-launch |
| R69 | Collapsing building: multi-state backgrounds, debris VFX, rumble audio | art / VFX / audio | E56 | Commission the states; procedural debris; Sonniss | Comm / Own / Sonniss | Missing | Post-launch |
| R70 | Club combat background and lighting overlays | art / VFX | E57 | Reuse club props if cleared; procedural light cones and tints | SM-GPL / Own | Have-unlicensed | M4 |
| R71 | Modular background tile kit (floors, walls, matching floor line) | art | E59, G71 | CC0 tiles (Kenney, OpenGameArt) recoloured, or commission | CC0/BY / Comm | Missing | Post-launch |
| R72 | Destructible cover and trap sprites with break states; break SFX | art / audio | E60, C34, A3 | SVG props + procedural break flipbooks; Freesound CC0 | Own / CC0/BY | Missing | M4 |
| R73 | Biome ambience loops (wind, snow, industrial hum, club bass, rain) | audio | E49–E58 | Freesound CC0, Sonniss | CC0/BY / Sonniss | Missing (`train_loop.ogg` exists; provenance unknown) | M4 |
| R74 | Level thumbnails (260x200) for new levels | UI | E49–E60, D39 | Auto-render from the level (§6.3) | Own | Missing | M4 |
| R75 | Doors and turrets | art | E60 | Reuse if cleared (`textures/doors/` 9 frames, `turrets/default`) | SM-GPL | Have-unlicensed | M4 |
| | **F: Customisation and progression** | | | | | | |
| R76 | Feet options and skin-tone/tint presets | art | F61 | SVG feet; tint is shader data | Own | Missing | M5 |
| R77 | Armour kit expansion to 60 pairs, plus set icons | art / UI | F62, F63, F64 | As R03; SVG set icons | Own / Comm | Missing | M5 |
| R78 | Lock overlay, "new" badge and unlock toast | UI / audio | F64, F68 | SVG or Kenney CC0 UI; Kenney CC0 toast | Own / CC0/BY | Missing | M5 |
| R79 | Mastery badges and rank icons | UI | F65 | SVG tier frames + auto-rendered weapon silhouettes | Own | Missing | M5 |
| R80 | Skill icons | UI | F66 | SVG | Own | Missing | Post-launch |
| R81 | Loadout picker art | UI | F67 | Reuse `WeaponThumbnailCache` thumbnails | Own | Placeholder | M5 |
| R82 | Achievement icons (30–50) | UI | F68 | SVG template (frame + glyph); Kenney CC0 glyphs | Own / CC0/BY | Missing | M5 |
| | **G: Community and longevity** | | | | | | |
| R83 | Level-editor prop and tile library | art | G71, E59 | Kenney and OpenGameArt CC0 packs, recoloured | CC0/BY | Missing | Post-launch |
| R84 | Wave-editor icons | UI | G72 | SVG | Own | Missing | Post-launch |
| R85 | Photo-mode filters and shutter SFX | VFX / audio | G76 | Procedural shaders; Kenney or Freesound CC0 shutter | Own / CC0/BY | Missing | M5 |
| R86 | Modder rig template pack (SVG templates, sample `.armor`, `.look`, `.anim`) | art | G79, G71 | Export of the §6.4 templates | Own (GPL or CC0 by choice) | Missing | Post-launch |
| | **Phase 2 combat and animation polish** | | | | | | |
| R87 | Wall impact dust and spark sheets | VFX | PC7 | Procedural particles | Own | Have-unlicensed (`bullet_impact`) | M3 |
| R88 | Material impact SFX (concrete, wood, metal, glass, flesh) and per-material sparks and dust | audio / VFX | PC8, A12 | Sonniss, Freesound CC0; procedural VFX | Sonniss / CC0/BY / Own | Missing | M3 |
| R89 | Reload clips per class (pistol, rifle, shotgun, revolver), magazine sprites, reload SFX | animation / art / audio | PC11, PA6, B23 | `.anim` presets; SVG mags; Sonniss or CC0 | Own / CC0/BY | Missing | Post-launch (needs a reload mechanic, approval) |
| R90 | Knockdown-to-floor and get-up clips (face up, face down) | animation | PC13, PA12, A4 | `.anim` | Own | Have-unlicensed (stun clips, SM-GPL) | M3 |
| R91 | High and low flinch clips | animation | PC5, PA11 | `.anim` (optional) | Own | Missing | M3 |
| R92 | Kick clips (4–8) | animation | PA7 | `.anim`, after a FOOT channel exists | Own | Missing | M3 |
| R93 | Jump, fall-loop and land clips | animation | PA10 | Procedural squash first, then `.anim` | Own | Missing | Post-launch |
| R94 | Turn and transition clips | animation | PA15, PC10 | `.anim` (optional) | Own | Missing | M5 |
| R95 | Walk and run clips per archetype | animation | PA1 | Procedural first; optional `.anim` | Own | Missing | Post-launch |
| R96 | Controller glyph sheets (Xbox, PlayStation, Switch; ~30 each) | UI | PC18 | Kenney CC0 input prompts | CC0/BY (use of platform symbols **unverified**) | Missing | M5 |
| R97 | Colour-blind-safe crosshair and health cues | UI | PC15, PC16 | SVG or procedural | Own | Missing | M5 |

**No new assets needed:** PC1–PC4, PC6, PC9, PC14, PC17, PA2 (procedural idle), PA3–PA5, PA13, PA14, PA16, B14, B17, B-ARCH, C25, C26, C33, C-ARCH, D38, D41, D46, D-ARCH, F69, F-ARCH, G73–G75, G77, G78 and G80. They still inherit the core replacements (R05, R07, R08, R09).

**What is cheap and what costs money:**
- **Cheap (Zeph alone, days):** the fonts (R08–R10), renames (R13), SVG props, icons and UI (R24, R33, R34, R37, R56–R58, R78–R82), procedural VFX (R19, R27, R35, R87), CC0 and Sonniss SFX (R05, R06, R25, R36, R88) and the placeholder music silence.
- **Cheap but slow:** `.anim` clips. Characters are rigid sprites posed by keyframes, with no frame-by-frame drawing (03 §1), so a non-artist can author clips as text with the preset tool (§6.4). Budget hours per clip, not money.
- **Needs money or a collaborator:** the base character design (R01), heavy-armour and boss art (R48, R54), painted backgrounds for new biomes (R66–R69; each level is one painting, 04 E49), the OST (R07, R55) and voice barks (R51). Get three quotes for each. Market prices are **unverified** here.
- **Decided by one email:** R04, R12, R28, R60, R63–R65, R70 and R75 are free if Studio Minus confirms GPL-3.0 (BL-4), and a commission bill if not.

## 5. Sourcing workflow for a non-artist

### 5.1 Sources that permit commercial sale (terms as cited by the auditor, 06 §2.10)

| Source | Terms | Use for | Watch out |
| --- | --- | --- | --- |
| Kenney (kenney.nl) | "all game assets on the asset pages are public domain licensed (CC0)", "even in commercial projects", "Attribution is not required" (https://kenney.nl/support) | UI icons, input prompts, particles, prop silhouettes, UI and impact SFX | Generic look; recolour to the house palette |
| Freesound, **CC0 filter only** | "for the 'zero' license you can do pretty much what you want". CC BY is commercially usable with credit. **Avoid BY-NC** ("you can't earn any money") (https://freesound.org/help/faq/) | Foley, impacts, ambience, gun layers | Licence is per sound; record each one |
| Sonniss GDC Game Audio Bundles (2015–2024) | "completely royalty-free for any commercial work", "No attribution is required". Redistribution "Not as sound effects ... But you can absolutely sell them as part of your finished game" (https://sonniss.com/gameaudiogdc/) | Gunshots, explosions, mechanical SFX | **Keep raw files out of the public GPL repo** (`public_repo: false`); ship them only packed in `base.waa` |
| OpenGameArt, CC0 or CC-BY filter | CC0 and CC-BY allow commercial use. CC-BY-SA requires derivatives to carry the same licence. Credit format: "[asset name]" by [author] licensed [licence]: [url] (search summary of https://opengameart.org/content/faq) | Backgrounds, tiles, music loops | Mixed licences on one site; check each file. Avoid SA for art you combine with your own |
| Google Fonts (OFL) | Bundling allowed; ship the copyright line and OFL text; no RFN after conversion (https://openfontlicense.org/ofl-faq/ FAQ 1.4, 1.20, 2.2) | R08, R09 | Check each family's own `OFL.txt` (Anton: https://fonts.google.com/specimen/Anton) |
| Commissioned work | A written agreement granting a non-exclusive, perpetual, worldwide licence (for music, a game-sync licence that includes streaming and video) (06 §2.10) | R01, R03 hero pieces, R07, R48, R54, R66–R69 | Sign before work starts |
| Procedural or own SVG | Zeph's own output (`Proprietary-Zeph`) | VFX, icons, props, patterns | Record the generator and seed |

### 5.2 How to verify a licence (every file, every time)
1. **Find the licence on the asset's own page**, not on a list, a pack summary or a forum post. Read the full licence once per source.
2. **Check the uploader is the author.** If a "free" sound or sprite looks like a commercial library item, skip it. This repo shipped library sounds with tags intact (BL-6).
3. **Reject** anything marked NC, ND, "personal use", "editorial", "free for non-commercial", "demo" or "do not redistribute", and anything with no licence at all.
4. **Scan the file's metadata** with the tag scanner (§6.2). A library or copyright tag means reject, or record why it is fine.
5. **Record it before committing** (§7): path, source URL, author, licence and version, retrieval date, whether you modified it, sha256, attribution text, and whether it may live in the public repo.
6. **Save evidence:** a PDF or HTML snapshot of the licence page and, for commissions, the signed contract. Keep both outside the public repo and reference them by id.
7. **Credit as required.** CC-BY entries flow into the credits automatically from `attribution_required: true`.

### 5.3 Never assume
- **Publicly available ≠ free for commercial use.** This repo is the proof: public on GitHub, yet most of its assets cannot be sold.
- **"In a GPL repo" ≠ GPL-licensed.** A repo licence can only cover what its owner owns (06 §2.8).
- **Fan tolerance ≠ licence.** Unenforced is not permitted.
- **"Royalty-free" ≠ CC0.** It usually means "paid once, may use in your product", not "may redistribute raw" (BOOM, Sonniss).
- **"Free on YouTube or SoundCloud" ≠ licensed for games** (BL-5).
- **A font you can download ≠ a font you can embed.** Converting it to an atlas is modification (OFL FAQ 2.2) and is barred by some EULAs (BL-8, BL-9).
- **A pack licence ≠ every file's licence.** Check each file on mixed-licence sites.
- **AI-generated assets:** copyright status and each tool's commercial terms vary and were **not researched (unverified)**. If you use one, record the tool, version, terms URL and date, keep the output `Proprietary-Zeph` only after checking the terms, and never prompt for MC characters or named artists' styles.

## 6. Tooling to build

None of these exist today (06 §2.10 searched `src/**`). Efforts assume a solo dev with AI help.

### 6.1 Asset importer and validator (effort M, 3–5 days)
Add a `--mode validate` CLI next to `--mode pack` (`Program.cs:32`), wrapped by an xUnit test in `src/MadnessInteractiveReloaded.Test/` so CI runs it. It walks `base/` and checks:
- **Sprites:** every PNG is 8-bit RGBA with real alpha. Warn if the outer 1 px border is not fully transparent (filtering bleed). Warn above 4096 px and fail above 8192 px.
- **Naming:** new files match `^[a-z0-9_]+(_left|_right)?\.png$`, with an allow-list for legacy names. Flipbook names (`<name>_<frames>_<cols>x<rows>.png`) must divide the image exactly.
- **Characters:** every `.armor` `left`/`right` path exists and the pair has equal size. Every `.hand` has all 7 poses × front/back.
- **Weapons:** `HoldPoints`, `BarrelEndPoint` and `CasingEjectionPoint` fall within the texture bounds ± N px.
- **Backgrounds:** QOI backgrounds carry the encoded alpha mask.
- **Clips:** `.anim` files parse with the invariant culture and have a non-zero duration (bugs B6 and B7 in 03 §3).
- **Pivot sheet (optional):** a debug PNG with pivot and anchor markers drawn on each new sprite, so a non-artist can eyeball alignment.

### 6.2 Licence manifest checker (effort M, 3–4 days, plus S for the tag scanner)
An xUnit gate in `MIR.Test`, which already copies `base/` (PreBuild `xcopy`). It loads `base/ASSETS.json` (§7), expands the globs, and **fails the build** if:
- any shipped file under `base/` has no entry;
- a licence is not on the allow-list (`CC0-1.0`, `CC-BY-4.0`, `OFL-1.1`, `GPL-3.0-or-later`, `Sonniss-GDC`, `Proprietary-Zeph`, `Licensed-<id>`);
- a `sha256` does not match;
- a `licence_file` is missing;
- an `attribution_required` entry has no credit text.

A companion CI script fails if any `public_repo: false` file is tracked by git (`git ls-files`). A **tag scanner** reads WAV/OGG/PNG metadata and fails on BOOM, PMSFX, Sound Ideas, Sound Pack Tree, "Copyright" or "©" unless the entry whitelists that tag. A `--mode credits` command generates the asset part of `credits.txt` and `NOTICE.txt`. Make `dotnet publish` depend on the gate (a pre-publish target or release script) so an unlisted asset can never ship.

### 6.3 Conversion scripts (effort M, about 1 week in total; each script S)
- **`svg2sprite`:** Inkscape CLI export of named layers (`left`, `right`, `base`, `slide`, `mag`) at 100% (1 px = 1 unit). It reads marker circles with ids `pivot`, `hold_0`, `hold_1`, `barrel_end` and `casing_eject`, converts them to pixel offsets from the texture centre (Y up), and writes them into the weapon JSON or the `.armor` `offset`.
- **Background pack:** fix `png2qoi.py` directory mode to use `os.path.join(input, p)` (06 §3). Pack bg, fg and decal mask with `src/mirbgencoder`, and split anything wider than 4096 px.
- **`audio_prep`:** the FFmpeg command-line tool, used as a dev tool only and never shipped. It trims silence, normalises loudness, converts SFX to 16-bit WAV and music to OGG Vorbis at ~112–128 kbps (06 §4), and strips metadata after it is recorded in the manifest.
- **`font_build`:** OFL TTF → `.wf` with `Walgelijk.FontGenerator`, with the atlas `Name` set to a non-RFN id (e.g. `mono-ui`).
- **`fx_render`:** a procedural flipbook renderer (noise-based explosion, smoke, dust, sparks, debris, snow) that writes correctly named sheets and a manifest entry with the generator and seed.
- **`thumb_render` and `pattern_bake`:** 260x200 level thumbnails from backgrounds, and 256 px skin patterns (camo, carbon, rust) from noise.

### 6.4 Character-rig and animation presets (effort M–L, 1–2 weeks; the SVG template alone is S)
- **SVG rig template:** one Inkscape file per part type: head, body, hand (7 poses × front/back), foot, hat, eyes, mask, body layer 1 and 2. Each has:
  - canvas guides at reference sizes (body ~194x351, as the agent suit);
  - pivot markers at `HeadRotationPivot` and `BodyRotationPivot`;
  - separate `left` and `right` layers;
  - a locked silhouette of the base body to draw armour against;
  - the house palette and outline width.

  This turns "draw a hat" into "edit flat shapes over a guide", which a non-artist can do.
- **`.look` generator:** builds a look from a short JSON list of piece ids.
- **`.anim` preset generator:** a small script that writes `.anim` text from parameterised poses, e.g. `slide(duration, lean)`, `knockdown(direction)`, `getup(face_up|face_down)`, `execution(weapon_class)`, `flinch(region)`, `kick(height)` and `reload(class)`. It uses easing helpers, is checked by the §6.1 parser and is previewed in `AnimationTestingSystem`. It covers R15–R23, R38, R47, R89–R95.

| Tool | Effort | Blocks |
| --- | --- | --- |
| 6.2 Manifest gate and tag scanner | M + S | M1 (do first) |
| 6.1 Validator | M | M2 |
| 6.3 Conversion scripts | M | M2 |
| 6.4 Rig and animation presets | M–L | M2 (template), M3 (anim presets) |

## 7. Provenance manifest format

**Location:** `base/ASSETS.json`, as the auditor proposed, so the test copy of `base/` and the packer both see it. Licence texts go in `base/licences/` (e.g. `base/licences/OFL-Anton.txt`) and ship inside the game. Files with `public_repo: false` (Sonniss raw files, bought assets) live in a private submodule or download and are packed only into release builds. Contracts and licence-page snapshots live outside the public repo and are referenced by id.

**Schema `iir-asset-manifest/1`.** These are the auditor's fields; fields marked † are additions proposed here.

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `schema` | string | yes | `"iir-asset-manifest/1"` |
| `defaults` | object | yes | `owner`, `licence` applied when an entry omits them |
| `assets[].path` | glob | yes | Relative to `base/`; the most specific match wins |
| `kind` | enum | yes | `sprite`, `background`, `sfx`, `music`, `font`, `anim`, `ui`, `vfx`, `video`, `data` |
| `licence` | enum | yes | `CC0-1.0`, `CC-BY-4.0`, `OFL-1.1`, `GPL-3.0-or-later`, `Sonniss-GDC`, `Proprietary-Zeph`, `Licensed-<id>` |
| `source` | URL or text | yes unless own | Where it came from |
| `author` | string | yes unless own | Creator |
| `retrieved` | date | for third-party | Download or delivery date |
| `modified` | bool | yes | Changed after download |
| `sha256` | map file→hash | recommended | Detects silent swaps |
| `attribution_required` | bool | yes | Adds the entry to the generated credits |
| `credit`† | string | if attribution required | Exact credit line |
| `public_repo` | bool | default `true` | `false` keeps the file out of the GPL repo |
| `licence_file` | path | OFL, CC-BY, contracts | Licence text shipped in `base/licences/` |
| `reserved_font_names`, `converted` | array, string | fonts | RFN guard and the conversion used |
| `contract`†, `evidence`† | id | commissions, third-party | Private contract or licence-page snapshot id |
| `generator`† | string | generated assets | Script, version and seed |
| `notes` | string | no | Free text |

**Example entries:**

```json
{
  "schema": "iir-asset-manifest/1",
  "defaults": { "owner": "Zeph", "licence": "Proprietary-Zeph" },
  "assets": [
    {
      "path": "sounds/firearms/pistol_heavy_*.wav",
      "kind": "sfx",
      "licence": "CC0-1.0",
      "source": "https://freesound.org/s/123456/",
      "author": "example_user",
      "retrieved": "2026-11-20",
      "modified": true,
      "sha256": { "pistol_heavy_1.wav": "…" },
      "attribution_required": false,
      "public_repo": true,
      "evidence": "EV-2026-0042",
      "notes": "Example from 06 §2.10. Layered with CC0 #654321; pitch-shifted"
    },
    {
      "path": "textures/bodies/default/iir_base_*.png",
      "kind": "sprite",
      "licence": "Licensed-COMM-2026-001",
      "source": "Commission: base character set (R01)",
      "author": "Example Artist",
      "retrieved": "2027-01-15",
      "modified": false,
      "sha256": { "iir_base_body_left.png": "…", "iir_base_body_right.png": "…" },
      "attribution_required": true,
      "credit": "Character art by Example Artist",
      "public_repo": true,
      "licence_file": "licences/COMM-2026-001-summary.txt",
      "contract": "COMM-2026-001",
      "notes": "Perpetual, worldwide, non-exclusive; modification, sale, streaming and trailers allowed; layered SVG sources delivered"
    },
    {
      "path": "textures/fx/explosion_round_24_6x4.png",
      "kind": "vfx",
      "licence": "Proprietary-Zeph",
      "modified": false,
      "attribution_required": false,
      "public_repo": true,
      "generator": "tools/fx_render explosion --frames 24 --grid 6x4 --seed 1337 (v0.1)",
      "notes": "Original, code-generated; no third-party input"
    }
  ]
}
```

The Freesound URL and the artist are placeholders. Replace them with real records.

## 8. Attribution and notice obligations

### 8.1 `NOTICE.txt` (generated from the manifest plus `obj/project.assets.json`; see BL-11)
- **Game code:** the GPL-3.0 text, Studio Minus's original copyright notices (keep them), Zeph's notice, and where to get the exact corresponding source for this build (a tagged public repo URL) (https://www.gnu.org/licenses/gpl-3.0.html).
- **Asset scope statement:** the code is GPL-3.0. Zeph's own art, audio and fonts are separate works in an aggregate and are not GPL-licensed, per GPLv3 §5 (https://www.gnu.org/licenses/gpl-3.0.html#section5). List any reused Studio Minus assets (SM-GPL) as GPL-3.0. Exact wording is **unverified**; take advice.
- **Every shipped third-party component** with its copyright line and licence text (06 §2.7):
  - Walgelijk packages: MIT per nuspec, or GPL-3.0 (repo `LICENSE`, `gh api repos/mestiez/Walgelijk/contents/LICENSE`).
  - OpenTK (MIT) and GLFW (zlib).
  - Newtonsoft.Json, Mono.Cecil, MonoMod, TextCopy, SkiaSharp, System.CommandLine, Roslyn and Microsoft.Extensions (MIT).
  - NVorbis (MIT).
  - PortAudioSharp2 (Apache-2.0 per its repo) and PortAudio (MIT-style, https://github.com/PortAudio/portaudio/blob/master/LICENSE.txt).
  - FFmpeg.AutoGen (LGPL-3.0).
  - nativefiledialog-extended (zlib) and NativeFileDialogExtendedSharp (GPL-3.0-only, https://github.com/Crauzer/NativeFileDialogSharp).
  - Harmony (MIT) and BigGustave (Unlicense).
  - FastNoise Lite (already listed; licence not restated by the auditor, **unverified**).
- **LGPL natives with source offers:**
  - OpenAL Soft 1.23.1 (https://github.com/kcat/openal-soft/blob/master/COPYING).
  - The FFmpeg build that replaces BL-10: licence text, exact source tarball and configure line (https://ffmpeg.org/legal.html).
- **Shader snippets:** MIT notices for glsl-voronoi-noise, Ashima/stegu webgl-noise and Inigo Quilez (06 §2.7).
- **Fonts:** each OFL font's copyright line and `OFL.txt` (https://openfontlicense.org/ofl-faq/ FAQ 1.20).
- **Assets:** every entry whose licence is not `Proprietary-Zeph`, with name, author, licence, URL and a modified flag. CC0 and Sonniss need no attribution (https://kenney.nl/support, https://sonniss.com/gameaudiogdc/), but listing them costs nothing and documents provenance.
- **Remove** the ImageSharp entry; it is not shipped (06 §2.7).

### 8.2 In-game credits (`base/data/credits.txt`, generated)
- Zeph; commissioned artists and composers with their agreed credit lines; CC-BY authors in the required format ("[asset name]" by [author] licensed [licence]: [url], per the OpenGameArt FAQ summary); font authors.
- Lineage: "Based on the open-source code of Madness Interactive Reloaded by Studio Minus (GPL-3.0); built with the Walgelijk engine."
- Any musician who grants permission (BL-5), credited exactly as they ask.
- A non-affiliation line: "Not affiliated with or endorsed by Krinkels, GIBBING TREE, LLC or Studio Minus." This wording is **unverified**.

### 8.3 itch.io store page
- **Rights warranty:** itch.io Terms §4 make you warrant that you hold all rights. §3 bars IP infringement, and §6 sets out DMCA takedowns (https://itch.io/docs/legal/terms). The quality guidelines say "always seek permission from the original creator and give proper credit" (https://itch.io/docs/creators/quality-guidelines). Publish only after the §6.2 gate passes with zero BL items.
- **No Madness branding** in the title, tags, description, screenshots or trailer: no MC characters or backgrounds.
- **Licence block:** say the code is GPL-3.0 and link the source for that exact build. Say where the FFmpeg and OpenAL sources are, and that the game's art and audio are separately licensed. Link or include the full credits.
- **Set buyer expectations:** buyers may redistribute the GPL code, with or without a fee (https://www.gnu.org/licenses/gpl-faq.en.html#DoesTheGPLRequireAvailabilityToPublic). What is sold is the build plus Zeph's original content.
- **Trailers and streaming:** every music licence must cover video and streaming use (06 §2.10).
