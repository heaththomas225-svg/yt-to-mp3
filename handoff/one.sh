# one.sh <MAIN> <Name>: build and run one test suite hidden, with the real data backed up; prints failures
cd /c/Users/thoma/yt-to-mp3 && source _env.sh; S="$LOCALAPPDATA/YtToMp3/settings.txt"; data_backup; cp -f "$S" "$S.keep"; rm -f $2.exe
"$CSC" -nologo -main:$1 -resource:logo.png,Purphex.logo.png -out:$2.exe $L $SRC $2.cs 2>&1 | grep -i error | head -5
timeout 900 ./RunHidden.exe $2.exe > /c/Users/thoma/nfout/$2.txt 2>&1; cp -f "$S.keep" "$S"; rm -f "$S.keep"; data_restore
grep -i "FAIL\|===" /c/Users/thoma/nfout/$2.txt | cut -c1-400
