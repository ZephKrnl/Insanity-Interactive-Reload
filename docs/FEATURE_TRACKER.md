# Insanity Interactive Reloaded: Feature Tracker

Milestones follow DEVELOPMENT_ROADMAP.md §5 (M2 and M3 are split into a/b sub-milestones).

This is the single living tracker for every feature ID in `.superpowers/audit/FEATURES.md`. It was created on 2026-11-17 by the Phase 1 synthesis.

- Every audit status comes from a Phase 1 auditor row. The sources are tagged:
  - [01] architecture
  - [02] combat
  - [03] animation/rendering
  - [04] AI/modes/levels
  - [05] systems/perf/QA
  - [06] assets/licensing

  The findings files are in `.superpowers/audit/`.
- Bug IDs (`BUG-nn`) and performance IDs (`PERF-nn`) point to `docs/TECHNICAL_AUDIT.md`.
- Asset and licence detail is in `docs/ASSET_REQUIREMENTS.md`.

## Legend

**Audit status** (one per feature, from FEATURES.md):

| Status | Meaning |
|---|---|
| `IMPLEMENTED` | Already in the code and judged to work (by reading the code) |
| `NEEDS_IMPROVEMENT` | In the code, but needs improvement |
| `MISSING` | Not in the code |
| `TECH_DIFFICULT` | Missing **and** technically hard |
| `DEPENDENT` | Missing and blocked on another feature |

**Implementation status:**

| Status | Meaning |
|---|---|
| `Implemented (verified)` | Exists and was confirmed working, by running it or by a passing test of the feature itself. **No feature qualifies yet.** Phase 1 was read-only and nobody launched the game. |
| `Implemented (unverified)` | Exists in the inherited MIR code. Auditors confirmed it by reading the code, not by playing. |
| `Partial` | Part of the feature exists (an audit status of `NEEDS_IMPROVEMENT`). The improvement work has not started. |
| `Not started` | No code for the feature yet. |
| `Proposed` | A work item suggested by the audit, such as a bug-fix group or an enabler, that is waiting for Zeph's approval. No code yet. |

**Difficulty:** for a solo developer with AI help.

| Grade | Time |
|---|---|
| S | 2 days or less |
| M | 1 week or less |
| L | 3 weeks or less |
| XL | More than 3 weeks |

**Proposed milestones:**

| Milestone | Theme |
|---|---|
| M1 | Technical foundation: critical bug fixes, save robustness, the CPU frame limiter, combat-feel quick wins and the animation foundation |
| M2 | Gameplay expansion: weapons, AI architecture, arenas and survival modes |
| M3 | Replayability: customisation, progression, bosses and challenges |
| M4 | Creator tools: map and wave editors, replay and modding |
| M5 | Release quality |
| Post-launch | After release |

"M5 (verify)" means the feature already exists and only needs a play-test check before release. "Approval" marks a change to MIR's core feel that the auditors said needs Zeph's decision.

## Summary

### Counts by audit status (118 feature IDs: 114 numbered features plus 4 `*-ARCH` enabler rows, B, C, D and F)

| Audit status | Count | Implementation status today |
|---|---|---|
| IMPLEMENTED | 21 | Implemented (unverified): 21 |
| NEEDS_IMPROVEMENT | 49 | Partial: 49 |
| MISSING | 36 | Not started: 36 |
| TECH_DIFFICULT | 5 | Not started: 5 |
| DEPENDENT | 7 | Not started: 7 |
| **Total** | **118** | Implemented (verified): **0** |

There are also 16 `Proposed` rows for fix groups and enablers (see the last table). They are not feature IDs and are not counted above.

### Counts by category

| Category | IDs | IMPL | NEEDS_IMPR | MISSING | TECH_DIFF | DEPENDENT |
|---|---|---|---|---|---|---|
| PC: Phase 2 combat polish | 18 | 7 | 10 | 0 | 0 | 1 |
| PA: procedural animation | 16 | 4 | 10 | 1 | 0 | 1 |
| A: combat and movement | 12 | 1 | 4 | 6 | 1 | 0 |
| B: weapons and equipment | 13 | 3 | 5 | 5 | 0 | 0 |
| C: enemy AI | 13 | 1 | 6 | 5 | 0 | 1 |
| D: game modes | 13 | 1 | 3 | 6 | 1 | 2 |
| E: maps and environments | 12 | 2 | 4 | 5 | 1 | 0 |
| F: customisation and progression | 11 | 2 | 4 | 5 | 0 | 0 |
| G: community and longevity | 10 | 0 | 3 | 3 | 2 | 2 |
| **Total** | **118** | **21** | **49** | **36** | **5** | **7** |

### Counts by proposed milestone (features only)

| M1 | M2a | M2b | M3a | M3b | M4 | M5 | Post-launch |
|---|---|---|---|---|---|---|---|
| 13 | 26 | 10 | 13 | 33 | 7 | 11 | 5 |

M2 and M3 are split into M2a/M2b and M3a/M3b in DEVELOPMENT_ROADMAP.md §5. Most M5 rows are verification of existing features.

### Release blockers that are not features

The licensing and IP findings in [06] block a paid release whatever features ship. See the `M5-LIC` row, `TECHNICAL_AUDIT.md` (licensing section) and `ASSET_REQUIREMENTS.md`.

### Coverage check

All 118 IDs in FEATURES.md have exactly one row:

- PC1–PC18
- PA1–PA16
- A1–A12
- B13–B24 and B-ARCH
- C25–C36 and C-ARCH
- D37–D48 and D-ARCH
- E49–E60
- F61–F70 and F-ARCH
- G71–G80

---

## PC: Phase 2 combat polish

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| PC1 | Responsive movement and aiming | IMPLEMENTED | Implemented (unverified) | S | PC3, PC16 | No | M1 (via PC3) | [02] Mouse aim uses the raw cursor (`PlayerCharacterSystem.cs:72-74`). The hand angle is unsmoothed (`HandPosingFunctions.cs:174-175`). A new direction press cancels the current hop (`PlayerCharacterSystem.cs:231-236`). Same-frame fire depends on system order (`SceneUtils.cs:210-220`; inferred). Pad aim is direction-only, with no smoothing or assist (`ControllerLoopEvent.cs:250-266`). |
| PC2 | Accurate melee and projectile collision detection | NEEDS_IMPROVEMENT | Partial | M | — | No | M1 (`melee_hit_mode` option, legacy default; the default switch and balance pass are M2a, approval-gated; the sharp-box fix is in M1-FIX-COMBAT) | [02] Melee is one ray along the cursor at the hit frame and ignores the animated weapon (`MeleeSequenceSystem.cs:165-200`, `MeleeUtils.cs:87-91`). The engine ray marches in 10 px steps (inferred to miss thin colliders). Thrown weapons use point rays (`VelocitySystem.cs:72,84`). See also BUG-25. [03] Hands, feet and apparel have no colliders. A swept hand-to-tip test shifts balance, so it **needs approval** and a playtest. |
| PC3 | Input buffering where appropriate | NEEDS_IMPROVEMENT | Partial | S | — | No | M1 | [02] Only melee combos buffer (`MeleeUtils.cs:33-34`). Fire, jump-dodge, throw and interact use same-frame `ActionPressed` (`PlayerCharacterSystem.cs:175-263`), so presses during cooldowns are lost. Add a buffer of about 100-150 ms. The controls stay unchanged. |
| PC4 | Smooth weapon switching | IMPLEMENTED | Implemented (unverified) | S | — | No | M5 (verify) | [02] An animated pickup swap of 0.16 s (`CharacterUtilities.cs:43-74`, `CharacterPickupComponent.cs:10-11`). There is no inventory, by design, and adding slots needs approval. Optional polish: toss the old weapon instead of dropping it in place. |
| PC5 | Directional hit reactions | IMPLEMENTED | Implemented (unverified) | S | — | Optional (up/down variants) | M2a (with PA11) | [02] Front/back light stuns by bullet direction (`BulletEmitter.cs:405-413`). An impact offset by hit height (`:312-323`). Facing-aware deaths (`CharacterUtilities.cs:212-234`). Front/back only; melee picks the direction from position, not from the swing. The animation side (PA11) is rated NEEDS_IMPROVEMENT by [03]. |
| PC6 | Weapon recoil and recovery | IMPLEMENTED | Implemented (unverified) | S | — | No | M2a (optional, via B-ARCH) | [02] `WeaponSystem.cs:256`; `CharacterPositionSystem.cs:304-318,589-598`. The crosshair follows recoil (`PlayerUISystem.cs:212-213`). Recoil is one scalar with no per-weapon pattern, and iron sights do not tighten spread. |
| PC7 | Shell ejection and impact effects | NEEDS_IMPROVEMENT | Partial | S | — | Maybe (dust and spark sheets) | M1 (stretch; moves to M2a if M1 runs long) | [02] Casings work (`WeaponSystem.cs:289-304`). Wall effects appear only inside `DecalZone`s (`BulletEmitter.cs:74-79`). There is no wall impact sound, and casing velocity is hardcoded. Also replace the per-hit `GL.ReadPixels` (PERF-04). |
| PC8 | Material-specific impact feedback | NEEDS_IMPROVEMENT | Partial | M | Material tags on level colliders (level data) | Yes: concrete, wood and metal impact SFX and sparks | M2a | [02] Only flesh vs non-flesh. Melee sounds differ for blade and blunt (`MeleeUtils.cs:218-244`). A metal-impact set exists but only the train uses it (`Prefabs.cs:89`). Needs a material tag on colliders plus a lookup in `CastBulletRay`. |
| PC9 | Smooth character movement | IMPLEMENTED | Implemented (unverified) | S | — | No | M2a (with PA1; approval) | [02] The hop model (`CharacterMovementSystem.cs:44-133`) is MIR's identity, and smoothing it **needs approval**. [03] PA1 proposes a stride phase and a run tier that keep the hop. |
| PC10 | Better animation transitions | NEEDS_IMPROVEMENT | Partial | M | PA14, PA15 | No | M2a (per-clip mix durations; its blend bugs stay in M1) | **Merged [02]+[03]; both say NEEDS_IMPROVEMENT.** A two-clip `Quad.InOut` lerp with a fixed 0.3125 s mix (`CharacterUtilities.cs:449-481`, `CharacterComponent.cs:176`). Blend bugs BUG-44, BUG-45 and BUG-60. [03 §7.5] proposes per-clip mix durations. |
| PC11 | Reloading animations | DEPENDENT | Not started | M (after the mechanic) | Reload mechanic (no ID; B-ARCH data), B23, PA4, PA6 | Yes: reload clips per weapon class, magazine sprites, SFX | M2a if reload is approved, otherwise Post-launch | **Conflict: [02] says MISSING, [03] says DEPENDENT. DEPENDENT kept** because the animation is blocked on a mechanic that doesn't exist. There is no reload field (`WeaponData.cs:79`), no reload action (`GameAction.cs:7-25`), and only the unused `AiComponent.WantsToReload` (`:68,89`). Reload replaces MIR's "throw the empty gun" loop, so it **needs approval**. |
| PC12 | Melee swing animations | IMPLEMENTED | Implemented (unverified) | S | — | Optional | M1 (BUG-22, BUG-46) | **Merged [02]+[03]; both say IMPLEMENTED.** 14 `.seq` files and 71 melee clips. The fifth `blunt.seq` swing never hits (BUG-22), and hit-frame timing drifts (BUG-46). The swing-to-hitbox link is PC2. |
| PC13 | Knockdown and recovery animations | NEEDS_IMPROVEMENT | Partial | M (clip-based, [02]) / L (live ragdoll, [03]) | PA12 (and PA14, PA15, PA16) | Yes: knockdown-to-floor and get-up (face up and face down) clips | M2a | **Merged [02]+[03]; both say NEEDS_IMPROVEMENT.** A baked `stun_heavy_*` fall-and-get-up (`CharacterUtilities.cs:286-312`), triggered only by throws and unarmed finishers. NPCs only, flat ground only. The player is never knocked down (`Prefabs.cs:952`), and living characters can't ragdoll (`MadnessUtils.cs:731`). The two difficulties are for different approaches; the live-ragdoll approach needs BUG-47 first. |
| PC14 | Dodging and evasive movement | IMPLEMENTED | Implemented (unverified) | S | — | No | M1 (BUG-35 only) | [02] Auto-dodge (`BulletEmitter.cs:218-262`), jump dodge (`CharacterUtilities.cs:318-394`), keyframed i-frames in `jumps/*.anim`, and Teleport Dash. Dodge regenerates only while moving (`CharacterSystem.cs:24-25`). That is by design, and changing it needs approval. |
| PC15 | Optional camera shake and accessibility settings | NEEDS_IMPROVEMENT | Partial | S | — | No | M1 (shake and flash toggle) / M5 (accessibility pack) | [05] Screenshake 0-200% (`Settings.cs:48`, `CameraShakeSystem.cs:16`). 35 `MadnessUtils.Flash` calls have no toggle (`MadnessUtils.cs:343`), and there are no other accessibility options. [02] Shake fires for every shooter, including off-screen ones (`WeaponSystem.cs:274`), has no cap, and never fires on hits. |
| PC16 | Adjustable mouse and controller sensitivity | NEEDS_IMPROVEMENT | Partial | S | PC18 | No | M1 | [05] Only the pad `AimDistance` and menu `CursorSpeed` (`Settings.cs:67-73`). Deadzones are hardcoded (`GamepadState.cs:13-15`). Mouse aim is the absolute cursor, so mouse "sensitivity" barely matters; offer a crosshair size instead. Pads need a deadzone, response curve, smoothing and invert. |
| PC17 | Configurable keybindings | NEEDS_IMPROVEMENT | Partial | S | — | No | M5 (its bugs are in M1-FIX-INPUT) | [05] Keyboard and pad rebinding UI (`SettingsSceneSystem.cs:300-415`), saved through `ControlScheme`. Gaps: Escape, Ctrl+wheel (BUG-34), disk click (BUG-71) and the experiment keys are hardcoded. A mouse binding is one button only (`UserInput.cs:17`). Duplicates are allowed, names are not localised, and Reset resets both devices (BUG-73). |
| PC18 | Native controller support where supported by the engine | NEEDS_IMPROVEMENT | Partial | M | — | Yes: button glyph sheets (Xbox, PlayStation, Switch; about 30 icons each) | M5 | [05] XInput through P/Invoke, first slot only (`XInputGamepadSource.cs:72-81`). Win32 cursor warper, menu navigation, rebinding and rumble. [01] About 139 controller unit tests pass; that is unit-level only, not play-tested. No glyphs, and a pad can't collect disks (BUG-71). Windows only. Linux and Steam Deck would need an `IGamepadSource` over GLFW (inferred available) or SDL3. |

## PA: procedural 2D animation

All rows are from [03]. Its section 7 has the proposed architecture: masked and additive layers, `IProceduralModifier` slots, a `CharacterMotionProfile` data file and optional `.anim` headers. It is designed so that, with no new data, the output stays identical to today's.

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| PA1 | Walking and running cycles | NEEDS_IMPROVEMENT | Partial | M | PA14, PA16 | Optional: walk and run `.anim` per archetype | M2a | Hop locomotion, circular feet and a noise head bob (`CharacterMovementSystem.cs:44-159`, `CharacterPositionSystem.cs:459-476`). The only walk clip is `zed_walk`. There is no stride phase, no run tier (one `TopWalkSpeed = 500`, `CharacterPositioning.cs:35`) and no backwards walk. Keep the hop. |
| PA2 | Idle breathing and subtle body movement | MISSING | Not started | S | PA16 | No | M2a | In gameplay the standing pose is completely static. A grep for `breath`/`idle` finds only the menu clip (`Shared/Animations.cs:41`). Add a procedural additive layer that fades out under clips. It is the cheapest visible "alive" upgrade. |
| PA3 | Cursor-directed aiming | IMPLEMENTED | Implemented (unverified) | S (polish) | — | No | M2a (polish with PA15) | `PlayerCharacterSystem.cs:52-103`; torso lean at `CharacterPositionSystem.cs:556`. Polish: the head follows the aim only 5% (`:447`). The flip is instant, with no turn. The `AimOrigin` easing is duplicated (`:58-63`). |
| PA4 | Independent torso and arm movement | NEEDS_IMPROVEMENT | Partial | M | PA14 | No | M2a | Hands are posed independently and the torso leans with the aim. But clips are one whole-body channel, and all 192 clips define BODY, HEAD and 2 HANDs. Needs per-limb masked layers, which unblock reload, kicks and throwing while walking. |
| PA5 | Weapon recoil | IMPLEMENTED | Implemented (unverified) | S (polish) | PA16 | No | M1 (constants move into the PA16 profile; no visible change) | The inputs are weapon data (`WeaponData.cs:99-105`). The response is procedural (`CharacterPositionSystem.cs:304-318,571-573,590-662`) and its constants are hardcoded. There is no head or shoulder follow-through. |
| PA6 | Reloading | DEPENDENT | Not started | M (after the mechanic) | B-ARCH (reload and ammo data), PA4, PA14 | Yes: reload clips per class, magazine sprites, SFX | M2a if reload is approved, otherwise Post-launch | No reload mechanic exists (see PC11). Approach: an upper-body layer clip, plus a procedural off-hand target on a weapon "mag point". The pattern already exists in `CharacterPickupSystem.cs:37-60`. |
| PA7 | Punching and kicking | NEEDS_IMPROVEMENT | Partial | M | PA16 (and PA4 to layer over walking) | Yes: 4-8 kick clips | M2a | Punches exist: 2 fist clips, 24 unarmed clips and 7 unarmed `.seq` files. Kicks are impossible because feet have no animation channel. `FootLimb.cs` has only a procedural offset, and the deserialiser knows only BODY, HEAD and HAND (`AnimationDeserialiser.cs:20-22`). Add a `FOOT` block, which is backwards compatible. |
| PA8 | Melee weapon swings | IMPLEMENTED | Implemented (unverified) | S (fix) | — | No | M1 (BUG-46) | 34 swing clips and 6 melee-stun clips; `MeleeSequenceSystem.cs:15-88`. Hit timing drifts (BUG-46). The ray hit test belongs to PC2. |
| PA9 | Dodging and sliding | NEEDS_IMPROVEMENT | Partial | M | PA4, PA14 | Yes: slide start, loop and exit clips | M2b (with A6) | `dodge_1/2`, 13 jump-dodge clips by agility tier, and the dash in `JumpDodgeSystem.cs:31-48`. There is no slide. The `smooth` header is ignored (BUG-50). |
| PA10 | Jumping and landing | NEEDS_IMPROVEMENT | Partial | L | PA14, PA15 | Yes: jump, fall-loop and land clips | Post-launch (a procedural landing squash could ship in M2) | Jumps are baked clip offsets over a floor-locked centre. Characters are kinematic, with no vertical physics (`Level.cs:52-85`, section 2.8). A true jump needs vertical velocity and ground detection. |
| PA11 | Directional hit reactions | NEEDS_IMPROVEMENT | Partial | M | PA14, PA16 | Optional: high and low flinch clips | M2a | An additive impact offset (`ImpactOffsetSystem.cs:18-31`). Light stuns are front/back only, and the player never flinches (`Prefabs.cs:952`). Proposal: turn `ImpactOffsetComponent` into a damped spring with per-limb impulses. [03] calls this the biggest combat-feel gain per line of code. A player flinch is a design call that needs approval. |
| PA12 | Knockdowns and recovery | NEEDS_IMPROVEMENT | Partial | L | PA14, PA15, PA16 | Yes: get-up clips (face up and face down) and more knockdown variants | M2a | A baked fall and get-up: in `stun_heavy_backwards.anim` the body tips to about 113° and is back to 0° at frame 53. NPCs only, flat ground only. `TurnIntoRagdoll` requires a dead character (`MadnessUtils.cs:731`). The heavy stun lacks `PreventWalking`, so movement input may slide a "lying" character (inferred). A live-ragdoll knockdown needs BUG-47 fixed first. |
| PA13 | Weapon pickup and throwing | IMPLEMENTED | Implemented (unverified) | S | — | No | M5 (verify) | A pickup clip plus a procedural hand reach (`CharacterPickupSystem.cs:10-60`). Throw clips release the weapon at 50% (`CharacterUtilities.cs:638-724`). The release is a delayed routine, not a clip event. |
| PA14 | Smooth animation blending | NEEDS_IMPROVEMENT | Partial | M | — | No | M1 (blend bugs BUG-44, BUG-45, BUG-60) / M2a (layer stack) | A two-clip crossfade of 0.3125 s, and a procedural-to-clip blend of 0.2 s (`CharacterPositionSystem.cs:333-353`). Bugs: BUG-44, BUG-45, BUG-60. Durations are fixed and global, the hand pose snaps at 50%, angles lerp linearly (BUG-52), and there are no masked or additive layers. |
| PA15 | Context-sensitive transitions | NEEDS_IMPROVEMENT | Partial | M | PA14 | Optional: turn and transition clips | M2a | Context picks are ad hoc: wall stuns, flat-ground checks, agility tiers. There is no state machine. Group names are string-checked (`CharacterPositionSystem.cs:55,552,633`). The flip is instant, with no turn. A thin `CharacterAnimationController` is proposed. |
| PA16 | Animation parameters exposed through data files or editor controls | NEEDS_IMPROVEMENT | Partial | M | — | No | M1 | Clip, `.seq`, stats and weapon-recoil data exist. Every procedural constant is in code (table in [03] §2.6). `ConVars` holds none of them. `AnimationTestingSystem` is a viewer only, and the repo has no Blender exporter. Proposal: a `CharacterMotionProfile` file whose defaults equal today's constants. |

## A: Combat and movement

All rows are from [02]. Asset notes marked [06] come from its assets-by-group table.

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| A1 | Dual-wielding pistols and SMGs | MISSING | Not started | L | — | Yes: dual hand poses, throw and pickup animations | M2a if approved | Each character has one `EquippedWeapon` (`CharacterComponent.cs:142`), and posing assumes one gun (`HandPosingFunctions.cs:170-208`). Touches the character, input, posing, AI, UI, throw, melee and weapon systems. It changes the core feel, so it **needs approval**. |
| A2 | Weapon disarming and stealing | NEEDS_IMPROVEMENT | Partial | M | A9 | Yes: steal/grab animation | M2a | Disarms happen through the unarmed finisher (`MeleeUtils.cs:249-262`), the perfect parry (`:151-152`) and thrown hits (`VelocitySystem.cs:223-232`). Telekinesis steals guns (`TelekinesisAbilityComponent.cs:45`). The player has no direct grab-the-gun move. `stun_gun_stolen.anim` already exists. |
| A3 | Environmental takedowns | MISSING | Not started | L | E60, A9 | Yes: paired animations, props | M3b | No interactable hazard hooks (searched `Character/`, `Weapons/`, `MeleeUtils`). |
| A4 | Executions with different animations | MISSING | Not started | M | PC13 | Yes: paired attacker/victim animations per weapon class ([06]: 3-6 per class) | M3b | No execution state. Deaths are chosen only by hit (`CharacterUtilities.cs:184-284`). Stunned enemies are the natural trigger. |
| A5 | Perfect dodge counterattacks | NEEDS_IMPROVEMENT | Partial | S | — | [02] none; [06] a counter-flash VFX and a counter sting SFX | M2a | A perfect deflect sends the bullet back with 0.2x slow motion (`BulletEmitter.cs:148-182`). There is a perfect melee parry (`MeleeUtils.cs:146-161`). There is no perfect jump-dodge window. |
| A6 | Sliding while shooting | MISSING | Not started | M | PC14 (and PA9) | Yes: slide animations | M2b | No slide state. The master jump-dodge already allows shooting mid-air. The `JumpDodgeComponent` dash could be reused. |
| A7 | Wall rebounds and aerial attacks | TECH_DIFFICULT | Not started | XL | — | Yes: many animations | Post-launch | Movement is floor-bound (`CharacterMovementSystem.cs:55,104,164`). This needs real vertical movement and character collision: a core rewrite that changes the game's identity. |
| A8 | Bullet deflection using melee weapons | IMPLEMENTED | Implemented (unverified) | S | — | No | M5 (verify) | `BulletEmitter.cs:126-214`; 23 weapon JSONs have `CanDeflectBullets: true`. Pellets can't be deflected. |
| A9 | Grab, throw and shove mechanics | MISSING | Not started | M | — | Yes: grab and shove animations | M2a | Only weapons can be thrown. The heavy stun (`StunHeavy`) is a ready-made reaction for a shove. |
| A10 | Advanced melee combos | NEEDS_IMPROVEMENT | Partial | M | PC2 | Yes: new swing animations | M3b | Linear `.seq` chains of 1-7 keys. No branching, directional, charged or launcher moves. Sequences are tied to the character's stats, not the weapon. |
| A11 | Optional weapon-jamming difficulty modifier | MISSING | Not started | S | — | Yes: jam and clear SFX, optional animation, jam icon | M3b | `WeaponComponent` has no jam state. Easiest as an Improbability disk plus an `IsJammed` flag. |
| A12 | Directional damage and armour penetration | NEEDS_IMPROVEMENT | Partial | M | B-ARCH | [02] none; [06] more broken-armour variants, ricochet sparks | M2a | Armour only *deflects*: by chance, frontal shots only, single pellets only, and only while the victim has dodge left (`BulletEmitter.cs:124-146`). It never reduces damage. Layer chances combine by `max` (`CharacterLook.cs:174-205`). Add `ArmourPenetration` and damage reduction. |

## B: Weapons and equipment

All rows are from [02], except B24, which merges [02] and [06]. The weapon census is 120 files: 82 firearms and 38 melee weapons.

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| B13 | Revolvers with distinct handling | NEEDS_IMPROVEMENT | Partial | S | B-ARCH (BUG-31) | Optional: cylinder part | M2a | The 4 revolvers handle exactly like pistols. Rotating parts are blocked because `AngleCurve` is ignored (BUG-31). No fan-fire or double-action. |
| B14 | Shotguns with different pellet patterns | NEEDS_IMPROVEMENT | Partial | S | B-ARCH | No | M2a | 10 shotguns with 4-8 pellets and one random-circle spread (`WeaponSystem.cs:270-273`). Add a `SpreadPattern` field. |
| B15 | Heavy machine guns | IMPLEMENTED | Implemented (unverified) | S | — | No | M5 (verify; an optional weight field could come in M2a) | m60, mg_42 and bren_gun (`ThrowableHeavy`, 30-120 rounds). No weight or movement penalty and no spin-up. |
| B16 | Throwing knives | NEEDS_IMPROVEMENT | Partial | S | — | No | M2a | A single `throwing_knife.json` that uses the generic throw. No stack, quick-throw or recovery. |
| B17 | Swords, axes and blunt weapons | IMPLEMENTED | Implemented (unverified) | S | — | No | M5 (verify) | 22 blades, 7 axes and 9 blunt weapons. Axes reuse `BluntSeq` (`MeleeUtils.cs:45`). |
| B18 | Improvised environmental weapons | NEEDS_IMPROVEMENT | Partial | M | E60 | Yes: prop sprites | M3a | streetsign, iron_pipe, hammer and plasma_cutter are ordinary pickups. There are no breakable props that become weapons. |
| B19 | Grenades and flashbangs | MISSING | Not started | M | B-ARCH | Yes: sprites, explosion and flash VFX, SFX | M2a | No explosive weapon type; explosions are level-specific (`Prefabs.cs:366-410`). Needs a fuse on `ThrowableProjectileComponent`, radial damage and stun, and a ragdoll impulse. |
| B20 | Riot shields | MISSING | Not started | M | B-ARCH | Yes: shield sprites, hand pose, block animations | M2b (with C27) | Not in the data. `HolyShieldAbilityComponent.cs:29-61` is a working bullet-blocker template. A one-handed pistol plus shield needs off-hand posing. |
| B21 | Experimental fictional weapons | IMPLEMENTED | Implemented (unverified) | S | — | Per weapon | M3b (beams and charge-up need code) | laser_rifle, railgun, mag_railgun and several fictional swords. All firearms are hitscan. `AdditionalComponents` is the code hook. |
| B22 | Weapon attachments | MISSING | Not started | L | B-ARCH | Yes: attachment sprites with anchor points | M3b | No attachment model. Silenced variants are copied files (`silenced_mp5.json` and others). |
| B23 | Alternate ammunition types | MISSING | Not started | M | B-ARCH, A12 | Yes: tracer and impact variants | M3b | Ammo is a single int (`WeaponComponent.cs:159`). Without reload there is nowhere to swap ammo, so it would be per-spawn variants or a disk. |
| B24 | Weapon skins and visual customisation | MISSING | Not started | M ([06]) / S ([02]) | B-ARCH, F64, F-ARCH | Yes, light: a tint/pattern shader plus 5-10 CC0 pattern textures; optional hand-made recolours | M3b | **Merged [02]+[06]; both say MISSING.** One `BaseTexture` per weapon (`WeaponInstructions.cs:27`), used directly by `Prefabs.CreateWeapon` (`Prefabs.cs:532,574`). [06]'s M is better evidenced because it counts the shader variant, the thumbnails (`WeaponThumbnailCache.cs:95`) and the cosmetic save. Skins must not change hitboxes or stats. |
| B-ARCH | Data-driven weapon architecture | NEEDS_IMPROVEMENT | Partial | M | — | No | M2a (BUG-31 in M2a) | Already data-driven: one JSON schema (37 fields), one generic `WeaponSystem`, no per-weapon classes. Missing hooks: reload, hit and impact SFX (`HitSounds` is commented out, `WeaponData.cs:146`), impact VFX, spread pattern, a fire rate separate from the burst interval, a per-weapon melee sequence, casing velocity, muzzle-flash scale, rotating parts, and variant inheritance. |

## C: Enemy AI

All rows are from [04].

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| C25 | Enemies that flank instead of rushing | MISSING | Not started | M | C-ARCH | No | M2b | The AI only keeps a 1-D distance band toward its target (`AiCharacterSystem.cs:632-660`). There is no squad or side assignment. In side view, flanking means taking the player's other side. |
| C26 | Enemies that retrieve dropped weapons | IMPLEMENTED | Implemented (unverified) | S | — | No | M5 (verify; tuning in M2) | `AiCharacterSystem.cs:117-126,362-369,390-422,578-620,662-718`. It includes the player's dropped guns. AI never reloads (`:142-147`). |
| C27 | Shield-bearing enemies | MISSING | Not started | L | B20, C-ARCH, A12 | Yes: shield sprite, hold and bash animations, SFX | M2b | No shield weapon (searched `*shield*`). Holy Shield is a player ability, and AI can't use abilities (`CharacterAbilityComponent.cs:38-39`). |
| C28 | Snipers with visible aiming indicators | NEEDS_IMPROVEMENT | Partial | M | C-ARCH | Maybe: laser sprite, sniper look | M2b | The accurate shot is already telegraphed with a line, a closing crosshair and a sound (`AccurateShotSystem.cs:70-100`). It has one global cooldown (`AiComponent.cs:99`). There is no sniper archetype. |
| C29 | Fast melee specialists | NEEDS_IMPROVEMENT | Partial | S | C-ARCH (for unique moves) | Maybe: lunge or dash animation | M2b | Rushers exist as data (`zed.stats`, sleepwalker) but run the same AI. |
| C30 | Heavy armoured enemies | NEEDS_IMPROVEMENT | Partial | S | A12 | Yes: heavy look, armour sprites | M2b | Possible in data today (`mag.stats`: head 50, body 91, scale 2.1). No armour-break feedback or heavy movement profile. |
| C31 | Medics supporting enemy squads | MISSING | Not started | M | C-ARCH, C32 | Yes: medic look, heal animation and VFX | M3b | No heal mechanic (`BodyPartComponent.cs:32` only subtracts). Combat is close to one-hit lethal, so healing is low value; restoring the dodge meter fits better. |
| C32 | Commanders coordinating groups | MISSING | Not started | M | C-ARCH | Yes: commander look, bark audio | M3b | The only coordination is the per-target attacker cap (`AiCharacterSystem.cs:35-51`). |
| C33 | Enemies that retreat when injured | MISSING | Not started | S | C-ARCH | No | M2b | `PanicLevel` only decays and gates shots. A back-off path already exists (`:513-514`). This is the easiest high-value behaviour. |
| C34 | Enemies using environmental cover | DEPENDENT | Not started | L | E60, C-ARCH | Yes: crouch, peek and vault animations; cover props | M3b | Blocked on cover objects (E60) and perception (C-ARCH). Characters stand on one floor line (`Level.cs:55-74`). Cover is meaningless while the AI is omniscient. |
| C35 | Elite enemies with unique attack patterns | NEEDS_IMPROVEMENT | Partial | M (each, after C-ARCH) | C-ARCH | Yes: animations, VFX | M3b | Elites are stat tiers only. The one bespoke pattern set is Rich's (`RichBossSystem.cs:281-330`). |
| C36 | Bosses with multiple combat phases | NEEDS_IMPROVEMENT | Partial | L | C-ARCH | Yes: per-boss animations, art and music | M3b | Rich switches between Sword and Gun phases at random after 9 s (`RichBossSystem.cs:281-299`), with a health bar and a death hook. It is hard-coded, with no HP thresholds. Proposal: a generic `BossComponent`. Rich is a Madness character (licensing). |
| C-ARCH | Modular AI architecture; no unfair information; AI and pathfinding debug tools | NEEDS_IMPROVEMENT | Partial | L | — | No | M2b | One 718-line ad hoc `AiCharacterSystem` (`:25-444`). Targeting and aim are omniscient, through walls (`:546-576,310-333`; BUG-79). No FSM, behaviour tree or perception. No navigation, because the floor is 1-D. Debug tools are DEBUG-build flags only. Keep the "wants" seam (`AiComponent.cs:64-70`) and add perception, per-archetype decisions and a debug overlay. |

## D: Game modes

All rows are from [04]. D46 also records [01] and [05].

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| D37 | Classic survival arena | NEEDS_IMPROVEMENT | Partial | M | D-ARCH | Maybe (the `exp_*` levels can be reused) | M3a | The pieces exist: Experiment autospawn (`ExperimentModeSystem.cs:530-664`) and a stub Arena. `ArenaModeSystem` is empty, and its menu entry is commented out (`MainMenuSystem.cs:246-248`). Missing: a fail state, survival timer, score and results screen. |
| D38 | Endless wave survival | MISSING | Not started | M | D-ARCH | No | M3a | A wave data model exists (`ArenaModeWave.cs:10-16`, `waves.json`), but its keys don't match (BUG-66) and nothing consumes it ([01] §2.9). |
| D39 | Campaign missions | IMPLEMENTED | Implemented (unverified) | M (polish) | — | Yes, for new missions | M3a (results screen through D-ARCH; BUG-64 in M1) | 3 campaigns; `LevelProgressSystem.cs:50-138`. Polish: an end-of-campaign results screen (BUG-83), the broken boss-music script (BUG-64), more objective variety. The shipped campaign content is Studio Minus and Madness IP (licensing blocker). |
| D40 | Boss rush | DEPENDENT | Not started | M (after C36) | C36, D-ARCH | Yes, per boss | M3b | Only one boss exists (Rich). A temporary campaign of boss levels is easy once there are 3 or more bosses. |
| D41 | Time attack | DEPENDENT | Not started | S (after G77) | G77, D42 | No | M3a | No run timer, only the cumulative `TotalTimeSpent`. |
| D42 | Score attack | MISSING | Not started | M | D-ARCH | Maybe: UI art, SFX | M3a (the score service comes with D-ARCH in M3a) | No score, combo or rank code. Kill events are already hooked (`Prefabs.cs:911-917`). |
| D43 | One-hit challenge | MISSING | Not started | S | D47 | Yes: disk icon | M3b | One new disk that sets dodge to 0. Fix BUG-23 first. |
| D44 | Randomised weapon challenge | MISSING | Not started | S | D47 | Yes: disk icon | M3b | Weapons are randomised only per Incident level (`IncidentConfig.cs:108`). |
| D45 | Roguelite mode with upgrades between encounters | MISSING | Not started | L | D-ARCH, D47, F66 | Yes: upgrade icons, shop UI | M3b | Build on Incident's run structure. `ArenaModeSaves` only sketches money and XP. |
| D46 | Daily seeded challenges | MISSING | Not started | M | D-ARCH (seeded RNG); G78 optional | No | M3a | Incident has a seed, but runs aren't reproducible: runtime RNG is unseeded, cached levels are mutated (BUG-62), level pools follow registry order, and the name is unstable (BUG-67). **The auditors differ on scope.** [04] says a per-run `System.Random` and no asset mutation give same-seed runs (M). [01] and [05] say identical, verifiable runs need fixed-step gameplay and a seedable engine RNG (`Utilities.cs:14` is unseeded; about 200 call sites). Both are right about different targets: the same seed giving the same levels and spawns is M, while replay-verifiable results need ARCH-DET. |
| D47 | Custom difficulty modifiers | NEEDS_IMPROVEMENT | Partial | M | — | Maybe: icons | M3b (BUG-23 in M1; BUG-61 in M3a) | 17 Improbability disks, but no difficulty presets. The fewer/more-enemies disks are broken (BUG-61), and stat copies lose fields (BUG-23). Add a difficulty scalar applied when stats are created. |
| D48 | Local cooperative multiplayer (feasibility) | TECH_DIFFICULT | Not started | L | — | Yes: a P2 look, HUD | Post-launch | Feasible as same-screen co-op, about 3 weeks of work. `FindPlayer` has 44 call sites in 24 files; the player is a singleton; there is one gamepad (`Gamepad.cs:13-24`) and one HUD. Factions, attack turns and camera targets already generalise. Fix the faction ally bug (BUG-42) first. |
| D-ARCH | Reusable spawning, wave config, scoring, objectives and progression | NEEDS_IMPROVEMENT | Partial | L | — | No | M3a | One spawner serves every mode, which is good. But there are no waves and no scoring, and objectives work only in Campaign (`SceneUtils.cs:340-344`). `GameMode` has no Arena or Incident value. State lives in global statics. [01] adds that each mode's systems are a hard-coded list in `SceneUtils.cs`. Proposal: a `WaveDirector`, an `ObjectiveComponent`, a `ScoreSystem` and a run timer. |

## E: Maps and environments

All rows are from [04]. New backgrounds are art cost, and reusing existing ones is subject to [06]'s licensing findings.

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| E49 | Abandoned industrial complex | NEEDS_IMPROVEMENT | Partial | M per level | — | Yes: painted background (background, foreground and decal mask) | M3a | Industrial interiors exist among the EotM backgrounds (inferred from file names). No "abandoned" theme. Each new level needs a full painted background; there is no tileset. |
| E50 | Underground laboratory | NEEDS_IMPROVEMENT | Partial | M per level | — | Yes: backgrounds | M3a | The hidden Mag campaign has facility and descent levels (inferred from names; `mag_campaign/meta.json` is `"hidden": true`). |
| E51 | Desert military outpost | MISSING | Not started | M | — | Yes: backgrounds, props | M3b | Art cost only. |
| E52 | Rooftop combat zone | IMPLEMENTED | Implemented (unverified) | — | — | No | M5 (verify) | `lvl_roof_1` (93 s survival) and `lvl_roof_2` (Rich boss). |
| E53 | Moving train | IMPLEMENTED | Implemented (unverified) | — | — | No | M5 (verify) | 9 train levels, `TrainMovingBackground`, `TrainShakeSystem`, `PassingTrainSystem` and `MachinistLevelSystem`. |
| E54 | Underground bunker | MISSING | Not started | M | — | Yes: backgrounds | M3b | The nearest are the depth and descend levels (inferred). Art cost only. |
| E55 | Prison facility | MISSING | Not started | M | — | Yes: backgrounds, prisoner looks | M3b | The `civil` faction could serve as prisoners. |
| E56 | Collapsing building | MISSING | Not started | L | E60, G80 | Yes: multi-state backgrounds, debris VFX, audio | M3b | A scripted set piece is feasible. A real physics collapse would be TECH_DIFFICULT with single-image backgrounds. |
| E57 | Nightclub with reactive lighting | NEEDS_IMPROVEMENT | Partial | M | — | Maybe: club combat background | M3a | `exp_club` is a sandbox map with a beat-synced screen tint (`ClubLightsSystem.cs:18-55`). There is no light system, and the compositor/HDR is disabled ([01]). |
| E58 | Snow-covered military base | MISSING | Not started | M | — | Yes: backgrounds, optional snow particles | M3b | Art cost only. |
| E59 | Procedurally assembled arena layouts | TECH_DIFFICULT | Not started | XL | D-ARCH | Yes: a modular tile kit (a new art pipeline) | Post-launch | The visual pipeline is one painting per room. A cheaper alternative is procedural *sequences* of handmade levels (Incident already does this) plus random spawns and props. |
| E60 | Interactive doors, traps and destructible cover | NEEDS_IMPROVEMENT | Partial | M–L | — | Yes: trap and cover sprites, break states, SFX | M3a | Doors are automatic, not player-operated or collidable (`DoorSystem.cs:31-95`). The only destructibles are `Turret` and the train engine. A prerequisite for A3, B18, C34 and E56. |

## F: Customisation and progression

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| F61 | Character appearance editor | IMPLEMENTED | Implemented (unverified) | S–M (polish) | F62 | No | M3b | [03] `CharacterCreationSystem.cs` (tabs at `:89-180`, import/export at `:210-247`). Missing: feet, tint or skin colour, flesh and gore, randomise, unlock gating. The preview's blood colour differs from the game's (BUG-58). |
| F62 | Clothing and accessories | IMPLEMENTED | Implemented (unverified) | S | — | Yes, for a commercial build: original pieces. [06] says most of the 250 pieces depict Madness Combat characters | M3b | [03] 172 head, 78 body and 10 glove `.armor` files; 3 head and 2 body layers; procedural damage. Bugs: BUG-51 (body layer order) and BUG-95 (hand armour can't be saved). |
| F63 | Armour sets | NEEDS_IMPROVEMENT | Partial | M | F61, F62, F-ARCH | Possibly set icons | M3b | [03] There is no "set" object. The 60 `.look` presets act as de facto sets. |
| F64 | Cosmetic unlocks | MISSING | Not started | M | F-ARCH, F68 (and F65, F61, F62, B24 per [06]) | Yes: lock overlay, "new" badge, unlock SFX, 30-60 original armour pairs, 10-20 weapon skins | M3b | **Merged [05]+[06]; both say MISSING.** Only a static `hidden` flag exists (`ArmourPiece.cs:57`; 38 pieces set it). The reusable pattern is the disk unlock set in `disks.txt` (`ImprobabilityDisks.cs:89-105`). Don't use the Madness Combat character armour as rewards in a paid build. |
| F65 | Weapon mastery challenges | MISSING | Not started | M | F69, F68 | Yes: badge icons | M3b | [05] `LevelStats` tracks only kills, deaths, attempts and time (`LevelStats.cs:11-15`). |
| F66 | Combat skill progression | MISSING | Not started | L | F-ARCH, D45 | Maybe UI icons | M3b | [05] Arena's XP and level fields are unused stubs (`ArenaModeSaves.cs:39-49`). Design risk: stat growth undercuts the skill-based feel, so prefer unlocking options. |
| F67 | Alternate starting loadouts | MISSING | Not started | S | D-ARCH | No | M3b | [05] No pre-level picker. The class disks already act as stat presets. |
| F68 | Achievement system | MISSING | Not started | M | F69, F-ARCH | Yes: 30-60 icons | M3b | [05] Nothing found. itch.io has no achievements API, so they would be in-game only. Cheats work in release builds (BUG-98), so cheated runs need a flag. |
| F69 | Player statistics and combat history | NEEDS_IMPROVEMENT | Partial | M | F-ARCH | No | M3b | [05] Per-campaign, per-level kills, deaths, attempts and time (`CampaignStats.cs:42-93`). No lifetime stats; no per-weapon, headshot or accuracy counts; no run history. |
| F70 | Unlockable challenge modifiers | NEEDS_IMPROVEMENT | Partial | S | — | No | M3b (BUG-71 in M1) | [05] 17 disks with incompatibility rules, saved to `disks.txt`, with a selection menu. Gaps: pads can't pick disks up (BUG-71), and names are hardcoded English. Some names reference outside IP (Willis, Gump, FLCL, Ghost Protocol). |
| F-ARCH | Reliable, versioned save/load; cosmetics separate from gameplay equipment | NEEDS_IMPROVEMENT | Partial | M | — | No | M1 (versioned, atomic saves) / M3b (profile container, cosmetics/gameplay split) | **Merged [01]+[05]; both say NEEDS_IMPROVEMENT.** Problems (BUG-02 to BUG-10): no version fields, non-atomic writes, look and arena saved only at clean exit, nothing saved on a crash, `arena.json` wiped at every exit, a corrupt campaign file crashes the campaign. Cosmetics are not separate in the data model: armour `DeflectChance` in a look feeds bullet deflection (`CharacterLook.cs:174-205`). In practice the player's look is flagged `Cosmetic` ([01]), and [02] says cosmetic looks skip deflection, so the effect on the player is probably nil (synthesis inference; not traced end to end). There is no profile container for unlocks and achievements. |

## G: Community and longevity

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| G71 | Custom map editor | NEEDS_IMPROVEMENT | Partial | L (player-ready, [01]) / M (content gaps, [04]) | F-ARCH, G80, G79 | Small: editor icons and thumbnails | M4 | **Conflict: [01] says NEEDS_IMPROVEMENT (infrastructure), [04] says IMPLEMENTED (content side). NEEDS_IMPROVEMENT kept.** A capable editor exists, with 20 placeable types per [04] (22 classes including 3 obsolete per [01]), property panels and test play. But it opens only from the F12 console (`MadnessCommands.cs:332-336`). It saves loose JSON that the game can't list unless it is packed into a `.waa` mod. Redo is broken (BUG-94), and sharing is unsafe (BUG-87). So it is not yet usable as a player feature. |
| G72 | Custom enemy wave editor | DEPENDENT | Not started | M (after its dependencies) | D-ARCH, D38, G71 | No | M4 | **Conflict: [01] says DEPENDENT, [04] says NEEDS_IMPROVEMENT. DEPENDENT kept.** There is no wave editor and no wave runtime: `ArenaModeSystem` is empty, and the waves load into an unused local (`ArenaScene.cs:42`). What exists is the level editor's flat random spawn list (`LevelEditorGuiSystem.cs:264-307`), which [04] counted as a partial base. Both auditors agree the wave format (D-ARCH) comes first. |
| G73 | User-created challenge sharing | MISSING | Not started | M | G71, G79, D46 | No | M4 (the seed-code part ships in M3a with D46; level and challenge sharing stay in M4) | [05] The Incident seed is random and can't be typed in (`IncidentModeMenuSystem.cs:30,189`), and the name hash is unstable (BUG-67). Level sharing needs BUG-87 fixed first. |
| G74 | Replay recording | TECH_DIFFICULT | Not started | XL (input replay) / L (state snapshots) | ARCH-DET | No | M4 | [05] The engine RNG is unseeded (`Walgelijk/Shared/Utilities.cs:14`) and has about 200 call sites. Gameplay runs on variable dt (96 `Update` systems vs 8 `FixedUpdate`). Inputs aren't recorded. [01] agrees. State-snapshot recording is the cheaper route. |
| G75 | Cinematic replay camera | DEPENDENT | Not started | M | G74, G76 | No | M4 | [05] The camera target stack is reusable (`CameraMovementComponent.cs:100-130`). |
| G76 | Photo mode | MISSING | Not started | S | — | No ([06]: filter shaders) | M5 | [05] Experiment edit mode already freezes the simulation and frees the camera (`ExperimentModeSystem.cs:842-866`). There is no hide-HUD and no screenshot save. No post-processing exists, because the compositor is off ([01]). |
| G77 | Speedrun timer | MISSING | Not started | S | F69 ([05]); none ([04]) | No | M3a | **Merged [04]+[05]; both say MISSING.** Only a cumulative per-level time, which includes failed attempts and cutscenes (`CampaignStatsTimerSystem.cs:10-29`) and shows only in the pause menu. A valid run needs a cheat flag (BUG-98). |
| G78 | Leaderboards with anti-cheat validation | TECH_DIFFICULT | Not started | XL | G74, D42, G77 | No | Post-launch | [05] There is no backend, and itch.io has no leaderboard service. Cheats and mods can patch anything, and the simulation isn't deterministic. Options: honour-system daily boards, or skip the feature. |
| G79 | Mod support | NEEDS_IMPROVEMENT | Partial | M | F-ARCH (C-ARCH and D-ARCH for richer hooks) | No (maybe mod-menu icons) | M4 (the failure-visibility fixes could come earlier) | [01] It works today: `.waa` data mods override registry entries, and Harmony script mods load through `IModEntry`. Problems: silent failures (BUG-90 to BUG-93); no enable/disable; dependencies ignored; no docs and no in-game packer. Mods are unsandboxed code, so the mod menu needs a warning. |
| G80 | Custom scenario scripting | NEEDS_IMPROVEMENT | Partial | M (harden) / L (safe player scripting) | G71, F-ARCH, G79 | No | M4 (BUG-15 and BUG-17 in M1; BUG-87 before any sharing) | [01] Roslyn C# stored in level JSON and run with full trust (`BuiltLevelScript.cs:55-124`); 5 shipped levels use it. A script that fails to compile recompiles every frame, and there is no API boundary. [04] One shipped script is already broken (BUG-64). |

## Fix groups and enablers (Proposed)

These rows make the Phase 1 bugs and enabling refactors visible in the plan. Each bug is listed once in `TECHNICAL_AUDIT.md`, which also names its fix group.

| ID | Feature | Audit status | Implementation status | Difficulty | Depends on | Assets needed | Proposed milestone | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| M1-FIX-PERF | CPU frame limiter and process priority | n/a (fix group) | Proposed | S | — | No | M1 | BUG-01. The limiter busy-waits at a 300 fps default, with VSync off and High priority. [01] inferred this from engine source; [05] confirmed it in the shipped 0.30.0 IL. The CPU load (one core at 100%) is inferred and not measured. About 50 lines, with no engine fork ([05]). |
| M1-FIX-SAVE | Save robustness | n/a (fix group) | Proposed | S–M | — | No | M1 | BUG-02 to BUG-10. One `SaveFile` helper: a version field, a temp file plus `File.Replace` with a `.bak`, a migration hook, saves from `finally` and the crash handler, and no more wiping `arena.json`. This is the foundation of F-ARCH and of every F-row. |
| M1-FIX-STAB | Loading, crash and error handling | n/a (fix group) | Proposed | S | — | No | M1 | BUG-11 to BUG-21. Surface faulted loads, return to the menu when a level load fails, write useful crash reports, fix the benchmark command, stop failed scripts recompiling every frame, and clean up temp files. |
| M1-FIX-COMBAT | Combat and AI correctness bugs | n/a (fix group) | Proposed | S | — | No | M1 | BUG-22 to BUG-43. Cheap fixes such as the blunt swing, the stats copy constructor, the sharp-box loop, thrown-weapon gates and the AI frame-skip. Verify BUG-29 with debug drawing before changing it, because a fix shifts balance. |
| M1-FIX-ANIM | Animation, ragdoll and rendering bugs | n/a (fix group) | Proposed | S | — | No | M1 | BUG-44 to BUG-60: blend bugs, hit-frame timing, the Verlet 256-entry buffer cap and the decal cap. Pin current output with golden tests first ([03] §7). |
| M1-FIX-LEVEL | Level, mode and data bugs | n/a (fix group) | Proposed | S | — | No | M1 (BUG-64, BUG-69) / M3a (BUG-61, BUG-62, BUG-63, BUG-65, BUG-66, BUG-67, BUG-68, BUG-70) | BUG-61 to BUG-70: mutation of cached `Level` assets, the inverted Incident check, the broken boss-music script, spawn and data key errors. |
| M1-FIX-INPUT | Input and settings bugs | n/a (fix group) | Proposed | S | — | No | M1 | BUG-71 to BUG-74. The main one: pad-only players can't collect disks. |
| M1-FIX-TEST | Test and CI baseline | n/a (fix group) | Proposed | S–M | — | No | M1 | BUG-75 to BUG-78. Also add a GitHub Actions job (there is no CI), an "all base data loads" test, settings and campaign save round-trip tests, and a "compile every shipped LevelScript" test. |
| M2-DEBT | AI, combat and mode design debt | n/a (fix group) | Proposed | (inside C-ARCH, B-ARCH, D-ARCH) | C-ARCH, B-ARCH, D-ARCH | No | M2a (BUG-81, BUG-82) / M2b (BUG-79, BUG-80) / M3a (BUG-83, BUG-84) | BUG-79 to BUG-84: omniscient targeting, attack slots that never rotate, the melee block matrix, feedback code inside the ray cast, no campaign results screen, dead arena code. |
| M2-FIX-AUDIO | Audio pass | n/a (fix group) | Proposed | M | — | No | M1 (BUG-85) / M2a (BUG-86, crossfades) | BUG-85 and BUG-86: no positional audio, and a suspected OpenAL source leak above 128 one-shots. That leak matters once wave modes bring crowds. Add music crossfades too ([05]). |
| M2-PERF | Simulation cost pass | n/a (enabler) | Proposed | M | M1-FIX-STAB (working benchmark) | No | M2b | PERF-02, PERF-03 and PERF-05 to PERF-11: AI on a fixed 30-60 Hz tick, Verlet sleeping and a capped push-out loop, span-based queries, the text-mesh cache. Build a real benchmark scene first so the gains can be measured. |
| ARCH-DET | Fixed-step gameplay and a seeded RNG | n/a (enabler) | Proposed | L | — | No | Post-launch (M4), approval-gated | [01] proposal #6 and [05] G74. Move character, AI, weapon and melee updates to `FixedUpdate` with interpolation, and route randomness through a per-run seeded RNG. Needed for verifiable D46, and for G74 and G78. |
| M4-FIX-SEC | Content-safety boundary for shared levels and mods | n/a (fix group) | Proposed | M | — | No | M1 (BUG-87 binder) / M4 (the rest, before any level or challenge sharing) | BUG-87 to BUG-89: a serialisation binder allow-list, opt-in for `LevelScript` and `GameSystem` in third-party levels, and sanitised mod IDs. Required before G71, G73 and G80 sharing. |
| M4-FIX-MOD | Mod loader and editor robustness | n/a (fix group) | Proposed | S–M | — | No | M4 (the mod-failure part could move to M1) | BUG-90 to BUG-95: silent mod failures, textures that mods can't override, the mod lifecycle, the level editor's redo and the armour editor's hand-armour save. |
| M5-FIX-REL | Release hygiene and distribution | n/a (fix group) | Proposed | S | — | No | M1 (BUG-96, BUG-97) / M5 (the rest) | BUG-96 to BUG-102: the upstream "BETA BUILD … Discord" banner, the framework-dependent build (players need .NET 9), cheats in release, no Retry or Quit confirmation, obsolete APIs. Plus the release-readiness items in [05] §7: UI scale, flash toggle, first-run tutorial prompt, demo build. |
| M5-LIC | Licensing, IP and asset compliance | n/a (release blocker) | Proposed | XL if a full reskin is needed | — | Yes: replacement music, SFX, fonts and likely characters (see ASSET_REQUIREMENTS.md) | M1 (decision) / M5 (completion) | BUG-103 to BUG-110 plus the IP blockers in [06]: the Madness characters and name, 44 unlicensed music tracks, commercial-library SFX, font licences, FFmpeg GPL compliance, an incomplete `NOTICE.txt`. [06] advises settling the IP question **before** writing more content. |

## Overlaps and conflicts resolved

| ID | Auditors | Their statuses | Tracker status | Reason |
|---|---|---|---|---|
| PC10 | [02], [03] | NEEDS_IMPROVEMENT, NEEDS_IMPROVEMENT | NEEDS_IMPROVEMENT | Agree. |
| PC11 | [02], [03] | MISSING, DEPENDENT | DEPENDENT | More precise: it is missing *and* blocked on a reload mechanic that needs approval. |
| PC12 | [02], [03] | IMPLEMENTED, IMPLEMENTED | IMPLEMENTED | Agree. |
| PC13 | [02], [03] | NEEDS_IMPROVEMENT (M), NEEDS_IMPROVEMENT (L) | NEEDS_IMPROVEMENT (M/L) | The status agrees. The difficulty depends on the approach (clip-based or live ragdoll). |
| B24 | [02], [06] | MISSING (S), MISSING (M) | MISSING (M) | [06]'s estimate includes the shader, thumbnails and the cosmetic save. |
| F64 | [05], [06] | MISSING, MISSING | MISSING | Agree. The dependency lists are combined. |
| F-ARCH | [01], [05] | NEEDS_IMPROVEMENT, NEEDS_IMPROVEMENT | NEEDS_IMPROVEMENT | Agree. [05] adds the corrupt-campaign crash (BUG-02) and the deflect-chance link. |
| G71 | [01], [04] | NEEDS_IMPROVEMENT, IMPLEMENTED | NEEDS_IMPROVEMENT | [04] judged the content side, [01] the player path. Players can't reach the editor or play its levels, and that is better evidenced. |
| G72 | [01], [04] | DEPENDENT, NEEDS_IMPROVEMENT | DEPENDENT | No wave editor or wave runtime exists. Both name D-ARCH as the prerequisite. |
| G77 | [04], [05] | MISSING, MISSING | MISSING | Agree. Only the dependency differs (none vs F69). |

## Decisions Zeph needs to make

The auditors marked these as changes to MIR's core feel or scope:

- Reload: PC11, PA6, B23
- Dual-wielding: A1
- Smoothing the hop movement: PC9, PA1
- Hit stop: [02] proposal #8
- Dodge regenerating while standing still
- Changing the bullet penetration and double-hit damage: BUG-29
- Replacing the aim-ray melee hit test: PC2
- Giving the player flinches and knockdowns: PA11, PC13
- The Madness IP question: M5-LIC
