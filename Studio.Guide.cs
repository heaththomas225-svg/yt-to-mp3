using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// SHOW ME: the guides take you to the page (and open the box) where the button lives, then point at it. Nothing is left for you to find by yourself.
partial class StudioForm
{
    // every pop-up box and extra page shut, so a guide starts from a clean screen
    void CloseSheetsForGuide()
    {
        ntOpen = false; busOpen = false; stylesOpen = false; themeOpen = false; psOpen = false; autoOpen = false; gainOpen = false; pickOpen = false; cpOpen = false; browserOpen = false; keyOn = false; packsOpen = false; menuOpen = false;
        if (amOpen) { try { AmCancel(); } catch { amOpen = false; } } if (soundsOpen) CloseSounds(); if (flexOpen) { try { FlexClose(true); } catch { } }
    }
    // goes to where the button is: tries every page and every box in turn (the first one that has it wins). Returns false when it is nowhere.
    internal bool GoToLabel(string label)
    {
        Win.Bring(this); Refresh(); if (!TargetOf(label).IsEmpty && !OverlayOpen) return true;   // (a repaint first, so what is known about the screen is current)
        var states = new List<Action>
        {
            () => { tab = 0; }, () => { tab = 1; mpTab = 0; }, () => { tab = 1; mpTab = 1; }, () => { tab = 5; }, () => { tab = 2; }, () => { tab = 3; projBin = false; }, () => { tab = 4; }, () => { tab = 6; profScroll = 0; }, () => { OpenArtists(); },
            () => { tab = 0; OpenThemeSheet(); }, () => { tab = 0; OpenNewTrackSheet(0); }, () => { tab = 0; OpenStyles(); }, () => { tab = 0; OpenBusSheet(); }, () => { tab = 0; OpenBrowser(false, -1); }, () => { tab = 0; OpenSounds(); },
            () => { tab = 0; if (eng.Tracks.Count > 0) { selTrack = 0; if (eng.Tracks[0].Fx.Count == 0) { var f = FxRegistry.Create("eq"); if (f != null) { f.Prepare(StudioEngine.SR); eng.Tracks[0].Fx.Add(f); } } selFx = 0; } },
        };
        int keepTab = tab;
        foreach (var st in states)
        {
            CloseSheetsForGuide(); try { st(); } catch { continue; } Refresh(); Application.DoEvents(); Refresh();
            if (!TargetOf(label).IsEmpty) return true;
        }
        CloseSheetsForGuide(); tab = keepTab; return false;
    }

    // SHOW ME for a whole guide: one prompt per button the guide talks about, each one taking you to the right page first
    internal void RunGuide(string topicId)
    {
        HelpTopic topic = null; foreach (var t in HelpContent.All) if (t.Id == topicId) topic = t; if (topic == null) return;
        var steps = new List<TourStep>(); int n = 0;
        foreach (var g in HelpContent.Guide(topic))
        {
            string lb = g[0], text = g[1]; n++; var st = new TourStep(() => TargetOf(lb), n + ". " + Win.Up(lb), text); st.Enter = () => { GoToLabel(lb); }; steps.Add(st);
        }
        if (steps.Count == 0) { Toast("This guide has no buttons to point at. Read it in the Help window."); return; }
        CloseSheetsForGuide(); Win.Bring(this); Tour.Run(this, steps);
    }
}
