using UnityEngine;
using LastMatch.Core;

namespace LastMatch.View
{
    /// <summary>One gem on screen: shape, outline, highlight, special badge, the player's face, and cage or fog overlays.</summary>
    public class GemView : MonoBehaviour
    {
        public int GemId;
        SpriteRenderer outline, body, highlight, rainbow, ring, badge, eyeL, eyeR, pupilL, pupilR, smile, frown, mouthO, xL, xR, browL, browR, glasses, cage, fog;
        TextMesh fogText;
        SpriteRenderer[] all;

        public static GemView Create(Transform parent)
        {
            var go = new GameObject("Gem");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<GemView>();
            v.Build();
            return v;
        }

        SpriteRenderer Add(string name, Sprite s, int order, float scale, Vector2 pos, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order; sr.color = color ?? Color.white;
            return sr;
        }

        void Build()
        {
            const float ex = .13f, ey = .04f;
            outline = Add("Outline", SpriteFactory.Shapes[0], 10, 1.13f, Vector2.zero, Palette.Ink);
            body = Add("Body", SpriteFactory.Shapes[0], 11, 1f, Vector2.zero);
            rainbow = Add("Rainbow", SpriteFactory.Rainbow, 11, 1f, Vector2.zero);
            highlight = Add("Highlight", SpriteFactory.Highlight, 12, .3f, new Vector2(-.14f, .16f), new Color(1, 1, 1, .55f)); highlight.transform.localRotation = Quaternion.Euler(0, 0, 35);
            ring = Add("Ring", SpriteFactory.Ring, 12, .95f, Vector2.zero, new Color(1, 1, 1, .6f));
            badge = Add("Badge", null, 13, 1f, Vector2.zero);
            eyeL = Add("EyeL", SpriteFactory.EyeWhite, 14, .32f, new Vector2(-ex, ey));
            eyeR = Add("EyeR", SpriteFactory.EyeWhite, 14, .32f, new Vector2(ex, ey));
            pupilL = Add("PupilL", SpriteFactory.Pupil, 15, .32f, new Vector2(-ex, ey));
            pupilR = Add("PupilR", SpriteFactory.Pupil, 15, .32f, new Vector2(ex, ey));
            smile = Add("Smile", SpriteFactory.Smile, 14, .46f, new Vector2(0, -.125f));
            frown = Add("Frown", SpriteFactory.Smile, 14, .4f, new Vector2(0, -.1f)); frown.flipY = true;
            mouthO = Add("MouthO", SpriteFactory.MouthO, 14, .34f, new Vector2(0, -.16f));
            xL = Add("XL", SpriteFactory.XMark, 15, .5f, new Vector2(-ex, ey));
            xR = Add("XR", SpriteFactory.XMark, 15, .5f, new Vector2(ex, ey));
            browL = Add("BrowL", SpriteFactory.Brow, 15, .45f, new Vector2(-ex - .01f, ey + .14f));
            browR = Add("BrowR", SpriteFactory.Brow, 15, .45f, new Vector2(ex + .01f, ey + .14f)); browR.flipX = true;
            glasses = Add("Glasses", SpriteFactory.Glasses, 15, .47f, new Vector2(0, ey));
            cage = Add("Cage", SpriteFactory.Cage, 18, 1f, Vector2.zero);
            fog = Add("Fog", SpriteFactory.FogTile, 18, 1f, Vector2.zero);
            var tgo = new GameObject("FogText"); tgo.transform.SetParent(transform, false);
            fogText = tgo.AddComponent<TextMesh>();
            fogText.font = UiKit.Font; fogText.text = "?"; fogText.fontSize = 64; fogText.characterSize = .09f; fogText.anchor = TextAnchor.MiddleCenter; fogText.alignment = TextAlignment.Center;
            fogText.color = Palette.Hex("#e6e1ff"); fogText.fontStyle = FontStyle.Bold;
            var mr = tgo.GetComponent<MeshRenderer>(); mr.material = UiKit.Font.material; mr.sortingOrder = 19;
            all = new[] { outline, body, highlight, rainbow, ring, badge, eyeL, eyeR, pupilL, pupilR, smile, frown, mouthO, xL, xR, browL, browR, glasses, cage, fog };
        }

        static void Show(SpriteRenderer sr, bool on) { if (sr.enabled != on) sr.enabled = on; }

        /// <summary>mood: happy, scared, cool, dead. look: pupil direction (-1..1). Returns nothing; call every frame.</summary>
        public void Apply(Gem g, CellType ty, bool isPlayerFace, string mood, Vector2 look, float t, float blink)
        {
            bool isFog = ty == CellType.Fog;
            float alpha = 1f, sx = 1f, sy = 1f;
            if (g.Born > 0) { float b = 1 - g.Born; sx = sy = .2f + .8f * (1 - Mathf.Pow(1 - b, 3)); }
            if (g.Squash > 0) { sx *= 1 + .18f * g.Squash; sy *= 1 - .18f * g.Squash; }
            if (g.Pop >= 0)
            {
                float p = g.Pop; float k = g.IsPlayer ? 1 + .35f * Mathf.Sin(p * Mathf.PI) : 1 + .5f * p; sx *= k; sy *= k;
                alpha *= g.IsPlayer ? (p < .7f ? 1 : 1 - (p - .7f) / .3f) : 1 - p;
            }
            transform.localScale = new Vector3(sx, sy, 1);

            foreach (var sr in all) Show(sr, false);
            fogText.gameObject.SetActive(isFog);
            if (isFog) { Show(fog, true); fog.color = new Color(1, 1, 1, alpha); return; }

            bool isRainbow = g.Special == Special.Rainbow;
            if (isRainbow)
            {
                Show(rainbow, true); rainbow.color = new Color(1, 1, 1, alpha);
                rainbow.transform.localRotation = Quaternion.Euler(0, 0, -t * 60f);
                Show(ring, true); ring.color = new Color(1, 1, 1, (.35f + .45f * (.5f + .5f * Mathf.Sin(t * 5))) * alpha);
            }
            else
            {
                var look2 = Palette.Gems[Mathf.Clamp(g.Color, 0, Palette.Gems.Length - 1)];
                var shape = SpriteFactory.Shapes[Mathf.Clamp(g.Color, 0, SpriteFactory.Shapes.Length - 1)];
                Show(outline, true); Show(body, true);
                outline.sprite = shape; body.sprite = shape;
                outline.color = new Color(look2.D.r, look2.D.g, look2.D.b, alpha);
                body.color = new Color(look2.C.r, look2.C.g, look2.C.b, alpha);
                if (!isPlayerFace) { Show(highlight, true); highlight.color = new Color(1, 1, 1, .55f * alpha); }
                if (g.Special != Special.None)
                {
                    Show(badge, true); badge.sprite = SpriteFactory.Badges[g.Special]; badge.color = new Color(1, 1, 1, alpha);
                    Show(ring, true); ring.color = new Color(1, 1, 1, (.35f + .45f * (.5f + .5f * Mathf.Sin(t * 5))) * alpha);
                }
            }
            if (ty == CellType.Cage) { Show(cage, true); cage.color = new Color(1, 1, 1, alpha); }
            if (!isPlayerFace) return;

            var inkA = new Color(1, 1, 1, alpha);
            if (mood == "dead")
            {
                Show(xL, true); Show(xR, true); Show(frown, true); xL.color = xR.color = frown.color = inkA; return;
            }
            if (mood == "cool")
            {
                Show(glasses, true); Show(smile, true); glasses.color = smile.color = inkA; return;
            }
            bool scared = mood == "scared";
            bool blinking = blink > 0 && !scared;
            float eyeScale = scared ? .41f : .32f;
            foreach (var e in new[] { eyeL, eyeR })
            {
                Show(e, true); e.color = inkA;
                e.transform.localScale = new Vector3(eyeScale, blinking ? eyeScale * .12f : eyeScale, 1);
            }
            if (!blinking)
            {
                float er = eyeScale * .25f, pr = scared ? .2f : .32f;
                foreach (var p in new[] { pupilL, pupilR })
                {
                    Show(p, true); p.color = inkA; p.transform.localScale = Vector3.one * pr;
                    float bx = p == pupilL ? -.13f : .13f;
                    p.transform.localPosition = new Vector3(bx + look.x * er * .45f, .04f + look.y * er * .45f, 0);
                }
            }
            if (scared)
            {
                Show(browL, true); Show(browR, true); Show(mouthO, true); browL.color = browR.color = mouthO.color = inkA;
                mouthO.transform.localPosition = new Vector3(0, -.16f + Mathf.Sin(t * 40) * .012f, 0);
            }
            else { Show(smile, true); smile.color = inkA; }
        }
    }
}
