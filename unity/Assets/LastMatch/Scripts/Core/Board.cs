using System;
using System.Collections.Generic;

namespace LastMatch.Core
{
    /// <summary>The grid, the cell types, and every match-3 rule. No Unity dependency.</summary>
    public class Board
    {
        public int Cols, Rows, NColors;
        public Gem[,] Grid;
        public CellType[,] Cells;
        public Random Rng = new Random();
        int nextId = 1;

        public int Key(int r, int c) => r * Cols + c;
        public int Key(Pos p) => p.R * Cols + p.C;
        public Pos FromKey(int k) => new Pos(k / Cols, k % Cols);
        public bool InB(int r, int c) => r >= 0 && r < Rows && c >= 0 && c < Cols;
        public bool IsOpen(int r, int c) => InB(r, c) && Cells[r, c] == CellType.Open;
        public bool IsLocked(int r, int c) => InB(r, c) && (Cells[r, c] == CellType.Cage || Cells[r, c] == CellType.Fog);
        public Gem At(int r, int c) => InB(r, c) ? Grid[r, c] : null;

        public Gem MakeGem(int color, Special sp = Special.None)
        {
            return new Gem { Id = nextId++, Color = color, Special = sp, Wob = (float)(Rng.NextDouble() * 6.28) };
        }

        public Pos? FindPlayer()
        {
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) { var g = Grid[r, c]; if (g != null && g.IsPlayer) return new Pos(r, c); }
            return null;
        }

        public Gem PlayerGem()
        {
            var p = FindPlayer(); return p.HasValue ? Grid[p.Value.R, p.Value.C] : null;
        }

        public void Swap(Pos a, Pos b)
        {
            var t = Grid[a.R, a.C]; Grid[a.R, a.C] = Grid[b.R, b.C]; Grid[b.R, b.C] = t;
        }

        // ---------- setup ----------

        public void BuildCells(Level L)
        {
            Cols = L.Cols; Rows = L.Rows; NColors = L.Colors;
            Cells = new CellType[Rows, Cols];
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++)
            {
                char ch = L.Mask != null ? L.Mask[r][c] : '#';
                Cells[r, c] = ch == '.' ? CellType.Hole : ch == 'L' ? CellType.Cage : ch == 'H' ? CellType.Fog : CellType.Open;
            }
        }

        /// <summary>Fills the board with no initial matches, the player at the level's start cell, and at least a few AI moves.</summary>
        public void Fill(Level L)
        {
            BuildCells(L);
            for (int tries = 0; tries < 300; tries++)
            {
                var grid = new Gem[Rows, Cols];
                for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++)
                {
                    if (Cells[r, c] == CellType.Hole) { grid[r, c] = null; continue; }
                    int col, guard = 0;
                    do
                    {
                        col = Rng.Next(NColors); guard++;
                    } while (guard < 30 && (
                        (c >= 2 && grid[r, c - 1] != null && grid[r, c - 2] != null && grid[r, c - 1].Color == col && grid[r, c - 2].Color == col) ||
                        (r >= 2 && grid[r - 1, c] != null && grid[r - 2, c] != null && grid[r - 1, c].Color == col && grid[r - 2, c].Color == col)));
                    var g = MakeGem(col); g.RX = c;
                    if (Cells[r, c] == CellType.Open) g.RY = r - Rows - 1 - (float)Rng.NextDouble() * 2f; else { g.RY = r; g.Born = 1f; }
                    grid[r, c] = g;
                }
                int pr = L.Start.R, pc = L.Start.C;
                if (grid[pr, pc] == null || Cells[pr, pc] != CellType.Open) continue;
                grid[pr, pc].IsPlayer = true;
                Grid = grid;
                if (FindMatches().Count == 0 && AllMoves(L.Feats).Count >= 3) return;
            }
        }

        // ---------- matching ----------

        bool Matchable(int r, int c)
        {
            var g = Grid[r, c]; return g != null && g.Color >= 0 && Cells[r, c] != CellType.Fog;
        }

        public List<Run> FindMatches()
        {
            var runs = new List<Run>();
            for (int r = 0; r < Rows; r++)
            {
                int c = 0;
                while (c < Cols)
                {
                    if (!Matchable(r, c)) { c++; continue; }
                    int col = Grid[r, c].Color; int e = c + 1;
                    while (e < Cols && Matchable(r, e) && Grid[r, e].Color == col) e++;
                    if (e - c >= 3) { var run = new Run { Horizontal = true, Color = col }; for (int x = c; x < e; x++) run.Cells.Add(new Pos(r, x)); runs.Add(run); }
                    c = e;
                }
            }
            for (int c = 0; c < Cols; c++)
            {
                int r = 0;
                while (r < Rows)
                {
                    if (!Matchable(r, c)) { r++; continue; }
                    int col = Grid[r, c].Color; int e = r + 1;
                    while (e < Rows && Matchable(e, c) && Grid[e, c].Color == col) e++;
                    if (e - r >= 3) { var run = new Run { Horizontal = false, Color = col }; for (int y = r; y < e; y++) run.Cells.Add(new Pos(y, c)); runs.Add(run); }
                    r = e;
                }
            }
            return runs;
        }

        public int MostCommonColor()
        {
            var cnt = new int[NColors];
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) { var g = Grid[r, c]; if (g != null && g.Color >= 0) cnt[g.Color]++; }
            int best = -1, bv = 0;
            for (int i = 0; i < NColors; i++) if (cnt[i] > bv) { bv = cnt[i]; best = i; }
            return best;
        }

        /// <summary>Cells a special at (r,c) hits when it goes off.</summary>
        public List<Pos> EffectCells(int r, int c, Special sp, Pos? pl)
        {
            var outl = new List<Pos>();
            int n = Math.Max(Rows, Cols);
            void Line(int dr, int dc) { for (int i = -n; i <= n; i++) outl.Add(new Pos(r + dr * i, c + dc * i)); }
            void Box(int k) { for (int dr = -k; dr <= k; dr++) for (int dc = -k; dc <= k; dc++) outl.Add(new Pos(r + dr, c + dc)); }
            switch (sp)
            {
                case Special.H: Line(0, 1); break;
                case Special.V: Line(1, 0); break;
                case Special.Cross: Line(0, 1); Line(1, 0); break;
                case Special.Diag: Line(1, 1); Line(1, -1); break;
                case Special.Bomb: Box(1); break;
                case Special.Mega: Box(2); break;
                case Special.Seeker:
                    outl.Add(new Pos(r, c));
                    if (pl.HasValue) for (int dr = -1; dr <= 1; dr++) for (int dc = -1; dc <= 1; dc++) outl.Add(new Pos(pl.Value.R + dr, pl.Value.C + dc));
                    break;
                case Special.Rainbow:
                    int col = MostCommonColor();
                    if (col >= 0) for (int rr = 0; rr < Rows; rr++) for (int cc = 0; cc < Cols; cc++) { var g = Grid[rr, cc]; if (g != null && g.Color == col) outl.Add(new Pos(rr, cc)); }
                    break;
            }
            return outl;
        }

        /// <summary>Expands matched runs plus seeds into cell -> cause, chaining through every special that gets hit. Fog cells are never cleared.</summary>
        public Dictionary<int, Cause> ExpandClear(List<Run> runs, List<Seed> seeds)
        {
            var clear = new Dictionary<int, Cause>();
            var q = new Queue<Pos>();
            var pl = FindPlayer();
            void Add(int r, int c, Cause cause)
            {
                if (!InB(r, c)) return; var g = Grid[r, c]; if (g == null) return;
                if (Cells[r, c] == CellType.Fog) return;
                int k = Key(r, c); if (clear.ContainsKey(k)) return; clear[k] = cause;
                if (g.Special != Special.None && !(g.Special == Special.Rainbow && cause == Cause.Rainbow)) q.Enqueue(new Pos(r, c));
            }
            if (runs != null) foreach (var run in runs) foreach (var cell in run.Cells) Add(cell.R, cell.C, Cause.Match);
            if (seeds != null) foreach (var s in seeds) Add(s.R, s.C, s.Cause);
            while (q.Count > 0)
            {
                var p = q.Dequeue(); var g = Grid[p.R, p.C];
                foreach (var e in EffectCells(p.R, p.C, g.Special, pl)) Add(e.R, e.C, SpecialInfo.ToCause(g.Special));
            }
            return clear;
        }

        static readonly int[] DR = { 0, 1, -1, 0, 0 };
        static readonly int[] DC = { 0, 0, 0, 1, -1 };

        public HashSet<int> CollectUnlocks(Dictionary<int, Cause> clear)
        {
            var outs = new HashSet<int>();
            foreach (var k in clear.Keys)
            {
                var p = FromKey(k);
                for (int i = 0; i < 5; i++) if (IsLocked(p.R + DR[i], p.C + DC[i])) outs.Add(Key(p.R + DR[i], p.C + DC[i]));
            }
            return outs;
        }

        static readonly HashSet<Special> Lines = new HashSet<Special> { Special.H, Special.V, Special.Cross, Special.Diag };
        static readonly HashSet<Special> Bombs = new HashSet<Special> { Special.Bomb, Special.Mega };

        /// <summary>What swapping the gems now at A and B does. Null means normal match rules apply.</summary>
        public List<Seed> SwapSeeds(Pos a, Pos b, bool combos)
        {
            var gA = Grid[a.R, a.C]; var gB = Grid[b.R, b.C];
            if (gA == null || gB == null) return null;
            var sA = gA.Special; var sB = gB.Special;
            var seeds = new List<Seed>();
            void Push(int r, int c, Cause cause) => seeds.Add(new Seed(r, c, cause));
            if (sA == Special.Rainbow || sB == Special.Rainbow)
            {
                if (sA == Special.Rainbow && sB == Special.Rainbow)
                {
                    for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) if (Grid[r, c] != null) Push(r, c, Cause.Rainbow);
                    return seeds;
                }
                var R = sA == Special.Rainbow ? a : b; var O = sA == Special.Rainbow ? gB : gA;
                Push(R.R, R.C, Cause.Rainbow);
                for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) { var g = Grid[r, c]; if (g != null && g.Color == O.Color) Push(r, c, Cause.Rainbow); }
                return seeds;
            }
            if (sA == Special.None || sB == Special.None || !combos) return null;
            if (Lines.Contains(sA) && Lines.Contains(sB))
            {
                foreach (var e in EffectCells(b.R, b.C, Special.Cross, null)) Push(e.R, e.C, Cause.Combo);
                if (sA == Special.Diag || sB == Special.Diag) foreach (var e in EffectCells(b.R, b.C, Special.Diag, null)) Push(e.R, e.C, Cause.Combo);
                Push(a.R, a.C, Cause.Combo); return seeds;
            }
            if ((Bombs.Contains(sA) && Lines.Contains(sB)) || (Bombs.Contains(sB) && Lines.Contains(sA)))
            {
                for (int d = -1; d <= 1; d++)
                {
                    foreach (var e in EffectCells(b.R + d, b.C, Special.H, null)) Push(e.R, e.C, Cause.Combo);
                    foreach (var e in EffectCells(b.R, b.C + d, Special.V, null)) Push(e.R, e.C, Cause.Combo);
                }
                Push(a.R, a.C, Cause.Combo); return seeds;
            }
            if (Bombs.Contains(sA) && Bombs.Contains(sB))
            {
                foreach (var e in EffectCells(b.R, b.C, Special.Mega, null)) Push(e.R, e.C, Cause.Combo);
                Push(a.R, a.C, Cause.Combo); return seeds;
            }
            Push(a.R, a.C, Cause.Combo); Push(b.R, b.C, Cause.Combo); return seeds;
        }

        public List<Created> PlanSpecials(List<Run> runs, HashSet<int> pivotKeys, int protectedKey, Features f)
        {
            var created = new List<Created>();
            var consumed = new HashSet<Run>();
            if (f.Bombs)
            {
                var inH = new Dictionary<int, Run>(); var inV = new Dictionary<int, Run>();
                foreach (var run in runs) { var m = run.Horizontal ? inH : inV; foreach (var cell in run.Cells) m[Key(cell)] = run; }
                foreach (var kv in inH)
                {
                    if (!inV.TryGetValue(kv.Key, out var runV)) continue;
                    if (consumed.Contains(kv.Value) || consumed.Contains(runV)) continue;
                    consumed.Add(kv.Value); consumed.Add(runV);
                    if (kv.Key == protectedKey) continue;
                    created.Add(new Created { Key = kv.Key, Special = Special.Bomb, Color = kv.Value.Color });
                }
            }
            foreach (var run in runs)
            {
                if (consumed.Contains(run) || run.Cells.Count < 4) continue;
                int n = run.Cells.Count;
                Special sp = Special.None;
                if (n >= 6 && f.Cross) sp = Special.Cross;
                else if (n >= 5 && f.Rainbow) sp = Special.Rainbow;
                else if (f.Blasters) sp = run.Horizontal ? Special.V : Special.H;
                if (sp == Special.None) continue;
                var keys = new List<int>();
                foreach (var cell in run.Cells) { int k = Key(cell); if (k != protectedKey) keys.Add(k); }
                if (keys.Count == 0) continue;
                int chosen = -1;
                foreach (var k in keys) if (pivotKeys != null && pivotKeys.Contains(k)) { chosen = k; break; }
                if (chosen < 0) chosen = keys[keys.Count / 2];
                created.Add(new Created { Key = chosen, Special = sp, Color = sp == Special.Rainbow ? -1 : run.Color });
            }
            return created;
        }

        /// <summary>Evaluates a swap without side effects. Null when the swap does nothing.</summary>
        public SwapEval EvalSwap(int r1, int c1, int r2, int c2, Features f)
        {
            if (!IsOpen(r1, c1) || !IsOpen(r2, c2)) return null;
            var a = Grid[r1, c1]; var b = Grid[r2, c2];
            if (a == null || b == null) return null;
            var A = new Pos(r1, c1); var B = new Pos(r2, c2);
            Swap(A, B);
            Dictionary<int, Cause> clear = null; int createdCount = 0;
            var seeds = SwapSeeds(A, B, f.Combos);
            if (seeds != null) clear = ExpandClear(null, seeds);
            else
            {
                var runs = FindMatches();
                if (runs.Count > 0) { clear = ExpandClear(runs, null); createdCount = PlanSpecials(runs, new HashSet<int> { Key(r1, c1), Key(r2, c2) }, -1, f).Count; }
            }
            var pl = clear != null ? FindPlayer() : null;
            Swap(A, B);
            if (clear == null) return null;
            bool lethal = pl.HasValue && clear.ContainsKey(Key(pl.Value));
            return new SwapEval { R1 = r1, C1 = c1, R2 = r2, C2 = c2, Clear = clear, Lethal = lethal, Score = clear.Count + createdCount * 4 + (lethal ? 100 : 0) };
        }

        public List<SwapEval> AllMoves(Features f)
        {
            var moves = new List<SwapEval>();
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++)
            {
                if (c + 1 < Cols) { var m = EvalSwap(r, c, r, c + 1, f); if (m != null) moves.Add(m); }
                if (r + 1 < Rows) { var m = EvalSwap(r, c, r + 1, c, f); if (m != null) moves.Add(m); }
            }
            return moves;
        }

        // ---------- gravity ----------

        /// <summary>Gravity runs inside each column segment of open cells. Holes and locks are floors and ceilings. Returns true when anything moved or spawned.</summary>
        public bool ApplyGravity(Func<Special> spawnSpecial, ref int supply, bool infinite)
        {
            bool moved = false;
            for (int c = 0; c < Cols; c++)
            {
                int r = Rows - 1;
                while (r >= 0)
                {
                    if (Cells[r, c] != CellType.Open) { r--; continue; }
                    int bottom = r; while (r >= 0 && Cells[r, c] == CellType.Open) r--; int top = r + 1;
                    int w = bottom;
                    for (int i = bottom; i >= top; i--)
                    {
                        var g = Grid[i, c]; if (g == null) continue;
                        if (i != w) { Grid[w, c] = g; Grid[i, c] = null; moved = true; }
                        w--;
                    }
                    int k = w - top + 1;
                    for (int i = w; i >= top; i--)
                    {
                        if (!infinite && supply <= 0) break;
                        var g = MakeGem(Rng.Next(NColors), spawnSpecial != null ? spawnSpecial() : Special.None); g.RX = c;
                        if (top == 0) g.RY = i - k - 0.3f; else { g.RY = i; g.Born = 1f; }
                        Grid[i, c] = g; if (!infinite) supply--; moved = true;
                    }
                }
            }
            return moved;
        }

        public bool AllSettled()
        {
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) { var g = Grid[r, c]; if (g != null && (g.RX != c || g.RY != r)) return false; }
            return true;
        }

        public bool Shuffle(Features f)
        {
            var cells = new List<Pos>(); var gems = new List<Gem>();
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) { var g = Grid[r, c]; if (g != null && !g.IsPlayer && Cells[r, c] == CellType.Open) { cells.Add(new Pos(r, c)); gems.Add(g); } }
            for (int tries = 0; tries < 60; tries++)
            {
                for (int i = gems.Count - 1; i > 0; i--) { int j = Rng.Next(i + 1); var t = gems[i]; gems[i] = gems[j]; gems[j] = t; }
                for (int i = 0; i < cells.Count; i++) Grid[cells[i].R, cells[i].C] = gems[i];
                if (FindMatches().Count == 0 && AllMoves(f).Count > 0) return true;
            }
            return false;
        }

        /// <summary>Counts of each cell type, for tests and debug HUDs.</summary>
        public void Count(out int open, out int holes, out int cages, out int fog)
        {
            open = holes = cages = fog = 0;
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++)
                switch (Cells[r, c]) { case CellType.Open: open++; break; case CellType.Hole: holes++; break; case CellType.Cage: cages++; break; default: fog++; break; }
        }
    }
}
