using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LastMatch.Core;

namespace LastMatch.View
{
    /// <summary>Owns the simulation, draws the board with sprites, builds the HUD and menus, and routes input.</summary>
    public class GameController : MonoBehaviour
    {
        GameSim sim; ProcAudio sfx; Camera cam;
        Transform boardRoot, cellRoot, gemRoot, fxRoot;
        readonly Dictionary<int, GemView> views = new Dictionary<int, GemView>();
        readonly Stack<GemView> viewPool = new Stack<GemView>();
        readonly List<SpriteRenderer> cellSprites = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> hiPool = new List<SpriteRenderer>();
        SpriteRenderer hand, arrow, shieldRing;
        TextMesh shiftText;

        class Fx { public FxEvent E; public float T; public SpriteRenderer A, B; }
        class Particle { public SpriteRenderer S; public float X, Y, VX, VY, Life, Size; }
        class Floater { public TextMesh T; public float X, Y, Age; public bool Big; public Color C; }
        readonly List<Fx> fxs = new List<Fx>();
        readonly List<Particle> particles = new List<Particle>(); readonly Stack<SpriteRenderer> dotPool = new Stack<SpriteRenderer>();
        readonly List<Floater> floats = new List<Floater>(); readonly Stack<TextMesh> textPool = new Stack<TextMesh>();
        float flashAlpha; Color flashColor = Color.white;

        // UI
        Text lvlName, scoreT, timeT, supplyT, movesT, dodgesT, chipsT; Image meterFill, flashImg, tintImg;
        readonly Button[] slotBtns = new Button[3]; readonly Text[] slotTxt = new Text[3];
        RectTransform overlayRoot; Image overlayBg; bool overlayShown; string overlayKind; Action overlayPrimary;
        int menuTier; int currentLevel = 1;   // 0 = endless
        float lastAspect = -1; int lastCols, lastRows;
        Vector2 ptrDown; bool ptrActive; float pendingEnd = -1; bool pendingWin, pendingNewBest; string lastHud = "";

        /// <summary>Hooks for automated tests.</summary>
        public GameSim Sim => sim;
        public bool OverlayShown => overlayShown;
        public void DebugStartLevel(int n) => StartLevel(n);
        public void DebugTogglePause() => TogglePause();
        public void DebugToMenu() => ToMenu();

        // ============================================================
        void Awake()
        {
            Application.targetFrameRate = 60;
            Input.simulateMouseWithTouches = true;
            SpriteFactory.Build();
            cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera"); cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Palette.Bg; cg.transform.position = new Vector3(0, 0, -5); cam.nearClipPlane = -10; cam.farClipPlane = 10; }
            cam.backgroundColor = Palette.Bg;
            sfx = gameObject.AddComponent<ProcAudio>(); sfx.Muted = SaveData.Muted;

            sim = new GameSim();
            sim.OnFloat += SpawnFloat; sim.OnFx += SpawnFx; sim.OnSfx += (n) => sfx.Play(n); sim.OnParticles += SpawnParticles;
            sim.OnGameOver += OnGameOver; sim.OnSlotsChanged += RefreshSlots;

            BuildWorld();
            BuildHud();
            currentLevel = Mathf.Min(LevelGen.TotalLevels, SaveData.Beaten + 1);
            sim.StartLevel(LevelGen.Make(currentLevel)); sim.Stop();
            RebuildCells();
            ShowMenu();
        }

        void BuildWorld()
        {
            boardRoot = new GameObject("Board").transform; boardRoot.position = new Vector3(0, -.3f, 0);
            cellRoot = new GameObject("Cells").transform; cellRoot.SetParent(boardRoot, false);
            gemRoot = new GameObject("Gems").transform; gemRoot.SetParent(boardRoot, false);
            fxRoot = new GameObject("Fx").transform; fxRoot.SetParent(boardRoot, false);
            shieldRing = NewSprite(fxRoot, "Shield", SpriteFactory.Ring, 9, Palette.Gold); shieldRing.transform.localScale = Vector3.one * 1.3f; shieldRing.enabled = false;
            arrow = NewSprite(fxRoot, "Arrow", SpriteFactory.Arrow, 29, Palette.Gold); arrow.enabled = false;
            hand = NewSprite(fxRoot, "Hand", SpriteFactory.Hand, 30, Color.white); hand.enabled = false;
            var tgo = new GameObject("ShiftText"); tgo.transform.SetParent(fxRoot, false);
            shiftText = tgo.AddComponent<TextMesh>(); shiftText.font = UiKit.Font; shiftText.fontSize = 64; shiftText.characterSize = .09f; shiftText.anchor = TextAnchor.MiddleCenter; shiftText.alignment = TextAlignment.Center; shiftText.color = Palette.Hex("#c77dff"); shiftText.fontStyle = FontStyle.Bold;
            var mr = tgo.GetComponent<MeshRenderer>(); mr.material = UiKit.Font.material; mr.sortingOrder = 41; tgo.SetActive(false);
        }

        static SpriteRenderer NewSprite(Transform parent, string name, Sprite s, int order, Color c)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order; sr.color = c; return sr;
        }

        void BuildHud()
        {
            var canvas = UiKit.MakeCanvas("HUD", 10);
            // full-screen tints under everything in the canvas
            tintImg = UiKit.Panel(canvas.transform, new Color(0, 0, 0, 0), "Tint"); UiKit.Stretch(tintImg.rectTransform); tintImg.raycastTarget = false;
            flashImg = UiKit.Panel(canvas.transform, new Color(1, 1, 1, 0), "Flash"); UiKit.Stretch(flashImg.rectTransform); flashImg.raycastTarget = false;

            var top = UiKit.Panel(canvas.transform, new Color(0, 0, 0, 0), "Top"); UiKit.AnchorTop(top.rectTransform, 120, 12, 12, 8); top.raycastTarget = false;
            var tv = UiKit.VStack(top.transform, 2, TextAnchor.UpperLeft); UiKit.Stretch(tv.GetComponent<RectTransform>());
            var titleRow = UiKit.HStack(tv.transform, 10, TextAnchor.MiddleLeft);
            var brand = UiKit.Label(titleRow.transform, "LAST MATCH", 26, Palette.Gold, TextAnchor.MiddleLeft); UiKit.Size(brand, 180, 30);
            lvlName = UiKit.Label(titleRow.transform, "", 13, Palette.Muted, TextAnchor.MiddleLeft); UiKit.Size(lvlName, 300, 30);
            var stats = UiKit.HStack(tv.transform, 6, TextAnchor.MiddleLeft);
            scoreT = StatBlock(stats.transform, "SCORE"); timeT = StatBlock(stats.transform, "SURVIVED"); supplyT = StatBlock(stats.transform, "GEM SUPPLY"); movesT = StatBlock(stats.transform, "AI MOVES LEFT"); dodgesT = StatBlock(stats.transform, "DODGED");
            chipsT = UiKit.Label(tv.transform, "", 13, Palette.Gold, TextAnchor.MiddleLeft); UiKit.Size(chipsT, 500, 22);

            var dock = UiKit.Panel(canvas.transform, new Color(0, 0, 0, 0), "Dock"); UiKit.AnchorBottom(dock.rectTransform, 110, 12, 12, 10); dock.raycastTarget = false;
            var dh = UiKit.HStack(dock.transform, 10, TextAnchor.MiddleCenter); UiKit.Stretch(dh.GetComponent<RectTransform>());
            var mv = UiKit.VStack(dh.transform, 4, TextAnchor.MiddleLeft); var mle = mv.gameObject.AddComponent<LayoutElement>(); mle.flexibleWidth = 1; mle.preferredHeight = 60;
            var ml = UiKit.Label(mv.transform, "SURVIVAL METER · EARNS A POWER-UP", 11, Palette.Muted, TextAnchor.MiddleLeft); UiKit.Size(ml, 0, 16);
            var bar = UiKit.Panel(mv.transform, Palette.Hex("#140e33"), "MeterBg", SpriteFactory.Square); UiKit.Size(bar, 0, 18); var ble = bar.GetComponent<LayoutElement>(); ble.flexibleWidth = 1;
            meterFill = UiKit.Panel(bar.transform, Palette.Gold, "MeterFill", SpriteFactory.Square); meterFill.type = Image.Type.Filled; meterFill.fillMethod = Image.FillMethod.Horizontal; meterFill.fillAmount = 0; UiKit.Stretch(meterFill.rectTransform, 2, 2, 2, 2);
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                slotBtns[i] = UiKit.MakeButton(dh.transform, "", Palette.PanelDark, Palette.Cream, 13, () => sim.UsePowerup(idx), 86, 64, "Slot" + i);
                slotTxt[i] = slotBtns[i].GetComponentInChildren<Text>();
            }

            var oc = UiKit.MakeCanvas("Overlay", 20);
            overlayBg = UiKit.Panel(oc.transform, new Color(.07f, .043f, .19f, .93f), "OverlayBg"); UiKit.Stretch(overlayBg.rectTransform);
            overlayRoot = overlayBg.rectTransform;
            RefreshSlots();
        }

        Text StatBlock(Transform parent, string label)
        {
            var v = UiKit.VStack(parent, 0, TextAnchor.MiddleCenter); UiKit.Size(v, 96, 46);
            var val = UiKit.Label(v.transform, "0", 22, Palette.Cream); UiKit.Size(val, 96, 26);
            var lbl = UiKit.Label(v.transform, label, 9, Palette.Muted); UiKit.Size(lbl, 96, 14);
            return val;
        }

        // ============================================================
        // level flow
        void StartLevel(int n)
        {
            currentLevel = n;
            sim.StartLevel(n == 0 ? LevelGen.Endless() : LevelGen.Make(n));
            RebuildCells(); ClearFx();
            lvlName.text = n == 0 ? "ENDLESS" : ("LEVEL " + n + " · " + sim.L.TierName.ToUpperInvariant());
            HideOverlay();
        }

        void OnGameOver(bool win)
        {
            bool newBest = false;
            if (sim.L.Infinite) { if (sim.Score > SaveData.BestEndless) { SaveData.BestEndless = (int)sim.Score; newBest = true; } }
            else if (win)
            {
                if (sim.L.Id > SaveData.Beaten) SaveData.Beaten = sim.L.Id;
                if (sim.Score > SaveData.Best(sim.L.Id)) { SaveData.SetBest(sim.L.Id, (int)sim.Score); newBest = true; }
            }
            pendingWin = win; pendingNewBest = newBest; pendingEnd = win ? .9f : 1.1f;
        }

        void TogglePause()
        {
            if (sim.Paused) { sim.TogglePause(); HideOverlay(); return; }
            if (!sim.Running) return;
            sim.TogglePause(); ShowPause();
        }

        void ToMenu() { sim.Stop(); pendingEnd = -1; ShowMenu(); }

        // ============================================================
        // input
        void Update()
        {
            float dt = Time.deltaTime;
            HandleInput();
            sim.Tick(dt);
            if (pendingEnd >= 0) { pendingEnd -= dt; if (pendingEnd <= 0) { pendingEnd = -1; ShowResult(pendingWin, pendingNewBest); } }
            Layout();
            SyncBoard();
            SyncFx(dt);
            SyncHud();
        }

        void HandleInput()
        {
            if (overlayShown)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { if (overlayKind == "pause") ToMenu(); else if (overlayKind == "help") ShowMenu(); }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) overlayPrimary?.Invoke();
                else if (overlayKind == "pause" && Input.GetKeyDown(KeyCode.P)) TogglePause();
                return;
            }
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) { TogglePause(); return; }
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) sim.QueueMove(-1, 0);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) sim.QueueMove(1, 0);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) sim.QueueMove(0, -1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) sim.QueueMove(0, 1);
            if (Input.GetKeyDown(KeyCode.Alpha1)) sim.UsePowerup(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) sim.UsePowerup(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) sim.UsePowerup(2);
            if (Input.GetKeyDown(KeyCode.Space)) sim.UsePowerup(0);

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                ptrDown = Input.mousePosition; ptrActive = true;
            }
            if (Input.GetMouseButtonUp(0) && ptrActive)
            {
                ptrActive = false;
                Vector2 up = Input.mousePosition; Vector2 d = up - ptrDown;
                float thresh = Screen.dpi > 0 ? Screen.dpi * .12f : 18f;
                if (d.magnitude > thresh) { if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) sim.QueueMove(0, (int)Mathf.Sign(d.x)); else sim.QueueMove(-(int)Mathf.Sign(d.y), 0); return; }
                var pl = sim.B.FindPlayer(); if (!pl.HasValue) return;
                var w = cam.ScreenToWorldPoint(up); var local = boardRoot.InverseTransformPoint(w);
                int c = Mathf.FloorToInt(local.x + sim.B.Cols / 2f), r = Mathf.FloorToInt(-local.y + sim.B.Rows / 2f);
                int dr = r - pl.Value.R, dc = c - pl.Value.C;
                if (Mathf.Abs(dr) + Mathf.Abs(dc) == 1) sim.QueueMove(dr, dc);
                else if (dr == 0 && dc == 0) { var g = sim.B.PlayerGem(); if (g != null) g.Squash = 1; }
                else if (Mathf.Abs(dr) >= Mathf.Abs(dc)) sim.QueueMove((int)Mathf.Sign(dr), 0);
                else sim.QueueMove(0, (int)Mathf.Sign(dc));
            }
        }

        void ToggleMute() { sfx.Muted = !sfx.Muted; SaveData.Muted = sfx.Muted; }

        // ============================================================
        // board drawing
        void Layout()
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (Mathf.Abs(aspect - lastAspect) < 1e-4f && lastCols == sim.B.Cols && lastRows == sim.B.Rows) return;
            lastAspect = aspect; lastCols = sim.B.Cols; lastRows = sim.B.Rows;
            cam.orthographicSize = Mathf.Max((sim.B.Cols + 1f) / (2f * aspect), sim.B.Rows / 2f + 2.9f);
            cam.transform.position = new Vector3(0, 0, -5);
        }

        Vector3 CellPos(float r, float c) => new Vector3(c - sim.B.Cols / 2f + .5f, -(r - sim.B.Rows / 2f + .5f), 0);

        void RebuildCells()
        {
            foreach (var s in cellSprites) Destroy(s.gameObject);
            cellSprites.Clear();
            for (int r = 0; r < sim.B.Rows; r++) for (int c = 0; c < sim.B.Cols; c++)
            {
                var ty = sim.B.Cells[r, c]; if (ty == CellType.Hole) continue;
                var s = NewSprite(cellRoot, "Cell", SpriteFactory.Cell, 0, ty == CellType.Open ? ((r + c) % 2 == 1 ? Palette.CellA : Palette.CellB) : Palette.CellLocked);
                s.transform.localPosition = CellPos(r, c); s.transform.localScale = Vector3.one * .97f;
                cellSprites.Add(s);
            }
            // cell tints follow lock state changes, so remember which cell each sprite is
            foreach (var kv in views) { kv.Value.gameObject.SetActive(false); viewPool.Push(kv.Value); }
            views.Clear();
        }

        SpriteRenderer Highlight(int i)
        {
            while (hiPool.Count <= i) { var s = NewSprite(cellRoot, "Hi", SpriteFactory.Cell, 1, Color.white); s.transform.localScale = Vector3.one * .97f; hiPool.Add(s); }
            hiPool[i].enabled = true; return hiPool[i];
        }

        GemView ViewFor(Gem g)
        {
            if (views.TryGetValue(g.Id, out var v)) return v;
            v = viewPool.Count > 0 ? viewPool.Pop() : GemView.Create(gemRoot);
            v.gameObject.SetActive(true); v.GemId = g.Id; views[g.Id] = v;
            return v;
        }

        readonly HashSet<int> seen = new HashSet<int>();
        readonly List<int> stale = new List<int>();

        void SyncBoard()
        {
            float t = Time.time;
            float pulse = .5f + .5f * Mathf.Sin(t * 9);
            var B = sim.B;
            boardRoot.position = new Vector3(0, -.3f, 0) + (sim.Shake > 0 ? new Vector3((UnityEngine.Random.value - .5f) * sim.Shake * .2f, (UnityEngine.Random.value - .5f) * sim.Shake * .2f, 0) : Vector3.zero);

            // locked cells may have opened since the cells were built
            int ci = 0;
            for (int r = 0; r < B.Rows; r++) for (int c = 0; c < B.Cols; c++)
            {
                var ty = B.Cells[r, c]; if (ty == CellType.Hole) continue;
                if (ci < cellSprites.Count) cellSprites[ci].color = ty == CellType.Open ? ((r + c) % 2 == 1 ? Palette.CellA : Palette.CellB) : Palette.CellLocked;
                ci++;
            }

            var pl = B.FindPlayer();
            string mood = "happy"; Vector2 look = new Vector2(sim.LastDX * .6f, -sim.LastDY * .6f);
            int hi = 0;
            var pv = sim.AiPreview;
            if (pv != null && sim.AiStateNow == AiState.Tele)
            {
                foreach (var k in pv.Clear.Keys)
                {
                    var p = B.FromKey(k); bool isPl = pl.HasValue && p.R == pl.Value.R && p.C == pl.Value.C;
                    var s = Highlight(hi++); s.transform.localPosition = CellPos(p.R, p.C);
                    s.color = isPl ? new Color(1, .23f, .36f, .35f + .35f * pulse) : new Color(1, .82f, .25f, .14f + .1f * pulse);
                }
                if (pv.Lethal && sim.Shield <= 0) mood = "scared";
            }
            if (sim.AiStateNow == AiState.Tele && sim.AiPlan != null)
            {
                var p = sim.AiPlan;
                foreach (var cell in new[] { new Pos(p.R1, p.C1), new Pos(p.R2, p.C2) })
                {
                    var s = Highlight(hi++); s.transform.localPosition = CellPos(cell.R, cell.C); s.color = new Color(1, 1, 1, .18f + .14f * pulse);
                }
            }
            for (int i = hi; i < hiPool.Count; i++) hiPool[i].enabled = false;

            if (sim.FingerVis > .02f && pl.HasValue) look = new Vector2(Mathf.Clamp((sim.FingerX - pl.Value.C - .5f) / 3f, -1, 1), -Mathf.Clamp((sim.FingerY - pl.Value.R - .5f) / 3f, -1, 1));
            if (sim.Shield > 0) mood = "cool";
            if (sim.Dead) mood = "dead";

            seen.Clear();
            for (int r = 0; r < B.Rows; r++) for (int c = 0; c < B.Cols; c++)
            {
                var g = B.Grid[r, c]; if (g == null) continue;
                var v = ViewFor(g); seen.Add(g.Id);
                float wob = g.IsPlayer ? Mathf.Sin(t * 3 + g.Wob) * .02f : 0;
                v.transform.localPosition = CellPos(g.RY, g.RX) + new Vector3(0, wob, 0);
                v.Apply(g, B.Cells[r, c], g.IsPlayer, mood, look, t, sim.Blink);
            }
            foreach (var g in sim.Popping)
            {
                var v = ViewFor(g); seen.Add(g.Id);
                v.transform.localPosition = CellPos(g.RY, g.RX);
                v.Apply(g, CellType.Open, g.IsPlayer, g.IsPlayer ? "dead" : "happy", look, t, 0);
            }
            stale.Clear();
            foreach (var kv in views) if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var id in stale) { var v = views[id]; v.gameObject.SetActive(false); viewPool.Push(v); views.Remove(id); }

            // shield aura
            if (pl.HasValue && sim.Shield > 0)
            {
                shieldRing.enabled = true; shieldRing.transform.localPosition = CellPos(pl.Value.R, pl.Value.C);
                shieldRing.transform.localRotation = Quaternion.Euler(0, 0, -t * 90); shieldRing.color = new Color(1, .82f, .25f, .35f + .25f * pulse);
            }
            else shieldRing.enabled = false;

            // hand and arrow
            if (sim.FingerVis > .02f && sim.AiPlan != null)
            {
                var p = sim.AiPlan;
                bool tele = sim.AiStateNow == AiState.Tele;
                arrow.enabled = tele;
                if (tele)
                {
                    var a = CellPos(p.R1, p.C1); var b = CellPos(p.R2, p.C2); var d = b - a;
                    arrow.transform.localPosition = a + d.normalized * .2f;
                    arrow.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    arrow.transform.localScale = new Vector3(Mathf.Max(.3f, d.magnitude - .3f), .55f, 1);
                    var col = (pv != null && pv.Lethal) ? Palette.Danger : Palette.Gold; col.a = sim.FingerVis * (.55f + .45f * pulse); arrow.color = col;
                }
                hand.enabled = true;
                var hp = CellPos(sim.FingerY - .5f, sim.FingerX - .5f);
                float bob = tele ? Mathf.Sin(t * 12) * .05f : 0;
                float jx = sim.Glitch > 0 ? (UnityEngine.Random.value - .5f) * .15f : 0, jy = sim.Glitch > 0 ? (UnityEngine.Random.value - .5f) * .15f : 0;
                hand.transform.localPosition = hp + new Vector3(.1f + jx, -.05f - bob + jy, 0);
                hand.transform.localScale = Vector3.one * 1.1f;
                var tint = Color.white;
                if (sim.Glitch > 0) tint = UnityEngine.Random.value < .5f ? Palette.Hex("#7ff0ff") : Palette.Hex("#ff7fe6");
                if (sim.Freeze > 0) tint = Palette.Hex("#bfe9ff");
                if (sim.Rush > 0) tint = Palette.Hex("#ffb3c0");
                tint.a = sim.FingerVis; hand.color = tint;
            }
            else { hand.enabled = false; arrow.enabled = false; }

            // shapeshift countdown
            bool showShift = pl.HasValue && sim.Running && sim.L.ShiftEvery > 0 && sim.ShiftT <= 3;
            if (shiftText.gameObject.activeSelf != showShift) shiftText.gameObject.SetActive(showShift);
            if (showShift) { shiftText.text = Mathf.CeilToInt(sim.ShiftT).ToString(); shiftText.transform.localPosition = CellPos(pl.Value.R - .85f, pl.Value.C); }
        }

        // ============================================================
        // effects
        void SpawnFloat(float x, float y, string text, string hex, bool big)
        {
            TextMesh tm;
            if (textPool.Count > 0) { tm = textPool.Pop(); tm.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("Float"); go.transform.SetParent(fxRoot, false);
                tm = go.AddComponent<TextMesh>(); tm.font = UiKit.Font; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
                var mr = go.GetComponent<MeshRenderer>(); mr.material = UiKit.Font.material; mr.sortingOrder = 40;
            }
            tm.text = text; tm.fontSize = 64; tm.characterSize = big ? .09f : .062f;
            floats.Add(new Floater { T = tm, X = x, Y = y, Age = 0, Big = big, C = Palette.Hex(hex) });
        }

        void SpawnFx(FxEvent e)
        {
            var col = e.Color >= 0 ? Palette.GemColor(e.Color) : Color.white;
            if (e.Kind == "flash") { flashAlpha = 1; flashColor = col; return; }
            var fx = new Fx { E = e, T = 0 };
            if (e.Kind == "beam")
            {
                fx.A = NewSprite(fxRoot, "Beam", SpriteFactory.Beam, 20, col);
                fx.B = NewSprite(fxRoot, "BeamCore", SpriteFactory.Beam, 21, Color.white);
            }
            else
            {
                fx.A = NewSprite(fxRoot, "Ring", SpriteFactory.Ring, 20, col);
                fx.B = NewSprite(fxRoot, "RingCore", SpriteFactory.Ring, 21, Color.white);
            }
            fxs.Add(fx);
        }

        void SpawnParticles(int c, int r, Gem g)
        {
            var col = g.Color >= 0 ? Palette.GemColor(g.Color) : Color.white;
            int n = g.IsPlayer ? 16 : 6;
            for (int i = 0; i < n; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2, sp = 2 + UnityEngine.Random.value * 4;
                SpriteRenderer s;
                if (dotPool.Count > 0) { s = dotPool.Pop(); s.enabled = true; } else s = NewSprite(fxRoot, "Dot", SpriteFactory.Dot, 22, Color.white);
                s.color = i % 3 == 0 ? Color.white : col;
                particles.Add(new Particle { S = s, X = c + .5f, Y = r + .5f, VX = Mathf.Cos(a) * sp, VY = -Mathf.Sin(a) * sp + 2, Life = 1, Size = .06f + UnityEngine.Random.value * .08f });
            }
        }

        void ClearFx()
        {
            foreach (var f in fxs) { Destroy(f.A.gameObject); if (f.B) Destroy(f.B.gameObject); }
            fxs.Clear();
            foreach (var p in particles) { p.S.enabled = false; dotPool.Push(p.S); }
            particles.Clear();
            foreach (var f in floats) { f.T.gameObject.SetActive(false); textPool.Push(f.T); }
            floats.Clear();
            flashAlpha = 0;
        }

        void SyncFx(float dt)
        {
            if (sim.Paused) dt = 0;
            for (int i = fxs.Count - 1; i >= 0; i--)
            {
                var f = fxs[i]; f.T += dt / f.E.Dur;
                if (f.T >= 1) { Destroy(f.A.gameObject); Destroy(f.B.gameObject); fxs.RemoveAt(i); continue; }
                float k = 1 - f.T;
                if (f.E.Kind == "beam")
                {
                    var a = CellPos(f.E.Y1 - .5f, f.E.X1 - .5f); var b = CellPos(f.E.Y2 - .5f, f.E.X2 - .5f); var d = b - a;
                    foreach (var s in new[] { f.A, f.B })
                    {
                        s.transform.localPosition = (a + b) / 2; s.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    }
                    f.A.transform.localScale = new Vector3(d.magnitude, 2.8f * k, 1); f.B.transform.localScale = new Vector3(d.magnitude, 1f * k, 1);
                    var ca = f.A.color; ca.a = .9f * k; f.A.color = ca; var cb = f.B.color; cb.a = .9f * k; f.B.color = cb;
                }
                else
                {
                    float rad = Mathf.Lerp(f.E.R0, f.E.R1, 1 - k * k);
                    var pos = CellPos(f.E.Y1 - .5f, f.E.X1 - .5f);
                    f.A.transform.localPosition = pos; f.B.transform.localPosition = pos;
                    f.A.transform.localScale = Vector3.one * (rad / .4375f); f.B.transform.localScale = Vector3.one * (rad / .4375f) * .92f;
                    var ca = f.A.color; ca.a = .9f * k; f.A.color = ca; var cb = f.B.color; cb.a = .6f * k; f.B.color = cb;
                }
            }
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i]; p.X += p.VX * dt; p.Y += p.VY * dt; p.VY -= 14 * dt; p.Life -= dt * 1.6f;
                if (p.Life <= 0) { p.S.enabled = false; dotPool.Push(p.S); particles.RemoveAt(i); continue; }
                p.S.transform.localPosition = CellPos(p.Y - .5f, p.X - .5f); p.S.transform.localScale = Vector3.one * (p.Size / .25f);
                var c = p.S.color; c.a = Mathf.Clamp01(p.Life); p.S.color = c;
            }
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var f = floats[i]; f.Age += dt / (f.Big ? 1.4f : 1f);
                if (f.Age >= 1) { f.T.gameObject.SetActive(false); textPool.Push(f.T); floats.RemoveAt(i); continue; }
                f.T.transform.localPosition = CellPos(f.Y - .5f - f.Age * .8f, f.X - .5f);
                var c = f.C; c.a = 1 - f.Age * f.Age; f.T.color = c;
            }
            if (flashAlpha > 0) { flashAlpha = Mathf.Max(0, flashAlpha - dt / .35f); var c = flashColor; c.a = .5f * flashAlpha; flashImg.color = c; }
            else if (flashImg.color.a > 0) flashImg.color = new Color(1, 1, 1, 0);
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 9);
            Color tint = new Color(0, 0, 0, 0);
            if (sim.Freeze > 0) tint = new Color(.63f, .86f, 1f, .12f);
            else if (sim.Rush > 0) tint = new Color(1f, .23f, .36f, .06f + .05f * pulse);
            else if (sim.Glitch > 0) tint = new Color(.25f, .71f, 1f, .08f);
            tintImg.color = tint;
        }

        // ============================================================
        // HUD
        static string FmtTime(float t) { int m = (int)(t / 60), s = (int)(t % 60); return m + ":" + (s < 10 ? "0" : "") + s; }
        static string FmtNum(int n) => n == int.MaxValue ? "∞" : n.ToString();

        void SyncHud()
        {
            string sig = ((int)sim.Time) + "|" + sim.Supply + "|" + sim.AiLeft + "|" + sim.Dodges + "|" + (int)(sim.Meter * 100) + "|" + Mathf.CeilToInt(sim.Shield) + "|" + Mathf.CeilToInt(sim.Glitch) + "|" + Mathf.CeilToInt(sim.Freeze) + "|" + (int)sim.Score + "|" + sim.Running + "|" + Mathf.CeilToInt(sim.Rush) + "|" + Mathf.CeilToInt(sim.ShiftT) + "|" + sfx.Muted;
            if (sig == lastHud) return; lastHud = sig;
            timeT.text = FmtTime(sim.Time); scoreT.text = ((int)sim.Score).ToString(); dodgesT.text = sim.Dodges.ToString();
            supplyT.text = FmtNum(sim.Supply); movesT.text = FmtNum(sim.AiLeft);
            supplyT.color = sim.Supply == 0 ? Palette.Danger : Palette.Cream; movesT.color = sim.AiLeft <= 5 ? Palette.Danger : Palette.Cream;
            meterFill.fillAmount = sim.Meter;
            var chips = new List<string>();
            if (sim.Shield > 0) chips.Add("SHIELD " + Mathf.CeilToInt(sim.Shield) + "s");
            if (sim.Glitch > 0) chips.Add("GLITCH " + Mathf.CeilToInt(sim.Glitch) + "s");
            if (sim.Freeze > 0) chips.Add("FREEZE " + Mathf.CeilToInt(sim.Freeze) + "s");
            if (sim.Rush > 0) chips.Add("RUSH " + Mathf.CeilToInt(sim.Rush) + "s");
            if (sim.Running && sim.L.ShiftEvery > 0) chips.Add("SHIFT IN " + Mathf.CeilToInt(sim.ShiftT) + "s");
            if (sim.Supply == 0 && sim.Running) chips.Add("SUPPLY EMPTY · BOARD DRAINING");
            if (sim.L != null && sim.L.Infinite && sim.Running) chips.Add("BEST " + SaveData.BestEndless);
            if (sfx.Muted) chips.Add("MUTED");
            chipsT.text = string.Join("   ", chips);
        }

        void RefreshSlots()
        {
            for (int i = 0; i < 3; i++)
            {
                bool filled = i < sim.Slots.Count;
                slotTxt[i].text = filled ? SpecialInfo.PowerupName(sim.Slots[i]).ToUpperInvariant() + "\n<size=10>" + (i + 1) + "</size>" : "<size=10>" + (i + 1) + "</size>";
                slotTxt[i].supportRichText = true;
                slotBtns[i].image.color = filled ? Palette.Panel : Palette.PanelDark;
                slotTxt[i].color = filled ? Palette.Gold : Palette.Muted;
            }
        }

        // ============================================================
        // overlays
        void HideOverlay() { overlayShown = false; overlayKind = ""; overlayPrimary = null; overlayBg.gameObject.SetActive(false); }

        Transform OpenOverlay(string kind)
        {
            overlayShown = true; overlayKind = kind; overlayPrimary = null;
            overlayBg.gameObject.SetActive(true);
            for (int i = overlayRoot.childCount - 1; i >= 0; i--) Destroy(overlayRoot.GetChild(i).gameObject);
            var v = UiKit.VStack(overlayRoot, 10, TextAnchor.MiddleCenter, 16);
            UiKit.Stretch(v.GetComponent<RectTransform>());
            return v.transform;
        }

        Text Title(Transform p, string s, int size = 44) { var t = UiKit.Label(p, s, size, Palette.Gold); UiKit.Size(t, 500, size + 10); return t; }
        Text Para(Transform p, string s, int size = 15, float h = 44) { var t = UiKit.Label(p, s, size, Palette.Hex("#e3dcff"), TextAnchor.MiddleCenter, FontStyle.Normal); UiKit.Size(t, 460, h); return t; }

        void ShowMenu()
        {
            var v = OpenOverlay("menu");
            Title(v, "LAST MATCH");
            Para(v, "You are the gem that woke up. Don't get matched.", 15, 26);
            menuTier = LevelGen.TierOf(Mathf.Min(LevelGen.TotalLevels, SaveData.Beaten + 1));
            var page = UiKit.VStack(v, 8, TextAnchor.UpperCenter); UiKit.Size(page, 460, 560);
            BuildLevelPage(page.transform);
            var row = UiKit.HStack(v, 10);
            UiKit.MakeButton(row.transform, "HOW TO PLAY", Palette.Panel, Palette.Cream, 14, ShowHelp, 150, 44);
            UiKit.MakeButton(row.transform, sfx.Muted ? "SOUND: OFF" : "SOUND: ON", Palette.Panel, Palette.Cream, 14, () => { ToggleMute(); ShowMenu(); }, 150, 44);
        }

        void BuildLevelPage(Transform page)
        {
            for (int i = page.childCount - 1; i >= 0; i--) Destroy(page.GetChild(i).gameObject);
            LevelGen.TierSpan(menuTier, out int first, out int last);
            var bar = UiKit.HStack(page, 8); UiKit.Size(bar, 460, 50);
            var prev = UiKit.MakeButton(bar.transform, "<", Palette.PanelDark, Palette.Cream, 22, () => { menuTier = Mathf.Max(0, menuTier - 1); BuildLevelPage(page); }, 46, 46);
            prev.interactable = menuTier > 0;
            var nameBox = UiKit.VStack(bar.transform, 0, TextAnchor.MiddleCenter); UiKit.Size(nameBox, 340, 48);
            var tn = UiKit.Label(nameBox.transform, LevelGen.Tiers[menuTier].Name.ToUpperInvariant(), 22, Palette.Gold); UiKit.Size(tn, 340, 28);
            var ts = UiKit.Label(nameBox.transform, "LEVELS " + first + " – " + last, 11, Palette.Muted); UiKit.Size(ts, 340, 16);
            var next = UiKit.MakeButton(bar.transform, ">", Palette.PanelDark, Palette.Cream, 22, () => { menuTier = Mathf.Min(LevelGen.Tiers.Length - 1, menuTier + 1); BuildLevelPage(page); }, 46, 46);
            next.interactable = menuTier < LevelGen.Tiers.Length - 1;
            Para(page, LevelGen.Tiers[menuTier].Desc, 13, 48);
            var grid = UiKit.Grid(page, new Vector2(84, 52), new Vector2(6, 6), 5); UiKit.Size(grid, 450, 6 * 58);
            int beaten = SaveData.Beaten;
            for (int n = first; n <= last; n++)
            {
                int lvl = n;
                bool done = beaten >= n, avail = beaten + 1 >= n;
                string sub = done ? (SaveData.Best(n) > 0 ? SaveData.Best(n).ToString() : "done") : avail ? "play" : "locked";
                var b = UiKit.MakeButton(grid.transform, n + "\n<size=10>" + sub + "</size>", avail ? (done ? Palette.PanelDark : Palette.Gold) : new Color(.16f, .12f, .39f, .5f), done ? Palette.Ok : avail ? Palette.Ink : Palette.Muted, 18, () => StartLevel(lvl), 84, 52);
                b.GetComponentInChildren<Text>().supportRichText = true;
                b.interactable = avail;
                if (!avail && !done) { var le = b.GetComponent<LayoutElement>(); le.ignoreLayout = false; }
            }
            var endless = UiKit.MakeButton(page, "ENDLESS  ·  best " + SaveData.BestEndless + "\n<size=11>Infinite gems and moves. Beat your best score.</size>", Palette.Hex("#4b2a7a"), Palette.Cream, 17, () => StartLevel(0), 450, 60);
            endless.GetComponentInChildren<Text>().supportRichText = true;
            overlayPrimary = () => StartLevel(Mathf.Min(LevelGen.TotalLevels, beaten + 1));
        }

        void ShowHelp()
        {
            var v = OpenOverlay("help");
            Title(v, "HOW TO PLAY", 34);
            Para(v, "MOVE: swipe or tap a neighbouring cell. Every move is a swap, and your swaps follow match-3 rules too. Line yourself up with two of your color and you're gone.", 14, 70);
            Para(v, "THE HAND shows where the AI player will swipe next. If your gem glows red, that swipe takes you out. Get out of the line.", 14, 56);
            Para(v, "SPECIALS appear as you climb the tiers: blasters, bombs, color bombs, crosses, diagonals and seekers. A special wears a badge showing what it does and keeps its gem color. Match it, or hit it with another blast, to set it off.", 14, 84);
            Para(v, "LOCKS: caged gems can't be swapped but can be matched. Fogged gems are hidden and can't be matched. Clearing anything next to a lock breaks it. Holes are just holes.", 14, 70);
            Para(v, "WIN by outlasting the AI's move budget, or by leaving it no legal move once the gem supply drains. Surviving fills the meter and earns power-ups. Endless never ends; chase the score.", 14, 70);
            UiKit.MakeButton(v, "BACK", Palette.Gold, Palette.Ink, 18, ShowMenu, 200, 52);
            overlayPrimary = ShowMenu;
        }

        void ShowPause()
        {
            var v = OpenOverlay("pause");
            Title(v, "PAUSED", 36);
            Para(v, "The AI is taking a coffee break.", 15, 26);
            var row = UiKit.HStack(v, 10);
            UiKit.MakeButton(row.transform, "RESUME", Palette.Gold, Palette.Ink, 18, TogglePause, 170, 54);
            UiKit.MakeButton(row.transform, "MENU", Palette.Panel, Palette.Cream, 18, ToMenu, 140, 54);
            UiKit.MakeButton(v, sfx.Muted ? "SOUND: OFF" : "SOUND: ON", Palette.Panel, Palette.Cream, 14, () => { ToggleMute(); ShowPause(); }, 150, 40);
            overlayPrimary = TogglePause;
        }

        void ShowResult(bool win, bool newBest)
        {
            var v = OpenOverlay(win ? "win" : "lose");
            var L = sim.L;
            if (win)
            {
                Title(v, L.Infinite ? "RUN OVER" : "LEVEL " + L.Id + " CLEARED", 36);
                Para(v, sim.WinReason + " You are the last match standing.", 14, 44);
            }
            else
            {
                Title(v, "MATCHED", 40);
                var c = Para(v, sim.CauseText, 15, 40); c.color = Palette.Danger;
                Para(v, sim.CauseByPlayer ? "And you did it to yourself. Ouch." : "The AI player got you.", 14, 26);
            }
            var stats = UiKit.HStack(v, 14); UiKit.Size(stats, 460, 50);
            ResultStat(stats.transform, ((int)sim.Score).ToString(), "SCORE"); ResultStat(stats.transform, FmtTime(sim.Time), "SURVIVED"); ResultStat(stats.transform, sim.AiMoves.ToString(), "AI SWIPES"); ResultStat(stats.transform, sim.Dodges.ToString(), "DODGED");
            if (newBest) { var b = Para(v, "New best score!", 16, 26); b.color = Palette.Ok; }
            int nextN = L.Id + 1; bool hasNext = !L.Infinite && win && nextN <= LevelGen.TotalLevels;
            if (hasNext && LevelGen.TierOf(nextN) != LevelGen.TierOf(L.Id))
            {
                var nf = Para(v, "NEW IN " + LevelGen.Tiers[LevelGen.TierOf(nextN)].Name.ToUpperInvariant() + ": " + LevelGen.Tiers[LevelGen.TierOf(nextN)].Desc, 13, 64); nf.color = Palette.Gold;
            }
            var row = UiKit.HStack(v, 10);
            if (win)
            {
                if (hasNext) { UiKit.MakeButton(row.transform, "LEVEL " + nextN, Palette.Gold, Palette.Ink, 18, () => StartLevel(nextN), 160, 54); overlayPrimary = () => StartLevel(nextN); }
                else { UiKit.MakeButton(row.transform, "PLAY ENDLESS", Palette.Gold, Palette.Ink, 18, () => StartLevel(0), 180, 54); overlayPrimary = () => StartLevel(0); }
                UiKit.MakeButton(row.transform, "REPLAY", Palette.Panel, Palette.Cream, 16, () => StartLevel(currentLevel), 120, 54);
            }
            else
            {
                UiKit.MakeButton(row.transform, L.Infinite ? "GO AGAIN" : "RETRY", Palette.Gold, Palette.Ink, 18, () => StartLevel(currentLevel), 160, 54); overlayPrimary = () => StartLevel(currentLevel);
            }
            UiKit.MakeButton(row.transform, "MENU", Palette.Panel, Palette.Cream, 16, ToMenu, 110, 54);
        }

        void ResultStat(Transform p, string val, string label)
        {
            var v = UiKit.VStack(p, 0, TextAnchor.MiddleCenter); UiKit.Size(v, 100, 50);
            var a = UiKit.Label(v.transform, val, 24, Palette.Cream); UiKit.Size(a, 100, 30);
            var b = UiKit.Label(v.transform, label, 10, Palette.Muted); UiKit.Size(b, 100, 16);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && sim.Running && !sim.Paused) TogglePause();
        }
    }
}
