# Insanity Interactive Reloaded: Development Log

A dated record of what was done, what was verified and what wasn't. **The newest entry is at the bottom.**

Entries before 2026-11-17 were reconstructed from:
- the git history (`full-history` and `audit/phase-1`);
- the plans in `docs/superpowers/`;
- the audit ledger, `.superpowers/audit/progress.md`;
- Zeph's own notes.

Where a fact isn't recorded anywhere, the entry says so rather than guessing.

IDs (`BUG-nn`, `PERF-nn`, `BL-n`, feature IDs) are the ones used in `docs/TECHNICAL_AUDIT.md`, `docs/FEATURE_TRACKER.md` and `docs/ASSET_REQUIREMENTS.md`.

---

## 2024-08-20 → 2025-01-17: inherited code (upstream)

**Done (upstream, not by Zeph)**
- Madness Interactive Reloaded by Studio Minus: 555 commits.
- The last upstream commit is "update harmony for net9" (2025-01-17).

**Where it lives**
- The upstream history is kept locally on `full-history`.
- The public repo carries only the squashed commit described on 2026-11-16.

---

## 2026-11-15: command-line build and XInput controller support

**Done**
- **Build:** `dotnet build src/MIR.sln` now works from the command line, including the Harmony submodule (`src/Directory.Build.targets`).
- **XInput controller support, the full layout, in 5 tasks.** The plan is `docs/superpowers/plans/2026-11-15-xinput-controller.md`.
  1. Gamepad state, deadzones, button edges and a rumble timer.
  2. An XInput source with throttled slot scanning.
  3. Gamepad bindings in `ControlScheme` and Settings, with display names.
  4. `ControllerLoopEvent`: device switching, aim, the menu cursor and click injection.
  5. Hooks into the HUD, tutorial, settings, zoom and rumble.
- **Final-review fix wave:**
  - menus that show a player count as menu mode, not gameplay;
  - release edges: held abilities are released when the pad is unplugged or loses focus;
  - sticky aim: pad aim restarts from the character's current aim;
  - mouse takeover is measured from an anchor;
  - B goes back.

  The same wave also fixed:
  - stick and trigger jitter flipping the active device;
  - rumble running on after the game loop ends;
  - non-ASCII direction names;
  - a fresh spawn aiming at the world origin;
  - a light trigger pull not selecting the pad.
- **Residual fix:** cursor truncation. The menu cursor now keeps its sub-pixel progress on diagonals, with a test for the slow-stick remainder.
- **Death screen:** retry with A.
- Merged and pushed.

**Verified**
- Zeph played on a real pad. **Gameplay verified.**
- Unit tests cover the controller layer: about 139 by the Phase 1 count, all passing on 2026-11-17.

**Not verified**
- The menu fixes landed after Zeph's pad session, so that session doesn't cover them.
- The number of tests run that day isn't recorded here.
- Not tested:
  - pads other than XInput (PlayStation or Switch pads through Steam Input);
  - more than one pad;
  - Linux.
- Phase 1 later found gaps in this area:
  - pad-only players can't collect Improbability disks (BUG-71);
  - deadzones are hardcoded;
  - there are no button glyphs.

---

## 2026-11-16: rebrand, pad menu navigation, pad rebinding, presentation

**Done**
- **Rebrand** to Insanity Interactive Reloaded, with a new logo.
- **Pad menu highlight navigation,** with a fix for modal dropdowns and dialogs. It covers modality, the first press, A on sliders and the capture lease; focus also jumps into a confirmation dialog as it opens.
- **Pad rebinding in Settings.**
- **Final-review fixes:**
  - Back cancels a rebind (capture is armed per button);
  - B on an open dropdown only closes it;
  - LT and RT scroll the credits, a view the cursor isn't over;
  - the Insanity splash screen;
  - settings, credits and licence-text polish.
- **Presentation:** a new icon, a hand-cut logo, and credits for Zeph's work.
- **IIR rename, with save migration:**
  - on first launch, `%LOCALAPPDATA%\MIR` is copied to `%LOCALAPPDATA%\IIR`;
  - an old-type redirect (`LegacyAssemblyRedirect`) keeps old `MIR` type names loading.
- **Splash:** a 3-second skippable splash. Its caption was added, then removed.
- **Published** as one squashed commit, `aa971d7` "Insanity Interactive Reloaded":
  - authored with Zeph's GitHub noreply address;
  - the 39 commits of 2026-11-15 and 2026-11-16, and the upstream history, stay on local branches (`full-history`, `feature/menus-rebrand`);
  - `docs/superpowers` was left out of the pushed tree.

**Verified**
- The MIR→IIR copy ran on first launch. Seen in the log.
- The published commit is linked to the ZephKrnl GitHub account.
- Test baseline as recorded in that day's presentation plan (not re-run for this log): 183 passed, 1 known flake.

**Not verified / not recorded**
- The plan included a pad-only play-check of the menus, settings and rebinding. No result is recorded here. Treat these as unit-tested only until Zeph plays them.
- Phase 1 later found edge cases in the migration:
  - a half-finished copy is never retried (BUG-08);
  - an older migration is dead code (BUG-09);
  - one version number serves both the saves and the "based on MIR" credit (BUG-10).

---

## 2026-11-16 → 2026-11-17: Phase 1 audit (branch `audit/phase-1`, from `aa971d7`)

**Done**
- **Six read-only auditors ran in parallel:**
  - [01] architecture and build;
  - [02] combat and weapons;
  - [03] animation, rendering and physics;
  - [04] AI, modes and levels;
  - [05] systems, performance and QA;
  - [06] assets and licensing.

  Only [01] built the solution and ran the tests.
- **Interrupted three times by usage limits,** and resumed each time:
  1. All six auditors stopped before writing any findings.
  2. All six stopped again part-way through.
  3. Both synthesis agents stopped after reading their inputs.

  After the first stop, findings were saved to disk after each section, so later stops lost nothing.
- **Synthesis produced three documents:**
  - `docs/TECHNICAL_AUDIT.md`: 110 bugs (8 High, 38 Med, 62 Low, 2 debt) and 17 PERF rows;
  - `docs/FEATURE_TRACKER.md`: 118 feature IDs, plus 16 Proposed fix-group rows;
  - `docs/ASSET_REQUIREMENTS.md`: release blockers BL-1 … BL-11 and asset rows R01 … R97.

**Verified (measured by [01])**
- **Debug build:** 0 errors and 110 warnings, in 13 s (warm).
- **Tests:** 190 of 191 pass. The failure, `HandArmourDeserialiserTest.PositiveCaseTest`, was root-caused to test order (BUG-75). The fix is one line.

**Not verified**
- Nobody launched or played the game. No feature is "Implemented (verified)".
- Nothing was profiled. The CPU load of BUG-01 is inferred from code, not measured.
- The licence findings are a technical reading, not legal advice, and several are marked unverified.
- A verification spot-check (`.superpowers/audit/VERIFICATION.md`) was dispatched alongside the roadmap. It wasn't on disk when this entry was written.

**Not done (by design)**
- Nothing was deleted or changed in the game, its assets or its history.

**Repository state**
- Nothing is committed yet: `docs/` is untracked on `audit/phase-1`.

---

## 2026-11-17: development roadmap and this log drafted

**Done**
- Wrote `docs/DEVELOPMENT_ROADMAP.md`. It contains:
  - where the project stands;
  - the commercial release reality, and a parallel Licensing & Assets track;
  - the top 10 improvements;
  - dependencies;
  - milestones M1, M2a, M2b, M3a, M3b, M4 and M5;
  - the launch scope;
  - M1 in 12 work packages;
  - the decisions for Zeph;
  - a month-by-month horizon;
  - a "Changes vs tracker" list.
- Wrote this log.

**Verified**
- IDs were cross-checked against the tracker and the audit.
- Every existing source file named in M1's "Proposed file changes" tables was checked to exist at `aa971d7`.

**Not verified**
- The estimates are estimates. Nothing was built, tested or launched for this entry.

**Not done**
- Nothing committed.
- The tracker hasn't been updated to match the roadmap's "Changes vs tracker" yet.

**Next**
- Zeph reviews and approves (or changes) M1.
- Zeph sends letters LA-1a and LA-1b.

---

## How to keep this log

- **One entry per merged branch.** Also add one for each non-code event that matters: a letter sent, a reply received, a decision made, a commission signed, a release.
- **Use this shape:**

  ```
  ## YYYY-MM-DD: <title> (branch `<name>`)

  **Done**: what changed, with IDs (BUG-nn, PC3, WP-07, …).
  **Verified**: how, with evidence:
    - tests: `dotnet test src/MIR.sln`: N passed, M failed, K skipped (plus any new test names);
    - manual: which checklist items were played, on which input (mouse/pad) and build (Debug/Release);
    - measurements: numbers with units and conditions (for example "CPU 23% of one core, main menu, cap 144").
  **Not verified**: what wasn't checked and why.
  **Tracker/roadmap**: status changes made, with the evidence for each.
  ```

- **Never upgrade a status without evidence.** "Implemented (verified)" needs a play-test or a passing test of the feature itself. Code review alone isn't enough.
- **Record decisions** with the date, the options and the choice, and link the roadmap section.
- **Don't rewrite old entries.** Add a correction as a new entry that names the one it corrects.
- **Keep entries short.** Link the plan, PR or report for detail.
