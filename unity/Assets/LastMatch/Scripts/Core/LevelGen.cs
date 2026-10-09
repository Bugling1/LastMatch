using System;
using System.Collections.Generic;

namespace LastMatch.Core
{
    public class Tier
    {
        public int From;
        public string Name;
        public string Desc;
        public Action<Features> Adds;
    }

    public class Level
    {
        public int Id;                 // 0 for Endless
        public string Name;
        public int TierIndex;
        public string TierName;
        public string Sub;
        public int Cols, Rows, Colors;
        public int AiMoves, Supply;
        public float Speed;
        public Pos Start;
        public string[] Mask;          // null means all open
        public Dictionary<Special, float> Spawn = new Dictionary<Special, float>();
        public Features Feats = new Features();
        public bool Infinite;
        public float UnlockEvery, CageEvery, ShiftEvery, RushEvery, RushLen;
    }

    /// <summary>The 250-level campaign. Levels are generated deterministically from their number.</summary>
    public static class LevelGen
    {
        public const int TotalLevels = 250;

        public static readonly Tier[] Tiers =
        {
            new Tier { From = 1,   Name = "Basics",        Desc = "Just you, the hand and plain matches. Three in a row clears, nothing more.", Adds = f => { } },
            new Tier { From = 10,  Name = "Blasters",      Desc = "Four in a row makes a row or column blaster. Match it and it sweeps the whole line.", Adds = f => { f.Blasters = true; } },
            new Tier { From = 25,  Name = "Bombs & Holes", Desc = "An L or T shape makes a bomb that clears 3x3. Boards start having holes.", Adds = f => { f.Bombs = true; f.Holes = true; } },
            new Tier { From = 50,  Name = "Color Bombs",   Desc = "Five in a row makes a color bomb. Swap it with any gem to wipe that color. Caged gems appear: matchable, not movable.", Adds = f => { f.Rainbow = true; f.Cages = true; } },
            new Tier { From = 80,  Name = "Fog & Combos",  Desc = "Fogged gems hide their color until something clears next to them. Six in a row makes a cross. Swapping two specials sets off a combo.", Adds = f => { f.Fog = true; f.Cross = true; f.Combos = true; } },
            new Tier { From = 110, Name = "Diagonals",     Desc = "Diagonal blasters spawn from the supply. Locks also open on a timer.", Adds = f => { f.Diag = true; f.TimedUnlock = true; } },
            new Tier { From = 140, Name = "Seekers",       Desc = "Seekers spawn from the supply. When one goes off it blasts wherever you are standing.", Adds = f => { f.Seeker = true; } },
            new Tier { From = 170, Name = "Closing Cages", Desc = "Cages close on random gems during play and the boards get bigger.", Adds = f => { f.Shifting = true; f.Big = true; } },
            new Tier { From = 200, Name = "Shapeshift",    Desc = "Your color changes every 15 seconds. Watch the countdown over your head.", Adds = f => { f.Shapeshift = true; } },
            new Tier { From = 230, Name = "Rush Hour",     Desc = "Every 20 seconds the hand goes into overdrive for five. Everything at once.", Adds = f => { f.Rush = true; } },
        };

        public static int TierOf(int n)
        {
            int t = 0; for (int i = 0; i < Tiers.Length; i++) if (Tiers[i].From <= n) t = i; return t;
        }

        public static void TierSpan(int t, out int first, out int last)
        {
            first = Tiers[t].From;
            last = (t + 1 < Tiers.Length ? Tiers[t + 1].From : TotalLevels + 1) - 1;
        }

        public static Features FeatsForTier(int t)
        {
            var f = new Features(); for (int i = 0; i <= t; i++) Tiers[i].Adds(f); return f;
        }

        /// <summary>mulberry32, so level n looks the same on every device and matches the web prototype.</summary>
        public class Rng32
        {
            uint a;
            public Rng32(int seed) { a = unchecked((uint)seed); }
            public double Next()
            {
                unchecked
                {
                    a += 0x6D2B79F5u;
                    uint t = a;
                    t = (t ^ (t >> 15)) * (1u | a);
                    t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            }
        }

        delegate bool Mark(int r, int c);
        delegate Mark Pattern(int R, int C);

        static readonly Pattern[] HolePatterns =
        {
            (R, C) => (r, c) => (r == 0 || r == R - 1) && (c == 0 || c == C - 1),
            (R, C) => (r, c) => (r <= 1 && (c <= 1 || c >= C - 2)) && !(r == 1 && (c == 1 || c == C - 2)),
            (R, C) => (r, c) => Math.Abs(r - (R - 1) / 2.0) < 1 && Math.Abs(c - (C - 1) / 2.0) < 1,
            (R, C) => (r, c) => (r == R / 2 || r == R / 2 - 1) && (c == 1 || c == C - 2),
            (R, C) => (r, c) => (r == R / 2) && (c == 0 || c == C - 1),
            (R, C) => (r, c) => (r == R - 1) && (c == 0 || c == C - 1 || Math.Abs(c - (C - 1) / 2.0) < 1),
            (R, C) => (r, c) => (r == 1 || r == R - 2) && (c == 1 || c == C - 2),
        };
        static readonly Pattern[] CagePatterns =
        {
            (R, C) => (r, c) => (r == 0 || r == R - 1) && (c <= 1 || c >= C - 2),
            (R, C) => (r, c) => (c == 0 || c == C - 1) && r >= 1 && r <= R - 2 && r % 2 == 1,
            (R, C) => (r, c) => Math.Abs(r - (R - 1) / 2.0) <= 1.5 && Math.Abs(c - (C - 1) / 2.0) <= 1.5 && !(Math.Abs(r - (R - 1) / 2.0) < 1 && Math.Abs(c - (C - 1) / 2.0) < 1),
            (R, C) => (r, c) => (r == 2 || r == R - 3) && (c == 2 || c == C - 3),
            (R, C) => (r, c) => r == 0 && c % 2 == 0,
            (R, C) => (r, c) => (r == 0 || r == R - 1) && Math.Abs(c - (C - 1) / 2.0) < 1.5,
        };
        static readonly Pattern[] FogPatterns =
        {
            (R, C) => (r, c) => r + c <= 1 || r + (C - 1 - c) <= 1,
            (R, C) => (r, c) => r + c <= 2 || r + (C - 1 - c) <= 2,
            (R, C) => (r, c) => (c == 0 || c == C - 1) && r >= 2 && r <= R - 3,
            (R, C) => (r, c) => r == R - 1 && (c <= 1 || c >= C - 2),
            (R, C) => (r, c) => r == 0 && (c <= 2 || c >= C - 3),
            (R, C) => (r, c) => (r == 1 || r == R - 2) && Math.Abs(c - (C - 1) / 2.0) < 1.5,
        };

        public static Level Make(int n)
        {
            int t = TierOf(n);
            TierSpan(t, out int first, out int last);
            int k = n - first, len = last - first + 1;
            double p = len > 1 ? (double)k / (len - 1) : 1.0;
            double g = (n - 1) / (double)(TotalLevels - 1);
            var rng = new Rng32(n * 7919 + 13);
            var F = FeatsForTier(t);
            int cols = 8, rows = 8;
            if (t == 0) { cols = rows = 7; }
            else if (t == 1) { cols = rows = k < 6 ? 7 : 8; }
            else if (t <= 2) { cols = rng.Next() < .3 ? 9 : 8; rows = 8; }
            else if (t <= 6) { double pb = rng.Next(); cols = pb < .4 ? 8 : 9; rows = pb > .75 ? 9 : 8; }
            else { double pb = rng.Next(); cols = pb < .5 ? 9 : 10; rows = 9; }
            int colors = n <= 8 ? 4 : 5;
            float speed = (float)Math.Round(0.95 + g * 1.35 + p * 0.15, 2);
            int aiMoves = (int)Math.Round(36 + g * 74 + p * 8);
            int supply = (int)Math.Round(aiMoves * (3.3 + 3 * g));
            var spawn = new Dictionary<Special, float>();
            if (F.Cross) spawn[Special.Cross] = (float)(.012 + .012 * g);
            if (F.Diag) spawn[Special.Diag] = (float)(.02 + .02 * (t == 5 ? p : 1));
            if (F.Seeker) spawn[Special.Seeker] = (float)(.02 + .025 * (t == 6 ? p : 1));

            var marks = new List<KeyValuePair<char, Mark>>();
            void AddPattern(Pattern[] lib, char kind, int count)
            {
                var idxs = new List<int>();
                while (idxs.Count < count) { int i = (int)Math.Floor(rng.Next() * lib.Length); if (!idxs.Contains(i)) idxs.Add(i); }
                foreach (var i in idxs) marks.Add(new KeyValuePair<char, Mark>(kind, lib[i](rows, cols)));
            }
            if (F.Holes) AddPattern(HolePatterns, '.', t == 2 ? (p < .4 ? 1 : 1 + (rng.Next() < .5 ? 1 : 0)) : 1 + (rng.Next() < .45 ? 1 : 0));
            if (F.Cages) AddPattern(CagePatterns, 'L', t == 3 ? (p < .5 ? 1 : 2) : 1 + (rng.Next() < .4 ? 1 : 0));
            if (F.Fog) AddPattern(FogPatterns, 'H', t == 4 ? (p < .5 ? 1 : 2) : 1);
            bool light = k < 3;
            var mask = new string[rows];
            for (int r = 0; r < rows; r++)
            {
                var row = new char[cols];
                for (int c = 0; c < cols; c++)
                {
                    char ch = '#';
                    foreach (var m in marks)
                    {
                        if (!m.Value(r, c)) continue;
                        if (light && ch == '#' && (r + c) % 2 == 1) continue;
                        ch = m.Key;
                    }
                    row[c] = ch;
                }
                mask[r] = new string(row);
            }
            Pos? start = null;
            int mid = (cols - 1) / 2;
            for (int r = rows - 2; r >= 0 && !start.HasValue; r--)
                for (int d = 0; d < cols && !start.HasValue; d++)
                    foreach (int c in new[] { mid + d, mid - d })
                        if (c >= 0 && c < cols && mask[r][c] == '#') { start = new Pos(r, c); break; }
            var L = new Level
            {
                Id = n, Name = "Level " + n, TierIndex = t, TierName = Tiers[t].Name, Cols = cols, Rows = rows, Colors = colors,
                AiMoves = aiMoves, Supply = supply, Speed = speed, Start = start ?? new Pos(rows - 1, 0), Mask = mask, Spawn = spawn, Feats = F,
            };
            if (F.TimedUnlock) L.UnlockEvery = (float)Math.Round(12 - 5 * g);
            if (F.Shifting) L.CageEvery = (float)Math.Round(26 - 8 * p);
            if (F.Shapeshift) L.ShiftEvery = 15;
            if (F.Rush) { L.RushEvery = 20; L.RushLen = 5; }
            return L;
        }

        public static Level Endless()
        {
            return new Level
            {
                Id = 0, Name = "Endless", TierName = "Endless", Sub = "Infinite gems and moves. Beat your best score.", Cols = 8, Rows = 8, Colors = 5, Infinite = true,
                Speed = 1.3f, Start = new Pos(5, 3), CageEvery = 22, RushEvery = 30, RushLen = 5,
                Spawn = new Dictionary<Special, float> { { Special.Cross, .015f }, { Special.Diag, .015f }, { Special.Seeker, .01f } },
                Feats = FeatsForTier(Tiers.Length - 1),
            };
        }
    }
}
