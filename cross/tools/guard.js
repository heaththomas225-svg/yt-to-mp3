// guard.js: hide Windows-only parts of a shared file from the cross-platform core with #if !PURPHEX_CORE ... #endif
//   node guard.js <file> class <ClassName>        the whole class (by brace matching)
//   node guard.js <file> member "<text>"          the member whose declaration line contains <text> (its body by brace matching, or the line)
//   node guard.js <file> line "<text>"            just the line containing <text>
//   node guard.js <file> using "<ns>"             the using line for a namespace
// The Windows build never defines PURPHEX_CORE, so it compiles exactly what it did before.
const fs = require('fs'); const [, , file, mode, what] = process.argv; const D = require('path').join(__dirname, '..', '..') + '/';
let s = fs.readFileSync(D + file, 'utf8'); const crlf = s.includes('\r\n'); let L = s.split(/\r?\n/);
function lineOf(pred, from = 0) { for (let i = from; i < L.length; i++) if (pred(L[i], i)) return i; return -1; }
function already(i) { return i > 0 && /^#if !PURPHEX_CORE/.test(L[i - 1]); }
function endOfBlock(start) {   // from the line holding the first '{' at or after start, to the line with its matching '}'
  let depth = 0, seen = false, inStr = false, inChr = false, verb = false;
  for (let i = start; i < L.length; i++) {
    const t = L[i]; let lineComment = false;
    for (let k = 0; k < t.length; k++) {
      const c = t[k], n = t[k + 1];
      if (lineComment) break;
      if (verb) { if (c === '"' && n === '"') { k++; continue; } if (c === '"') verb = false; continue; }
      if (inStr) { if (c === '\\') { k++; continue; } if (c === '"') inStr = false; continue; }
      if (inChr) { if (c === '\\') { k++; continue; } if (c === "'") inChr = false; continue; }
      if (c === '/' && n === '/') { lineComment = true; continue; }
      if (c === '@' && n === '"') { verb = true; k++; continue; }
      if (c === '"') { inStr = true; continue; }
      if (c === "'") { inChr = true; continue; }
      if (c === '{') { depth++; seen = true; } else if (c === '}') { depth--; if (seen && depth === 0) return i; }
    }
  }
  throw new Error('no matching brace from line ' + (start + 1));
}
function wrap(a, b) { if (already(a)) { console.log('  already guarded: ' + file + ':' + (a + 1)); return; } L.splice(b + 1, 0, '#endif'); L.splice(a, 0, '#if !PURPHEX_CORE'); }
let a = -1, b = -1;
if (mode === 'class') { const rx = new RegExp('^\\s*(public |internal |static |sealed |abstract |partial )*(class|struct|enum|interface) ' + what + '\\b'); a = lineOf((t) => rx.test(t)); if (a < 0) throw new Error('no class ' + what + ' in ' + file); b = endOfBlock(a); }
else if (mode === 'member') { a = lineOf((t) => t.includes(what)); if (a < 0) throw new Error('no member "' + what + '" in ' + file); const opens = L[a].indexOf('{') >= 0 || (L[a + 1] || '').trim().startsWith('{'); const oneLine = L[a].indexOf('{') >= 0 && (() => { try { return endOfBlock(a) === a; } catch (e) { return false; } })(); b = oneLine || !opens ? a : endOfBlock(a); }
else if (mode === 'line') { a = lineOf((t) => t.includes(what)); if (a < 0) throw new Error('no line "' + what + '" in ' + file); b = a; }
else if (mode === 'using') { a = lineOf((t) => t.trim() === 'using ' + what + ';'); if (a < 0) { console.log('  (no using ' + what + ' in ' + file + ')'); process.exit(0); } b = a; }
else throw new Error('mode?');
wrap(a, b); fs.writeFileSync(D + file, L.join(crlf ? '\r\n' : '\n')); console.log('guarded ' + file + ':' + (a + 1) + '-' + (b + 1) + ' (' + mode + ' ' + what + ')');
