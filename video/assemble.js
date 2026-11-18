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
const PLAN = [
  ['intro', 'fade', 1.0],
  // the showcase: quick cuts while the song plays
  ['sc-studio', 'smoothleft', 0.5], ['sc-mixer', 'smoothleft', 0.5], ['sc-master', 'dissolve', 0.5], ['sc-basslab', 'fade', 0.6],
  // how to download (a box over the blurred studio), then back into the studio
  ['download', 'fadeblack', 0.8],
  // the tour
  ['t-projects', 'smoothup', 0.6], ['t-import', 'dissolve', 0.5], ['t-toolbar', 'smoothleft', 0.6], ['t-mixer', 'dissolve', 0.5], ['t-vocals', 'smoothleft', 0.6],
  ['t-vsts', 'dissolve', 0.5], ['t-assign', 'smoothleft', 0.6], ['t-pads', 'dissolve', 0.5], ['t-beatlab', 'smoothleft', 0.6], ['t-tag', 'dissolve', 0.5], ['t-basslab', 'smoothup', 0.6], ['t-pianoroll', 'dissolve', 0.5],
  ['t-flex', 'smoothleft', 0.6], ['t-sounds', 'dissolve', 0.5], ['t-hub', 'smoothup', 0.6], ['t-master', 'fade', 0.7], ['t-lyrics', 'dissolve', 0.5],
  ['t-sidepanel', 'smoothleft', 0.6], ['t-themes', 'dissolve', 0.5], ['t-settings', 'smoothleft', 0.6], ['t-link', 'dissolve', 0.5], ['t-export', 'smoothup', 0.6],
  ['t-converter', 'fadeblack', 1.0], ['outro', null, 0],
];
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
run([...inputs, '-filter_complex', filter, '-map', '[vout]', '-map', '[aout]', '-c:v', 'libx264', '-preset', 'slow', '-crf', '17', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '192k', '-movflags', '+faststart', out1080]);
console.log('wrote ' + out1080);
// the 4K copy (the website's quality button), and the poster: a frame from the showcase
run(['-i', out1080, '-vf', 'scale=3840:2160:flags=lanczos', '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-pix_fmt', 'yuv420p', '-c:a', 'copy', '-movflags', '+faststart', path.join(DIR, 'purphex-showcase-4k.mp4')]);
run(['-ss', String(Math.min(8, d[0] + 2)), '-i', out1080, '-frames:v', '1', '-q:v', '3', path.join(DIR, 'showcase-poster.jpg')]);
console.log('wrote the 4K copy and the poster');
