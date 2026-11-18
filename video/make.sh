#!/bin/bash
# Makes the Purphex Music video for the website from start to finish (hidden, low priority, one job at a time):
#   pictures of the website -> every part of the DAW recorded -> joined with transitions and the lo-fi soundtrack.
#   bash video/make.sh [part names...]      (with names: only those parts are recorded again, then everything is joined)
# The result: D:/PurphexTests/video/purphex-showcase-1080.mp4 (+ -4k.mp4 and showcase-poster.jpg). Copy them to purphex-web/public/media to publish.
cd /c/Users/thoma/ytm-kits && source _env.sh
V=D:/PurphexTests/video; mkdir -p $V/parts $V/appdata/sounds
[ -d $V/appdata/sounds/jazzy-lofi ] || cp -r "$LOCALAPPDATA/YtToMp3/sounds/jazzy-lofi" $V/appdata/sounds/
"$CSC" -nologo -out:"$(cygpath -w $V/LofiComposer.exe)" 'video\LofiComposer.cs' || exit 1
"$CSC" -nologo -main:SR -resource:logo.png,Purphex.logo.png -out:"$(cygpath -w $V/ShowRec.exe)" $L $SRC 'video\ShowRec.cs' 2>&1 | grep -i " error" && exit 1
node video/web.js $V/web || exit 1
/c/Users/thoma/yt-to-mp3/RunHidden.exe "$(cygpath -w $V/ShowRec.exe)" "$(cygpath -w $V/parts)" "$@" 2>&1 | tee $V/record.log
node video/assemble.js $V
