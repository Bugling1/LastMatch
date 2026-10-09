using System;
using System.Collections;
using System.Collections.Generic;

namespace LastMatch.Core
{
    public class Tuning
    {
        public float AiSpeed = 1f;
        public float FirstMove = 1f, ThinkStart = 1f, ThinkEnd = .35f, TeleStart = .55f, TeleEnd = .25f, RampSeconds = 60f, AggroStart = .6f, AggroEnd = .95f;
        public float MeterTime = 9f;
        public int MaxSlots = 3;
        public float PopSeconds = .23f;
        public float SwapSpeed = 9f, Gravity = 45f;
        public int ScorePerSecond = 10, ScoreDodge = 150, ScoreAiSwipe = 25, ScoreCombo = 60, ScoreUnlock = 40;
    }

    public enum AiState { Idle, Tele, Exec }

    public struct FxEvent
    {
        public string Kind;        // beam, ring, flash
        public float X1, Y1, X2, Y2, R0, R1, Dur;
        public int Color;          // gem color index, -1 white
        public string Hex;         // explicit color when Color < -1
    }

    public class GameSim
    {
        public readonly Board B = new Board();
        public Level L;
        public Features F => L.Feats;
        public Tuning T = new Tuning();

        public float Time, Score; float scoreAcc;
        public int AiMoves, AiLeft, Dodges, Cleared, Supply;
        public bool Busy, Dead, Won, Running, Paused;
        public string CauseText = "", WinReason = "";
        public bool CauseByPlayer;

        public AiState AiStateNow = AiState.Idle;
        public float AiThink, AiTele, AiInterval = 1f, AiTeleTime = .5f, Aggression = .6f;
        public SwapEval AiPlan, AiPreview;
        public bool AiWasLethal;

        public bool FingerActive; public float FingerVis, FingerX, FingerY, FingerTX, FingerTY;
        public float Shield, Glitch, Freeze, Meter, Rush, ShiftT;
        float unlockT, cageT, rushT; bool shiftWarned;
        public readonly List<PowerupType> Slots = new List<PowerupType>();
        public readonly List<Gem> Popping = new List<Gem>();
        public float Shake, Blink; float blinkT = 2f;
        public int LastDX, LastDY;
        public bool SupplyWarned;

        // events for the view
        public event Action<float, float, string, string, bool> OnFloat;   // x, y, text, hex color, big
        public event Action<FxEvent> OnFx;
        public event Action<string> OnSfx;
        public event Action<int, int, Gem> OnParticles;                      // c, r, gem
        public event Action<bool> OnGameOver;
        public event Action OnSlotsChanged;

        public static readonly object WaitSettle = new object();
        Stack<IEnumerator> proc; float procWait; bool procSettle; int settleGuard;

        struct Queued { public int DR, DC; public float T; }
        Queued? queued; float playerCd;
        readonly Random rnd = new Random();

        // ---------- level flow ----------

        public void StartLevel(Level level)
        {
            L = level;
            Supply = L.Infinite ? int.MaxValue : L.Supply; AiLeft = L.Infinite ? int.MaxValue : L.AiMoves;
            Busy = false; Dead = false; Won = false; Running = true; Paused = false;
            Time = 0; Score = 0; scoreAcc = 0; AiMoves = 0; Dodges = 0; Cleared = 0; playerCd = 0; queued = null;
            Shield = Glitch = Freeze = Meter = Rush = 0; Slots.Clear();
            unlockT = L.UnlockEvery; cageT = L.CageEvery; ShiftT = L.ShiftEvery; shiftWarned = false; rushT = L.RushEvery;
            Popping.Clear(); CauseText = ""; WinReason = ""; SupplyWarned = false; Shake = 0;
            AiStateNow = AiState.Idle; AiThink = T.FirstMove / PaceMultiplier(); AiPlan = null; AiPreview = null; AiWasLethal = false;
            FingerActive = false; FingerVis = 0;
            proc = null;
            B.Fill(L);
            OnSlotsChanged?.Invoke();
            StartProc(InitialSettle());
        }

        IEnumerator InitialSettle() { yield return WaitSettle; }

        public void Stop() { Running = false; Paused = false; FingerActive = false; AiPreview = null; proc = null; Busy = false; }

        void EndGame(bool win)
        {
            if (!Running) return;
            Running = false; Won = win; FingerActive = false; AiPreview = null;
            if (win) { Sfx("win"); var pl = B.FindPlayer(); if (pl.HasValue) Float(pl.Value.C + .5f, pl.Value.R, "AI is stuck!", "#4ee089", true); }
            OnGameOver?.Invoke(win);
        }

        public void TogglePause()
        {
            if (Paused) { Paused = false; return; }
            if (!Running) return;
            Paused = true;
        }

        // ---------- helpers ----------

        void Float(float x, float y, string text, string hex = "#ffd23f", bool big = false) => OnFloat?.Invoke(x, y, text, hex, big);
        void Sfx(string name) => OnSfx?.Invoke(name);
        public Pos? Player => B.FindPlayer();

        float PaceMultiplier()
        {
            float m = T.AiSpeed * (L.Speed > 0 ? L.Speed : 1f);
            if (L.Infinite) m *= 1f + Math.Min(1.6f, Time / 120f);
            if (Rush > 0) m *= 2f;
            return m;
        }

        Special SpawnSpecial()
        {
            if (L.Spawn == null || L.Spawn.Count == 0) return Special.None;
            float boost = 1f; if (L.Infinite) boost = 1f + Math.Min(3f, Time / 60f);
            foreach (var kv in L.Spawn) if (rnd.NextDouble() < kv.Value * boost) return kv.Key;
            return Special.None;
        }

        void KillPlayer(Cause cause, bool byPlayer)
        {
            Dead = true; CauseText = SpecialInfo.CauseText(cause); CauseByPlayer = byPlayer; Shake = .6f; Sfx("death");
        }

        void UnlockCell(int r, int c, bool announce)
        {
            if (!B.IsLocked(r, c)) return;
            B.Cells[r, c] = CellType.Open; var g = B.Grid[r, c]; if (g != null) g.Born = .6f;
            if (announce) { Float(c + .5f, r, "Unlocked!", "#4ee089"); Sfx("unlock"); Score += T.ScoreUnlock; }
        }

        // ---------- processes (the async flow of the web version, as tickable iterators) ----------

        void StartProc(IEnumerator e)
        {
            proc = new Stack<IEnumerator>(); proc.Push(e); Busy = true; procWait = 0; procSettle = false; settleGuard = 0;
        }

        void AdvanceProc(float dt)
        {
            if (proc == null) return;
            if (procWait > 0) { procWait -= dt; if (procWait > 0) return; }
            if (procSettle) { settleGuard++; if (!B.AllSettled() && settleGuard < 250) return; procSettle = false; }
            var st = proc;
            while (st == proc && st.Count > 0)
            {
                var top = st.Peek();
                bool more = top.MoveNext();
                if (st != proc) return;                  // a new process replaced this one mid-step
                if (!more) { st.Pop(); continue; }
                var cur = top.Current;
                if (cur is IEnumerator inner) { st.Push(inner); continue; }
                if (cur is float f) { procWait = f; return; }
                if (cur == WaitSettle) { procSettle = true; settleGuard = 0; return; }
                return; // null: wait one tick
            }
            if (st == proc && st.Count == 0) { proc = null; Busy = false; }
        }

        void PopCells(Dictionary<int, Cause> clear, int combo)
        {
            if (clear.Count == 0) return;
            bool hitSpecial = false;
            foreach (var k in clear.Keys)
            {
                var p = B.FromKey(k); var g = B.Grid[p.R, p.C]; if (g == null) continue;
                if (g.Special != Special.None) { hitSpecial = true; SpecialFx(p.R, p.C, g); }
                B.Grid[p.R, p.C] = null; g.Pop = 0; Popping.Add(g); OnParticles?.Invoke(p.C, p.R, g);
                if (B.Cells[p.R, p.C] == CellType.Cage) B.Cells[p.R, p.C] = CellType.Open;
            }
            Cleared += clear.Count;
            Sfx("pop" + Math.Min(combo, 6));
            if (hitSpecial) { Shake = .4f; Sfx("blast"); }
        }

        void SpecialFx(int r, int c, Gem g)
        {
            void Beam(float r1, float c1, float r2, float c2) => OnFx?.Invoke(new FxEvent { Kind = "beam", X1 = c1 + .5f, Y1 = r1 + .5f, X2 = c2 + .5f, Y2 = r2 + .5f, Color = g.Color, Dur = .4f });
            void Ring(float rr, float cc, float rad) => OnFx?.Invoke(new FxEvent { Kind = "ring", X1 = cc + .5f, Y1 = rr + .5f, R0 = .3f, R1 = rad, Color = g.Color, Dur = .45f });
            var sp = g.Special;
            if (sp == Special.H || sp == Special.Cross) Beam(r, -1, r, B.Cols);
            if (sp == Special.V || sp == Special.Cross) Beam(-1, c, B.Rows, c);
            if (sp == Special.Diag) { int n = Math.Max(B.Rows, B.Cols); Beam(r - n, c - n, r + n, c + n); Beam(r - n, c + n, r + n, c - n); }
            if (sp == Special.Bomb) Ring(r, c, 1.9f);
            if (sp == Special.Mega) { Ring(r, c, 2.9f); Ring(r, c, 1.6f); }
            if (sp == Special.Seeker) { var pl = B.FindPlayer(); if (pl.HasValue) { Beam(r, c, pl.Value.R, pl.Value.C); Ring(pl.Value.R, pl.Value.C, 1.9f); } else Ring(r, c, 1f); }
            if (sp == Special.Rainbow) OnFx?.Invoke(new FxEvent { Kind = "flash", Color = -1, Dur = .35f });
        }

        IEnumerator ResolveBoard(HashSet<int> pivotKeys, List<Seed> seeds, bool byPlayer)
        {
            int combo = 0;
            while (true)
            {
                var runs = seeds != null ? new List<Run>() : B.FindMatches();
                if (seeds == null && runs.Count == 0)
                {
                    if (B.ApplyGravity(SpawnSpecial, ref Supply, L.Infinite)) { yield return WaitSettle; continue; }
                    break;
                }
                combo++;
                var pl = B.FindPlayer();
                int protectedKey = (pl.HasValue && Shield > 0) ? B.Key(pl.Value) : -1;
                var created = seeds != null ? new List<Created>() : B.PlanSpecials(runs, pivotKeys, protectedKey, F);
                var clear = B.ExpandClear(runs, seeds);
                seeds = null;
                if (pl.HasValue)
                {
                    int pk = B.Key(pl.Value);
                    if (clear.ContainsKey(pk))
                    {
                        if (Shield > 0) { clear.Remove(pk); Float(pl.Value.C + .5f, pl.Value.R, "Blocked!", "#ffd23f"); Sfx("shield"); }
                        else KillPlayer(clear[pk], byPlayer);
                    }
                }
                var unlocks = B.CollectUnlocks(clear);
                if (combo >= 2 && !Dead) { Float(B.Cols / 2f, B.Rows / 2f - 1, "Combo x" + combo, "#ffffff", true); Score += T.ScoreCombo * combo; }
                PopCells(clear, combo);
                yield return T.PopSeconds;
                bool announced = false;
                foreach (var k in unlocks) { var p = B.FromKey(k); if (B.IsLocked(p.R, p.C)) { UnlockCell(p.R, p.C, !announced); announced = true; } }
                foreach (var cr in created)
                {
                    var p = B.FromKey(cr.Key);
                    if (B.Grid[p.R, p.C] != null) continue;
                    var g = B.MakeGem(cr.Color, cr.Special); g.RX = p.C; g.RY = p.R; g.Born = 1f; B.Grid[p.R, p.C] = g;
                    OnFx?.Invoke(new FxEvent { Kind = "ring", X1 = p.C + .5f, Y1 = p.R + .5f, R0 = .2f, R1 = 1.1f, Color = -1, Dur = .4f });
                    Float(p.C + .5f, p.R - .2f, SpecialInfo.Name(cr.Special) + "!", "#ffffff"); Sfx("made");
                }
                if (Dead) yield break;
                B.ApplyGravity(SpawnSpecial, ref Supply, L.Infinite);
                yield return WaitSettle;
                pivotKeys = new HashSet<int>();
            }
        }

        bool swapFizzled;
        IEnumerator DoSwap(Pos a, Pos b, bool byPlayer)
        {
            swapFizzled = false;
            B.Swap(a, b); yield return WaitSettle;
            var seeds = B.SwapSeeds(a, b, F.Combos);
            if (seeds != null) { yield return ResolveBoard(new HashSet<int>(), seeds, byPlayer); yield break; }
            var runs = B.FindMatches();
            if (runs.Count == 0 && !byPlayer) { B.Swap(a, b); yield return WaitSettle; swapFizzled = true; yield break; }
            yield return ResolveBoard(new HashSet<int> { B.Key(a), B.Key(b) }, null, byPlayer);
        }

        void AfterResolve()
        {
            if (!Running) return;
            if (Dead) { EndGame(false); return; }
            if (B.AllMoves(F).Count == 0)
            {
                if (L.Infinite) { Float(B.Cols / 2f, B.Rows / 2f, "No moves. Reshuffle!", "#ffffff", true); ShuffleBoard(); return; }
                WinReason = "The AI player has no legal swipe left."; EndGame(true); return;
            }
            if (AiLeft <= 0) { WinReason = "The AI player used up all " + L.AiMoves + " of its moves."; EndGame(true); }
        }

        // ---------- AI ----------

        void UpdateAI(float dt)
        {
            if (Freeze > 0) return;
            if (AiStateNow == AiState.Idle)
            {
                AiThink -= dt;
                if (AiThink <= 0 && !Busy && AiLeft > 0)
                {
                    float d = Math.Min(1f, Time / T.RampSeconds), m = PaceMultiplier();
                    AiInterval = Lerp(T.ThinkStart, T.ThinkEnd, d) / m;
                    AiTeleTime = Lerp(T.TeleStart, T.TeleEnd, d) / m;
                    Aggression = Lerp(T.AggroStart, T.AggroEnd, d);
                    var mv = ChooseMove();
                    if (mv == null) { AfterResolve(); return; }
                    AiPlan = mv; AiStateNow = AiState.Tele; AiTele = AiTeleTime; AiWasLethal = mv.Lethal; AiPreview = mv;
                    FingerActive = true; FingerX = FingerTX = mv.C1 + .5f; FingerY = FingerTY = mv.R1 + .5f;
                    Sfx("land");
                }
            }
            else if (AiStateNow == AiState.Tele)
            {
                AiTele -= dt;
                AiPreview = Busy ? null : B.EvalSwap(AiPlan.R1, AiPlan.C1, AiPlan.R2, AiPlan.C2, F);
                if (AiTele <= 0 && !Busy) { AiStateNow = AiState.Exec; AiPreview = null; StartProc(AiExecute()); }
            }
        }

        SwapEval ChooseMove()
        {
            var moves = B.AllMoves(F); if (moves.Count == 0) return null;
            var lethal = moves.FindAll(m => m.Lethal); var safe = moves.FindAll(m => !m.Lethal);
            var pool = (lethal.Count > 0 && (rnd.NextDouble() < Aggression || safe.Count == 0)) ? lethal : safe;
            pool.Sort((a, b) => b.Score.CompareTo(a.Score));
            int n = Math.Min(3, pool.Count);
            float[] w = { .6f, .28f, .12f }; float total = 0; for (int i = 0; i < n; i++) total += w[i];
            double x = rnd.NextDouble() * total;
            for (int i = 0; i < n; i++) { x -= w[i]; if (x <= 0) return pool[i]; }
            return pool[0];
        }

        IEnumerator AiExecute()
        {
            var p = AiPlan; AiLeft--; AiMoves++;
            var info = Glitch > 0 ? null : B.EvalSwap(p.R1, p.C1, p.R2, p.C2, F);
            var P1 = new Pos(p.R1, p.C1); var P2 = new Pos(p.R2, p.C2);
            FingerTX = p.C2 + .5f; FingerTY = p.R2 + .5f;
            if (info == null)
            {
                Sfx(Glitch > 0 ? "glitch" : "swap");
                if (B.IsOpen(p.R1, p.C1) && B.IsOpen(p.R2, p.C2)) { B.Swap(P1, P2); yield return WaitSettle; yield return .07f; B.Swap(P1, P2); yield return WaitSettle; }
                else yield return .16f;
                if (Glitch > 0) Float(p.C2 + .5f, p.R2, "GLITCHED", "#41b6ff"); else Float(p.C2 + .5f, p.R2, "Missed!", "#ffffff");
                if (AiWasLethal) { Dodges++; Score += T.ScoreDodge; }
                Sfx("miss");
            }
            else
            {
                Sfx("swap");
                if (AiWasLethal && !info.Lethal) { Dodges++; Score += T.ScoreDodge; var pl = B.FindPlayer(); if (pl.HasValue) Float(pl.Value.C + .5f, pl.Value.R, "Dodged!", "#4ee089"); }
                yield return DoSwap(P1, P2, false);
            }
            if (!Dead) Score += T.ScoreAiSwipe;
            FingerActive = false;
            AiStateNow = AiState.Idle; AiThink = AiInterval;
            AfterResolve();
        }

        // ---------- player ----------

        public void QueueMove(int dr, int dc) { if (!Running || Dead) return; queued = new Queued { DR = dr, DC = dc, T = .4f }; }

        void PlayerMove(int dr, int dc)
        {
            var plq = B.FindPlayer(); if (!plq.HasValue) return;
            var pl = plq.Value; var g = B.Grid[pl.R, pl.C];
            int r2 = pl.R + dr, c2 = pl.C + dc;
            LastDX = dc; LastDY = dr;
            if (!B.InB(r2, c2) || B.Cells[r2, c2] == CellType.Hole) { g.Squash = 1; Sfx("bump"); return; }
            if (B.Cells[r2, c2] != CellType.Open) { g.Squash = 1; Sfx("bump"); Float(c2 + .5f, r2, "Locked", "#b9adde"); return; }
            playerCd = .1f; Sfx("swap");
            StartProc(PlayerMoveProc(pl, new Pos(r2, c2)));
        }

        IEnumerator PlayerMoveProc(Pos from, Pos to)
        {
            yield return DoSwap(from, to, true);
            AfterResolve();
        }

        void GrantPowerup()
        {
            var type = (PowerupType)rnd.Next(4);
            Slots.Add(type); Sfx("power");
            Float(B.Cols / 2f, B.Rows - 1.2f, "+ " + SpecialInfo.PowerupName(type), "#ffd23f", true);
            OnSlotsChanged?.Invoke();
        }

        public bool UsePowerup(int i)
        {
            if (!Running || Dead || Paused) return false;
            if (i < 0 || i >= Slots.Count) return false;
            var type = Slots[i];
            if (type == PowerupType.Shuffle && Busy) return false;
            Slots.RemoveAt(i); OnSlotsChanged?.Invoke(); Sfx("use");
            var pl = B.FindPlayer();
            if (type == PowerupType.Shield) Shield = 6;
            else if (type == PowerupType.Glitch) Glitch = 5;
            else if (type == PowerupType.Freeze) Freeze = 4;
            else ShuffleBoard();
            if (pl.HasValue) Float(pl.Value.C + .5f, pl.Value.R, SpecialInfo.PowerupName(type) + "!", "#ffd23f");
            return true;
        }

        void ShuffleBoard()
        {
            B.Shuffle(F); AiPreview = null;
            StartProc(ShuffleProc());
        }
        IEnumerator ShuffleProc() { yield return WaitSettle; AfterResolve(); }

        void SpawnCage()
        {
            var cand = new List<Pos>(); int caged = 0, open = 0;
            for (int r = 0; r < B.Rows; r++) for (int c = 0; c < B.Cols; c++)
            {
                var g = B.Grid[r, c]; if (g == null) continue;
                if (B.Cells[r, c] == CellType.Cage) caged++;
                if (B.Cells[r, c] == CellType.Open) { open++; if (!g.IsPlayer && g.Special == Special.None && g.RX == c && g.RY == r) cand.Add(new Pos(r, c)); }
            }
            if (cand.Count == 0 || caged >= open * .25f) return;
            var pl = B.FindPlayer();
            var far = cand.FindAll(x => !pl.HasValue || Math.Abs(x.R - pl.Value.R) + Math.Abs(x.C - pl.Value.C) > 1);
            var list = far.Count > 0 ? far : cand;
            var cell = list[rnd.Next(list.Count)];
            B.Cells[cell.R, cell.C] = CellType.Cage; B.Grid[cell.R, cell.C].Born = .5f;
            Float(cell.C + .5f, cell.R, "Caged!", "#b9adde"); Sfx("cage");
        }

        void TimedUnlock()
        {
            var locked = new List<Pos>();
            for (int r = 0; r < B.Rows; r++) for (int c = 0; c < B.Cols; c++) if (B.IsLocked(r, c)) locked.Add(new Pos(r, c));
            if (locked.Count == 0) return;
            var cell = locked[rnd.Next(locked.Count)]; UnlockCell(cell.R, cell.C, true);
        }

        bool Shapeshift()
        {
            var plq = B.FindPlayer(); if (!plq.HasValue) return true;
            var pl = plq.Value; var g = B.Grid[pl.R, pl.C];
            int cur = g.Color; var order = new List<int>();
            for (int i = 0; i < B.NColors; i++) if (i != cur) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
            foreach (var col in order)
            {
                g.Color = col;
                bool bad = false;
                foreach (var run in B.FindMatches()) foreach (var x in run.Cells) if (x.R == pl.R && x.C == pl.C) bad = true;
                if (!bad)
                {
                    g.Born = .7f; Float(pl.C + .5f, pl.R, "Shapeshift!", "#c77dff"); Sfx("shift");
                    OnFx?.Invoke(new FxEvent { Kind = "ring", X1 = pl.C + .5f, Y1 = pl.R + .5f, R0 = .2f, R1 = 1.3f, Color = 4, Dur = .5f });
                    return true;
                }
            }
            g.Color = cur; return false;
        }

        // ---------- tick ----------

        public void Tick(float dt)
        {
            if (Paused) return;
            AnimateGems(dt); AnimateFx(dt);
            if (!Running) { AdvanceProc(dt); return; }
            Time += dt;
            Shield = Math.Max(0, Shield - dt); Glitch = Math.Max(0, Glitch - dt); Freeze = Math.Max(0, Freeze - dt);
            playerCd = Math.Max(0, playerCd - dt);
            if (!Dead)
            {
                scoreAcc += dt * T.ScorePerSecond; if (scoreAcc >= 1) { int n = (int)Math.Floor(scoreAcc); Score += n; scoreAcc -= n; }
                if (Slots.Count < T.MaxSlots) { Meter += dt / T.MeterTime; if (Meter >= 1) { Meter = 0; GrantPowerup(); } }
                else Meter = Math.Min(1, Meter + dt / T.MeterTime);
                if (L.UnlockEvery > 0) { unlockT -= dt; if (unlockT <= 0) { unlockT = L.UnlockEvery; TimedUnlock(); } }
                if (L.CageEvery > 0 && !Busy) { cageT -= dt; if (cageT <= 0) { cageT = Math.Max(8, L.CageEvery - Time / 30); SpawnCage(); } }
                if (L.ShiftEvery > 0)
                {
                    ShiftT -= dt;
                    if (ShiftT <= 3 && !shiftWarned) { shiftWarned = true; Sfx("tick"); }
                    if (ShiftT <= 0) { if (!Busy && Shapeshift()) { ShiftT = L.ShiftEvery; shiftWarned = false; } else ShiftT = 1.5f; }
                }
                if (L.RushEvery > 0)
                {
                    if (Rush > 0) Rush = Math.Max(0, Rush - dt);
                    else { rushT -= dt; if (rushT <= 0) { rushT = L.RushEvery; Rush = L.RushLen; Float(B.Cols / 2f, B.Rows / 2f - 1, "RUSH!", "#ff3b5c", true); Sfx("rush"); } }
                }
            }
            if (queued.HasValue)
            {
                var q = queued.Value; q.T -= dt;
                if (q.T <= 0) queued = null;
                else if (!Busy && playerCd <= 0 && !Dead) { queued = null; PlayerMove(q.DR, q.DC); }
                else queued = q;
            }
            if (Supply == 0 && !SupplyWarned) { SupplyWarned = true; Float(B.Cols / 2f, 1.2f, "Supply empty!", "#ff3b5c", true); }
            UpdateAI(dt);
            AdvanceProc(dt);
        }

        void AnimateGems(float dt)
        {
            for (int r = 0; r < B.Rows; r++) for (int c = 0; c < B.Cols; c++)
            {
                var g = B.Grid[r, c]; if (g == null) continue;
                float dx = c - g.RX;
                if (dx != 0) { float st = T.SwapSpeed * dt; g.RX = Math.Abs(dx) <= st ? c : g.RX + Math.Sign(dx) * st; }
                float dy = r - g.RY;
                if (dy > 0)
                {
                    g.VY = Math.Max(g.VY + T.Gravity * dt, T.SwapSpeed); float st = g.VY * dt;
                    if (dy <= st) { g.RY = r; if (g.VY > 12) g.Squash = 1; g.VY = 0; } else g.RY += st;
                }
                else if (dy < 0) { float st = T.SwapSpeed * dt; g.RY = -dy <= st ? r : g.RY - st; }
                if (g.Born > 0) g.Born = Math.Max(0, g.Born - dt * 4);
                if (g.Squash > 0) g.Squash = Math.Max(0, g.Squash - dt * 5);
            }
        }

        void AnimateFx(float dt)
        {
            for (int i = Popping.Count - 1; i >= 0; i--)
            {
                var g = Popping[i]; g.Pop += dt / (g.IsPlayer ? .9f : T.PopSeconds);
                if (g.Pop >= 1) Popping.RemoveAt(i);
            }
            FingerVis += ((FingerActive ? 1 : 0) - FingerVis) * Math.Min(1, dt * 9);
            FingerX += (FingerTX - FingerX) * Math.Min(1, dt * 14); FingerY += (FingerTY - FingerY) * Math.Min(1, dt * 14);
            if (Shake > 0) Shake = Math.Max(0, Shake - dt);
            blinkT -= dt; if (blinkT <= 0) { Blink = .12f; blinkT = 1.8f + (float)rnd.NextDouble() * 3; }
            if (Blink > 0) Blink -= dt;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
