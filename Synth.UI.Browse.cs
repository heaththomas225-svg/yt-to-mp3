using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Preset browser: categories on the left, presets on the right, and buttons to save, rename, duplicate, delete and initialise.
partial class SynthCanvas
{
    bool browseOpen; string browseCat = "Wobble"; float browseScroll; string browseMsg = "";
    List<CommPreset> commList; bool commLoading; string commErr = ""; int commGen;
    // the sounds other people shared (fetched in the background; shown under the Community category)
    void CommFetch()
    {
        int gen = ++commGen; commLoading = true; commErr = "";
        System.Threading.ThreadPool.QueueUserWorkItem(o =>
        {
            List<CommPreset> l = null; string err = ""; try { l = CommunityPresets.Fetch("synth", "popular"); } catch { err = "Could not reach the Purphex community. Check your internet connection."; }
            try { BeginInvoke(new Action(() => { if (gen != commGen) return; commLoading = false; commList = l ?? new List<CommPreset>(); commErr = err; Poke(); })); } catch { }
        });
    }
    void PresetMenu(PointF pt)
    {
        browseOpen = true; browseScroll = 0; browseMsg = ""; string c = SynthPresets.CategoryOf(fx.PresetName); if (c.Length > 0) browseCat = c;
    }

    string AskName(string prompt, string def)
    {
        Typing = true; try { return StudioDialogs.Ask(FindForm(), prompt, def); } finally { Typing = false; Poke(); }
    }

    void PaintPresetBrowser(Graphics g)
    {
        if (!browseOpen) return;
        var all = Rc(0, 0, W, H); using (var b = new SolidBrush(Color.FromArgb(190, 5, 2, 12))) g.FillRectangle(b, all);
        var box = Rc(150, 50, W - 300, 620); Fill(g, Color.FromArgb(252, 26, 14, 48), box, 14); Stroke(g, Acc, box, 14, 1.5f);
        var blocker = Add(all); blocker.Down = pt => { if (!box.Contains(pt)) browseOpen = false; };
        Txt(g, "PRESETS", 20, Ink, Rc(box.X + 20, box.Y + 10, 300, 30), StringAlignment.Near, true);
        Txt(g, "Current: " + (modified ? "* " : "") + fx.PresetName, 12, Dim, Rc(box.X + 20, box.Y + 40, 500, 18), StringAlignment.Near, false);
        var close = Rc(box.Right - 100, box.Y + 14, 82, 28); Fill(g, Hot(close) ? PanelHi : Panel, close, 6); Stroke(g, Edge, close, 6, 1); Txt(g, "CLOSE", 12, Ink, close, StringAlignment.Center, true); Add(close).Down = pt => { browseOpen = false; };

        var cats = new List<string>(SynthPresets.Categories); cats.Insert(cats.Count - 1, "My presets"); cats.Insert(cats.Count - 1, "Community");
        float cx = box.X + 20, cy = box.Y + 72;
        for (int i = 0; i < cats.Count; i++)
        {
            string c = cats[i]; var r = Rc(cx, cy + i * 33, 190, 30); bool sel = c == browseCat; int n = c == "My presets" ? SynthPresets.UserNames().Count : (c == "Community" ? (commList == null ? 0 : commList.Count) : SynthPresets.InCategory(c).Count);
            Fill(g, sel ? PanelHi : Panel, r, 6); Stroke(g, sel ? Acc : Edge, r, 6, sel ? 2 : 1); Txt(g, c, 13, sel ? Ink : Dim, Rc(r.X + 12, r.Y, 120, r.Height), StringAlignment.Near, sel); Txt(g, n.ToString(), 11, Dim, Rc(r.Right - 44, r.Y, 36, r.Height), StringAlignment.Far, false);
            var rg = Add(r); rg.Down = pt => { browseCat = c; browseScroll = 0; browseMsg = ""; if (c == "Community") CommFetch(); };
        }
        // list
        var list = Rc(box.X + 230, box.Y + 72, box.Width - 250, 408); Fill(g, Color.FromArgb(235, 14, 7, 32), list, 8); Stroke(g, Edge, list, 8, 1);
        bool comm = browseCat == "Community"; var names = browseCat == "My presets" ? SynthPresets.UserNames() : (comm ? new List<string>() : SynthPresets.InCategory(browseCat)); if (comm && commList != null) foreach (var cp0 in commList) names.Add(cp0.Name);
        float rowH = 36; float maxScroll = Math.Max(0, names.Count * rowH - (list.Height - 8)); browseScroll = Math.Max(0, Math.Min(maxScroll, browseScroll));
        var lw = Add(list); lw.Wheel = d => { browseScroll = Math.Max(0, Math.Min(maxScroll, browseScroll - d * 36)); };
        var gs = g.Save(); g.SetClip(Rc(list.X + 2, list.Y + 2, list.Width - 4, list.Height - 4), System.Drawing.Drawing2D.CombineMode.Intersect);
        if (names.Count == 0) Txt(g, comm ? (commLoading || commList == null ? "Loading the community..." : (commErr.Length > 0 ? commErr : "Nobody has shared a Bass Lab sound yet. Be the first: press SHARE below.")) : (browseCat == "My presets" ? "No presets of your own yet. Press SAVE AS to keep the current sound." : "Nothing here."), 13, Dim, Rc(list.X + 16, list.Y + 16, list.Width - 32, 24), StringAlignment.Near, false);
        for (int i = 0; i < names.Count; i++)
        {
            string n = names[i]; var r = Rc(list.X + 8, list.Y + 6 + i * rowH - browseScroll, list.Width - 16, rowH - 4); if (r.Bottom < list.Y || r.Y > list.Bottom) continue;
            bool cur = n == fx.PresetName, hot = Hot(r); Fill(g, cur ? Color.FromArgb(150, 90, 50, 170) : (hot ? Color.FromArgb(150, 60, 34, 110) : Color.FromArgb(120, 44, 24, 80)), r, 7); if (cur) Stroke(g, Acc2, r, 7, 1.5f);
            if (comm) { var cp1 = commList[i]; Txt(g, n, 14, Ink, Rc(r.X + 14, r.Y, r.Width * 0.55f, r.Height), StringAlignment.Near, false); Txt(g, "BY " + cp1.Author + "   " + cp1.Uses + (cp1.Uses == 1 ? " use" : " uses"), 11, Dim, Rc(r.X + r.Width * 0.55f, r.Y, r.Width * 0.45f - 12, r.Height), StringAlignment.Far, false); var rgc = Add(r); rgc.Tip = "Click to use " + n; rgc.Down = pt => { if (!CommunityPresets.Safe("synth", cp1.Data)) { browseMsg = "That sound is not valid."; return; } try { SynthPresets.ApplyData(fx, cp1.Name, cp1.Data); modified = false; browseMsg = "Loaded " + cp1.Name + " by " + cp1.Author + ". Press SAVE AS to keep your own copy."; CommunityPresets.Tell("use", cp1.Id); } catch (Exception ex) { browseMsg = "Could not load it: " + ex.Message; } }; continue; }
            Txt(g, n, 14, Ink, Rc(r.X + 14, r.Y, r.Width - 28, r.Height), StringAlignment.Near, cur);
            var rg = Add(r); rg.Tip = "Click to load " + n; rg.Down = pt => { SynthPresets.Apply(fx, n); modified = false; browseMsg = "Loaded " + n; };
        }
        g.Restore(gs);
        // actions
        float by = list.Bottom + 12; float bx = list.X; var user = SynthPresets.UserNames(); bool isUser = user.Contains(fx.PresetName);
        bx = Action(g, "INITIALISE PATCH", bx, by, 170, true, "Reset everything to a clean, plain sound.", () => { SynthPresets.Initialise(fx); modified = false; browseMsg = "Patch initialised."; });
        bx = Action(g, "SAVE AS...", bx, by, 110, true, "Keep the current sound under a name.", () =>
        {
            string s = AskName("Preset name:", SynthPresets.IsFactory(fx.PresetName) ? "My " + fx.PresetName : fx.PresetName); if (s == null) return;
            try { if (SynthPresets.UserNames().Contains(s.Trim()) && MessageBox.Show(this, "\"" + s.Trim() + "\" exists. Replace it?", "Save preset", MessageBoxButtons.YesNo) != DialogResult.Yes) return; SynthPresets.SaveUser(fx, s); modified = false; browseCat = "My presets"; browseMsg = "Saved " + s.Trim(); CommunityPresets.AutoShare("synth", s.Trim(), fx.Serialize(), ProjectLibrary.ProfileName, msg => { if (msg != null) { try { BeginInvoke(new Action(() => { browseMsg = msg; Poke(); })); } catch { } } }); }
            catch (Exception ex) { browseMsg = ex.Message; }
        });
        bx = Action(g, "SHARE", bx, by, 80, true, "Share the current sound with everyone in the Community category.", () =>
        {
            string data = fx.Serialize(); if (!CommunityPresets.Safe("synth", data)) { browseMsg = "That sound is too big or has something that cannot be shared."; return; }
            string s = AskName("Name your sound (everyone can see and use it):", SynthPresets.IsFactory(fx.PresetName) ? "My " + fx.PresetName : fx.PresetName); if (s == null) return; string who = ProjectLibrary.ProfileName; if (string.IsNullOrWhiteSpace(who)) who = "Producer"; browseMsg = "Sharing...";
            System.Threading.ThreadPool.QueueUserWorkItem(o2 => { string err = CommunityPresets.Share("synth", s, who, data); try { BeginInvoke(new Action(() => { browseMsg = err ?? "Shared! Find it under Community."; if (err == null) { browseCat = "Community"; CommFetch(); } Poke(); })); } catch { } });
        });
        bx = Action(g, "DUPLICATE", bx, by, 110, true, "Make a copy you can edit.", () => { try { string n2 = SynthPresets.DuplicateUser(fx.PresetName); SynthPresets.Apply(fx, n2); modified = false; browseCat = "My presets"; browseMsg = "Made " + n2; } catch (Exception ex) { browseMsg = ex.Message; } });
        bx = Action(g, "RENAME", bx, by, 96, isUser, "Rename your preset.", () => { string s = AskName("New name:", fx.PresetName); if (s == null) return; try { string old = fx.PresetName; SynthPresets.RenameUser(old, s); fx.PresetName = s.Trim(); browseMsg = "Renamed to " + s.Trim(); } catch (Exception ex) { browseMsg = ex.Message; } });
        bx = Action(g, "DELETE", bx, by, 90, isUser, "Delete your preset (built-in ones cannot be deleted).", () => { if (MessageBox.Show(this, "Delete preset \"" + fx.PresetName + "\"?", "Delete preset", MessageBoxButtons.YesNo) != DialogResult.Yes) return; try { SynthPresets.DeleteUser(fx.PresetName); browseMsg = "Deleted " + fx.PresetName; fx.PresetName = "Init"; } catch (Exception ex) { browseMsg = ex.Message; } });
        Txt(g, browseMsg, 12, Good, Rc(box.X + 230, by + 40, box.Width - 250, 20), StringAlignment.Near, true);
        Txt(g, "Your presets store the oscillators, drawn waves, filters, envelopes, LFO shapes, modulation, effect chain and voice settings.", 11, Dim, Rc(box.X + 20, box.Bottom - 40, box.Width - 40, 18), StringAlignment.Near, false);
    }

    float Action(Graphics g, string label, float x, float y, float w, bool enabled, string tip, Action a)
    {
        var r = Rc(x, y, w, 30); Fill(g, enabled ? (Hot(r) ? PanelHi : Panel) : Color.FromArgb(14, 8, 26), r, 6); Stroke(g, enabled ? Acc : Color.FromArgb(60, 60, 90), r, 6, 1);
        Txt(g, label, 11, enabled ? Ink : Color.FromArgb(90, 80, 120), r, StringAlignment.Center, true);
        if (enabled) { var rg = Add(r); rg.Tip = tip; rg.Down = pt => a(); }
        return x + w + 8;
    }
}
