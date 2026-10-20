// moves whole top-level classes out of a UI file into a new shared file (brace matched); the Windows build gets the new file in SRC
//   node movetypes.js <newfile.cs> "<header comment>" File.cs:ClassName [File.cs:ClassName ...]
const fs = require('fs'); const D = require('path').join(__dirname, '..', '..') + '/';
const [, , outFile, header, ...specs] = process.argv; const blocks = []; const usings = new Set(['using System;', 'using System.Collections.Generic;']);
function endOfBlock(L, start) { let depth = 0, seen = false; for (let i = start; i < L.length; i++) { const t = L[i].replace(/"(\\.|[^"\\])*"/g, '""').replace(/'(\\.|[^'\\])'/g, "''").replace(/\/\/.*$/, ''); for (const c of t) { if (c === '{') { depth++; seen = true; } else if (c === '}') { depth--; if (seen && depth === 0) return i; } } } throw new Error('no end'); }
for (const spec of specs) {
  const [f, cls] = spec.split(':'); let s = fs.readFileSync(D + f, 'utf8'); const cr = s.includes('\r\n'); const L = s.split(/\r?\n/);
  const rx = new RegExp('^\\s*(public |internal |static |sealed |abstract |partial )*(class|struct|enum) ' + cls + '\\b'); let a = L.findIndex((t) => rx.test(t)); if (a < 0) throw new Error('no ' + cls + ' in ' + f);
  while (a > 0 && /^\s*\/\//.test(L[a - 1])) a--;   // its comment lines go with it
  const b = endOfBlock(L, L.findIndex((t) => rx.test(t))); blocks.push(L.slice(a, b + 1).join('\n')); L.splice(a, b - a + 1);
  for (const u of L.filter((t) => /^using [A-Za-z.]+;$/.test(t))) if (!/Windows|Drawing|Win32/.test(u)) usings.add(u);
  fs.writeFileSync(D + f, L.join(cr ? '\r\n' : '\n')); console.log('moved ' + cls + ' from ' + f);
}
fs.writeFileSync(D + outFile, ([...usings].join('\n') + '\n\n// ' + header + '\n' + blocks.join('\n\n') + '\n').split('\n').join('\r\n'));
let env = fs.readFileSync(D + '_env.sh', 'utf8'); if (env.indexOf(' ' + outFile + ' ') < 0) { env = env.replace('Midi.Ev.cs ', 'Midi.Ev.cs ' + outFile + ' '); fs.writeFileSync(D + '_env.sh', env); }
console.log('wrote ' + outFile);
