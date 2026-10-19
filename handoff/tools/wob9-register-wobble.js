const fs=require('fs');const D='C:/Users/thoma/yt-to-mp3/';const files={};
function get(f){if(!files[f]){let s=fs.readFileSync(D+f,'utf8');files[f]={crlf:s.includes('\r\n'),s:s.split('\r\n').join('\n')};}return files[f];}
function edit(f,a,b){const o=get(f);const i=o.s.indexOf(a);if(i<0){console.log('MISSING in '+f+': '+a.slice(0,120));process.exit(1);}if(o.s.indexOf(a,i+1)>=0){console.log('TWICE in '+f+': '+a.slice(0,100));process.exit(1);}o.s=o.s.slice(0,i)+b+o.s.slice(i+a.length);}
const B='Sounds.BassDesign.cs';
edit(B,`AddP("env", "Filter envelope", 0, 4, 0, "oct");`,`AddC("vowels", "Vowels", Vowels, 0); AddC("ratio", "FM ratio", Ratios, 4); AddP("pw", "Pulse width", 8, 92, 50, "%");
        AddP("env", "Filter envelope", 0, 4, 0, "oct");`);
edit(B,`    public static readonly string[] Shapes = {`,`    public static readonly string[] Vowels = { "aoui", "oaei", "uoa", "aeo", "iaou", "ouai", "eao", "iou", "aie", "iaeu" }, Ratios = { "0.5", "1", "1.41", "1.5", "2", "2.73", "3", "3.5" };
    public static readonly string[] Shapes = {`);
edit(B,`        patch.EnvAmt = Get("env"); patch.PitchEnv = Get("pitchenv");
        string s =`,`        patch.EnvAmt = Get("env"); patch.PitchEnv = Get("pitchenv"); patch.Vowels = Vowels[Math.Max(0, Math.Min(Vowels.Length - 1, (int)Get("vowels")))]; patch.FmRatio = double.Parse(Ratios[Math.Max(0, Math.Min(Ratios.Length - 1, (int)Get("ratio")))], CultureInfo.InvariantCulture); patch.Pw = Get("pw") / 100.0;
        string s =`);
edit(B,`Set("env", (float)p.EnvAmt); Set("pitchenv", (float)p.PitchEnv); patch.Vowels = p.Vowels; patch.Pw = p.Pw;`,`Set("env", (float)p.EnvAmt); Set("pitchenv", (float)p.PitchEnv); int vi = Array.IndexOf(Vowels, p.Vowels); Set("vowels", vi >= 0 ? vi : 0); int fi = 0; double bd = 99; for (int k = 0; k < Ratios.Length; k++) { double d = Math.Abs(double.Parse(Ratios[k], CultureInfo.InvariantCulture) - p.FmRatio); if (d < bd) { bd = d; fi = k; } } Set("ratio", fi); Set("pw", (float)(p.Pw * 100)); patch.Vowels = p.Vowels; patch.Pw = p.Pw;`);
edit('Studio.Dsp.cs',`        new Info("sampler", "Purphex Sampler",`,`        new Info("wobble", "Purphex Wobble", "Instruments", "The wobble bass engine: tempo-synced LFO filter, vowels, FM, grit, sub and mid layers"),
        new Info("sampler", "Purphex Sampler",`);
edit('Studio.Dsp.cs',`            case "sampler": return new SamplerFx();`,`            case "sampler": return new SamplerFx();
            case "wobble": return new WobbleFx();`);
edit('Studio.Mix.cs',`        string[] more; if (PresetsExtra.Data.TryGetValue(type, out more))`,`        if (type == "wobble") foreach (var s in WobbleFx.FactoryPresets()) { int i = s.IndexOf('|'); l.Add(new KeyValuePair<string, string>(s.Substring(0, i), s.Substring(i + 1))); }
        string[] more; if (PresetsExtra.Data.TryGetValue(type, out more))`);
edit('_env.sh',`Sounds.Bake.cs`,`Sounds.Bake.cs Sounds.Wobble.cs Sounds.BassDesign.cs`);
for(const f in files)fs.writeFileSync(D+f,files[f].crlf?files[f].s.split('\n').join('\r\n'):files[f].s);console.log('OK');
