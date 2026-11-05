# Mobile improvements: shared foundations, design system and quality passes

**Status:** open, into `main` · **Date:** 2026-11-05 · **Branch:** `claude/mobile-improvements`

## Why this work happened

- **The ask:** improve samaat-mobile the way the desktop repo was improved today (desktop PRs #1 to #5), following the plan in §6 of the desktop's session record (`docs/sessions/2026-11-05-session-record.md` on `docs/session-2026-11-05`), and open one PR.
- **What the survey found:**
  - The submodule was pinned at `e264134`, from before PR #1: none of the shared tokens, the driver list, the `CommandRunner` fix, the worst-case data, the motion fixes or the pairing fix.
  - `AndroidHost.cs` held a fourth copy of the driver list (Jieli and Fast Pair, so no Bose).
  - There was no architecture, design-system or contrast test, and no decision record.
  - `Mobile.axaml` used literal sizes, and `DeviceItemViewModel` was 696 lines.

## Baseline and result

- **Before:** `dotnet build Samaat.Mobile.slnx` gave 0 warnings and `dotnet test` passed 18 tests.
- **After:** 0 warnings and **163** tests, all passing. The Android projects are not compiled here (see "Not verified here").

## Decisions per step

### 1. Foundations

| Decision | Why | Rejected alternative |
|---|---|---|
| Move the pin to `473a430` (the tip of `feature/ui-redesign`, PRs #1 to #5) | Brings every shared fix; it built unchanged | Wait for the audit-fixes PR (not pushed yet; the plan said not to depend on it) |
| `Samaat.Android` builds its registry with `BuiltInDrivers.CreateRegistry(devices)` | One driver list for every app; the phone gains Bose | Keep a phone list and add Bose to it (the drift the desktop just removed) |
| `Samaat.Mobile` drops its Jieli and Fast Pair references; `Samaat.Android` references `Samaat.Drivers` | Mobile never used them; they were there only for Android to inherit | Mobile references `Samaat.Drivers` (would make the UI project a second composition root) |
| `Samaat.Drivers` and `Samaat.Protocols.Bose` in both solutions | `Samaat.Drivers` now compiles in CI even without the Android SDK | Android solution only |
| `ArchitectureTests`: a per-project allow-list, plus four rules | The desktop's approach; layering becomes a build failure | Review only |
| Vendor ECC's `common` and `csharp` rule packs in `.claude/rules/ecc/`, byte-identical to the desktop's | The same rules in both repos | User-level install (lost every cloud session) |

The four layering rules:

- Mobile references shared projects only.
- The platform layer depends on no phone project.
- Only `Samaat.Android` and the preview are composition roots.
- Every built project is in its solution.

A guard also fails if any project writes its own driver list again.

**Not done: enabling the ECC plugin in `.claude/settings.json`.** Claude Code's auto mode refused to let the session write its own settings file ("self-modification"), twice. The owner can add it with:

```json
{
  "extraKnownMarketplaces": { "ecc": { "source": { "source": "github", "repo": "affaan-m/ECC" } } },
  "enabledPlugins": { "ecc@ecc": true }
}
```

### 2. Design system

| Decision | Why | Rejected alternative |
|---|---|---|
| Shared `Tokens.axaml` and `Metrics.axaml` for everything that matches; `Metrics.Mobile.axaml` only for what a phone needs to differ | One source of truth across both apps; the phone's differences are named and justified | Copy the desktop's values (32 px targets and 13 px body text are too small on a phone) or keep literals |
| Phone tokens: 44 px touch target (WCAG 2.5.5); option, device and nav rows at 52, 72 and 56; radii 10, 12 and 14 for 44 px controls; a type ramp one step up (label 12, meta 13, body 15, panel title 16); figure 26 (narrower rows); code 40 | Touch and arm's-length reading. Each value is pinned by a test that it differs from the shared token for the same role, so a duplicate can't creep in | Add these to the shared `Metrics.axaml` (they're phone-only) |
| `MobileApp` merges `Metrics.axaml` itself | The desktop made `Artwork.axaml` merge it for compatibility; relying on that is accidental coupling | Rely on `Artwork.axaml` |
| Port `DesignSystemTests` and `ContrastTests` | Enforced, as on the desktop | A guideline |

The ported design-system tests fail on:

- hex colours, literal font sizes and literal shadows;
- tokenised radii and literal touch targets;
- the sparkle icon and emoji.

The contrast tests cover the desktop's shared token pairs and the phone's own pairs. The contrast check moved out of `MobileTests`.

**Verified:** the token refactor (`361d862` to `7861c91`) renders all 16 screenshots within the run-to-run noise floor (see "How it was verified"); none changed.

### 3. Unslop pass (polish mode)

**Audit result:** light slop (the unslop skill's sign list, as vendored in the desktop repo).

- **Clean already:** no gradients, glows, emoji or uppercase labels; one icon set (Fluent); tested contrast.
- **Found:**
  - lime-tinted selection fills on noise and EQ options and on the current tab;
  - 700-weight titles;
  - a sparkle icon for Adaptive (set in C#, so the markup test missed it);
  - off-grid spacing (6, 10, 14);
  - full stops on one-sentence captions.

**Direction:** the desktop's "Listening room", unchanged.

- **Character:** exact, candid, quietly technical.
- **World:** ink surfaces, one lime signal.
- **Moments:** the device stage and the figures.
- **Signature:** the battery ring.

| Decision | Why | Rejected alternative |
|---|---|---|
| Selection is a `Surface2` step with an accent hairline, lit tile and check; the current tab is a neutral key with only its glyph lit | The desktop's PR #2 rule: the accent marks one thing | Keep the lime-tinted fills |
| Headings at 600 | Hierarchy from size | 700 |
| Adaptive uses `Icon.Adaptive` | The sparkle reads as "AI magic"; the desktop already uses this icon | Keep it |
| Spacing on the 4 px grid; the 2 px nudges stay | Rhythm. A test enforces it | Leave stray values |
| One-sentence captions lose the full stop | Captions are labels. Toasts and results from shared code (`HandoffCoordinator`) keep theirs, which is a desktop follow-up | Change the shared messages here (not allowed: the submodule is read-only) |

The grid changes in detail:

- rows: 12;
- alerts and toasts: 12×8;
- option rows: 8;
- nav bar: 8×4;
- gaps: 8;
- the device row's text inset: 12.

**Suggested, not done:**

- Device rows use one headphones glyph for every device, while the desktop shows each device's thumbnail.
- The overall level appears three times on the device page.

### 4. Stress test (break-ui)

| Decision | Why | Rejected alternative |
|---|---|---|
| Worst-case and empty data come in through the simulator (`SimulatedMobile.Create(scenario:)`, preview `--simulate=worst` or `--simulate=empty`) | The skill's rule: change the data at the boundary, never the component | Hand-edited views |
| `WorstCaseRenderTests` (44 frames, at 412 and 360 px, both themes) | The frames show regressions; the checks make them failures | Screenshots only |

`WorstCaseRenderTests` fails a frame on:

- any text cut without an ellipsis;
- anything past the right edge.

It covers:

- the worst-case and empty data sets;
- a computer with a long name and many earbuds;
- a long toast.

**What broke, and the fix.** Report first, then fix; each fix has a test in `WorstCaseFixesTests`.

| What broke | Fix |
|---|---|
| A long device name ran under Refresh and off the screen in the page title | The title gets the remaining width and wraps. Not `MaxLines`: Avalonia drops the overflow without an ellipsis, so a cut name looks complete |
| Computer names were cut without an ellipsis in the nearby row, the pairing title, the paired panel, "Earbuds on …" and the device page | Identity text (which computer you pair with) wraps; rows of the computer's earbuds trim with an ellipsis. No tooltips: a phone has no hover |
| The battery caption claimed every level was reported for "Jo" (no right-bud level, no case), and said "aren't reported" for a disconnected device | The desktop's caption rule: name every piece without a level; say to connect when nothing is known |
| The empty list stretched to the bottom and offered no way out | It hugs its text and offers "Bluetooth settings" |
| A long toast covered the end of the page | Room as tall as the toast is added below the content while it shows (the desktop's fix) |
| `SimulatedPhoneAudio` threw on devices without a Bluetooth address | They're left out, as Android would list them |
| (Found by step 5's fix) a long EQ preset name was cut at 360 px | Option labels wrap |

**Held up:**

- 0 % and 100 % with tabular figures;
- right-to-left Urdu;
- emoji;
- unreported readings as "–";
- "critically low" in words;
- the "Pair with a computer" path for devices without an address.

### 5. Motion

**Inventory:**

- the shared `BatteryRing` value glide (260 ms, strong ease-out 0.23, 1, 0.32, 1);
- four indeterminate busy bars, only during a real wait;
- Fluent's instant press scale.

There are no page or tab transitions, entrance animations, size or position animation, or decorative loops. Nothing on a high-frequency action needed removing.

| Decision | Why | Rejected alternative |
|---|---|---|
| Test that the shared ring fix holds where the phone uses it (one `DeviceView`, its data context the open device) | Switching from 81 % to 55 % must jump; the same device still glides | Trust the desktop's test (different host control) |
| Reduce motion through `IMobilePlatform.PrefersReducedMotion` | Android reads "Remove animations" (animator duration scale 0) and re-reads it on resume. A default interface member keeps other platforms compiling | A setting inside Samaat (the phone already has one) |
| A `reduce-motion` class on the root view, not the window | Android has no window | |
| Busy bars take `IsIndeterminate` from the style | A local value beats every style, so reduced motion couldn't stop the sweep. A markup test enforces it | |

Under reduce motion, the desktop's rules apply: the ring jumps, busy bars hold still at half opacity, and presses don't shrink.

**Found along the way:** `UserControl.shell` never matched `MainView`, because Avalonia type selectors match the exact style key.

- The phone's body text was Fluent's 14 px instead of the intended 15.
- The shell background relied on the window style, which Android doesn't have.
- **Fix:** `:is(UserControl).shell`, with a test. This changes text size across the app, which is intended. The docs screenshots were regenerated.

The owner can run `/review-animations` for a full review; this session can't invoke that skill.

### 6. Backend cleanup

| Decision | Why | Rejected alternative |
|---|---|---|
| Extract the two decision functions, `BatterySummary` and `ComputerMoveState`, as plain records with `From(...)` | Testable without a UI; 21 test cases, written against the old behaviour before the move | Child view models with new bindings (a wider change across every view) |
| Split the rest into partial files by responsibility, keeping bindings unchanged | Core (175 lines), Battery, Artwork, Sound, Computer; figure and option types in their own files. Views and tests needed no change | One file (it mixed five concerns) |
| `MobileLog` (recent entries, `Trace`, logcat on Android) with `Observe()` for fire-and-forget tasks | A failed start used to leave an empty app and no trace | A logging framework (the desktop is designing shared logging; the phone should adopt that) |
| A failed start is also shown | The user needs to know | Log only |
| Platform-layer catches left as they are | They convert exceptions into results with stated reasons, and the platform can't reference `Samaat.Mobile` | |

`MobileLog` now records:

- the app's start and the link service's start;
- the pairing decision and its cleanup;
- broad catches: artwork photos, asset extraction, the foreground service.

There were no `async void` methods. `ErrorHandlingTests` scans for fire-and-forget tasks and silent broad catches.

**Deterministic renders:** `FrozenClock`, used only in the tests.

- The wall time stands still and the simulator's drain tick never fires; other timers run for real.
- Re-runs now differ only in the random pairing code, the busy-bar sweep and anti-aliasing noise.
- This was the desktop's open item for its own screenshots.

### 7. Docs

- README: drivers, layering, design-system rules, stress flags, the submodule branch, and links to `feature/ui-redesign`.
- `docs/TRY-IT.md`: the desktop branch is now `feature/ui-redesign`; the stress flags.
- This record.

## How it was verified

- `dotnet build Samaat.Mobile.slnx`: 0 warnings, 0 errors. `dotnet test`: 163 passed (18 before). Run before every commit.
- **Test-first:** each new test failed before its change: as an assertion, or as a compile error where the API was new. The exceptions:
  - the shared ring test and the token-resolution tests, which check behaviour that already existed;
  - the `DeviceItemPartsTests`, which characterise behaviour before a move.
- **Pixel parity for refactors.** Each commit was rendered in a git worktree with the same `FrozenClock` harness, and the frames compared:
  - **The noise floor:** two runs of identical code differ by at most 64 per channel on at most 10 pixels, plus frames with the random code or a busy-bar sweep.
  - **Step 2** (`361d862` to `7861c91`): 16 of 16 within the floor.
  - **Step 6** (`45d1e2b` to the split): 16 of 16 docs frames and 44 of 44 stress frames within the floor.
  - **Step 1** (pin move, `6c29407` to `361d862`): 15 within the floor, and one with the ring's arc end moved by a pixel. That's expected: the old ring glided between devices, the new one jumps.
- **A false start, reported:** the first parity checks compared RGBA images with Pillow's `getbbox()`, which looks only at alpha, so they always said "identical". The checks above were redone in RGB. The early renders were also non-deterministic (real-clock drain, random code); `FrozenClock` came out of this.

## Not verified here (needs the Android SDK and a real phone)

- `Samaat.Platform.Android` and `Samaat.Android` were **not compiled**. `dotnet workload install android` succeeded, but the apt-packaged SDK doesn't pick up the user-local workload, and there is no Android SDK (`android.jar`) in the container.
- The changed Android code is small and uses existing types, but is unverified until the Android CI job runs:
  - `AndroidHost`: `BuiltInDrivers.CreateRegistry`, logcat forwarding, `PrefersReducedMotion`, two logged catches;
  - `MainActivity.OnResume`: `RefreshMotion()`;
  - `LinkService`: a logged catch, and `Observe` on start.
- On a phone: Bose earbuds through the phone's registry; "Remove animations" on and off while Samaat is in the background; logcat output under the tag "Samaat"; the 15 px body text and the wrapped titles on a real 360 dp screen.

## Follow-ups for the desktop repo (shared code; the submodule was not edited)

- `src/Samaat.Handoff/Moving/HandoffCoordinator.cs:102` and `:135`: "… moved to …." ends with a full stop, unlike the captions; both apps show it.
- `src/Samaat.App/Features/Overview/DeviceViewModel.cs:505-519` and the phone's `BatterySummary`: the same caption rule written twice. Move it beside `NoiseControlPresentation` in shared code.
- `src/Samaat.Simulator/WorstCaseDevices.cs:55`, `:63` and `:70`: three worst-case devices have no Bluetooth address, so the phone can't exercise connect or move with them. Give them addresses (and one device a long address-less case, if that path matters).
- `src/Samaat.UI/Styles/Artwork.axaml:75-83`: the ring's glide has no reduce-motion rule, so each app overrides it (desktop `Gauges.axaml`, phone `Mobile.axaml`). A shared `.reduce-motion` rule would serve both.
- `src/Samaat.UI/Styles/Icons.axaml:25`: `Icon.Sparkle` is now unused by both apps and can go.
- The desktop's `RendersScreenshots` real-clock churn (its audit issue 10): the phone's `FrozenClock` approach (`tests/Samaat.Mobile.Tests/FrozenClock.cs`) would work there too.
- Shared logging (audit issue 5): when it lands, replace `MobileLog` with it.
- The open "Overall" label decision applies to the phone too: the list row shows the lowest bud.

## Rules and procedures applied

- The desktop's `CONTRIBUTING.md`: test-first, warnings as errors, never hide an unsupported feature.
- The ECC common and C# rules (DRY: one driver list, one token per value; files under 800 lines; explicit error handling).
- The unslop skill (Damir Sokolovsky, CC BY 4.0), followed from the desktop's vendored copy.
- break-ui and review-animations standards (Emil Kowalski), as applied on the desktop.
- One commit per step, a full build and test run before each, and this record.

## Left open

- Enable the ECC plugin (snippet above).
- Run the Android CI job and a real phone (above).
- If the desktop's audit-fixes PR (phone-handoff flag, logging) merges, move the pin and adopt its logging.
- `/review-animations` by the owner.
- Device thumbnails in the list; one less repetition of the overall level.
- The battery caption isn't recomputed when the connection changes, only when the battery changes. Kept as it was (no behaviour change in step 6); worth a small fix.
