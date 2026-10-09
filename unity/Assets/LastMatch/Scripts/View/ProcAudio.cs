using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastMatch.View
{
    /// <summary>Chiptune blips synthesized at runtime, so there are no audio files to ship.</summary>
    public class ProcAudio : MonoBehaviour
    {
        const int Rate = 22050;
        AudioSource src;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        public bool Muted;

        struct Tone { public float F, Dur, Vol, Slide, Delay; public string Type; public Tone(float f, float dur, string type, float vol, float slide = 0, float delay = 0) { F = f; Dur = dur; Type = type; Vol = vol; Slide = slide; Delay = delay; } }

        void Awake() { src = gameObject.AddComponent<AudioSource>(); src.playOnAwake = false; }

        public void Play(string name)
        {
            if (Muted) return;
            if (!cache.TryGetValue(name, out var clip)) { clip = Build(name); cache[name] = clip; }
            if (clip != null) src.PlayOneShot(clip);
        }

        static List<Tone> Recipe(string name)
        {
            var t = new List<Tone>();
            if (name.StartsWith("pop")) { int n = int.Parse(name.Substring(3)); t.Add(new Tone(420 + n * 90, .16f, "square", .045f, 220)); return t; }
            switch (name)
            {
                case "swap": t.Add(new Tone(300, .08f, "triangle", .05f, 160)); break;
                case "blast": t.Add(new Tone(200, .25f, "saw", .07f, 500)); t.Add(new Tone(90, .3f, "square", .05f, -40, .02f)); break;
                case "miss": t.Add(new Tone(170, .25f, "saw", .05f, -90)); break;
                case "glitch": t.Add(new Tone(900, .05f, "square", .04f, -600)); t.Add(new Tone(500, .08f, "saw", .04f, 400, .06f)); break;
                case "death": t.Add(new Tone(320, .55f, "saw", .08f, -280)); t.Add(new Tone(200, .7f, "square", .05f, -150, .1f)); break;
                case "power": t.Add(new Tone(523, .1f, "square", .05f)); t.Add(new Tone(659, .1f, "square", .05f, 0, .09f)); t.Add(new Tone(784, .22f, "square", .05f, 0, .18f)); break;
                case "use": t.Add(new Tone(700, .12f, "triangle", .06f, 300)); break;
                case "win": { float[] fs = { 523, 659, 784, 1047 }; for (int i = 0; i < 4; i++) t.Add(new Tone(fs[i], .18f, "square", .05f, 0, i * .12f)); t.Add(new Tone(1319, .5f, "triangle", .06f, 0, .5f)); break; }
                case "land": t.Add(new Tone(140, .05f, "triangle", .03f, -40)); break;
                case "shield": t.Add(new Tone(600, .1f, "sine", .05f, 200)); break;
                case "unlock": t.Add(new Tone(880, .08f, "triangle", .05f)); t.Add(new Tone(1320, .14f, "triangle", .05f, 0, .07f)); break;
                case "cage": t.Add(new Tone(220, .12f, "square", .05f, -60)); t.Add(new Tone(160, .2f, "square", .04f, -40, .1f)); break;
                case "bump": t.Add(new Tone(120, .07f, "square", .04f, -30)); break;
                case "made": t.Add(new Tone(660, .07f, "triangle", .04f, 300)); t.Add(new Tone(990, .12f, "triangle", .04f, 200, .06f)); break;
                case "shift": t.Add(new Tone(500, .1f, "sine", .05f, 400)); t.Add(new Tone(900, .2f, "sine", .05f, -300, .1f)); break;
                case "tick": t.Add(new Tone(1000, .05f, "square", .035f)); break;
                case "rush": t.Add(new Tone(300, .1f, "saw", .06f, 300)); t.Add(new Tone(600, .25f, "saw", .06f, 300, .1f)); break;
            }
            return t;
        }

        static AudioClip Build(string name)
        {
            var tones = Recipe(name); if (tones.Count == 0) return null;
            float total = 0; foreach (var t in tones) total = Mathf.Max(total, t.Delay + t.Dur + .03f);
            int n = Mathf.CeilToInt(total * Rate);
            var data = new float[n];
            foreach (var t in tones)
            {
                int start = (int)(t.Delay * Rate), len = (int)(t.Dur * Rate);
                float f0 = t.F, f1 = Mathf.Max(30, t.F + t.Slide);
                double phase = 0;
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float u = (float)i / len;
                    float f = t.Slide != 0 ? f0 * Mathf.Pow(f1 / f0, u) : f0;
                    phase += f / Rate; if (phase >= 1) phase -= 1;
                    float ph = (float)phase, s;
                    switch (t.Type)
                    {
                        case "square": s = ph < .5f ? 1 : -1; break;
                        case "saw": s = 2 * ph - 1; break;
                        case "triangle": s = 4 * Mathf.Abs(ph - .5f) - 1; break;
                        default: s = Mathf.Sin(ph * Mathf.PI * 2); break;
                    }
                    float gain = t.Vol * Mathf.Pow(.0001f / t.Vol, u) * 2.5f;   // same exponential decay as the web version, scaled up for mobile speakers
                    data[start + i] += s * gain;
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1, 1);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
