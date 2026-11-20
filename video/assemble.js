// Joins the parts of the Purphex Music video (recorded by video/ShowRec.cs) into one MP4: a transition into every section, then the lo-fi soundtrack
// (video/LofiComposer.cs, an original piece made in code, no copyright) composed to the exact length, faded in and out.
//   node video/assemble.js <video folder>        (the parts are in <folder>/parts, the result is <folder>/purphex-showcase-1080.mp4 and -4k.mp4)
// To add a section later: record it as a part in ShowRec.cs and add its name to PLAN below (in the order it should play).
const { execFileSync } = require('child_process'); const fs = require('fs'); const path = require('path');
const DIR = process.argv[2] || 'D:/PurphexTests/video'; const P = path.join(DIR, 'parts');
const BIN = path.join(process.env.LOCALAPPDATA, 'YtToMp3/tools/ffmpeg/ffmpeg-master-latest-win64-gpl/bin'); const FF = path.join(BIN, 'ffmpeg.exe'), FP = path.join(BIN, 'ffprobe.exe');
const dur = (f) => parseFloat(execFileSync(FP, ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', f]).toString());
const run = (args) => execFileSync(FF, ['-v', 'error', '-y', ...args], { stdio: ['ignore', 'inherit', 'inherit'], maxBuffer: 1 << 26 });
// [part, transition INTO the next part, its length in seconds]
// Clean transitions only: a dip to black, a push, a wipe or a circle reveal. (A cross-dissolve laid two almost identical studio screens and two subtitles
// over each other, which looked like a glitch.) The parts are joined in this order; the transition is the one INTO the next part.
const PARTS = ['intro', 'sc-studio', 'sc-mixer', 'sc-master', 'sc-basslab', 'download', 't-projects', 't-import', 't-toolbar', 't-mixer', 't-vocals', 't-vsts', 't-assign', 't-pads', 't-beatlab', 't-tag', 't-basslab', 't-pianoroll', 't-flex', 't-sounds', 't-hub', 't-master', 't-lyrics', 't-sidepanel', 't-themes', 't-settings', 't-link', 't-export', 't-converter', 'outro'];
const CYCLE = [['slideleft', 0.55], ['fadeblack', 0.6], ['wipeleft', 0.5], ['circleopen', 0.7], ['slideup', 0.55], ['fadeblack', 0.6]];
const SPECIAL = { 'intro': ['fadeblack', 1.0], 'sc-basslab': ['fadeblack', 0.7], 'download': ['fadeblack', 0.9], 't-converter': ['fadeblack', 1.0] };   // into and out of the download box, and the ends, dip to black
const PLAN = PARTS.map((n, i) => i === PARTS.length - 1 ? [n, null, 0] : [n].concat(SPECIAL[n] || CYCLE[i % CYCLE.length]));
const clips = PLAN.filter(([n]) => { const ok = fs.existsSync(path.join(P, n + '.mp4')); if (!ok) console.log('(missing part, skipped: ' + n + ')'); return ok; });
const d = clips.map(([n]) => dur(path.join(P, n + '.mp4')));
let filter = '', last = '[0:v]', t = d[0];
for (let i = 1; i < clips.length; i++) {
  const tr = clips[i - 1][1] || 'fade', td = Math.min(clips[i - 1][2] || 0.5, d[i - 1] / 3, d[i] / 3), off = (t - td).toFixed(3), lab = i === clips.length - 1 ? '[vout]' : `[x${i}]`;
  filter += `${last}[${i}:v]xfade=transition=${tr}:duration=${td.toFixed(3)}:offset=${off}${lab};`; last = lab; t = t - td + d[i];
}
const total = t; console.log('video length ' + total.toFixed(1) + ' s (' + (total / 60).toFixed(1) + ' min), ' + clips.length + ' parts');
// the soundtrack: 80 BPM (a bar is 3 s), composed a little longer than the video, then faded
const music = path.join(DIR, 'soundtrack.wav'); const comp = path.join(DIR, 'LofiComposer.exe');
execFileSync(comp, [music, String(Math.ceil(total / 3) + 2), '7'], { stdio: 'inherit' });
const inputs = []; clips.forEach(([n]) => inputs.push('-i', path.join(P, n + '.mp4'))); inputs.push('-i', music);
const fo = Math.max(0, total - 4).toFixed(2);
filter += `[${clips.length}:a]atrim=0:${total.toFixed(3)},afade=t=in:d=2,afade=t=out:st=${fo}:d=4,volume=0.9[aout]`;
const out1080 = path.join(DIR, 'purphex-showcase-1080.mp4');
run([...inputs, '-filter_complex', filter, '-map', '[vout]', '-map', '[aout]', '-c:v', 'libx264', '-preset', 'slower', '-crf', '21', '-tune', 'animation', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '160k', '-movflags', '+faststart', out1080]);   // (tuned for screen pictures: sharp, and under GitHub's 100 MB a file)
console.log('wrote ' + out1080);
// the 4K copy (the website's quality button), and the poster: a frame from the showcase
run(['-i', out1080, '-vf', 'scale=3840:2160:flags=lanczos', '-c:v', 'libx264', '-preset', 'slow', '-crf', '27', '-tune', 'animation', '-pix_fmt', 'yuv420p', '-c:a', 'copy', '-movflags', '+faststart', path.join(DIR, 'purphex-showcase-4k.mp4')]);   // (an enlarged 1080p: kept under 100 MB)
run(['-ss', String(Math.min(8, d[0] + 2)), '-i', out1080, '-frames:v', '1', '-q:v', '3', path.join(DIR, 'showcase-poster.jpg')]);
console.log('wrote the 4K copy and the poster');
