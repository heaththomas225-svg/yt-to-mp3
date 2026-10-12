using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// The X of the DAW: YOU ARE LEAVING THE DAW. If the project is not saved it offers to save it (you give it a name, and it is in My Projects afterwards).
partial class StudioForm
{
    int editCount, savedEdit; bool leaving, welcomeOn;
    // when Purphex Music is opened it starts on My Projects, with a banner: open a project or start a new one
    internal void ShowWelcome() { welcomeOn = true; GoTab(3); }
    float PaintWelcome(Graphics g, float W, float y)
    {
        float h = 104; var box = Rc(24, y, W - 48 - 56, h); FillRR(g, TM.C(225, 22, 12, 48), box, 16); Glow(g, NeonCyan, box, 16, 0.55f, 3); StrokeRR(g, Color.FromArgb(150, NeonViolet), box, 16, 1.4f);
        Txt(g, "WELCOME TO PURPHEX MUSIC", 20, Ink, Rc(box.X + 22, box.Y + 12, box.Width - 300, 30), StringAlignment.Near, true);
        Txt(g, "Open one of your projects below, or start something new.", 13, DimC, Rc(box.X + 22, box.Y + 44, box.Width - 300, 22), StringAlignment.Near, false);
        var nb = Rc(box.X + 22, box.Y + 66, 230, 30); Btn(g, nb, "START A NEW PROJECT", 2, () => { welcomeOn = false; tab = 0; NewProject(); }, 12); Hint2(nb, "An empty project: add music, record or add an instrument");
        var ob = Rc(nb.Right + 12, box.Y + 66, 230, 30); Btn(g, ob, "OPEN A PROJECT FILE...", 1, () => { welcomeOn = false; OpenClick(); }, 12); Hint2(ob, "Find a project file (.ytmproj) on your PC");
        var cl = Rc(box.Right - 40, box.Y + 10, 28, 28); Btn(g, cl, "x", 3, () => welcomeOn = false, 12); Hint2(cl, "Hide this banner");
        return h + 14;
    }
    // the question when opening something would replace the project that is open now
    string OverwriteText(string name)
    {
        return "OPENING \"" + name + "\" WILL OVERWRITE THE PROJECT THAT IS OPEN NOW." + "\n\n" + (HasUnsaved ? "It has changes that are not saved: they will be lost (press No, then SAVE, if you want to keep them)." + "\n\n" : "It is closed and replaced by this one (it stays in My Projects if you saved it)." + "\n\n") + "Confirm?";
    }
    internal Func<string, string> nameHook;   // a test answers for itself (the name of the project)
    // the project has something that is not saved (it was never saved, or it was changed since)
    internal bool HasUnsaved { get { return eng.Tracks.Count > 0 && (string.IsNullOrEmpty(projPath) || editCount != savedEdit); } }
    // only a click on the X (or Alt+F4, or Close in the window menu) asks: the program closing the window itself (it has asked already) or a test does not
    bool userX;
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        bool fromX = userX; userX = false;
        if (!leaving && fromX && e.CloseReason == CloseReason.UserClosing && !AskLeave()) { e.Cancel = true; return; }
        base.OnFormClosing(e);
    }
    internal void NoteUserX() { userX = true; }
    protected override void OnHandleDestroyed(EventArgs e) { EmojiSprites.Ready -= EmojiReady; base.OnHandleDestroyed(e); }
    void EmojiReady() { try { lastInput = DateTime.Now; Invalidate(); } catch { } }
    bool AskLeave()
    {
        Func<string, MessageBoxButtons, DialogResult> ask = (t, b) => exitAsk != null ? exitAsk(t) : MessageBox.Show(this, t, "Leaving the DAW", b, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (ask("YOU ARE LEAVING THE DAW.\n\nThis closes Purphex Music (the DAW). Whatever is playing stops.\n\nLeave?", MessageBoxButtons.YesNo) != DialogResult.Yes) return false;
        if (HasUnsaved)
        {
            var r = ask("Your project is not saved.\n\nSave it before you leave? You can give it a name, and you will find it in My Projects.\n\nYes: save it.\nNo: leave without saving.\nCancel: stay in the DAW.", MessageBoxButtons.YesNoCancel);
            if (r == DialogResult.Cancel) return false; if (r == DialogResult.Yes && !QuickSaveNamed()) return false;
        }
        leaving = true; return true;
    }
    // saves the project (asking for a name when it has none) into the projects folder, so it shows up in My Projects; false = it was not saved
    internal bool QuickSaveNamed()
    {
        if (eng.Tracks.Count == 0) return true; FlexCommitIfOpen(); string path = projPath;
        if (string.IsNullOrEmpty(path))
        {
            string def = projName == "Untitled project" ? "My song" : projName; string nm = nameHook != null ? nameHook(def) : Ask("Name your project (you will find it in My Projects):", def); if (nm == null) return false;
            foreach (char c in Path.GetInvalidFileNameChars()) nm = nm.Replace(c, ' '); nm = nm.Trim(); if (nm.Length == 0) return false;
            try { Directory.CreateDirectory(ProjectLibrary.DefaultFolder); } catch { }
            path = Path.Combine(ProjectLibrary.DefaultFolder, nm + ".ytmproj"); int n = 2; while (File.Exists(path)) path = Path.Combine(ProjectLibrary.DefaultFolder, nm + " " + (n++) + ".ytmproj");
        }
        try { ProjectIO.Save(eng, path); projPath = path; projName = Path.GetFileNameWithoutExtension(path); NoteProject(path, true); Toast("Project saved: " + projName + " (in My Projects)"); return true; }
        catch (Exception ex) { MessageBox.Show(this, "Could not save the project: " + ex.Message, "Save"); return false; }
    }
}
