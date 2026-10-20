// autoport.js File1.cs File2.cs ...: makes a shared file compile in the cross-platform core without changing the Windows build:
//  - every "partial class StudioForm" block and every window/control class (": PxWindow", ": Form", ": Control", ": UserControl", ": Panel"...) is wrapped in #if !PURPHEX_CORE
//  - a one-line "ShowWindow(IWin32Window owner) { ... }" that opens a window gets an empty stand-in for the core (#else)
// Safe to run again: blocks already guarded are skipped.
const fs = require('fs'); const D = require('path').join(__dirname, '..', '..') + '/';
const UI = /:\s*(PxWindow|Form|Control|UserControl|Panel|ContextMenuStrip|ToolStripRenderer|NativeWindow|TextBox|RichTextBox|Button|Label|ListBox|ComboBox|PictureBox|ScrollableControl|ToolStripControlHost|ProfessionalColorTable|IMessageFilter|VstWindow|SynthWindow|PxInstWindow|[A-Za-z]*Window|[A-Za-z]*Sheet|[A-Za-z]*Dialog)\b/;
function endOfBlock(L, start) {
  let depth = 0, seen = false, inStr = false, verb = false, inChr = false, block = false;
  for (let i = start; i < L.length; i++) { const t = L[i]; let lc = false;
    for (let k = 0; k < t.length; k++) { const c = t[k], n = t[k + 1];
      if (lc) break;
      if (block) { if (c === '*' && n === '/') { block = false; k++; } continue; }
      if (verb) { if (c === '"' && n === '"') { k++; continue; } if (c === '"') verb = false; continue; }
      if (inStr) { if (c === '\\') { k++; continue; } if (c === '"') inStr = false; continue; }
      if (inChr) { if (c === '\\') { k++; continue; } if (c === "'") inChr = false; continue; }
      if (c === '/' && n === '/') { lc = true; continue; }
      if (c === '/' && n === '*') { block = true; k++; continue; }
      if (c === '@' && n === '"') { verb = true; k++; continue; }
      if (c === '$' && n === '"') { inStr = true; k++; continue; }
      if (c === '"') { inStr = true; continue; }
      if (c === "'") { inChr = true; continue; }
      if (c === '{') { depth++; seen = true; } else if (c === '}') { depth--; if (seen && depth === 0) return i; } } }
  throw new Error('no matching brace from line ' + (start + 1));
}
for (const f of process.argv.slice(2)) {
  let s = fs.readFileSync(D + f, 'utf8'); const cr = s.includes('\r\n'); const L = s.split(/\r?\n/); let n = 0;
  for (let i = 0; i < L.length; i++) {
    const t = L[i]; let up = i - 1; while (up >= 0 && /^\s*(\/\/|\[)/.test(L[up])) up--; const guarded = up >= 0 && /^#if !PURPHEX_CORE/.test(L[up]);
    const isCls = /^\s*(public |internal |static |sealed |abstract |partial )*(class|struct) /.test(t);
    if (isCls && !guarded && (/partial class StudioForm\b/.test(t) || /\bclass App\b/.test(t) || UI.test(t))) {
      let a = i; while (a > 0 && /^\s*(\/\/|\[)/.test(L[a - 1])) a--; const b = endOfBlock(L, i); L.splice(b + 1, 0, '#endif'); L.splice(a, 0, '#if !PURPHEX_CORE'); i = b + 2; n++; continue;
    }
    if (!guarded && /public override void ShowWindow\(IWin32Window owner\)\s*\{.*\}\s*$/.test(t) && /Window|Show\(/.test(t.split('{').slice(1).join('{')) && !/\{\s*\}\s*$/.test(t)) {
      const ind = t.match(/^\s*/)[0]; L.splice(i, 1, '#if !PURPHEX_CORE', t, '#else', ind + 'public override void ShowWindow(IWin32Window owner) { }', '#endif'); i += 4; n++;
    }
  }
  fs.writeFileSync(D + f, L.join(cr ? '\r\n' : '\n')); console.log(f + ': ' + n + ' guarded');
}
