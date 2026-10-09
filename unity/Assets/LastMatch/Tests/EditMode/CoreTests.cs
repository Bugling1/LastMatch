using System;
using System.Collections.Generic;
using NUnit.Framework;
using LastMatch.Core;

namespace LastMatch.Tests
{
    public class CoreTests
    {
        [Test]
        public void EveryCampaignLevelIsPlayable()
        {
            for (int n = 1; n <= LevelGen.TotalLevels; n++)
            {
                var L = LevelGen.Make(n);
                var b = new Board();
                b.Fill(L);
                Assert.IsNotNull(b.Grid, "level " + n + " has no grid");
                var pl = b.FindPlayer();
                Assert.IsTrue(pl.HasValue, "level " + n + " has no player");
                Assert.AreEqual(CellType.Open, b.Cells[pl.Value.R, pl.Value.C], "level " + n + " player on a locked cell");
                Assert.GreaterOrEqual(b.AllMoves(L.Feats).Count, 3, "level " + n + " has too few legal moves");
                Assert.AreEqual(0, b.FindMatches().Count, "level " + n + " starts with a match");
                b.Count(out int open, out _, out _, out _);
                Assert.GreaterOrEqual(open, 30, "level " + n + " has too few open cells");
            }
        }

        [Test]
        public void TiersStartWhereTheCampaignSays()
        {
            Assert.AreEqual(0, LevelGen.TierOf(1));
            Assert.AreEqual(0, LevelGen.TierOf(9));
            Assert.AreEqual(1, LevelGen.TierOf(10));
            Assert.AreEqual(2, LevelGen.TierOf(25));
            Assert.AreEqual(9, LevelGen.TierOf(250));
            Assert.IsFalse(LevelGen.Make(9).Feats.Blasters);
            Assert.IsTrue(LevelGen.Make(10).Feats.Blasters);
            Assert.IsFalse(LevelGen.Make(24).Feats.Bombs);
            Assert.IsTrue(LevelGen.Make(25).Feats.Bombs);
            Assert.IsTrue(LevelGen.Make(250).Feats.Rush);
        }

        [Test]
        public void LevelsAreDeterministic()
        {
            var a = LevelGen.Make(137); var b = LevelGen.Make(137);
            Assert.AreEqual(string.Join("/", a.Mask), string.Join("/", b.Mask));
            Assert.AreEqual(a.Cols, b.Cols); Assert.AreEqual(a.AiMoves, b.AiMoves); Assert.AreEqual(a.Speed, b.Speed);
        }

        [Test]
        public void NoSpecialsBeforeBlastersTier()
        {
            var L = LevelGen.Make(3);
            var b = new Board(); b.BuildCells(L); b.Grid = new Gem[b.Rows, b.Cols];
            // a horizontal run of four
            for (int c = 0; c < 4; c++) b.Grid[0, c] = b.MakeGem(1);
            var runs = b.FindMatches();
            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(0, b.PlanSpecials(runs, new HashSet<int>(), -1, L.Feats).Count);
            Assert.AreEqual(1, b.PlanSpecials(runs, new HashSet<int>(), -1, LevelGen.Make(12).Feats).Count);
        }

        [Test]
        public void RowBlasterClearsWholeRow()
        {
            var L = LevelGen.Make(12);
            var b = new Board(); b.BuildCells(L); b.Grid = new Gem[b.Rows, b.Cols];
            for (int r = 0; r < b.Rows; r++) for (int c = 0; c < b.Cols; c++) b.Grid[r, c] = b.MakeGem((r * 3 + c) % 5);
            b.Grid[4, 2] = b.MakeGem(0, Special.H);
            var seeds = new List<Seed> { new Seed(4, 2, Cause.Match) };
            var clear = b.ExpandClear(null, seeds);
            Assert.AreEqual(b.Cols, clear.Count);
            for (int c = 0; c < b.Cols; c++) Assert.IsTrue(clear.ContainsKey(b.Key(4, c)));
        }

        [Test]
        public void GravityRespectsHolesAndLocks()
        {
            var L = new Level { Cols = 3, Rows = 5, Colors = 3, Mask = new[] { "###", "#.#", "#L#", "###", "###" }, Start = new Pos(4, 0), Feats = new Features() };
            var b = new Board(); b.BuildCells(L); b.Grid = new Gem[b.Rows, b.Cols];
            // middle column: gem above the hole, cage gem, nothing below
            b.Grid[0, 1] = b.MakeGem(0); b.Grid[0, 1].RX = 1; b.Grid[0, 1].RY = 0;
            b.Grid[2, 1] = b.MakeGem(1); b.Grid[2, 1].RX = 1; b.Grid[2, 1].RY = 2;
            int supply = 0;
            bool moved = b.ApplyGravity(null, ref supply, false);
            Assert.IsFalse(moved, "nothing can fall: the gem sits on its segment floor, the cage holds, and there is no supply");
            Assert.IsNotNull(b.Grid[0, 1], "gem above the hole must stay put");
            Assert.IsNull(b.Grid[1, 1], "hole stays empty");
            Assert.IsNotNull(b.Grid[2, 1], "caged gem stays put");
            Assert.IsNull(b.Grid[3, 1]); Assert.IsNull(b.Grid[4, 1]);
            // with supply, the bottom segment fills in place (its ceiling is the cage, not the top of the board)
            supply = 10; b.ApplyGravity(null, ref supply, false);
            Assert.IsNotNull(b.Grid[3, 1]); Assert.IsNotNull(b.Grid[4, 1]);
            Assert.AreEqual(1f, b.Grid[4, 1].Born, "in-place spawn pops in rather than falling");
            Assert.AreEqual(0, supply, "two empty side columns (5 cells each) plus the 2-cell segment use the whole supply of 10");
        }

        [Test]
        public void AiNeverSwapsLockedCells()
        {
            var L = new Level { Cols = 4, Rows = 3, Colors = 3, Mask = new[] { "LLLL", "####", "####" }, Start = new Pos(2, 0), Feats = new Features() };
            var b = new Board(); b.BuildCells(L); b.Grid = new Gem[b.Rows, b.Cols];
            for (int r = 0; r < 3; r++) for (int c = 0; c < 4; c++) b.Grid[r, c] = b.MakeGem(c % 2 == 0 ? 0 : 1);
            b.Grid[0, 1] = b.MakeGem(0); // would match with (1,0)? no matter, row 0 is caged
            foreach (var m in b.AllMoves(L.Feats)) { Assert.AreNotEqual(0, m.R1); Assert.AreNotEqual(0, m.R2); }
        }

        [Test]
        public void SimulationSurvivesRandomPlayAcrossTiers()
        {
            var rnd = new Random(42);
            foreach (int n in new[] { 1, 12, 30, 60, 90, 120, 150, 180, 210, 240 })
            {
                var sim = new GameSim();
                int floats = 0; sim.OnFloat += (x, y, t, h, b) => floats++;
                sim.StartLevel(LevelGen.Make(n));
                sim.Shield = 1e9f;
                int frames = 0;
                while (frames < 2400 && sim.Running)
                {
                    sim.Tick(1f / 60f); frames++;
                    if (frames % 12 == 0) sim.QueueMove(new[] { -1, 1, 0, 0 }[rnd.Next(4)], new[] { 0, 0, -1, 1 }[rnd.Next(4)]);
                    if (frames % 300 == 0) sim.Shield = 1e9f;
                }
                Assert.IsFalse(sim.Dead, "shielded player died on level " + n);
                Assert.Greater(sim.AiMoves, 0, "AI never moved on level " + n);
                for (int r = 0; r < sim.B.Rows; r++) for (int c = 0; c < sim.B.Cols; c++)
                    if (sim.B.Cells[r, c] == CellType.Hole) Assert.IsNull(sim.B.Grid[r, c], "gem in a hole on level " + n);
            }
        }

        [Test]
        public void EndlessReshufflesInsteadOfEnding()
        {
            var sim = new GameSim();
            sim.StartLevel(LevelGen.Endless());
            sim.Shield = 1e9f;
            for (int i = 0; i < 1800; i++) sim.Tick(1f / 60f);
            Assert.IsTrue(sim.Running);
            Assert.AreEqual(int.MaxValue, sim.Supply);
        }
    }
}
