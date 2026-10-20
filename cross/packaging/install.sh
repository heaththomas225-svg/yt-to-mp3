#!/bin/sh
# Adds Purphex Music to your app menu (for your user only; nothing needs sudo). Run it from the unpacked Purphex folder.
set -e
here="$(cd "$(dirname "$0")" && pwd)"
dest="$HOME/.local/share/purphex"
mkdir -p "$dest" "$HOME/.local/share/applications" "$HOME/.local/share/icons/hicolor/512x512/apps" "$HOME/.local/bin"
cp -R "$here/." "$dest/"
chmod +x "$dest/Purphex"
ln -sf "$dest/Purphex" "$HOME/.local/bin/purphex"
cp "$here/purphex.png" "$HOME/.local/share/icons/hicolor/512x512/apps/purphex.png"
sed "s#^Exec=.*#Exec=$dest/Purphex#" "$here/purphex.desktop" > "$HOME/.local/share/applications/purphex.desktop"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$HOME/.local/share/applications" || true
echo "Purphex Music is installed. Open it from your app menu, or type: purphex"
