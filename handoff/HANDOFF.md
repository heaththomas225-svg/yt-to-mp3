# Handoff (updated 2026-10-20)

Branch `handoff-3.72-overhaul` (everything) and `release-3.72.0` (3.72.0 only, plus `handoff/release.sh`). `main` is still 3.71.0.
Nothing here is released: the cloud session that did this work cannot build or test on Windows. Do the two releases on the Windows PC.

## On the Windows PC
1. `cp handoff/env.sh _env.sh`, fix the paths at the top (CSC, data folder). Build `RunHidden.exe` from RunHidden.cs. Notes and rules: private repo `purphex-bot`, branch `handoff-notes`, `handoff/memory/MEMORY.md`.
2. **3.72.0** (piano roll, sampler, GENERATE PATTERN, lag fixes): `git checkout release-3.72.0 && bash handoff/release.sh 3.72.0`. It builds, runs the whole `run_tests` (hidden desktop, data folder backed up), refuses to release unless every suite passes, writes the manifest, commits, pushes and runs `gh release create` with the full CHANGELOG section. Then do the website changelog (purphex-web) as usual.
3. **3.73.0** (sound library overhaul): `git checkout handoff-3.72-overhaul`, merge or rebase onto what 3.72.0 released, then `bash handoff/release.sh 3.73.0`. Version, Changelog.cs, CHANGELOG.md and Help are already written for 3.73.0. Add `OverhaulTest.cs` to the suites (done in env.sh: "OH OverhaulTest"). `Sounds.Grooves.cs` is in env.sh SRC.
4. After 3.73.0 is out: merge `purphex-bot` branch `handoff-notes` into master (the bot text mentions Purphex Wobble and the 20 bassline packs, which exist only from 3.73.0). `npm test` passes.
5. Re-bake the real library on the PC (the app does it on install; the old library is version 7 and offers an update to 8). Expected: 222 packs, about 45,600 checked sounds (numbers from the Linux/mono bake of the final code).

## What 3.73.0 contains (all done, tested under mono; NOT run on Windows)
- Sounds.Grooves.cs: hand-written grooves per genre family, per-genre percussion palettes, honest feel labels. UKG swing 0.60-0.66, UK bassline 0.58-0.63.
- Catalog: UK Bassline = 20 packs (the 8 ids from the old notes kept; the other 12 names are mine: the user's own list of 20 names was not in the handoff, so rename if theirs differ).
- InstantLib: BassDesign wired in (loops, one-shots, MIDI); `Moving()` pushes a weak LFO'd bass until BassDesign.Check passes; names/tags carry archetype + LFO division; atmospheres ("oa"), textures ("ox"), songstarter kits ("ks"); 24 candidates for each drum one-shot kind.
- LibraryBaker version 8: spares (a left-out sound is replaced), cross-pack near-copy check through `fp.bin` (FpIndex; also exact hashes across packs), finer real-time fingerprint, Wobble presets (.purphexwobble) for engine basses, `Studio.Sounds.cs InstallWobblePreset`.
- Purphex Wobble plugin registered (Studio.Dsp.cs, Studio.Mix.cs), hidden settings now saved with the plugin.
- Per-pack kits: wide jitter in `StyleFor`, hat/cymbal metal sets, more chord progressions and voicings.
- `LibraryCheck.EmptyOptions` (no empty filter option) and `handoff/tools/soundaudit.cs` (modes: grooves, bass, drums, empty, catalog, audit, bake). Build with `-main:SA`.
- Review fixes: piano roll settings reset per project, whole-word track jobs, ghost-note cache, sampler zone lock, background refresh on colour-mix change, swung notes never overlap.

## Known, not fixed
- Sampler edits are not undone by Ctrl+Z (the snapshot holds the same SamplerFx object); GENERATE's "set the tempo" is not undone; LOAD IT INTO A SAMPLER uses the whole source file, not the trimmed clip.
- Wobble engine: the vowel (formant) branch is also low-passed by the LFO filter (kept: it is part of the baked sound).
- In a 4-shard bake a few sounds slip past the cross-pack check (16 exact repeats, 165 near-copies of 45,634); the app installs one pack at a time and catches them.
