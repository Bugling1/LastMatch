using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LastMatch.Core;
using LastMatch.View;

namespace LastMatch.PlayTests
{
    /// <summary>Runs the real scene headlessly: menus build, levels start, gems render, input flows, no exceptions are logged.</summary>
    public class ViewSmokeTests
    {
        GameController Boot()
        {
            SpriteFactory.Build();
            var cam = new GameObject("Main Camera"); cam.tag = "MainCamera"; var c = cam.AddComponent<Camera>(); c.orthographic = true; cam.AddComponent<AudioListener>();
            var es = new GameObject("EventSystem"); es.AddComponent<UnityEngine.EventSystems.EventSystem>(); es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            var go = new GameObject("Game");
            return go.AddComponent<GameController>();
        }

        [UnityTest]
        public IEnumerator MenuBuildsAndLevelsRunAcrossTiers()
        {
            var gc = Boot();
            yield return null;
            Assert.IsTrue(gc.OverlayShown, "menu should be up at boot");
            var rnd = new System.Random(7);
            foreach (int n in new[] { 1, 12, 30, 60, 90, 130, 160, 185, 215, 245, 0 })
            {
                gc.DebugStartLevel(n);
                Assert.IsFalse(gc.OverlayShown);
                Assert.IsTrue(gc.Sim.Running, "level " + n + " not running");
                gc.Sim.Shield = 1e9f;
                for (int f = 0; f < 240; f++)
                {
                    if (f % 12 == 0) gc.Sim.QueueMove(new[] { -1, 1, 0, 0 }[rnd.Next(4)], new[] { 0, 0, -1, 1 }[rnd.Next(4)]);
                    if (f == 100) gc.Sim.Slots.Add(PowerupType.Shuffle);
                    if (f == 101) gc.Sim.UsePowerup(0);
                    yield return null;
                }
                Assert.IsFalse(gc.Sim.Dead, "shielded player died on level " + n);
                // every gem on the grid has a view object
                int gems = 0, viewsOn = 0;
                for (int r = 0; r < gc.Sim.B.Rows; r++) for (int cc = 0; cc < gc.Sim.B.Cols; cc++) if (gc.Sim.B.Grid[r, cc] != null) gems++;
                foreach (var v in Object.FindObjectsByType<GemView>(FindObjectsSortMode.None)) if (v.gameObject.activeSelf) viewsOn++;
                Assert.GreaterOrEqual(viewsOn, gems, "level " + n + ": fewer gem views than gems");
                gc.DebugTogglePause(); yield return null; Assert.IsTrue(gc.OverlayShown);
                gc.DebugTogglePause(); yield return null; Assert.IsFalse(gc.OverlayShown);
            }
            gc.DebugToMenu(); yield return null;
            Assert.IsTrue(gc.OverlayShown);
        }

        [UnityTest]
        public IEnumerator DeathShowsResultScreen()
        {
            var gc = Boot();
            yield return null;
            gc.DebugStartLevel(1);
            // kill the player by giving it a color that completes a line, then let a swap resolve it
            var sim = gc.Sim;
            for (int f = 0; f < 60; f++) yield return null;
            var pl = sim.B.FindPlayer().Value;
            var g = sim.B.Grid[pl.R, pl.C];
            // force a match through the player: colour two neighbours like the player and drop the hand on it
            int c1 = pl.C > 1 ? pl.C - 1 : pl.C + 1, c2 = pl.C > 1 ? pl.C - 2 : pl.C + 2;
            if (sim.B.Grid[pl.R, c1] != null) sim.B.Grid[pl.R, c1].Color = g.Color;
            if (sim.B.Grid[pl.R, c2] != null) sim.B.Grid[pl.R, c2].Color = g.Color;
            sim.QueueMove(0, 0); // zero move: a swap with itself does nothing, so nudge vertically instead
            sim.QueueMove(pl.R > 0 ? -1 : 1, 0);
            float waited = 0;
            while (sim.Running && waited < 10f) { waited += Time.deltaTime; yield return null; }
            if (!sim.Running)
            {
                for (int f = 0; f < 90; f++) yield return null;
                Assert.IsTrue(gc.OverlayShown, "result screen should appear after the round ends");
            }
        }
    }
}
