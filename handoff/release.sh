#!/bin/bash
# release.sh <version>: build, test and release Purphex on the Windows PC (Git Bash), from the repo folder with _env.sh in place.
#   bash handoff/release.sh 3.72.0
# It stops at the first problem and never releases a build whose tests did not all pass. No Co-Authored-By lines, ever.
# Steps: BUILD_OK  ->  full run_tests (hidden desktop, real data folder backed up and restored)  ->  write_manifest  ->  commit  ->  push  ->  gh release (full CHANGELOG section as the notes).
set -e
VER="$1"; [ -n "$VER" ] || { echo "usage: bash handoff/release.sh <version>"; exit 1; }
cd "$(dirname "$0")/.."; [ -f _env.sh ] || { echo "copy handoff/env.sh to _env.sh and fix the paths at the top first"; exit 1; }
. ./_env.sh
grep -q "const string Version = \"$VER\"" YtToMp3.cs || { echo "YtToMp3.cs is not at version $VER"; exit 1; }
grep -q "Latest = \"$VER\"" Changelog.cs || { echo "Changelog.cs is not at version $VER"; exit 1; }
grep -q "^## $VER " CHANGELOG.md || { echo "CHANGELOG.md has no section for $VER"; exit 1; }
out=$(build_app); echo "$out"; echo "$out" | grep -q BUILD_OK || { echo "BUILD FAILED"; exit 1; }
run_tests 2>&1 | tee _rt.txt
# every line must be ' 0 failed', 'checks passed', 'audit passed' or 'ok=1' (never grep -v passed: '111 passed, 1 failed' contains passed)
bad=$(grep -v " 0 failed\|checks passed\|audit passed\|^ok=1" _rt.txt || true)
if [ -n "$bad" ] || ! grep -q "^ok=1" _rt.txt; then echo "TESTS DID NOT ALL PASS:"; echo "$bad"; exit 1; fi
write_manifest "$VER" "Purphex Music.exe"
awk -v v="## $VER " 'index($0, v) == 1 {on = 1; next} on && /^## / {exit} on' CHANGELOG.md > _notes.md
[ -s _notes.md ] || { echo "empty release notes"; exit 1; }
git add -A -- . ':!_*' ':!*.exe' ; git status --short | grep -v "^??" | head -5 || true
git commit -m "Purphex $VER" -m "$(head -c 1500 _notes.md)"
git push origin HEAD
gh release create "v$VER" "Purphex Music.exe" YtToMp3.exe version.txt --title "Purphex Music $VER" --notes-file _notes.md
rm -f _notes.md _rt.txt
echo "RELEASED $VER"
