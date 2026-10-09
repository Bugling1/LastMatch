using System;
using System.Collections.Generic;

namespace LastMatch.Core
{
    public enum Special { None, H, V, Cross, Diag, Bomb, Mega, Seeker, Rainbow }
    public enum CellType { Open, Hole, Cage, Fog }
    public enum Cause { Match, H, V, Cross, Diag, Bomb, Mega, Seeker, Rainbow, Combo }
    public enum PowerupType { Shield, Glitch, Freeze, Shuffle }

    public struct Pos
    {
        public int R, C;
        public Pos(int r, int c) { R = r; C = c; }
        public override string ToString() => "(" + R + "," + C + ")";
    }

    /// <summary>One gem. Render state (RX/RY etc.) lives here so the simulation can animate it without a view.</summary>
    public class Gem
    {
        public int Id;
        public int Color;          // -1 for a color bomb
        public Special Special;
        public bool IsPlayer;
        public float RX, RY;       // render position in cell units
        public float VY;           // fall speed
        public float Born;         // 1 -> 0 scale-in
        public float Squash;       // 1 -> 0 landing squash
        public float Pop = -1f;    // -1 idle, 0..1 popping
        public float Wob;          // phase offset for idle wobble
    }

    public class Run
    {
        public bool Horizontal;
        public int Color;
        public List<Pos> Cells = new List<Pos>();
    }

    public struct Seed
    {
        public int R, C;
        public Cause Cause;
        public Seed(int r, int c, Cause cause) { R = r; C = c; Cause = cause; }
    }

    public struct Created
    {
        public int Key;
        public Special Special;
        public int Color;
    }

    public class SwapEval
    {
        public int R1, C1, R2, C2;
        public Dictionary<int, Cause> Clear;
        public bool Lethal;
        public float Score;
    }

    /// <summary>Which mechanics a level has. Each campaign tier switches more of these on.</summary>
    public class Features
    {
        public bool Blasters, Bombs, Holes, Rainbow, Cages, Fog, Cross, Combos, Diag, TimedUnlock, Seeker, Shifting, Big, Shapeshift, Rush;

        public Features Clone() => (Features)MemberwiseClone();

        public static Features All()
        {
            return new Features { Blasters = true, Bombs = true, Holes = true, Rainbow = true, Cages = true, Fog = true, Cross = true, Combos = true, Diag = true, TimedUnlock = true, Seeker = true, Shifting = true, Big = true, Shapeshift = true, Rush = true };
        }
    }

    public static class SpecialInfo
    {
        public static string Name(Special s)
        {
            switch (s)
            {
                case Special.H: return "Row blaster";
                case Special.V: return "Column blaster";
                case Special.Cross: return "Cross blaster";
                case Special.Diag: return "Diagonal blaster";
                case Special.Bomb: return "Bomb";
                case Special.Mega: return "Mega bomb";
                case Special.Seeker: return "Seeker";
                case Special.Rainbow: return "Color bomb";
                default: return "";
            }
        }

        public static Cause ToCause(Special s)
        {
            switch (s)
            {
                case Special.H: return Cause.H;
                case Special.V: return Cause.V;
                case Special.Cross: return Cause.Cross;
                case Special.Diag: return Cause.Diag;
                case Special.Bomb: return Cause.Bomb;
                case Special.Mega: return Cause.Mega;
                case Special.Seeker: return Cause.Seeker;
                case Special.Rainbow: return Cause.Rainbow;
                default: return Cause.Match;
            }
        }

        public static string CauseText(Cause c)
        {
            switch (c)
            {
                case Cause.H: return "A row blaster swept you away.";
                case Cause.V: return "A column blaster swept you away.";
                case Cause.Cross: return "A cross blaster caught you at the junction.";
                case Cause.Diag: return "A diagonal blaster sliced through you.";
                case Cause.Bomb: return "You were caught in a bomb blast.";
                case Cause.Mega: return "A mega bomb flattened you.";
                case Cause.Seeker: return "A seeker hunted you down.";
                case Cause.Rainbow: return "A color bomb wiped out your color.";
                case Cause.Combo: return "A special combo tore through you.";
                default: return "You got matched into a line of three.";
            }
        }

        public static string PowerupName(PowerupType p)
        {
            switch (p)
            {
                case PowerupType.Shield: return "Shield";
                case PowerupType.Glitch: return "Glitch";
                case PowerupType.Freeze: return "Freeze";
                default: return "Shuffle";
            }
        }

        public static string PowerupDesc(PowerupType p)
        {
            switch (p)
            {
                case PowerupType.Shield: return "You cannot be matched for 6s";
                case PowerupType.Glitch: return "AI's swipes fail for 5s";
                case PowerupType.Freeze: return "AI stops thinking for 4s";
                default: return "Reshuffle every gem but you";
            }
        }
    }
}
