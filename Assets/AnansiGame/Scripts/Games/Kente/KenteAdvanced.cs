using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Second half of the kente game (same rules as the HTML prototype, Kente Weaving Prototype.dc.html):
//  - Basic / Free weave / Advanced tabs on the setup card
//  - Advanced weaving: thin threads, row blocks, any strip in any order, tap or swipe, hold to keep weaving
//  - My Kente gallery: every finished cloth, continue weaving, multi-select save / delete, transparent export
//  - the cloth winds onto the loom's front and top beams (the model's fold_bottom / fold_top objects)
public partial class KenteWeavingGame
{
    const int ADV_PER = 8;                 // thin threads in one row block
    const float ADV_TH = ROW_H / ADV_PER;  // one thin thread, stage px
    const float START_OFF = 56f;           // the first row starts this far above the front beam, then the cloth slides down

    bool adv, firstTime;
    int advStripsN = 4, advBlocksN = 8;

    // cloth texture mapping (filled in by BuildLoom)
    int texH = TEX_H; float texBot = BASE, stageU = 0.01f; bool flipCloth, hasFolds;
    int TexRow(int y) => flipCloth ? texH - 1 - y : y;
    float ScrollFor(int r) => Mathf.Max(0, r + 1 - VISIBLE_ROWS) * ROW_H - START_OFF + Mathf.Min(START_OFF, r * ROW_H);

    static readonly Color32 Clear32 = new Color32(0, 0, 0, 0);
    static Color32 Over(Color32 d, Color s)
    {
        float sa = s.a, da = d.a / 255f, oa = sa + da * (1 - sa); if (oa <= 0) return Clear32;
        return new Color32((byte)Mathf.Clamp((s.r * 255f * sa + d.r * da * (1 - sa)) / oa, 0, 255), (byte)Mathf.Clamp((s.g * 255f * sa + d.g * da * (1 - sa)) / oa, 0, 255),
            (byte)Mathf.Clamp((s.b * 255f * sa + d.b * da * (1 - sa)) / oa, 0, 255), (byte)Mathf.Clamp(oa * 255f, 0, 255));
    }
    static string Nums(List<int> l) { var s = new List<string>(); foreach (var i in l) s.Add((i + 1).ToString()); return string.Join(", ", s); }

    // =====================================================================
    // shuttle runs by itself (a tap on the shuttle)
    bool running; float runT, runFrom, runTo; Action runEnd; float classicDownT, classicX0;
    void RunShuttle(float from, float to, Action end) { running = true; runT = Now; runFrom = from; runTo = to; runEnd = end; }
    void UpdateRun(float t)
    {
        if (running)
        {
            float dur = Mathf.Abs(runTo - runFrom) / (RX - LX) * .42f + .06f, k = Mathf.Clamp01((t - runT) / dur);
            shx = Mathf.Lerp(runFrom, runTo, UI.EaseOut(k));
            if (k >= 1) { running = false; shx = runTo; var cb = runEnd; runEnd = null; cb?.Invoke(); }
        }
        if (aRet) { float k = Mathf.Clamp01((t - aRetT) / .18f); shx = Mathf.Lerp(aRetFrom, aRetTo, UI.EaseOut(k)); if (k >= 1) aRet = false; }
    }

    // =====================================================================
    // Advanced weaving
    class AdvState { public int n, blocks, cap, edge, cur, passes; public List<List<string>> strips = new List<List<string>>(); public string[] next; }
    AdvState A;
    bool aDrag, aMoved, aLift, aAuto, aRet; int aE0, aAutoN, aEa, aEb; int[] aCov; string aDir0;
    float aPx, aDownT, aLastMove, aAutoT, aAutoA, aAutoB, aRetT, aRetFrom, aRetTo;
    float advShY = -1, rollFrom, rollTo, rollT0, rollDur = .1f; bool rollInit;
    float stripFullT = -99; readonly List<int> stripFullList = new List<int>();

    float AdvEdge(int i) => LX + i * (RX - LX) / A.n;
    int AdvStripAt(float x) => Mathf.Clamp(Mathf.FloorToInt((x - LX) / (RX - LX) * A.n), 0, A.n - 1);
    float AdvFell(int si) => BASE - A.strips[si].Count * ADV_TH + scroll;
    int AdvTotal() { int k = 0; foreach (var p in A.strips) k += p.Count; return k; }
    void AdvRet(float to) { aRet = true; aRetT = Now; aRetFrom = shx; aRetTo = to; }

    void BeginAdv()
    {
        st = St.Weave; ShowOnly(weaveScr); if (loomGO != null) loomGO.SetActive(true);
        int n = pickN, blocks = pickR; advStripsN = n; advBlocksN = blocks;
        A = new AdvState { n = n, blocks = blocks, cap = blocks * ADV_PER, next = new string[n] };
        for (int i = 0; i < n; i++) A.strips.Add(new List<string>());
        shx = LX; aDrag = aRet = aAuto = running = dragging = returning = false; stripDoneT = -1; beatT = -99; if (stripDoneBadge != null) stripDoneBadge.gameObject.SetActive(false);
        scroll = -START_OFF; rollInit = false; advShY = -1; actT = Now; advMiniKey = ""; clothKey = "";
        SetHint(sel != null ? "Swipe across the strips you want. Hold at the end to keep weaving." : "Pick a thread, then swipe across the strips you want.");
        coachTip.gameObject.SetActive(false); coachRing.gameObject.SetActive(false); coachDot.gameObject.SetActive(false); coachBox.gameObject.SetActive(false);
        ApplyWeaveLayout(); UpdateSpools();
    }

    // From the start edge to the far edge of the strip the finger is in. A tiny nudge weaves nothing.
    int[] AdvSpan(int e0, float px, out int end)
    {
        int n = A.n; float cw = (RX - LX) / n, x0 = AdvEdge(e0), dx = px - x0; end = e0;
        if (Mathf.Abs(dx) < cw * .12f) return new int[0];
        int si = Mathf.Clamp(Mathf.FloorToInt((px - LX) / cw - (dx < 0 ? 1e-4f : -1e-4f)), 0, n - 1);
        var l = new List<int>();
        if (dx > 0) { int last = Mathf.Max(e0, si); for (int k = e0; k <= last; k++) l.Add(k); end = last + 1; }
        else { int first = Mathf.Min(e0 - 1, si); for (int k = first; k < e0; k++) l.Add(k); end = first; }
        return l.ToArray();
    }
    List<int> AdvWouldWeave(int[] cov, string dir) { var r = new List<int>(); foreach (var si in cov) if (A.strips[si].Count < A.cap && (string.IsNullOrEmpty(A.next[si]) || A.next[si] == dir)) r.Add(si); return r; }

    // One pass of the shuttle in direction dir ("R" / "L"). Each strip alternates: a strip due to go the other way is passed over.
    int AdvPass(int[] cov, string dir, int endEdge)
    {
        var woven = new List<int>(); var skipped = new List<int>(); var full = new List<int>();
        foreach (int si in cov) { if (A.strips[si].Count >= A.cap) full.Add(si); else if (!string.IsNullOrEmpty(A.next[si]) && A.next[si] != dir) skipped.Add(si); else woven.Add(si); }
        if (woven.Count == 0 && full.Count > 0) // a finished strip can't take more thread: it snaps
        {
            int lo = Mathf.Min(full.ToArray()), hi = Mathf.Max(full.ToArray());
            snapA0 = AdvEdge(lo); snapB0 = AdvEdge(hi + 1); snapY = AdvFell(lo) - 4; snapT = Now; shakeT = Now;
            SetHint(full.Count > 1 ? $"Snap! Strips {Nums(full)} are finished. Weave another strip, or press Finish." : $"Snap! Strip {lo + 1} is finished. Weave another strip, or press Finish.", true);
            return 0;
        }
        foreach (int si in woven) { A.strips[si].Add(sel); A.next[si] = dir == "R" ? "L" : "R"; }
        A.edge = endEdge; A.cur = dir == "R" ? Mathf.Max(cov) : Mathf.Min(cov); // the strip the shuttle ended on
        if (woven.Count > 0) { A.passes++; beatT = Now; }
        var justDone = new List<int>(); foreach (var si in woven) if (A.strips[si].Count >= A.cap) justDone.Add(si);
        if (justDone.Count > 0) { stripFullT = Now; stripFullList.Clear(); stripFullList.AddRange(justDone); }
        actT = Now;
        bool allFull = true; foreach (var p in A.strips) if (p.Count < A.cap) allFull = false;
        string Side(string d) => d == "R" ? "right" : "left";
        if (skipped.Count > 0 && woven.Count == 0) SetHint($"Strip {Nums(skipped)} goes {Side(A.next[skipped[0]])} next, so the shuttle passed over it.", true);
        else if (skipped.Count > 0) SetHint($"Woven. Strip {Nums(skipped)} was passed over: it goes {Side(A.next[skipped[0]])} next.");
        else if (justDone.Count > 0 && !allFull) SetHint($"Strip {Nums(justDone)} finished! Its blocks are full.");
        else if (A.passes == 1) SetHint($"{byId[sel].name}. One thin thread. Tap the shuttle to keep weaving this strip, or tap another strip to move.");
        else if (allFull) SetHint("Every block is full. Press Finish to see your cloth.");
        else { foreach (var si in woven) if (A.strips[si].Count % ADV_PER == 0) { SetHint($"Block {A.strips[si].Count / ADV_PER} of strip {si + 1} is full. Next block starts above it."); break; } }
        return woven.Count;
    }

    void AdvPointerDown(Vector2 p)
    {
        if (running || aDrag) return;
        if (sel == null) { shakeT = Now; SetHint("First a thread. Tap a spool on the right.", true); return; }
        float x = Mathf.Clamp(PanelToLoomXAt(p.x, advShY), LX, RX), ex = AdvEdge(A.edge);
        if (Mathf.Abs(x - ex) > Mathf.Max(46f, (RX - LX) / A.n * .45f)) { AdvSelect(AdvStripAt(x)); return; } // tapping another strip moves the shuttle there
        aRet = false; aDrag = true; aE0 = A.edge; aPx = ex; aDownT = Now; aLastMove = Now; aMoved = aLift = aAuto = false; shx = ex; actT = Now;
    }
    void AdvPointerDrag(Vector2 p)
    {
        float nx = Mathf.Clamp(PanelToLoomXAt(p.x, advShY), LX, RX);
        if (Mathf.Abs(nx - aPx) > 1.5f) { aLastMove = Now; if (aAuto) AdvStopAuto(); }
        if (!aLift && Mathf.Abs(nx - AdvEdge(aE0)) > 8) aMoved = true;
        aPx = nx; if (!aAuto) shx = nx;
    }
    void AdvPointerUp()
    {
        aDrag = false;
        if (aAuto) { AdvStopAuto(); SetHint($"{AdvTotal()} threads so far. Pick a colour and swipe again, or press Finish."); return; }
        if (aLift) // lifted shuttle: moves to the nearest strip edge without weaving
        {
            int e = Mathf.RoundToInt((aPx - LX) / (RX - LX) * A.n);
            A.edge = e; A.cur = Mathf.Clamp(e < A.n ? e : e - 1, 0, A.n - 1);
            AdvRet(AdvEdge(A.edge)); SetHint("Shuttle moved. Swipe from here to weave."); return;
        }
        if (!aMoved && Now - aDownT < .3f) // tap: weave the current strip in the direction it is due; the shuttle stays on it
        {
            int e0 = aE0, si = A.cur; string dir = e0 == si ? "R" : "L"; int end = dir == "R" ? si + 1 : si;
            RunShuttle(AdvEdge(e0), AdvEdge(end), () => { AdvPass(new[] { si }, dir, end); if (A.edge != end) AdvRet(AdvEdge(A.edge)); });
            return;
        }
        var cov = AdvSpan(aE0, aPx, out int end2);
        if (cov.Length > 0) { AdvPass(cov, aPx > AdvEdge(aE0) ? "R" : "L", end2); AdvRet(AdvEdge(A.edge)); } // glides out to the strip's edge (or back, if it snapped)
        else { AdvRet(AdvEdge(aE0)); SetHint("Swipe across a strip. The shuttle always runs edge to edge.", true); }
    }
    void AdvStopAuto() { aAuto = false; AdvRet(AdvEdge(A.edge)); }

    // the shuttle rests on the side of the current strip it is due to leave from
    void AdvPlace() { A.edge = A.next[A.cur] == "L" ? A.cur + 1 : A.cur; AdvRet(AdvEdge(A.edge)); }
    void AdvSelect(int si)
    {
        if (A == null || aDrag || running) return; si = Mathf.Clamp(si, 0, A.n - 1);
        if (si == A.cur) return; A.cur = si; AdvPlace();
        SetHint(A.strips[si].Count >= A.cap ? $"Strip {si + 1} is finished." : $"Strip {si + 1}. Tap the shuttle or swipe.");
    }
    void AdvFill()
    {
        if (A == null || aDrag || running) return;
        if (sel == null) { shakeT = Now; SetHint("First a thread. Tap a spool on the right.", true); return; }
        var p = A.strips[A.cur]; if (p.Count >= A.cap) { SetHint($"Strip {A.cur + 1} is finished.", true); return; }
        int k = ADV_PER - (p.Count % ADV_PER);
        for (int i = 0; i < k; i++) { p.Add(sel); var d0 = string.IsNullOrEmpty(A.next[A.cur]) ? (A.edge == A.cur ? "R" : "L") : A.next[A.cur]; A.next[A.cur] = d0 == "R" ? "L" : "R"; }
        A.passes += k; AdvPlace(); beatT = Now; actT = Now;
        if (p.Count >= A.cap) { stripFullT = Now; stripFullList.Clear(); stripFullList.Add(A.cur); }
        SetHint($"{byId[sel].name} block filled on strip {A.cur + 1}.");
    }
    void AdvUndo()
    {
        if (A == null || aDrag || running) return;
        var p = A.strips[A.cur]; if (p.Count == 0) { SetHint($"Strip {A.cur + 1} has nothing to unpick.", true); return; }
        p.RemoveAt(p.Count - 1); A.next[A.cur] = p.Count > 0 ? (A.next[A.cur] == "R" ? "L" : "R") : null; A.passes++;
        AdvPlace(); SetHint($"Unpicked a thread on strip {A.cur + 1}.");
    }
    void AdvUndoBlock()
    {
        if (A == null || aDrag || running) return;
        var p = A.strips[A.cur]; if (p.Count == 0) { SetHint($"Strip {A.cur + 1} has nothing to unpick.", true); return; }
        int keep = (p.Count - 1) / ADV_PER * ADV_PER, k = p.Count - keep; p.RemoveRange(keep, k);
        if (k % 2 == 1) A.next[A.cur] = A.next[A.cur] == "R" ? "L" : "R";
        if (p.Count == 0) A.next[A.cur] = null; A.passes++;
        AdvPlace(); SetHint($"Unpicked a block on strip {A.cur + 1}.");
    }
    void AdvFinish()
    {
        if (A == null || aDrag || running) return;
        if (AdvTotal() == 0) { SetHint("Weave at least one thread first.", true); return; }
        var nx = new string[A.n]; for (int i = 0; i < A.n; i++) nx[i] = A.next[i] ?? "";
        GalleryAdd(new GEntry { mode = "adv", n = A.n, blocks = A.blocks, strips = PackStrips(A.strips), next = nx });
        if (loomGO != null) loomGO.SetActive(false);
        st = St.End; ShowOnly(endScr); endT = Now;
        endTitle.text = "Your kente"; endBody.text = "Saved to My Kente. Advanced cloths are just for fun, so this one stays out of your Gifts.";
        againLbl.text = "Weave another"; SetEndIcon(A.strips); EndExtras();
    }

    void UpdateWeaveAdv(float t)
    {
        if (aDrag)
        {
            if (!aMoved && !aLift && t - aDownT > .45f) { aLift = true; SetHint("Shuttle lifted. Slide it to any strip edge. Nothing is woven."); }
            if (!aAuto && aMoved && !aLift)
            {
                var cov = AdvSpan(aE0, aPx, out int end);
                if (cov.Length > 0 && t - aLastMove > .38f) // held still at the end of a swipe: the helper keeps weaving
                {
                    aDir0 = aPx > AdvEdge(aE0) ? "R" : "L"; AdvPass(cov, aDir0, end);
                    aAuto = true; aAutoT = t; aAutoN = 0; aEa = aE0; aEb = end; aCov = cov; aAutoA = AdvEdge(aE0); aAutoB = AdvEdge(end);
                }
            }
            if (aAuto)
            {
                int k = Mathf.FloorToInt((t - aAutoT) / .22f); string back = aDir0 == "R" ? "L" : "R";
                while (aAuto && aAutoN < k) { aAutoN++; if (AdvPass(aCov, aAutoN % 2 == 1 ? back : aDir0, aAutoN % 2 == 1 ? aEa : aEb) == 0 && snapT == Now) AdvStopAuto(); }
                if (aAuto) { float ph = ((t - aAutoT) % .44f) / .44f, tri = ph < .5f ? ph * 2 : 2 - ph * 2; shx = aAutoB + (aAutoA - aAutoB) * tri; }
            }
        }
        // roll the cloth so the current strip's weaving edge stays on the loom; a big change (switching strips) turns the beams slowly
        float tgt = Mathf.Max(0, A.strips[A.cur].Count * ADV_TH - (BASE - WIN_TOP - START_OFF - 70)) - START_OFF;
        if (!rollInit || Mathf.Abs(rollTo - tgt) > .5f) { float dist = Mathf.Abs(tgt - scroll); rollFrom = scroll; rollTo = tgt; rollT0 = t; rollDur = dist > ROW_H ? Mathf.Min(1.1f, .52f + dist * .0016f) : .14f; rollInit = true; }
        { float k = Mathf.Clamp01((t - rollT0) / rollDur), e = k < .5f ? 2 * k * k : 1 - Mathf.Pow(-2 * k + 2, 2) / 2; scroll = Mathf.Lerp(rollFrom, rollTo, e); }
        int si = A.cur;
        if (aDrag && !aLift && aMoved) { var cov = AdvSpan(aE0, aPx, out _); if (cov.Length > 0) si = aPx > AdvEdge(aE0) ? cov[cov.Length - 1] : cov[0]; }
        float want = Mathf.Clamp(AdvFell(si) - ADV_TH / 2, WIN_TOP + 44, BASE - 48); // batten + shuttle never leave the window while the cloth rolls
        advShY = advShY < 0 ? want : Mathf.Lerp(advShY, want, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 7f));

        int fullBlocks = 0; foreach (var p in A.strips) fullBlocks += p.Count / ADV_PER;
        pillText.text = $"{A.n} strip{(A.n > 1 ? "s" : "")} × {A.blocks} blocks   <color=#4F7C3A>{fullBlocks} of {A.n * A.blocks} full</color>";
        hintText.text = hint; hintText.color = hintBad ? Red : Ink;
        var cp = A.strips[A.cur];
        advCurLbl.text = $"Strip {A.cur + 1} of {A.n}"; UI.Alpha(advPrevImg, A.cur > 0 ? 1 : .35f); UI.Alpha(advNextImg, A.cur < A.n - 1 ? 1 : .35f);
        advFillLbl.text = $"Fill block {Mathf.Min(A.blocks, cp.Count / ADV_PER + 1)}"; advFillSw.color = sel != null ? byId[sel].color : new Color(0, 0, 0, 0);
        advFillGrp.alpha = sel != null && cp.Count < A.cap ? 1 : .4f; advUndoGrp.alpha = advUndoBlkGrp.alpha = cp.Count > 0 ? 1 : .4f; advFinishGrp.alpha = AdvTotal() > 0 ? 1 : .5f;
        DrawAdvMini();
        float se = t - shakeT; weaveScr.anchoredPosition = new Vector2(se < .3f ? Mathf.Sin(se * 90f) * 8f * (1 - se / .3f) : 0, 0);
        float ae = t - hintT; ananseSmall.rectTransform.anchoredPosition = new Vector2(150, AnanseBaseY(ananseSmall.rectTransform.sizeDelta.y) + (ae < .5f ? 7f * Mathf.Abs(Mathf.Sin(ae / .5f * Mathf.PI * 2)) : 0));
        stripDoneBadge.gameObject.SetActive(false);
        UpdateLoom(t); UpdateSnap(t);
        bool on = !(aDrag || running || aRet) && cp.Count < A.cap && loomCam != null; // arrow on the shuttle: which way this strip goes next
        advArrow.gameObject.SetActive(on);
        if (on) { var p = LoomToStage(shx, advShY - 34); UI.TL(advArrow, p.x - 17, p.y - 17, 34, 34); advArrowTri.localScale = new Vector3(A.edge == A.cur ? 1 : -1, 1, 1); }
    }

    // ---------- Advanced cloth on the loom ----------
    readonly Color32[] rowTpl = new Color32[TEX_W];
    static readonly Color[] StripTints = { new Color(232 / 255f, 163 / 255f, 61 / 255f, .13f), new Color(79 / 255f, 124 / 255f, 58 / 255f, .13f), new Color(176 / 255f, 58 / 255f, 46 / 255f, .12f), new Color(47 / 255f, 95 / 255f, 150 / 255f, .13f) };
    void DrawClothAdv(float t)
    {
        int n = A.n; float colW = 400f / n, step = 400f / 36;
        List<int> pend = null;
        if (aDrag && aMoved && !aLift && !aAuto && sel != null) { var cov = AdvSpan(aE0, aPx, out _); pend = AdvWouldWeave(cov, aPx > AdvEdge(aE0) ? "R" : "L"); }
        float fl = t - stripFullT, flash = fl < .9f ? 1 - fl / .9f : 0;
        var sb = new StringBuilder();
        foreach (var p in A.strips) sb.Append(p.Count).Append(',');
        sb.Append('|').Append(A.cur).Append('|').Append(A.passes).Append('|').Append(Mathf.RoundToInt(scroll * 2)).Append('|').Append(Mathf.RoundToInt(flash * 12));
        if (pend != null) { sb.Append('|'); foreach (var i in pend) sb.Append(i).Append('.'); sb.Append(sel); }
        string key = sb.ToString(); if (key == clothKey) return; clothKey = key;

        Color32 warpA = new Color32(246, 236, 217, 255), warpB = new Color32(160, 130, 90, 230), gold = Gold;
        for (int x = 0; x < TEX_W; x++) // warp threads, each strip's tint, and the current strip's glow (all UNDER the woven threads)
        {
            float xf = x + .5f; int wi = Mathf.FloorToInt(xf / step); float lx = xf - (wi + .5f) * step + 2;
            Color32 c = lx >= 0 && lx < 4 ? warpA : lx >= 4 && lx < 5 ? warpB : Clear32;
            int si = Mathf.Min(n - 1, (int)(xf / colW));
            if (n > 1) c = Over(c, StripTints[si % 4]);
            if (si == A.cur) { c = Over(c, new Color(1f, .84f, .47f, .34f)); float ix = xf - si * colW; if (ix < 3 || ix > colW - 3) c = gold; }
            rowTpl[x] = c;
        }
        for (int y = 0; y < texH; y++) Array.Copy(rowTpl, 0, clothPx, y * TEX_W, TEX_W);
        for (int si = 0; si < n; si++)
        {
            int x0 = Mathf.RoundToInt(si * colW) + (si > 0 ? 1 : 0), x1 = Mathf.RoundToInt((si + 1) * colW) - (si < n - 1 ? 1 : 0);
            var p = A.strips[si]; int cnt = p.Count + (pend != null && pend.Contains(si) ? 1 : 0);
            for (int k = 0; k < cnt; k++)
            {
                bool ghost = k >= p.Count; string id = ghost ? sel : p[k];
                float top = BASE - (k + 1) * ADV_TH + scroll;
                int y0 = Mathf.RoundToInt(texBot - (top + ADV_TH)), y1 = Mathf.RoundToInt(texBot - top);
                if (y1 <= 0 || y0 >= texH) continue; y0 = Mathf.Max(0, y0); y1 = Mathf.Min(texH, Mathf.Max(y1, y0 + 1));
                Color32 c = byId[id].color, lt = Lighten(c, .2f), dk = Mul(c, .84f);
                float off = (k % 2 == 1 ? step / 2 : 0) + step * .5f - 1.5f;
                for (int y = y0; y < y1; y++)
                {
                    int row = TexRow(y) * TEX_W;
                    for (int x = x0; x < x1; x++)
                    {
                        float wx = ((x - off) % step + step) % step; Color32 col = wx < 3 ? lt : c; if (y == y0) col = dk; // warp peeks through, offset every other pick
                        clothPx[row + x] = ghost ? Over(clothPx[row + x], new Color(col.r / 255f, col.g / 255f, col.b / 255f, .6f)) : col;
                    }
                }
            }
            if (flash > 0 && stripFullList.Contains(si))
                for (int y = 0; y < texH; y++) for (int x = x0; x < x1; x++) clothPx[y * TEX_W + x] = Over(clothPx[y * TEX_W + x], new Color(1f, .88f, .55f, .55f * flash));
        }
        var ink = new Color(61 / 255f, 36 / 255f, 22 / 255f, .8f);
        for (int si = 1; si < n; si++) { int xc = Mathf.RoundToInt(si * colW); for (int y = 0; y < texH; y++) for (int x = xc - 1; x < xc + 1; x++) if (x >= 0 && x < TEX_W) clothPx[y * TEX_W + x] = Over(clothPx[y * TEX_W + x], ink); }
        var tick = new Color(ink.r, ink.g, ink.b, .35f); // small ticks mark each block
        for (int b = 1; b <= A.blocks; b++)
        {
            int ty = Mathf.RoundToInt(texBot - (BASE - b * ADV_PER * ADV_TH + scroll)); if (ty < 0 || ty >= texH) continue;
            int row = TexRow(ty) * TEX_W;
            for (int si = 0; si <= n; si++) for (int x = Mathf.RoundToInt(si * colW) - 5; x < Mathf.RoundToInt(si * colW) + 5; x++) if (x >= 0 && x < TEX_W) clothPx[row + x] = Over(clothPx[row + x], tick);
        }
        clothTex.SetPixels32(clothPx); clothTex.Apply(false);
    }

    // ---------- left panel: mini cloth ----------
    const int MW = 220, MH = 138;
    RawImage advMini; Texture2D advMiniTex; Color32[] advMiniPx; string advMiniKey = ""; float miniOx, miniSw;
    void MiniRect(float x, float y, float w, float h, Color c)
    {
        int x0 = Mathf.Max(0, Mathf.RoundToInt(x)), x1 = Mathf.Min(MW, Mathf.RoundToInt(x + w)), y0 = Mathf.Max(0, Mathf.RoundToInt(y)), y1 = Mathf.Min(MH, Mathf.RoundToInt(y + h));
        if (y1 <= y0 && h > 0) y1 = Mathf.Min(MH, y0 + 1);
        for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) advMiniPx[yy * MW + xx] = c.a >= 1 ? (Color32)c : Over(advMiniPx[yy * MW + xx], c);
    }
    void DrawAdvMini()
    {
        var sb = new StringBuilder(); foreach (var p in A.strips) sb.Append(p.Count).Append(','); sb.Append(A.cur).Append('|').Append(A.passes);
        string key = sb.ToString(); if (key == advMiniKey) return; advMiniKey = key;
        int N = A.n, B = A.blocks; float u = Mathf.Min((MW - 14) / (2f * N), (MH - 14) / (float)B), sw = 2 * u, cw = sw * N, ch = u * B, th = u / ADV_PER;
        float ox = Mathf.Round((MW - cw) / 2), oy = Mathf.Round((MH - ch) / 2); miniOx = ox; miniSw = sw;
        for (int i = 0; i < advMiniPx.Length; i++) advMiniPx[i] = Clear32;
        MiniRect(ox - 4, oy - 4, cw + 8, ch + 8, Ink); MiniRect(ox, oy, cw, ch, new Color32(234, 220, 195, 255));
        for (int si = 0; si < N; si++) for (int k = 0; k < A.strips[si].Count; k++) MiniRect(ox + si * sw, oy + k * th, sw, th + .6f, byId[A.strips[si][k]].color);
        for (int b = 1; b < B; b++) MiniRect(ox, oy + b * u - .5f, cw, 1, new Color(61 / 255f, 36 / 255f, 22 / 255f, .3f));
        for (int si = 1; si < N; si++) MiniRect(ox + si * sw - 1, oy, 2, ch, Ink);
        float cx = ox + A.cur * sw; MiniRect(cx, oy, sw, 2, Gold); MiniRect(cx, oy + ch - 2, sw, 2, Gold); MiniRect(cx, oy, 2, ch, Gold); MiniRect(cx + sw - 2, oy, 2, ch, Gold);
        advMiniTex.SetPixels32(advMiniPx); advMiniTex.Apply(false);
    }
    public void MiniClick(float x) { if (A == null || miniSw <= 0) return; int si = Mathf.FloorToInt((x - miniOx) / miniSw); if (si >= 0 && si < A.n) AdvSelect(si); }

    // ---------- size preview grid (one texture: identical 1px lines at any screen scale) ----------
    RawImage previewRaw; Texture2D previewTex;
    [Tooltip("Bottom beam: cloth winds from the top of the beam down over the front (anticlockwise seen from the right). Untick if it ever looks reversed.")]
    public bool bottomFoldFlip = true;
    void DrawPreviewGrid(float u, Color[] tones)
    {
        const int S = 4; // texels per stage px
        int cw = Mathf.RoundToInt(u * 2) * S, ch = Mathf.RoundToInt(u) * S, W = cw * pickN, H = ch * pickR;
        if (previewRaw == null)
        {
            previewRaw = UI.Node("Grid", previewBox).gameObject.AddComponent<RawImage>(); previewRaw.raycastTarget = false;
        }
        previewRaw.gameObject.SetActive(true);
        UI.TL(previewRaw.rectTransform, 3, 3, W / (float)S, H / (float)S);
        if (previewTex != null) Destroy(previewTex);
        previewTex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var px = new Color32[W * H]; Color32 line = new Color32(43, 24, 12, 255);
        for (int x = 0; x < W; x++)
        {
            int si = x / cw, lx = x % cw; Color32 col = tones[si % tones.Length];
            bool vLine = si > 0 && lx < 2 * S; // solid 2 stage px line on the left of every strip but the first
            for (int y = 0; y < H; y++)
            {
                int ly = y % ch; bool hLine = y >= ch && ly < 2 * S; // solid 2 stage px line between every block row
                px[y * W + x] = vLine || hLine ? line : col;
            }
        }
        previewTex.SetPixels32(px); previewTex.Apply(true); previewRaw.texture = previewTex;
    }

    // ---------- Advanced UI ----------
    GameObject clothPanelGO, advPanelGO; Text advCurLbl, advFillLbl; Image advFillSw, advPrevImg, advNextImg;
    CanvasGroup advFillGrp, advUndoGrp, advUndoBlkGrp, advFinishGrp; RectTransform advArrow, advArrowTri;
    static Sprite triS;
    static Sprite TriSprite { get { if (triS == null) triS = UI.Tex(64, (x, y) => Mathf.Min(x - 14f, (54f - x) * .6f - Mathf.Abs(y - 32f)) + .5f, Vector4.zero); return triS; } }
    void Tri(Transform parent, bool right) { var t = UI.Img("Arrow", parent, TriSprite, Ink); UI.Center(t.rectTransform, 0, 0, 16, 16).localScale = new Vector3(right ? 1 : -1, 1, 1); }

    void BuildAdvWeaveUI()
    {
        var lp = UI.Panel("AdvClothPanel", weaveScr, Cream, 30, Ink); UI.TL(lp.rectTransform, 40, 96, 256, 460); UI.KenteBand(lp.rectTransform, 30); advPanelGO = lp.gameObject;
        var tt = UI.Label(lp.transform, "Your cloth", 26, Brown, TextAnchor.MiddleCenter, font); UI.TL(tt.rectTransform, 0, 46, 256, 34);
        advMiniTex = new Texture2D(MW, MH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear }; advMiniPx = new Color32[MW * MH];
        advMini = UI.Node("MiniCloth", lp.transform).gameObject.AddComponent<RawImage>(); advMini.texture = advMiniTex; UI.TL(advMini.rectTransform, 18, 86, MW, MH);
        advMini.raycastTarget = true; advMini.gameObject.AddComponent<KenteMiniInput>().game = this;
        var sw = UI.Panel("StripSwitcher", lp.transform, Ink, 16, null); UI.TL(sw.rectTransform, 18, 234, 220, 46);
        var prev = UI.Btn(sw.transform, "", Gold, Ink, null, () => AdvSelect(A.cur - 1), 16, font, null, 12); UI.TL((RectTransform)prev.transform, 4, 4, 38, 38); advPrevImg = prev.GetComponent<Image>(); Tri(prev.transform, false);
        var next = UI.Btn(sw.transform, "", Gold, Ink, null, () => AdvSelect(A.cur + 1), 16, font, null, 12); UI.TL((RectTransform)next.transform, 178, 4, 38, 38); advNextImg = next.GetComponent<Image>(); Tri(next.transform, true);
        var wl = UI.Label(sw.transform, "WEAVING", 10, Gold, TextAnchor.MiddleCenter, font); UI.TL(wl.rectTransform, 44, 5, 132, 14);
        advCurLbl = UI.Label(sw.transform, "", 18, Cream, TextAnchor.MiddleCenter, font); UI.TL(advCurLbl.rectTransform, 44, 18, 132, 24);
        var fill = UI.Btn(lp.transform, "", CardBg, Brown, Tan, AdvFill, 16, font, Ink, 16); UI.TL((RectTransform)fill.transform, 18, 290, 220, 44); advFillGrp = fill.gameObject.AddComponent<CanvasGroup>();
        advFillLbl = fill.GetComponentInChildren<Text>(); advFillLbl.alignment = TextAnchor.MiddleLeft; advFillLbl.rectTransform.offsetMin = new Vector2(50, 0);
        advFillSw = UI.Panel("Swatch", fill.transform, new Color(0, 0, 0, 0), 5, Ink, 2.5f); UI.TL(advFillSw.rectTransform, 20, 12, 20, 20);
        var u1 = UI.Btn(lp.transform, "Undo thread", CardBg, Brown, Tan, AdvUndo, 15, font, Ink, 16); UI.TL((RectTransform)u1.transform, 18, 342, 106, 44); advUndoGrp = u1.gameObject.AddComponent<CanvasGroup>();
        var u2 = UI.Btn(lp.transform, "Undo block", CardBg, Brown, Tan, AdvUndoBlock, 15, font, Ink, 16); UI.TL((RectTransform)u2.transform, 132, 342, 106, 44); advUndoBlkGrp = u2.gameObject.AddComponent<CanvasGroup>();
        var fin = UI.Btn(lp.transform, "Finish cloth", Green, Cream, GreenDark, AdvFinish, 20, font, Ink, 18); UI.TL((RectTransform)fin.transform, 18, 396, 220, 48); advFinishGrp = fin.gameObject.AddComponent<CanvasGroup>();
        advPanelGO.SetActive(false);
        var ar = UI.Panel("DirArrow", weaveScr, Gold, 17, Ink, 3); advArrow = ar.rectTransform; UI.TL(advArrow, 0, 0, 34, 34);
        var tri = UI.Img("Tri", ar.transform, TriSprite, Ink); advArrowTri = UI.Center(tri.rectTransform, 1, 0, 14, 14);
        ar.gameObject.SetActive(false);
    }
    void ApplyWeaveLayout()
    {
        if (clothPanelGO != null) clothPanelGO.SetActive(!adv);
        if (advPanelGO != null) advPanelGO.SetActive(adv);
        if (advArrow != null) advArrow.gameObject.SetActive(false);
    }

    // =====================================================================
    // Setup card: Basic / Free weave / Advanced
    Text setupHint; readonly Text[] stepLbl = new Text[4], stepSub = new Text[4];
    RectTransform tabsRT, tabHi; float tabHiX = -1; GameObject tutStepsGO, myKenteSetupBtn;
    void BuildSetupExtras(RectTransform card, int firstContent)
    {
        card.sizeDelta = new Vector2(900, 604);
        for (int i = firstContent; i < card.childCount; i++) { var c = (RectTransform)card.GetChild(i); c.anchoredPosition += new Vector2(0, -44); }
        var bg = UI.Panel("ModeTabs", card, CardBg, 23, null); tabsRT = UI.TL(bg.rectTransform, 220, 148, 460, 46);
        var hi = UI.Panel("Selected", bg.transform, Gold, 19, Ink, 3); tabHi = UI.TL(hi.rectTransform, 6, 4, 148, 38);
        string[] names = { "Basic", "Free weave", "Advanced" };
        for (int i = 0; i < 3; i++) { int k = i; var b = UI.Btn(bg.transform, names[i], new Color(0, 0, 0, 0), Ink, null, () => SetMode(k), 17, font, null, 19); UI.TL((RectTransform)b.transform, 5 + i * 150, 4, 150, 38); }
        var how = UI.TL(UI.Node("HowTo", card), 90, 214, 420, 240); tutStepsGO = how.gameObject;
        var ht = UI.Label(how, "How to weave it", 20, Brown, TextAnchor.MiddleLeft, font); UI.TL(ht.rectTransform, 0, 0, 400, 28);
        string[] steps = { "The pattern card shows the colour for every row.", "Tap that thread to load the shuttle.", "Swipe or tap the shuttle to weave the row." };
        for (int i = 0; i < 3; i++)
        {
            var dot = UI.Panel("Step", how, Gold, 13, null); UI.TL(dot.rectTransform, 0, 44 + i * 58, 26, 26);
            var dn = UI.Label(dot.transform, (i + 1).ToString(), 14, Ink, TextAnchor.MiddleCenter, font); UI.Stretch(dn.rectTransform);
            var tx = UI.Label(how, steps[i], 16, Ink, TextAnchor.UpperLeft, font); UI.TL(tx.rectTransform, 38, 46 + i * 58, 360, 44);
        }
        var mk = UI.Btn(card, "My Kente", CardBg, Ink, Tan, OpenGallery, 17, font, Ink, 18); UI.TL((RectTransform)mk.transform, 716, 60, 150, 44); myKenteSetupBtn = mk.gameObject;
    }
    void SetMode(int k)
    {
        if (st != St.Setup) return;
        bool nAdv = k == 2, nFree = k == 1; if (nAdv == adv && nFree == free) return;
        adv = nAdv; free = nFree; ToSetup();
    }
    void SetupLayout()
    {
        bool basic = !free && !adv;
        setupTitle.text = adv ? "Advanced weaving" : free ? "How big is your kente?" : firstTime ? "Your first kente" : "Basic kente";
        setupSub.text = adv ? "Choose the strips and row blocks. You finish the cloth when it looks right."
            : free ? "Choose the strips and rows, then weave any colours you like."
            : firstTime ? "Your first kente is set: 3 strips, 6 rows each. Follow the pattern and Kwaku Ananse will guide you."
            : "Pick a kente to weave. The pattern card shows the colour for every row.";
        tabsRT.gameObject.SetActive(!firstTime); myKenteSetupBtn.SetActive(!firstTime);
        presetsRow.SetActive(free); steppersGO.SetActive(free || adv); patternsGO.SetActive(basic && !firstTime); tutStepsGO.SetActive(basic && firstTime);
        if (stepLbl[2] != null) stepLbl[2].text = adv ? "Row blocks" : "Rows per strip";
        if (stepSub[2] != null) stepSub[2].text = adv ? $"2 to 24 blocks. Each holds {ADV_PER} thin threads" : "4 to 24 rows, woven bottom to top";
        setupHint.text = adv ? $"One swipe, one thin thread. {ADV_PER} threads fill a block. Just for fun: saved to My Kente." : "Old weavers say the Sky God is kind to a generous cloth…";
    }
    void UpdateSetupTabs(float t)
    {
        if (tabHi == null || !tabsRT.gameObject.activeInHierarchy) return;
        int idx = adv ? 2 : free ? 1 : 0; float target = 6 + idx * 150;
        tabHiX = tabHiX < 0 ? target : Mathf.Lerp(tabHiX, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 14f)); // the gold pill slides to the chosen tab
        tabHi.anchoredPosition = new Vector2(tabHiX, tabHi.anchoredPosition.y);
    }
    void RefreshSetup()
    {
        bool canStep = free || adv; int minR = adv ? 2 : 4;
        valN.text = pickN.ToString(); valR.text = pickR.ToString();
        UI.Alpha(stepBtns[0], canStep && pickN > 1 ? 1 : .35f); UI.Alpha(stepBtns[1], canStep && pickN < 12 ? 1 : .35f);
        UI.Alpha(stepBtns[2], canStep && pickR > minR ? 1 : .35f); UI.Alpha(stepBtns[3], canStep && pickR < 24 ? 1 : .35f);
        // whole-pixel cells so every division line is the same 1px (fractional sizes made some lines vanish)
        float u = Mathf.Max(4f, Mathf.Floor(230f / Mathf.Max(2 * pickN, pickR)));
        previewBox.sizeDelta = new Vector2(u * 2 * pickN + 6, u * pickR + 6);
        foreach (var c in previewCols) Destroy(c.gameObject); previewCols.Clear();
        if (previewRaw != null) previewRaw.gameObject.SetActive(canStep);
        if (!canStep)
        {
            var pat = KenteData.Patterns[KenteData.PatternIndex]; float sw = u * 2f;
            for (int si = 0; si < 3; si++) for (int ri = 0; ri < pickR; ri++)
                { var im = UI.Img("Cell", previewBox, null, byId[pat.strips[si][ri]].color); UI.TL(im.rectTransform, 3 + si * sw, 3 + (pickR - 1 - ri) * u, sw, u + .5f); previewCols.Add(im); }
            shapeLbl.text = pat.name; infoLbl.text = firstTime ? "3 strips · 6 rows each" : pat.tag + " · 18 rows to weave";
            for (int k = 0; k < patternBgs.Count; k++) { bool on = k == KenteData.PatternIndex; patternBgs[k].color = on ? CardOn : CardBg; patternRings[k].SetActive(on); }
        }
        else
        {
            // one cell per block (Advanced) or per row (Free), with a thin line between them
            var tones = new[] { Gold, Green, Red, (Color)new Color32(47, 93, 138, 255), (Color)new Color32(43, 29, 20, 255), Gold };
            DrawPreviewGrid(u, tones);
            float ratio = 2f * pickN / pickR;
            shapeLbl.text = Mathf.Abs(ratio - 1) < .01f ? "Square cloth" : ratio > 1 ? "Wide cloth" : "Long cloth";
            infoLbl.text = adv ? $"{pickN * pickR} blocks · {pickN * pickR * ADV_PER} threads" : (pickN * pickR) + " rows to weave in total";
        }
        for (int i = 0; i < presetBgs.Count; i++) { var p = Presets[i]; presetBgs[i].color = pickN == p.n && pickR == p.r ? Gold : CardBg; }
    }

    // =====================================================================
    // End screen extras + transparent export
    bool transparentBg; readonly List<GameObject> chkMarks = new List<GameObject>(); GameObject endMyKente;
    void MakeCheck(Transform parent, float x, float y, float w, string label)
    {
        var row = UI.Btn(parent, "", new Color(0, 0, 0, 0), Ink, null, () => { transparentBg = !transparentBg; foreach (var m in chkMarks) m.SetActive(transparentBg); }, 16, font, null, 8);
        UI.TL((RectTransform)row.transform, x, y, w, 30);
        var box = UI.Panel("Box", row.transform, Cream, 6, Ink, 3); UI.TL(box.rectTransform, 0, 1, 28, 28);
        var on = UI.Panel("On", box.transform, Green, 4, null); UI.Stretch(on.rectTransform, 4);
        var a = UI.Img("c1", on.transform, null, Cream); UI.Center(a.rectTransform, -3, -1, 3, 8).localRotation = Quaternion.Euler(0, 0, 45);
        var b = UI.Img("c2", on.transform, null, Cream); UI.Center(b.rectTransform, 2, 1, 3, 13).localRotation = Quaternion.Euler(0, 0, -40);
        on.gameObject.SetActive(transparentBg); chkMarks.Add(on.gameObject);
        var t = row.GetComponentInChildren<Text>(); t.text = label; t.alignment = TextAnchor.MiddleLeft; t.fontSize = 16; t.rectTransform.offsetMin = new Vector2(40, 0);
    }
    void BuildEndExtras(RectTransform card)
    {
        card.sizeDelta = new Vector2(560, 612);
        MakeCheck(card, 40, 492, 480, "Transparent background (just the cloth)");
        var l = UI.Btn(card, "Open My Kente", new Color(0, 0, 0, 0), Brown, null, OpenGallery, 18, font, null, 12); UI.TL((RectTransform)l.transform, 170, 540, 220, 40); endMyKente = l.gameObject;
    }
    void EndExtras() { if (endMyKente != null) endMyKente.SetActive(!firstTime); }
    void SetEndIcon(List<List<string>> threads)
    {
        foreach (Transform c in endIcon) if (c.name == "Mini") Destroy(c.gameObject);
        var tex = RenderClothTex(threads, 120, true, false);
        var ri = UI.Node("Mini", endIcon).gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false;
        float s = Mathf.Min(104f / tex.width, 104f / tex.height); UI.Center(ri.rectTransform, 0, 0, tex.width * s, tex.height * s).localRotation = Quaternion.Euler(0, 0, 8);
    }

    static List<List<string>> Expand(List<List<string>> rows)
    {
        var r = new List<List<string>>();
        foreach (var p in rows) { var l = new List<string>(); foreach (var c in p) for (int i = 0; i < ADV_PER; i++) l.Add(c); r.Add(l); }
        return r;
    }
    // Picture of a cloth: strips sit edge to edge (no gaps), each keeps its own height. A row block is twice as wide as tall.
    Texture2D RenderClothTex(List<List<string>> thr, int maxSide, bool transparent, bool fringe)
    {
        int N = Mathf.Max(1, thr.Count), maxT = 1; foreach (var p in thr) maxT = Mathf.Max(maxT, p.Count);
        const int colT = 2 * ADV_PER;
        float t = Mathf.Min(maxSide / (float)(N * colT), maxSide / (float)maxT);
        if (fringe) t = Mathf.Clamp(t, 1.5f, 8f); t = Mathf.Max(.25f, t);
        int cw = Mathf.Max(1, Mathf.RoundToInt(N * colT * t)), ch = Mathf.Max(1, Mathf.RoundToInt(maxT * t));
        int m = fringe ? Mathf.Max(24, Mathf.RoundToInt(t * 8)) : 2, fr = fringe ? Mathf.Max(14, Mathf.RoundToInt(t * 4)) : 0;
        int W = cw + m * 2, H = ch + m * 2 + fr * 2;
        var px = new Color32[W * H]; Color32 bg = transparent ? Clear32 : (Color32)Cream; for (int i = 0; i < px.Length; i++) px[i] = bg;
        int ox = m, oy = m + fr, warp = Mathf.Max(2, Mathf.RoundToInt(2.2f * t)); var fringeC = new Color32(215, 187, 144, 255);
        for (int si = 0; si < thr.Count; si++)
        {
            int x0 = ox + Mathf.RoundToInt(si * colT * t), x1 = ox + Mathf.RoundToInt((si + 1) * colT * t); var p = thr[si];
            for (int k = 0; k < p.Count; k++)
            {
                int y0 = oy + Mathf.RoundToInt(k * t), y1 = Mathf.Max(y0 + 1, oy + Mathf.RoundToInt((k + 1) * t));
                Color32 c = byId.ContainsKey(p[k]) ? (Color32)byId[p[k]].color : (Color32)Tan, lt = Lighten(c, .16f), dk = Mul(c, .86f);
                for (int y = y0; y < y1 && y < H; y++) for (int x = x0; x < x1; x++)
                    {
                        Color32 col = c;
                        if (t >= 2) { int wx = (x - x0 + (k % 2 == 1 ? warp / 2 : 0)) % warp; if (wx < Mathf.Max(1, warp * .3f)) col = lt; if (t >= 3 && y == y0) col = dk; }
                        px[y * W + x] = col;
                    }
            }
            if (fr > 0)
            {
                int lw = Mathf.Max(1, Mathf.RoundToInt(t * .35f)), topY = oy + Mathf.RoundToInt(p.Count * t);
                for (int x = x0 + warp / 2; x < x1; x += warp) for (int q = 0; q < lw; q++) for (int y = 0; y < fr; y++)
                        { if (x + q < W) { px[(oy - 1 - y) * W + x + q] = fringeC; if (topY + y < H && p.Count > 0) px[(topY + y) * W + x + q] = fringeC; } }
            }
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px); tex.Apply(false); return tex;
    }
    void SaveClothPng(List<List<string>> thr)
    {
        if (thr == null || thr.Count == 0) return;
        var tex = RenderClothTex(thr, 4000, transparentBg, true);
        string file = $"my-kente-{thr.Count}-strips-{DateTime.Now:yyyyMMdd-HHmmss}-{UnityEngine.Random.Range(100, 999)}.png";
        byte[] png = tex.EncodeToPNG(); Destroy(tex);
        saveMsg = "Saving…"; savedT = Now + 60f;
        KenteGallery.Save(png, file, (ok, msg) => { saveMsg = msg; savedT = Now; if (ok) onImageSaved.Invoke(Path.Combine(Application.persistentDataPath, "Kente", file)); });
    }

    // =====================================================================
    // My Kente gallery (saved in Application.persistentDataPath/MyKente.json)
    [Serializable] class GEntry { public long id; public string mode; public int n, rowsN, blocks, pat = -1; public string[] strips, next; }
    [Serializable] class GIndex { public List<GEntry> items = new List<GEntry>(); }
    static string GPath => Path.Combine(Application.persistentDataPath, "MyKente.json");
    static GIndex LoadG() { try { if (File.Exists(GPath)) return JsonUtility.FromJson<GIndex>(File.ReadAllText(GPath)) ?? new GIndex(); } catch (Exception e) { Debug.LogWarning("My Kente: could not read " + GPath + " " + e.Message); } return new GIndex(); }
    static void SaveG(GIndex g) { try { File.WriteAllText(GPath, JsonUtility.ToJson(g)); } catch (Exception e) { Debug.LogWarning("My Kente: could not save " + e.Message); } }
    static string[] PackStrips(List<List<string>> s) { var r = new string[s.Count]; for (int i = 0; i < s.Count; i++) r[i] = string.Join(",", s[i]); return r; }
    static List<List<string>> Unpack(string[] a) { var r = new List<List<string>>(); if (a != null) foreach (var s in a) r.Add(string.IsNullOrEmpty(s) ? new List<string>() : new List<string>(s.Split(','))); return r; }
    List<List<string>> GThreads(GEntry e) => e.mode == "adv" ? Unpack(e.strips) : Expand(Unpack(e.strips));
    static string ModeName(string m) => m == "adv" ? "Advanced" : m == "free" ? "Free weave" : "Basic";
    static string GLabel(GEntry e) => new DateTime(e.id).ToString("d MMM") + " · " + e.n + " strips · " + ModeName(e.mode);

    long editId; // a cloth reopened from My Kente is replaced when it is finished again
    void GalleryAdd(GEntry e)
    {
        var g = LoadG(); if (editId != 0) g.items.RemoveAll(x => x.id == editId); editId = 0;
        e.id = DateTime.Now.Ticks; g.items.Insert(0, e); if (g.items.Count > 60) g.items.RemoveRange(60, g.items.Count - 60); SaveG(g);
    }
    void GalleryAddCurrent() => GalleryAdd(new GEntry { mode = free ? "free" : "basic", n = done.Count, rowsN = free ? freeRowsN : 6, pat = free ? -1 : KenteData.PatternIndex, strips = PackStrips(done), next = new string[0] });

    RectTransform galScr, galGridView, galContent, galDetail; RawImage gdImg; RectTransform gdImgRT;
    Text galTitle, galSub, galEmpty, galSelLbl, galSaveLbl, galDelLbl, galCloseLbl, gdLabel, gdInfo, gdContLbl, gdSaveLbl, gdDelLbl;
    Image galSelBg; GameObject galSelBtn, galSaveBtn, galDelBtn; CanvasGroup galSaveGrp, galDelGrp;
    List<GEntry> gList = new List<GEntry>(); GEntry gOpen; HashSet<long> gSel; bool gDelConfirm; long gConfirmId;
    readonly Dictionary<long, Texture2D> gThumbs = new Dictionary<long, Texture2D>(); Texture2D gBig;
    class GCell { public GEntry e; public Image bg, tickBg; public GameObject tick, check, ring; }
    readonly List<GCell> gCells = new List<GCell>();
    public bool GalleryIsOpen => galScr != null && galScr.gameObject.activeSelf;

    void BuildGallery()
    {
        galScr = MakeScreen("MyKente");
        var dim = UI.Img("Dim", galScr, null, new Color(.07f, .04f, .02f, .78f)); UI.Stretch(dim.rectTransform, -4000); dim.raycastTarget = true;
        var card = UI.Panel("Card", galScr, Cream, 36, Ink); UI.Center(card.rectTransform, 0, 0, 980, 600);
        galTitle = UI.Label(card.transform, "My Kente", 36, Brown, TextAnchor.MiddleLeft, font); UI.TL(galTitle.rectTransform, 34, 26, 340, 44);
        galSub = UI.Label(card.transform, "", 14, Hint, TextAnchor.MiddleLeft, font); UI.TL(galSub.rectTransform, 34, 72, 340, 20);
        var sel = UI.Btn(card.transform, "Select", CardBg, Ink, Tan, GalleryToggleSelect, 18, font, Ink, 18); UI.TL((RectTransform)sel.transform, 386, 28, 120, 50);
        galSelBtn = sel.gameObject; galSelBg = sel.GetComponent<Image>(); galSelLbl = sel.GetComponentInChildren<Text>();
        var sv = UI.Btn(card.transform, "Save", Gold, Ink, GoldDark, GallerySaveSelected, 18, font, Ink, 18); UI.TL((RectTransform)sv.transform, 516, 28, 130, 50);
        galSaveBtn = sv.gameObject; galSaveLbl = sv.GetComponentInChildren<Text>(); galSaveGrp = galSaveBtn.AddComponent<CanvasGroup>();
        var dl = UI.Btn(card.transform, "Delete", Red, Cream, (Color)new Color32(125, 36, 27, 255), GalleryDeleteSelected, 18, font, (Color)new Color32(125, 36, 27, 255), 18); UI.TL((RectTransform)dl.transform, 656, 28, 150, 50);
        galDelBtn = dl.gameObject; galDelLbl = dl.GetComponentInChildren<Text>(); galDelGrp = galDelBtn.AddComponent<CanvasGroup>();
        var cl = UI.Btn(card.transform, "Close", CardBg, Ink, Tan, GalleryBack, 18, font, Ink, 18); UI.TL((RectTransform)cl.transform, 816, 28, 130, 50); galCloseLbl = cl.GetComponentInChildren<Text>();
        // grid of cloths (scrolls)
        var view = UI.Img("Grid", card.transform, null, new Color(0, 0, 0, 0)); galGridView = UI.TL(view.rectTransform, 34, 110, 912, 462); view.raycastTarget = true; view.gameObject.AddComponent<RectMask2D>();
        galContent = UI.Node("Content", view.transform); galContent.anchorMin = new Vector2(0, 1); galContent.anchorMax = new Vector2(1, 1); galContent.pivot = new Vector2(.5f, 1); galContent.anchoredPosition = Vector2.zero; galContent.sizeDelta = Vector2.zero;
        var grid = galContent.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(216, 214); grid.spacing = new Vector2(16, 16); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
        galContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = view.gameObject.AddComponent<ScrollRect>(); sr.content = galContent; sr.viewport = galGridView; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30;
        galEmpty = UI.Label(view.transform, "No cloths yet. Finish a kente and it waits here.", 19, Hint, TextAnchor.MiddleCenter, font); UI.Stretch(galEmpty.rectTransform);
        // one cloth, big
        galDetail = UI.TL(UI.Node("Detail", card.transform), 34, 110, 912, 462);
        var pic = UI.Panel("PicBg", galDetail, CardBg, 24, null); UI.TL(pic.rectTransform, 0, 0, 580, 462);
        gdImg = UI.Node("Cloth", pic.transform).gameObject.AddComponent<RawImage>(); gdImgRT = UI.Center(gdImg.rectTransform, 0, 0, 500, 400); gdImg.raycastTarget = false;
        gdLabel = UI.Label(galDetail, "", 24, Ink, TextAnchor.UpperLeft, font); UI.TL(gdLabel.rectTransform, 610, 30, 300, 64);
        gdInfo = UI.Label(galDetail, "", 15, Hint, TextAnchor.UpperLeft, font); UI.TL(gdInfo.rectTransform, 610, 100, 300, 24);
        MakeCheck(galDetail, 610, 136, 300, "Transparent background");
        var co = UI.Btn(galDetail, "Continue weaving", Green, Cream, GreenDark, () => { if (gOpen != null) ContinueFrom(gOpen); }, 20, font, (Color)GreenDark, 20); UI.TL((RectTransform)co.transform, 610, 186, 300, 58); gdContLbl = co.GetComponentInChildren<Text>();
        var gs = UI.Btn(galDetail, "Save image", Gold, Ink, GoldDark, () => { if (gOpen != null) SaveClothPng(GThreads(gOpen)); }, 20, font, Ink, 20); UI.TL((RectTransform)gs.transform, 610, 258, 300, 58); gdSaveLbl = gs.GetComponentInChildren<Text>();
        var gd = UI.Btn(galDetail, "Delete", Cream, Red, null, GalleryDeleteOpen, 18, font, Red, 20); UI.TL((RectTransform)gd.transform, 610, 330, 300, 50); gdDelLbl = gd.GetComponentInChildren<Text>();
        galScr.gameObject.SetActive(false);
    }

    void OpenGallery()
    {
        gList = LoadG().items; gOpen = null; gSel = null; gDelConfirm = false; gConfirmId = 0;
        galScr.gameObject.SetActive(true); galScr.SetAsLastSibling(); if (quitBtnT != null) quitBtnT.SetAsLastSibling();
        GalleryRebuild();
    }
    void CloseGallery()
    {
        if (galScr != null) galScr.gameObject.SetActive(false);
        foreach (var t in gThumbs.Values) if (t != null) Destroy(t); gThumbs.Clear();
        if (gBig != null) { Destroy(gBig); gBig = null; }
    }
    void GalleryBack() { if (gOpen != null) { gOpen = null; GalleryRefresh(); } else CloseGallery(); }
    Texture2D Thumb(GEntry e) { if (!gThumbs.TryGetValue(e.id, out var t) || t == null) { t = RenderClothTex(GThreads(e), 150, true, false); gThumbs[e.id] = t; } return t; }
    void GalleryRebuild()
    {
        foreach (Transform c in galContent) Destroy(c.gameObject); gCells.Clear();
        foreach (var e in gList)
        {
            var ee = e;
            var cell = UI.Panel("Cloth", galContent, CardBg, 20, null); cell.raycastTarget = true;
            var b = cell.gameObject.AddComponent<Button>(); b.targetGraphic = cell; b.onClick.AddListener(() => GalleryCellTap(ee));
            var tex = Thumb(e); var ri = UI.Node("Thumb", cell.transform).gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false;
            float s = Mathf.Min(150f / tex.width, 150f / tex.height); UI.TL(ri.rectTransform, (216 - tex.width * s) / 2, 16 + (150 - tex.height * s) / 2, tex.width * s, tex.height * s);
            var lb = UI.Label(cell.transform, GLabel(e), 13, Brown, TextAnchor.MiddleCenter, font); UI.TL(lb.rectTransform, 6, 174, 204, 32);
            var ring = UI.Panel("SelRing", cell.transform, new Color(0, 0, 0, 0), 20, Ink, 4); ring.raycastTarget = false; UI.Stretch(ring.rectTransform);
            var tk = UI.Panel("Tick", cell.transform, Cream, 15, Ink, 3); UI.TL(tk.rectTransform, 216 - 12 - 30, 12, 30, 30);
            var ck = UI.Node("Check", tk.transform);
            var a = UI.Img("c1", ck, null, Cream); UI.Center(a.rectTransform, -3, -1, 3, 8).localRotation = Quaternion.Euler(0, 0, 45);
            var c2 = UI.Img("c2", ck, null, Cream); UI.Center(c2.rectTransform, 2, 1, 3, 13).localRotation = Quaternion.Euler(0, 0, -40);
            gCells.Add(new GCell { e = e, bg = cell, tickBg = tk, tick = tk.gameObject, check = ck.gameObject, ring = ring.gameObject });
        }
        GalleryRefresh();
    }
    void GalleryRefresh()
    {
        bool detail = gOpen != null; int ns = gSel != null ? gSel.Count : 0;
        galGridView.gameObject.SetActive(!detail); galDetail.gameObject.SetActive(detail);
        galTitle.text = detail ? "Your kente" : "My Kente";
        galSub.text = detail ? "" : $"{gList.Count} cloth{(gList.Count == 1 ? "" : "s")} you have woven";
        galEmpty.gameObject.SetActive(!detail && gList.Count == 0);
        galSelBtn.SetActive(!detail && gList.Count > 0); galSaveBtn.SetActive(!detail && gSel != null); galDelBtn.SetActive(!detail && gSel != null);
        galSelLbl.text = gSel != null ? "Done" : "Select"; galSelBg.color = gSel != null ? Gold : CardBg;
        galSaveLbl.text = ns > 0 ? $"Save {ns}" : "Save"; galSaveGrp.alpha = ns > 0 ? 1 : .45f;
        galDelLbl.text = ns == 0 ? "Delete" : gDelConfirm ? $"Sure? ({ns})" : $"Delete {ns}"; galDelGrp.alpha = ns > 0 ? 1 : .45f;
        galCloseLbl.text = detail ? "All cloths" : "Close";
        foreach (var c in gCells) { bool on = gSel != null && gSel.Contains(c.e.id); c.bg.color = on ? CardOn : CardBg; c.tick.SetActive(gSel != null); c.tickBg.color = on ? Green : Cream; c.check.SetActive(on); c.ring.SetActive(on); }
        if (detail)
        {
            var e = gOpen; int total = 0; foreach (var s in Unpack(e.strips)) total += s.Count;
            gdLabel.text = GLabel(e);
            gdInfo.text = e.mode == "adv" ? $"{total} threads" : $"{total} rows";
            gdContLbl.text = e.mode == "basic" ? "Weave this pattern again" : "Continue weaving";
            gdDelLbl.text = gConfirmId == e.id ? "Tap again to delete" : "Delete";
        }
    }
    void GalleryCellTap(GEntry e)
    {
        if (gSel != null) { if (!gSel.Remove(e.id)) gSel.Add(e.id); gDelConfirm = false; GalleryRefresh(); return; }
        gOpen = e; gConfirmId = 0;
        if (gBig != null) Destroy(gBig);
        gBig = RenderClothTex(GThreads(e), 900, true, false); gBig.filterMode = FilterMode.Point; gdImg.texture = gBig;
        float s = Mathf.Min(540f / gBig.width, 420f / gBig.height); gdImgRT.sizeDelta = new Vector2(gBig.width * s, gBig.height * s);
        GalleryRefresh();
    }
    void GalleryToggleSelect() { gSel = gSel == null ? new HashSet<long>() : null; gDelConfirm = false; GalleryRefresh(); }
    void GallerySaveSelected() { if (gSel == null || gSel.Count == 0) return; foreach (var e in gList) if (gSel.Contains(e.id)) SaveClothPng(GThreads(e)); }
    void GalleryDeleteSelected()
    {
        if (gSel == null || gSel.Count == 0) return;
        if (!gDelConfirm) { gDelConfirm = true; GalleryRefresh(); return; }
        var g = LoadG(); g.items.RemoveAll(x => gSel.Contains(x.id)); SaveG(g); gList = g.items; gSel = null; gDelConfirm = false; GalleryRebuild();
    }
    void GalleryDeleteOpen()
    {
        if (gOpen == null) return;
        if (gConfirmId != gOpen.id) { gConfirmId = gOpen.id; GalleryRefresh(); return; }
        long id = gOpen.id; var g = LoadG(); g.items.RemoveAll(x => x.id == id); SaveG(g); gList = g.items; gOpen = null; gConfirmId = 0; GalleryRebuild();
    }
    void UpdateGallery(float t)
    {
        if (gOpen != null && gdSaveLbl != null) gdSaveLbl.text = t - savedT < 3f ? saveMsg : "Save image";
        if (gSel != null && gSel.Count > 0 && galSaveLbl != null) galSaveLbl.text = t - savedT < 3f ? saveMsg : $"Save {gSel.Count}";
    }

    // Back to the loom with a saved cloth, in the mode it was woven in.
    void ContinueFrom(GEntry e)
    {
        CloseGallery(); StopAllCoroutines(); firstTime = false; editId = e.id; var s = Unpack(e.strips);
        if (e.mode == "adv")
        {
            adv = true; free = false; int maxL = 0; foreach (var p in s) maxL = Mathf.Max(maxL, p.Count);
            pickN = Mathf.Max(1, e.n); pickR = e.blocks > 0 ? e.blocks : Mathf.Max(2, Mathf.CeilToInt(maxL / (float)ADV_PER)); BeginAdv();
            for (int i = 0; i < A.n; i++)
            {
                A.strips[i] = i < s.Count ? s[i] : new List<string>();
                string nx = e.next != null && i < e.next.Length ? e.next[i] : "";
                A.next[i] = !string.IsNullOrEmpty(nx) ? nx : A.strips[i].Count > 0 ? (A.strips[i].Count % 2 == 1 ? "L" : "R") : null;
            }
            A.cur = 0; A.passes = AdvTotal(); AdvPlace(); SetHint("Back to this cloth. Keep weaving, or unpick to change it."); return;
        }
        if (e.mode == "free" && s.Count > 0)
        {
            adv = false; free = true; int maxR = 0; foreach (var p in s) maxR = Mathf.Max(maxR, p.Count);
            freeStrips = s.Count; freeRowsN = e.rowsN > 0 ? e.rowsN : maxR;
            done.Clear(); for (int i = 0; i < s.Count - 1; i++) done.Add(s[i]); BuildClothPreview(); BeginStrip(s.Count - 1);
            freeRows = new List<string>(s[s.Count - 1]); rows = freeRows.Count; sideLeft = rows % 2 == 0; shx = sideLeft ? LX : RX; scroll = ScrollFor(rows);
            SetHint("Back on the last strip. Undo to unpick rows, or keep weaving."); return;
        }
        editId = 0; adv = false; free = false; if (e.pat >= 0) KenteData.PatternIndex = e.pat; ToSetup(); // Basic: same pattern, woven fresh
    }

    // =====================================================================
    // Cloth wound on the beams (the model's fold_bottom / fold_top objects give each roll's centre and radius)
    class Roller { public GameObject go; public Mesh mesh; public Texture2D tex; public Color32[] px; public Vector3 c; public float r0, rad = -1f, meet; public bool bottom; public string key = ""; }
    Roller rollB, rollT; float stageSB = BASE + 9999, stageST = WIN_TOP - 9999; const int RW = 512, RH = 128;
    static bool IsFold(Transform t) { for (; t != null; t = t.parent) if (t.name.StartsWith("fold_", StringComparison.OrdinalIgnoreCase)) return true; return false; }

    void BuildRollers()
    {
        var sh = Shader.Find("KenteGame/ClothRoll"); if (sh == null) sh = Shader.Find("KenteGame/Cloth");
        rollB = MakeRoller(Find(loomGO.transform, "fold_bottom"), true, sh);
        rollT = MakeRoller(Find(loomGO.transform, "fold_top"), false, sh);
        foreach (var r in loomGO.GetComponentsInChildren<Renderer>(true)) if (IsFold(r.transform)) r.enabled = false; // guides only
    }
    Roller MakeRoller(Transform f, bool bottom, Shader sh)
    {
        if (f == null || sh == null) return null;
        var b = BoundsOf(f); float R = Mathf.Max(b.extents.y, .01f); Vector3 c = b.center;
        float dz = Mathf.Clamp(Vector3.Dot(clothOrigin - c, clothNormal), -R * .95f, R * .95f);
        float dy = Mathf.Sqrt(R * R - dz * dz) * (bottom ? 1 : -1); // where the flat cloth meets the roll
        var r = new Roller { c = c, r0 = R + .003f * unitK, bottom = bottom, meet = Mathf.Atan2(dy, dz) };
        float meetStage = BASE - (c.y + dy - clothOrigin.y) / stageU;
        if (bottom) stageSB = meetStage; else stageST = meetStage;
        r.tex = new Texture2D(RW, RH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear }; r.px = new Color32[RW * RH];
        r.go = new GameObject(bottom ? "ClothRoll_bottom" : "ClothRoll_top", typeof(MeshFilter), typeof(MeshRenderer));
        r.go.transform.position = c; r.go.transform.rotation = Quaternion.identity; r.go.transform.SetParent(loomGO.transform, true);
        r.mesh = new Mesh { name = r.go.name }; r.go.GetComponent<MeshFilter>().sharedMesh = r.mesh;
        r.go.GetComponent<MeshRenderer>().sharedMaterial = new Material(sh) { mainTexture = r.tex };
        RollerMesh(r, r.r0); r.go.SetActive(false);
        return r;
    }
    void RollerMesh(Roller r, float rad)
    {
        const int SEG = 96; float half = clothW * .5f;
        Vector3 off = Vector3.Dot(clothOrigin + clothRight * half - r.c, clothRight) * clothRight; // line the roll up with the cloth
        var v = new Vector3[(SEG + 1) * 2]; var uv = new Vector2[v.Length]; var nrm = new Vector3[v.Length]; var tri = new int[SEG * 6];
        for (int i = 0; i <= SEG; i++)
        {
            float u = i / (float)SEG, th = r.meet + u * Mathf.PI * 2; Vector3 d = Mathf.Cos(th) * clothNormal + Mathf.Sin(th) * clothUp;
            v[i * 2] = off + d * rad - clothRight * half; v[i * 2 + 1] = off + d * rad + clothRight * half;
            uv[i * 2] = new Vector2(u, 0); uv[i * 2 + 1] = new Vector2(u, 1); nrm[i * 2] = nrm[i * 2 + 1] = d;
        }
        for (int i = 0; i < SEG; i++) { int a = i * 2, k = i * 6; tri[k] = a; tri[k + 1] = a + 2; tri[k + 2] = a + 1; tri[k + 3] = a + 1; tri[k + 4] = a + 2; tri[k + 5] = a + 3; }
        r.mesh.Clear(); r.mesh.vertices = v; r.mesh.uv = uv; r.mesh.normals = nrm; r.mesh.triangles = tri; r.mesh.RecalculateBounds(); r.rad = rad;
    }
    void UpdateRollers()
    {
        if (rollB == null && rollT == null) return;
        List<List<string>> lanes; float th;
        if (adv && A != null) { lanes = A.strips; th = ADV_TH; } else { lanes = new List<List<string>> { Woven() }; th = ROW_H; }
        PaintRoller(rollB, lanes, th); PaintRoller(rollT, lanes, th);
    }
    static void Band(Color32[] px, float xa, float xb, int y0, int y1, Color32 c)
    {
        int a = Mathf.FloorToInt(xa), b = Mathf.CeilToInt(xb); if (b - a >= RW) { a = 0; b = RW; }
        for (int x = a; x < b; x++) { int xi = ((x % RW) + RW) % RW; for (int y = y0; y < y1; y++) px[y * RW + xi] = c; }
    }
    // The wound arc equals the length of cloth that has gone past the beam, so it wraps gradually, one block at a time.
    void PaintRoller(Roller r, List<List<string>> lanes, float th)
    {
        if (r == null) return;
        int n = Mathf.Max(1, lanes.Count); float S = r.bottom ? stageSB : stageST, maxW = 0; var Lr = new float[n];
        var sb = new StringBuilder(); sb.Append(Mathf.RoundToInt(scroll * 2)).Append('|').Append(adv && A != null ? A.passes : done.Count * 1000 + rows).Append('|');
        for (int si = 0; si < lanes.Count; si++)
        {
            float len = lanes[si].Count * th;
            Lr[si] = r.bottom ? Mathf.Clamp(BASE + scroll - S, 0, len) : Mathf.Clamp(S - (BASE - len + scroll), 0, len);
            maxW = Mathf.Max(maxW, Lr[si]); sb.Append(lanes[si].Count).Append(',');
        }
        if (maxW < .5f) { if (r.go.activeSelf) r.go.SetActive(false); return; }
        if (!r.go.activeSelf) r.go.SetActive(true);
        float turns = maxW * stageU / (Mathf.PI * 2 * r.r0), rad = r.r0 + Mathf.Min(.015f, Mathf.Floor(turns) * .004f) * unitK; // each full turn adds a thin layer
        if (Mathf.Abs(rad - r.rad) > 1e-5f) RollerMesh(r, rad);
        string key = sb.ToString(); if (key == r.key) return; r.key = key;
        float k2px = RW / (Mathf.PI * 2 * rad / stageU); // stage px of cloth -> texture px round the beam (true size)
        for (int i = 0; i < r.px.Length; i++) r.px[i] = Clear32;
        for (int si = 0; si < lanes.Count; si++)
        {
            if (Lr[si] <= 0) continue;
            int y0 = si * RH / n, y1 = (si + 1) * RH / n; var p = lanes[si]; int N = p.Count;
            for (int q = 0; q < N; q++)
            {
                int k = r.bottom ? q : N - 1 - q; // oldest (outermost from the meeting point) first, newest last
                float yTop = BASE - (k + 1) * th + scroll, yBot = yTop + th, d0, d1;
                if (r.bottom) { d0 = yTop - S; d1 = yBot - S; } else { d0 = S - yBot; d1 = S - yTop; }
                if (d1 <= 0) continue; d0 = Mathf.Max(0, d0);
                float xa = r.bottom ? (RW - d1 * k2px) * -1f + RW : d0 * k2px, xb = r.bottom ? (RW - d0 * k2px) * -1f + RW : d1 * k2px;
                if (r.bottom && bottomFoldFlip) { xa = d0 * k2px; xb = d1 * k2px; }
                Color32 c = byId.ContainsKey(p[k]) ? (Color32)byId[p[k]].color : (Color32)Tan;
                Band(r.px, xa, xb, y0, y1, c);
                float e = Mathf.Max(1, th * k2px * .12f); if (r.bottom && !bottomFoldFlip) Band(r.px, xa, xa + e, y0, y1, Mul(c, .8f)); else Band(r.px, xb - e, xb, y0, y1, Mul(c, .8f));
            }
        }
        r.tex.SetPixels32(r.px); r.tex.Apply(false);
    }
}

// Tap on the little cloth in the Advanced panel to pick a strip.
public class KenteMiniInput : MonoBehaviour, IPointerClickHandler
{
    public KenteWeavingGame game;
    public void OnPointerClick(PointerEventData e)
    {
        var r = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(r, e.position, e.pressEventCamera, out var p);
        game.MiniClick(p.x - r.rect.xMin);
    }
}
