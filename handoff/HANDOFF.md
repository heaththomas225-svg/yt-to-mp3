# Handoff: continue on the other PC (2026-10-20)

Branch `handoff-3.72-overhaul`. Nothing here is released yet. `main` is still 3.71.0.

## Set up on the new PC
1. `git fetch && git checkout handoff-3.72-overhaul`
2. `cp handoff/env.sh _env.sh` and fix the paths at the top (CSC = .NET 4 csc.exe, LOCALAPPDATA data folder). `handoff/one.sh` runs one test suite hidden with the real data backed up: `bash one.sh <MAIN> <Name>` (copy it to a scratch folder and fix its paths).
3. Build RunHidden.exe from RunHidden.cs (tests must always run through it: the PC is used for gaming).
4. The private notes (rules, gotchas, history) are in the private repo `purphex-bot`, branch `handoff-notes`, folder `handoff/memory/`. Read `MEMORY.md` there first. The rules: no Co-Authored-By lines; never ask questions or pause; render and look at every changed screen; back up the data folder around tests.

## Step 1: release 3.72.0 (piano roll, sampler, GENERATE PATTERN, lag fixes, scroll bar above the ruler, centred toolbar)
- Already done: version 3.72.0 in YtToMp3.cs / Changelog.cs / CHANGELOG.md, help topic. Test fixes for the scroll-bar band: DawTest, ClipsTest, NewFeatTest, StudioUiTest, ScreenAudit (all pass alone). Studio.UI.cs `ArrGap` keeps a 46 px gap under the rows when tracks overflow (pinned + ADD TRACK).
- Passed alone after the fixes: DawTest, ClipsTest, NewFeatTest, ScreenAudit, StudioUiTest. Earlier full run: everything else passed except the suites broken by an edit made mid-run (ProfPersistTest, SocialTest, ContrastAudit, GlitchTest, PxTest, CreativeTest, InspectorTest, LibraryTest, PianoRollTest were not run). The full `run_tests` must pass before releasing. Check it with: `grep -v " 0 failed\|checks passed\|audit passed\|^ok=1"`.
- Sounds.Wobble.cs and Sounds.BassDesign.cs are in SRC (new work, compiles). They are not registered in the plugin list yet. Keep that out of 3.72.0, or include it and say so in the notes.
- Then: build_app, write_manifest 3.72.0, commit (no `_*` files), push, `gh release create v3.72.0` with the full CHANGELOG section as the body, and the website changelog (gate on the npm test exit code).
- Never edit source files while run_tests runs (SRC is read at the start).

## Step 2: sound library overhaul (the user's big prompt, all genres)
Done:
- **Sounds.Wobble.cs**: the wobble engine.
  - Runs 2x oversampled. Sub layer plus mid layer, with a 24 dB high-pass at 150 Hz on the mid and drive before the filter.
  - 24 dB low-pass swept by a tempo-synced LFO. The rate can change every beat ("8,16,8,24").
  - Vowel formant filter, LFO jumps smoothed, soft limiter.
  - Measured: exact rates (4.6 Hz = 1/8 at 140 BPM), sub ~50% / mid ~45%, filter swing 9–12 dB.
  - Tools: `handoff/tools/wobtest.cs` (renders patches, measures them, writes a spectrogram) and `wobaudit.cs`.
- **Sounds.BassDesign.cs**:
  - 20 archetypes (wobble, filthy, rubber, elastic, growl, talking, fm, square, reese, neuro, metallic, riddim, speedgarage, subwob, organwob, stabwob, warped, classic, laser, experimental).
  - `FlavourFor`: in UK Bassline packs every bass is LFO'd.
  - Phrase banks (hold / riff / stab / speed-garage offbeats, swing), and `Check()` (mid >= 15%, swing >= 6 dB).
  - The `WobbleFx` instrument ("wobble") with factory presets. Register it with `handoff/tools/wob9-register-wobble.js` (only its Studio.Dsp.cs and Studio.Mix.cs parts; the rest is already applied).

To do:
1. Wire BassDesign into InstantLib (Sounds.Instant.cs):
   - `RenderBass`, `ob` one-shots and `mb` MIDI: use `BassDesign.UsesEngine(FlavourFor(rec, fl))`, then `Patch(fl, Rnd(seed))`, `Phrase(...)` and `WobbleEngine.Render`.
   - Names and tags carry the archetype and LFO division (`RateWords`).
   - Bass Lab presets become Wobble presets for engine flavours.
2. **Catalog (Sounds.Catalog.cs)**: the UK Bassline section becomes exactly the user's 20 named packs, each with its own archetype set. Reuse ids where it fits: filthy-wobblers, rubberband-bass, talking-bassline, speed-garage-revival, bassline-drop-toolkit, rave-bassline, uk-bass-underground, elastic-basslines. Remove the other 8 old bassline packs.
3. **Drums (Sounds.Packs.cs, Sounds.Instant.cs)**: the cause of the "strange random noises":
   - `DrumKit.Render`'s percussion lane cycles Tom/Tom/Shaker/Rim/Conga/Cowbell in every genre.
   - `InstantLib.Vary` sprays percussion on 28% of steps, random hats on 50%, and moves kicks at random.
   - The fix:
     - The percussion lane picks a named sound per genre (DnB: shaker/rim, bassline: shaker, afro: conga, phonk: cowbell, and so on).
     - Hand-written groove variations per genre replace the random spray.
     - Each pack gets its own kit character (StyleFor jitter is only ±3% now, so same-genre packs sound alike).
     - UKG swing goes to 0.60–0.66, and bass lines swing with the drums.
4. Cross-pack near-duplicate check in LibraryBaker (same type, bucketed fingerprints). Add a test that every filter option has at least one sound.
5. Bump LibraryBaker.Version, re-bake the library at low priority (`tools/libbake.cs`), and audit every bass with `BassDesign.Check`.
6. Then: DnB subgenres, drill/trap, house/techno, lo-fi/R&B, EDM, the wider genres. QA tests A–G from the prompt. Then a bug haul.

## Step 3: Discord bot
In `purphex-bot`, branch `handoff-notes`, not yet on master:
- `src/features.js`: `/features [area]`, `/whatsnew`, `/tip`.
- Hub buttons, `/help` field, FAQ entries (piano roll, ghost notes, sampler, generate pattern, buses, wobble, updated plugins/sounds/MIDI answers).

To do:
- Set `LIB` in features.js to the true library numbers after the re-bake.
- Add tests, `npm test`, push to master after app 3.73 is out (the text mentions Purphex Wobble and the 20 bassline packs).
