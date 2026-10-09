using System;
using System.Collections.Generic;
using UnityEngine;
using LastMatch.Core;

namespace LastMatch.View
{
    /// <summary>A tiny anti-aliased rasterizer so the whole game ships without art files.</summary>
    class Raster
    {
        public readonly int W, H;
        readonly Color[] px;
        public Raster(int w, int h) { W = w; H = h; px = new Color[w * h]; for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0); }

        void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || a <= 0) return;
            a = Mathf.Clamp01(a) * c.a;
            var d = px[y * W + x];
            float outA = a + d.a * (1 - a);
            if (outA <= 0) { px[y * W + x] = new Color(0, 0, 0, 0); return; }
            px[y * W + x] = new Color((c.r * a + d.r * d.a * (1 - a)) / outA, (c.g * a + d.g * d.a * (1 - a)) / outA, (c.b * a + d.b * d.a * (1 - a)) / outA, outA);
        }

        /// <summary>Fills every pixel whose signed distance (from sdf) is below zero, anti-aliased.</summary>
        public void Fill(Func<float, float, float> sdf, Color c, int x0 = 0, int y0 = 0, int x1 = -1, int y1 = -1)
        {
            if (x1 < 0) x1 = W; if (y1 < 0) y1 = H;
            for (int y = Math.Max(0, y0); y < Math.Min(H, y1); y++) for (int x = Math.Max(0, x0); x < Math.Min(W, x1); x++)
            {
                float d = sdf(x + .5f, y + .5f);
                Blend(x, y, c, .5f - d);
            }
        }

        public void Circle(float cx, float cy, float r, Color c) => Fill((x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r, c, (int)(cx - r - 2), (int)(cy - r - 2), (int)(cx + r + 3), (int)(cy + r + 3));
        public void Ellipse(float cx, float cy, float rx, float ry, Color c) => Fill((x, y) => { float dx = (x - cx) / rx, dy = (y - cy) / ry; float k = Mathf.Sqrt(dx * dx + dy * dy); return (k - 1) * Mathf.Min(rx, ry); }, c, (int)(cx - rx - 2), (int)(cy - ry - 2), (int)(cx + rx + 3), (int)(cy + ry + 3));
        public void Ring(float cx, float cy, float r, float thick, Color c) => Fill((x, y) => Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) - thick / 2, c, (int)(cx - r - thick), (int)(cy - r - thick), (int)(cx + r + thick + 1), (int)(cy + r + thick + 1));
        public void Arc(float cx, float cy, float r, float thick, float a0, float a1, Color c)
        {
            Fill((x, y) =>
            {
                float ang = Mathf.Atan2(y - cy, x - cx); if (ang < a0 || ang > a1) return 9f;
                return Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) - thick / 2;
            }, c, (int)(cx - r - thick), (int)(cy - r - thick), (int)(cx + r + thick + 1), (int)(cy + r + thick + 1));
        }
        public void Capsule(float x1, float y1, float x2, float y2, float w, Color c)
        {
            Fill((x, y) =>
            {
                float vx = x2 - x1, vy = y2 - y1, len2 = vx * vx + vy * vy;
                float t = len2 > 0 ? Mathf.Clamp01(((x - x1) * vx + (y - y1) * vy) / len2) : 0;
                float px2 = x1 + vx * t, py2 = y1 + vy * t;
                return Mathf.Sqrt((x - px2) * (x - px2) + (y - py2) * (y - py2)) - w / 2;
            }, c, (int)(Mathf.Min(x1, x2) - w), (int)(Mathf.Min(y1, y2) - w), (int)(Mathf.Max(x1, x2) + w + 1), (int)(Mathf.Max(y1, y2) + w + 1));
        }
        public void RoundRect(float x, float y, float w, float h, float rad, Color c)
        {
            float cx = x + w / 2, cy = y + h / 2, hw = w / 2 - rad, hh = h / 2 - rad;
            Fill((px2, py2) => { float dx = Mathf.Max(Mathf.Abs(px2 - cx) - hw, 0), dy = Mathf.Max(Mathf.Abs(py2 - cy) - hh, 0); return Mathf.Sqrt(dx * dx + dy * dy) - rad; }, c, (int)x - 1, (int)y - 1, (int)(x + w) + 2, (int)(y + h) + 2);
        }
        /// <summary>Signed distance to a simple polygon: distance to the nearest edge, negative inside (even-odd).</summary>
        public static Func<float, float, float> PolySdf(Vector2[] pts)
        {
            return (x, y) =>
            {
                float d = float.MaxValue; bool inside = false;
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                {
                    var a = pts[j]; var b = pts[i];
                    float ex = b.x - a.x, ey = b.y - a.y;
                    float t = Mathf.Clamp01(((x - a.x) * ex + (y - a.y) * ey) / (ex * ex + ey * ey + 1e-6f));
                    float qx = a.x + ex * t - x, qy = a.y + ey * t - y;
                    d = Mathf.Min(d, Mathf.Sqrt(qx * qx + qy * qy));
                    if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y + 1e-9f) + a.x) inside = !inside;
                }
                return inside ? -d : d;
            };
        }
        public static Func<float, float, float> RoundRectSdf(float x, float y, float w, float h, float rad)
        {
            float cx = x + w / 2, cy = y + h / 2, hw = w / 2 - rad, hh = h / 2 - rad;
            return (px2, py2) => { float dx = Mathf.Max(Mathf.Abs(px2 - cx) - hw, 0), dy = Mathf.Max(Mathf.Abs(py2 - cy) - hh, 0); return Mathf.Sqrt(dx * dx + dy * dy) - rad; };
        }
        public void Poly(Vector2[] pts, Color c)
        {
            float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;
            foreach (var p in pts) { minx = Mathf.Min(minx, p.x); miny = Mathf.Min(miny, p.y); maxx = Mathf.Max(maxx, p.x); maxy = Mathf.Max(maxy, p.y); }
            Fill(PolySdf(pts), c, (int)minx - 2, (int)miny - 2, (int)maxx + 3, (int)maxy + 3);
        }
        public void Shade(Func<float, float, Color> f)
        {
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { var c = f(x + .5f, y + .5f); if (c.a > 0) Blend(x, y, c, 1); }
        }

        public Sprite ToSprite(float pixelsPerUnit, Vector2? pivot = null, float border = 0)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px); tex.Apply();
            if (border > 0) return Sprite.Create(tex, new Rect(0, 0, W, H), pivot ?? new Vector2(.5f, .5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            return Sprite.Create(tex, new Rect(0, 0, W, H), pivot ?? new Vector2(.5f, .5f), pixelsPerUnit);
        }
    }

    public static class Palette
    {
        public struct GemLook { public Color C, D, L; public string Shape; }
        public static readonly GemLook[] Gems =
        {
            new GemLook { C = Hex("#ff5470"), D = Hex("#9c0f36"), L = Hex("#ffd6de"), Shape = "circle" },
            new GemLook { C = Hex("#ffb52e"), D = Hex("#a45500"), L = Hex("#fff0c2"), Shape = "diamond" },
            new GemLook { C = Hex("#4ee089"), D = Hex("#0f6f37"), L = Hex("#d6ffe6"), Shape = "hex" },
            new GemLook { C = Hex("#41b6ff"), D = Hex("#0a4f92"), L = Hex("#d3efff"), Shape = "square" },
            new GemLook { C = Hex("#c77dff"), D = Hex("#5e1a99"), L = Hex("#efdcff"), Shape = "star" },
        };
        public static readonly Color Ink = Hex("#1d1233"), Gold = Hex("#ffd23f"), Danger = Hex("#ff3b5c"), Ok = Hex("#4ee089"), Sky = Hex("#41b6ff"), Muted = Hex("#b9adde"), Cream = Hex("#fff6e5");
        public static readonly Color CellA = Hex("#2a1f63"), CellB = Hex("#312672"), CellLocked = Hex("#231a52"), Board = Hex("#1c1447"), Bg = Hex("#2b1e5e"), Panel = Hex("#3a2d7a"), PanelDark = Hex("#241a52"), Rim = Hex("#4b3f95");

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c); return c;
        }
        public static Color GemColor(int i) => i >= 0 && i < Gems.Length ? Gems[i].C : Color.white;
    }

    /// <summary>Every sprite the game draws, generated once at startup.</summary>
    public static class SpriteFactory
    {
        public const float PPU = 128f;      // one cell = 128 px = 1 world unit
        public static Sprite[] Shapes;       // by gem index
        public static Sprite[] GemBodies;    // shaded, outlined, glossy gem per color
        public static Sprite Panel9, Shadow9, Glow, Backdrop;
        public static Sprite Highlight, EyeWhite, Pupil, Smile, MouthO, XMark, Glasses, Brow, Dot, Ring, Beam, Cell, Hand, Arrow, Cage, FogTile, Rainbow, Square;
        public static Dictionary<Special, Sprite> Badges = new Dictionary<Special, Sprite>();
        public static Dictionary<PowerupType, Sprite> PowerupIcons = new Dictionary<PowerupType, Sprite>();
        static bool built;

        public static void Build()
        {
            if (built) return; built = true;
            Shapes = new Sprite[Palette.Gems.Length];
            for (int i = 0; i < Shapes.Length; i++) Shapes[i] = Shape(Palette.Gems[i].Shape);
            GemBodies = new Sprite[Palette.Gems.Length];
            for (int i = 0; i < GemBodies.Length; i++) GemBodies[i] = GemBody(i);
            Panel9 = MakePanel(); Shadow9 = MakeShadow(); Glow = MakeGlow(); Backdrop = MakeBackdrop();
            Highlight = Make(64, 32, r => r.Ellipse(32, 16, 28, 12, Color.white));
            EyeWhite = Make(64, 72, r => { r.Ellipse(32, 36, 30, 34, Palette.Ink); r.Ellipse(32, 36, 25, 29, Color.white); });
            Pupil = Make(32, 32, r => { r.Circle(16, 16, 14, Palette.Ink); r.Circle(11, 21, 4.5f, Color.white); });
            Smile = Make(96, 64, r => r.Arc(48, 54, 42, 7, -Mathf.PI + .3f, -.3f, Palette.Ink));
            MouthO = Make(48, 56, r => r.Ellipse(24, 28, 20, 26, Palette.Ink));
            XMark = Make(48, 48, r => { r.Capsule(8, 8, 40, 40, 7, Palette.Ink); r.Capsule(40, 8, 8, 40, 7, Palette.Ink); });
            Glasses = Make(128, 48, r => { r.RoundRect(2, 6, 54, 36, 10, Palette.Ink); r.RoundRect(72, 6, 54, 36, 10, Palette.Ink); r.Capsule(56, 20, 72, 20, 6, Palette.Ink); r.Ellipse(20, 30, 9, 5, new Color(1, 1, 1, .35f)); });
            Brow = Make(48, 16, r => r.Capsule(4, 12, 44, 4, 6, Palette.Ink));
            Dot = Make(32, 32, r => r.Circle(16, 16, 15, Color.white));
            Ring = Make(128, 128, r => r.Ring(64, 64, 56, 12, Color.white));
            Beam = Make(128, 32, r => r.Capsule(16, 16, 112, 16, 28, Color.white));
            Cell = MakeCell();
            Square = Make(32, 32, r => r.RoundRect(0, 0, 32, 32, 6, Color.white));
            Hand = MakeHand();
            Arrow = Make(128, 48, r => r.Poly(new[] { new Vector2(2, 16), new Vector2(84, 16), new Vector2(84, 2), new Vector2(126, 24), new Vector2(84, 46), new Vector2(84, 32), new Vector2(2, 32) }, Color.white), new Vector2(0, .5f));
            Cage = MakeCage();
            FogTile = Make(128, 128, r => { r.RoundRect(10, 10, 108, 108, 26, Palette.Ink); r.RoundRect(14, 14, 100, 100, 24, Palette.Hex("#4d4480")); r.Circle(44, 60, 22, new Color(1, 1, 1, .08f)); r.Circle(80, 70, 20, new Color(1, 1, 1, .08f)); });
            Rainbow = MakeRainbow();
            foreach (Special sp in Enum.GetValues(typeof(Special))) if (sp != Special.None && sp != Special.Rainbow) Badges[sp] = MakeBadge(sp);
            PowerupIcons[PowerupType.Shield] = Make(64, 64, r =>
            {
                r.Poly(new[] { new Vector2(32, 3), new Vector2(7, 14), new Vector2(7, 36), new Vector2(32, 61), new Vector2(57, 36), new Vector2(57, 14) }, Color.white);
                r.Poly(new[] { new Vector2(32, 11), new Vector2(14, 19), new Vector2(14, 34), new Vector2(32, 52), new Vector2(50, 34), new Vector2(50, 19) }, new Color(0, 0, 0, .35f));
                r.Capsule(22, 32, 29, 25, 5, Color.white); r.Capsule(29, 25, 43, 41, 5, Color.white);
            });
            PowerupIcons[PowerupType.Glitch] = Make(64, 64, r =>
            {
                r.Poly(new[] { new Vector2(38, 62), new Vector2(12, 30), new Vector2(29, 30), new Vector2(24, 2), new Vector2(52, 36), new Vector2(35, 36), new Vector2(40, 62) }, Color.white);
            });
            PowerupIcons[PowerupType.Freeze] = Make(64, 64, r =>
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI / 3; float dx = Mathf.Cos(a) * 27, dy = Mathf.Sin(a) * 27;
                    r.Capsule(32 - dx, 32 - dy, 32 + dx, 32 + dy, 5, Color.white);
                    foreach (int s in new[] { -1, 1 })
                    {
                        float bx = 32 + dx * .62f, by = 32 + dy * .62f, ba = a + s * Mathf.PI / 4;
                        r.Capsule(bx, by, bx + Mathf.Cos(ba) * 8, by + Mathf.Sin(ba) * 8, 4, Color.white);
                        bx = 32 - dx * .62f; by = 32 - dy * .62f; ba = a + Mathf.PI + s * Mathf.PI / 4;
                        r.Capsule(bx, by, bx + Mathf.Cos(ba) * 8, by + Mathf.Sin(ba) * 8, 4, Color.white);
                    }
                }
            });
            PowerupIcons[PowerupType.Shuffle] = Make(64, 64, r =>
            {
                void ArrowAt(float y0, float y1, bool rightwards)
                {
                    float x0 = rightwards ? 6 : 58, x1 = rightwards ? 58 : 6;
                    r.Capsule(x0, y0, (x0 + x1) / 2, (y0 + y1) / 2, 5, Color.white); r.Capsule((x0 + x1) / 2, (y0 + y1) / 2, x1 - (rightwards ? 6 : -6), y1, 5, Color.white);
                    float hx = x1, hy = y1; float dir = rightwards ? 1 : -1;
                    r.Poly(new[] { new Vector2(hx + dir * 2, hy), new Vector2(hx - dir * 11, hy - 8), new Vector2(hx - dir * 11, hy + 8) }, Color.white);
                }
                ArrowAt(46, 18, true); ArrowAt(18, 46, true);
            });
        }

        static Sprite Make(int w, int h, Action<Raster> draw, Vector2? pivot = null)
        {
            var r = new Raster(w, h); draw(r); return r.ToSprite(PPU, pivot);
        }

        static Sprite Shape(string shape)
        {
            // 128 px canvas, shape radius ~ 48 px (0.375 units), matching the web prototype's 0.38 cell gem size
            const float s = 48f, cx = 64f, cy = 64f;
            return Make(128, 128, r =>
            {
                switch (shape)
                {
                    case "circle": r.Circle(cx, cy, s, Color.white); break;
                    case "diamond": r.Poly(new[] { new Vector2(cx, cy - s * 1.08f), new Vector2(cx + s * .92f, cy), new Vector2(cx, cy + s * 1.08f), new Vector2(cx - s * .92f, cy) }, Color.white); break;
                    case "hex": { var pts = new Vector2[6]; for (int i = 0; i < 6; i++) { float a = Mathf.PI / 6 + i * Mathf.PI / 3; pts[i] = new Vector2(cx + Mathf.Cos(a) * s * 1.05f, cy + Mathf.Sin(a) * s * 1.05f); } r.Poly(pts, Color.white); break; }
                    case "square": r.RoundRect(cx - s * .92f, cy - s * .92f, s * 1.84f, s * 1.84f, s * .35f, Color.white); break;
                    default: { var pts = new Vector2[10]; for (int i = 0; i < 10; i++) { float a = -Mathf.PI / 2 + i * Mathf.PI / 5; float rr = i % 2 == 1 ? s * .62f : s * 1.12f; pts[i] = new Vector2(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr); } r.Poly(pts, Color.white); break; }
                }
            });
        }

        static Func<float, float, float> ShapeSdf(string shape, float cx, float cy, float s)
        {
            switch (shape)
            {
                case "circle": return (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - s;
                case "diamond": return Raster.PolySdf(new[] { new Vector2(cx, cy - s * 1.08f), new Vector2(cx + s * .92f, cy), new Vector2(cx, cy + s * 1.08f), new Vector2(cx - s * .92f, cy) });
                case "hex": { var pts = new Vector2[6]; for (int i = 0; i < 6; i++) { float a = Mathf.PI / 6 + i * Mathf.PI / 3; pts[i] = new Vector2(cx + Mathf.Cos(a) * s * 1.05f, cy + Mathf.Sin(a) * s * 1.05f); } return Raster.PolySdf(pts); }
                case "square": return Raster.RoundRectSdf(cx - s * .92f, cy - s * .92f, s * 1.84f, s * 1.84f, s * .35f);
                default: { var pts = new Vector2[10]; for (int i = 0; i < 10; i++) { float a = -Mathf.PI / 2 + i * Mathf.PI / 5; float rr = i % 2 == 1 ? s * .62f : s * 1.12f; pts[i] = new Vector2(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr); } return Raster.PolySdf(pts); }
            }
        }

        /// <summary>A gem with a dark outline, a light-from-top-left gradient, a darker rim and a glossy spot, all baked.</summary>
        static Sprite GemBody(int color)
        {
            var look = Palette.Gems[color];
            const float cx = 64, cy = 64, s = 48;
            var sdf = ShapeSdf(look.Shape, cx, cy, s);
            float lx = cx - s * .35f, ly = cy + s * .4f;
            return Make(128, 128, r =>
            {
                r.Fill((x, y) => sdf(x, y) - 5f, look.D);
                r.Shade((x, y) =>
                {
                    float d = sdf(x, y); if (d > .5f) return new Color(0, 0, 0, 0);
                    float a = Mathf.Clamp01(.5f - d);
                    float k = Mathf.Sqrt((x - lx) * (x - lx) + (y - ly) * (y - ly)) / (s * 1.25f);
                    Color c = k < .35f ? Color.Lerp(look.L, look.C, k / .35f) : Color.Lerp(look.C, look.D, Mathf.Clamp01((k - .35f) / .75f));
                    float rim = Mathf.Clamp01(-d / 7f);
                    c = Color.Lerp(Color.Lerp(look.D, c, .35f), c, rim);
                    c.a = a; return c;
                });
                float gx = cx - s * .36f, gy = cy + s * .42f, ca = Mathf.Cos(.6f), sa = Mathf.Sin(.6f);
                r.Shade((x, y) =>
                {
                    if (sdf(x, y) > -2) return new Color(0, 0, 0, 0);
                    float dx = x - gx, dy = y - gy; float u = (dx * ca + dy * sa) / (s * .3f), v = (-dx * sa + dy * ca) / (s * .15f);
                    float q = u * u + v * v; if (q > 1) return new Color(0, 0, 0, 0);
                    return new Color(1, 1, 1, .6f * (1 - q * q));
                });
            });
        }

        static Sprite MakeCell()
        {
            var sdf = Raster.RoundRectSdf(2, 2, 124, 124, 22);
            return Make(128, 128, r => r.Shade((x, y) =>
            {
                float d = sdf(x, y); if (d > .5f) return new Color(0, 0, 0, 0);
                float a = Mathf.Clamp01(.5f - d);
                float v = .82f + .18f * (y / 128f);
                float edge = Mathf.Clamp01(-d / 3f);
                v *= .78f + .22f * edge;
                if (y > 118 && d < -1) v = Mathf.Min(1.1f, v + .12f * Mathf.Clamp01((y - 118) / 6f));
                return new Color(v, v, v, a);
            }));
        }

        static Sprite MakePanel()
        {
            var sdf = Raster.RoundRectSdf(0, 0, 96, 96, 28);
            var r = new Raster(96, 96);
            r.Shade((x, y) => { float d = sdf(x, y); if (d > .5f) return new Color(0, 0, 0, 0); float v = .9f + .1f * (y / 96f); return new Color(v, v, v, Mathf.Clamp01(.5f - d)); });
            return r.ToSprite(PPU, null, 32);
        }

        static Sprite MakeShadow()
        {
            var sdf = Raster.RoundRectSdf(24, 24, 80, 80, 28);
            var r = new Raster(128, 128);
            r.Shade((x, y) => { float d = sdf(x, y); float a = d <= 0 ? 1 : Mathf.Pow(Mathf.Clamp01(1 - d / 24f), 2); return new Color(0, 0, 0, a); });
            return r.ToSprite(PPU, null, 48);
        }

        static Sprite MakeGlow()
        {
            return Make(128, 128, r => r.Shade((x, y) => { float d = Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)) / 64f; if (d > 1) return new Color(0, 0, 0, 0); float a = Mathf.Pow(1 - d, 2); return new Color(1, 1, 1, a); }));
        }

        static Sprite MakeBackdrop()
        {
            return Make(128, 128, r => r.Shade((x, y) =>
            {
                float dx = (x - 64) / 64f, dy = (y - 92) / 80f; float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                return Color.Lerp(Palette.Hex("#45308f"), Palette.Hex("#16103a"), Mathf.SmoothStep(0, 1, d));
            }));
        }

        static Sprite MakeRainbow()
        {
            return Make(128, 128, r =>
            {
                r.Shade((x, y) =>
                {
                    float dx = x - 64, dy = y - 64, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 48) return new Color(0, 0, 0, 0);
                    float ang = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2) + 1f) % 1f;
                    var c = Color.HSVToRGB(ang, .85f, 1f);
                    if (d < 10) c = Palette.Ink; else if (d < 22) c = Color.white;
                    return c;
                });
                r.Ring(64, 64, 48, 5, Palette.Hex("#2a1040"));
            });
        }

        static Sprite MakeBadge(Special sp)
        {
            return Make(128, 128, r =>
            {
                const float cx = 64, cy = 64, s = 48;
                r.Circle(cx, cy, s * .6f, new Color(.114f, .07f, .2f, .82f));
                r.Ring(cx, cy, s * .6f, s * .07f, new Color(1, 1, 1, .9f));
                float a = s * .2f, d = s * .38f; var w = Color.white;
                void Tri(float x, float y, float ang, float size)
                {
                    var pts = new Vector2[3];
                    Vector2 P(float lx, float ly) => new Vector2(x + lx * Mathf.Cos(ang) - ly * Mathf.Sin(ang), y + lx * Mathf.Sin(ang) + ly * Mathf.Cos(ang));
                    pts[0] = P(size, 0); pts[1] = P(-size * .7f, -size * .8f); pts[2] = P(-size * .7f, size * .8f);
                    r.Poly(pts, w);
                }
                if (sp == Special.H || sp == Special.Cross) { r.Capsule(cx - d * .6f, cy, cx + d * .6f, cy, s * .12f, w); Tri(cx - d, cy, Mathf.PI, a); Tri(cx + d, cy, 0, a); }
                if (sp == Special.V || sp == Special.Cross) { r.Capsule(cx, cy - d * .6f, cx, cy + d * .6f, s * .12f, w); Tri(cx, cy - d, -Mathf.PI / 2, a); Tri(cx, cy + d, Mathf.PI / 2, a); }
                if (sp == Special.Diag) for (int i = 0; i < 4; i++) { float ang = Mathf.PI / 4 + i * Mathf.PI / 2; r.Capsule(cx, cy, cx + Mathf.Cos(ang) * d * .55f, cy + Mathf.Sin(ang) * d * .55f, s * .12f, w); Tri(cx + Mathf.Cos(ang) * d, cy + Mathf.Sin(ang) * d, ang, a * .9f); }
                if (sp == Special.Bomb || sp == Special.Mega)
                {
                    r.Ring(cx, cy, s * .34f, s * .09f, w); r.Circle(cx, cy, s * .12f, w);
                    if (sp == Special.Mega) r.Ring(cx, cy, s * .48f, s * .06f, new Color(1, 1, 1, .8f));
                    r.Capsule(cx + s * .25f, cy + s * .25f, cx + s * .42f, cy + s * .6f, s * .09f, Palette.Hex("#ffb52e"));
                    r.Circle(cx + s * .42f, cy + s * .6f, s * .1f, Palette.Gold);
                }
                if (sp == Special.Seeker)
                {
                    r.Circle(cx, cy, s * .27f, Palette.Danger); r.Circle(cx - s * .08f, cy + s * .08f, s * .08f, w);
                    for (int i = 0; i < 4; i++) { float ang = i * Mathf.PI / 2 + .4f; r.Capsule(cx + Mathf.Cos(ang) * s * .34f, cy + Mathf.Sin(ang) * s * .34f, cx + Mathf.Cos(ang) * s * .52f, cy + Mathf.Sin(ang) * s * .52f, s * .08f, w); }
                }
            });
        }

        static Sprite MakeHand()
        {
            // 128 x 160, fingertip at the top centre (pivot there), hand hangs down and to the right
            return Make(128, 160, r =>
            {
                r.Ellipse(74, 60, 40, 26, new Color(0, 0, 0, .35f));
                void Shapes(Color c, float grow)
                {
                    r.RoundRect(44 - grow, 92 - grow, 24 + grow * 2, 60 + grow * 2, 12 + grow, c);
                    r.RoundRect(44 - grow, 52 - grow, 62 + grow * 2, 48 + grow * 2, 20 + grow, c);
                    for (int i = 0; i < 3; i++) r.Circle(76 + i * 14, 98 - i * 4, 10 + grow, c);
                    r.Ellipse(40, 72, 12 + grow, 17 + grow, c);
                }
                Shapes(Palette.Ink, 5); Shapes(Color.white, 0);
                r.Capsule(50, 130, 62, 130, 2.5f, new Color(.114f, .07f, .2f, .45f)); r.Capsule(50, 116, 62, 116, 2.5f, new Color(.114f, .07f, .2f, .45f));
            }, new Vector2(56f / 128f, 1f - 8f / 160f));
        }

        static Sprite MakeCage()
        {
            return Make(128, 128, r =>
            {
                float h = 56; const float cx = 64, cy = 64;
                void Bars(Color c, float w)
                {
                    for (int i = -1; i <= 1; i++) r.Capsule(cx + i * 32, cy - h, cx + i * 32, cy + h, w, c);
                    for (int i = -1; i <= 1; i += 2) r.Capsule(cx - h, cy + i * 26, cx + h, cy + i * 26, w, c);
                }
                Bars(Palette.Ink, 14); Bars(Palette.Hex("#c9cbe6"), 7);
                r.RoundRect(cx - 15, 108, 30, 18, 5, Palette.Ink); r.RoundRect(cx - 13, 110, 26, 14, 4, Palette.Hex("#c9cbe6"));
            });
        }
    }
}
