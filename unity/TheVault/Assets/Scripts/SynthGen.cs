// Procedural sound effects in plain C# (no UnityEngine): every game sound is synthesised at startup.
// VaultAudio turns these into AudioClips, unless a real recording exists in Resources/Audio/<name>.
using System;

public static class SynthGen
{
    public const int SR = 44100;
    // name -> number of variants
    public static readonly (string name, int variants, bool loop)[] SOUNDS =
    {
        ("step_carpet", 4, false), ("step_concrete", 4, false), ("land", 1, false), ("jump", 1, false), ("slide", 1, true),
        ("whoosh", 1, false), ("shatter", 2, false), ("click", 1, false), ("lock", 1, false), ("chime", 1, false), ("buzzer", 1, false),
        ("klaxon", 1, true), ("rumble", 1, false), ("motor", 1, false), ("clang", 1, false), ("beep", 1, false), ("creak", 2, false),
        ("breath", 1, true), ("growl", 1, false), ("bstep", 2, false), ("heartbeat", 1, false), ("drone", 1, true), ("hum", 1, true),
        ("amb_creak", 1, false), ("amb_knock", 1, false), ("amb_vent", 1, false), ("amb_steps", 1, false), ("stinger", 1, false),
    };

    const float TAU = (float)(Math.PI * 2);
    static float[] Buf(float sec) => new float[(int)(sec * SR)];
    static float Exp(float t, float tau) => t < 0 ? 0 : (float)Math.Exp(-t / tau);
    static float Att(float t, float a) => t < 0 ? 0 : t < a ? t / a : 1;

    class Rng { uint s; public Rng(int seed) { s = (uint)seed * 2654435761u + 1; } public float N() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 8388607.5f - 1f; } public float U() => (N() + 1) * 0.5f; }

    // one-pole filters and a simple state-variable bandpass
    struct LP { float y; public float Do(float x, float hz) { float a = 1 - (float)Math.Exp(-TAU * hz / SR); y += a * (x - y); return y; } }
    struct HP { LP lp; public float Do(float x, float hz) => x - lp.Do(x, hz); }
    struct BP
    {
        float low, band;
        public float Do(float x, float hz, float q = 1.5f)
        {
            float f = 2 * (float)Math.Sin(Math.PI * Math.Min(hz, SR / 6f) / SR);
            low += f * band; float high = x - low - band / q; band += f * high; return band;
        }
    }

    static void Normalize(float[] b, float peak = 0.9f)
    {
        float m = 1e-6f; foreach (var v in b) m = Math.Max(m, Math.Abs(v));
        for (int i = 0; i < b.Length; i++) b[i] = b[i] / m * peak;
    }
    // tiny fade-in (keeps sharp attacks), longer fade-out (no click at the end)
    static void FadeEnds(float[] b, float inSec = 0.0005f, float outSec = 0.008f)
    {
        int ni = Math.Max(1, Math.Min(b.Length / 2, (int)(inSec * SR))), no = Math.Max(1, Math.Min(b.Length / 2, (int)(outSec * SR)));
        for (int i = 0; i < ni; i++) b[i] *= (float)i / ni;
        for (int i = 0; i < no; i++) b[b.Length - 1 - i] *= (float)i / no;
    }
    // loop seam: crossfade the tail into the head
    static float[] MakeLoop(float[] b, float xf = 0.08f)
    {
        int n = (int)(xf * SR); var o = new float[b.Length - n];
        for (int i = 0; i < o.Length; i++) o[i] = b[i];
        for (int i = 0; i < n; i++) { float k = (float)i / n; o[i] = b[i] * k + b[o.Length + i] * (1 - k); }
        return o;
    }
    static void AddEcho(float[] b, params (float delay, float gain)[] taps)
    {
        var src = (float[])b.Clone();
        foreach (var (d, g) in taps) { int off = (int)(d * SR); for (int i = off; i < b.Length; i++) b[i] += src[i - off] * g; }
    }
    static void Thump(float[] b, float at, float f0, float f1, float tau, float amp)
    {
        float ph = 0; int s0 = (int)(at * SR);
        for (int i = s0; i < b.Length; i++) { float t = (float)(i - s0) / SR; if (t > tau * 8) break; float f = f1 + (f0 - f1) * Exp(t, tau * 0.7f); ph += TAU * f / SR; b[i] += (float)Math.Sin(ph) * Exp(t, tau) * Att(t, 0.002f) * amp; }
    }

    public static float[] Make(string name, int v)
    {
        var r = new Rng(name.GetHashCode() ^ (v * 7919)); float[] b;
        switch (name)
        {
            case "step_carpet":
            {
                b = Buf(0.2f); LP lp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] = lp.Do(r.N(), 600 + v * 60) * (Exp(t, 0.035f) + 0.4f * Exp(t - 0.05f, 0.025f) * (t > 0.05f ? 1 : 0)) * 2.2f; }
                Thump(b, 0, 90, 60, 0.03f, 0.5f); break;
            }
            case "step_concrete":
            {
                b = Buf(0.3f); BP bp = default; LP lp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR, n = r.N(); b[i] = bp.Do(n, 2400 + v * 300, 2f) * Exp(t, 0.012f) * 1.4f + lp.Do(n, 900) * Exp(t, 0.03f) * 1.5f; }
                Thump(b, 0, 120, 80, 0.025f, 0.45f); AddEcho(b, (0.023f, 0.25f), (0.041f, 0.15f), (0.067f, 0.08f)); break;
            }
            case "land":
            {
                b = Buf(0.35f); LP lp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] = lp.Do(r.N(), 500) * Exp(t, 0.06f) * 2.5f; }
                Thump(b, 0, 80, 45, 0.07f, 0.9f); break;
            }
            case "jump":
            {
                b = Buf(0.18f); BP bp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] = bp.Do(r.N(), 1300, 1f) * (float)Math.Sin(Math.PI * t / 0.18f); }
                break;
            }
            case "slide":
            {
                b = Buf(1.1f); BP bp = default; LP lp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR, n = r.N(); b[i] = bp.Do(n, 1100 + 300 * (float)Math.Sin(t * 9), 0.8f) * (0.8f + 0.2f * (float)Math.Sin(t * 23)) + lp.Do(n, 250) * 0.6f; }
                Normalize(b); return MakeLoop(b);
            }
            case "whoosh":
            {
                b = Buf(0.35f); BP bp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR, k = t / 0.35f; b[i] = bp.Do(r.N(), 400 + 2200 * k, 1.2f) * (float)Math.Sin(Math.PI * k); }
                break;
            }
            case "shatter":
            {
                b = Buf(0.9f); HP hp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] = hp.Do(r.N(), 2500) * Exp(t, 0.02f) * 1.5f; }
                Thump(b, 0, 300, 150, 0.02f, 0.4f);
                for (int k = 0; k < 55; k++)
                {
                    float at = -(float)Math.Log(1 - r.U() * 0.99f) * 0.12f, f = 2200 + r.U() * 6500, tau = 0.015f + r.U() * 0.07f, amp = 0.15f + r.U() * 0.35f, ph = r.U() * TAU;
                    int s0 = (int)(at * SR); for (int i = s0; i < b.Length && i < s0 + (int)(tau * 6 * SR); i++) { float t = (float)(i - s0) / SR; b[i] += (float)Math.Sin(ph + TAU * f * t) * Exp(t, tau) * amp; }
                }
                break;
            }
            case "click":
            {
                b = Buf(0.06f);
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] = r.N() * Exp(t, 0.002f) + (float)Math.Sin(TAU * 3200 * t) * Exp(t, 0.006f) * 0.6f; }
                break;
            }
            case "lock":
            {
                b = Buf(0.6f);
                Thump(b, 0, 160, 110, 0.04f, 0.8f);
                foreach (var (f, a) in new[] { (930f, 0.4f), (1480f, 0.3f), (2310f, 0.2f) }) for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] += (float)Math.Sin(TAU * f * t) * Exp(t, 0.08f) * a; }
                for (int i = (int)(0.13f * SR); i < b.Length; i++) { float t = (float)i / SR - 0.13f; b[i] += r.N() * Exp(t, 0.004f) * 0.8f + (float)Math.Sin(TAU * 2100 * t) * Exp(t, 0.03f) * 0.3f; }
                break;
            }
            case "chime":
            {
                b = Buf(1.4f);
                foreach (var (at, f) in new[] { (0f, 880f), (0f, 1318.5f), (0.16f, 1318.5f), (0.16f, 1760f) })
                    for (int i = (int)(at * SR); i < b.Length; i++) { float t = (float)i / SR - at; b[i] += ((float)Math.Sin(TAU * f * t) + 0.2f * (float)Math.Sin(TAU * f * 2.01f * t)) * Exp(t, 0.45f) * Att(t, 0.003f) * 0.4f; }
                break;
            }
            case "buzzer":
            {
                b = Buf(0.5f); LP lp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; float sq = Math.Sign(Math.Sin(TAU * 110 * t)) + 0.5f * Math.Sign(Math.Sin(TAU * 220.7f * t)); b[i] = lp.Do(sq, 1800) * Att(t, 0.01f) * (t > 0.42f ? (0.5f - t) / 0.08f : 1); }
                break;
            }
            case "klaxon":
            {
                b = Buf(1.28f); LP lp = default; float ph = 0;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR, f = (t % 0.64f) < 0.32f ? 520 : 390; ph += f / SR; float saw = 2 * (ph - (float)Math.Floor(ph)) - 1; b[i] = lp.Do(saw, 2500); }
                Normalize(b, 0.8f); return MakeLoop(b, 0.04f);
            }
            case "rumble":
            {
                b = Buf(2.8f); LP lp = default, lp2 = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; float env = Att(t, 0.4f) * Exp(t - 0.8f, 0.9f); b[i] = (lp2.Do(lp.Do(r.N(), 120), 120) * 6 + (float)Math.Sin(TAU * 38 * t) * 0.6f) * env; }
                break;
            }
            case "motor":
            {
                b = Buf(0.9f); LP lp = default; float ph = 0;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR, f = 50 + 15 * Att(t, 0.3f); ph += f / SR; float saw = 2 * (ph - (float)Math.Floor(ph)) - 1; b[i] = (lp.Do(saw + r.N() * 0.6f, 700)) * Att(t, 0.08f) * (t > 0.7f ? (0.9f - t) / 0.2f : 1); }
                break;
            }
            case "clang":
            {
                b = Buf(1.6f);
                float[] fr = { 180, 431, 709, 1123, 1612, 2290 };
                for (int k = 0; k < fr.Length; k++) for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] += (float)Math.Sin(TAU * fr[k] * t) * Exp(t, 0.7f / (1 + k * 0.6f)) * (0.5f / (1 + k * 0.4f)); }
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] += r.N() * Exp(t, 0.01f) * 0.8f; }
                Thump(b, 0, 90, 60, 0.06f, 0.7f); break;
            }
            case "beep":
            {
                b = Buf(0.3f);
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; bool on = t < 0.08f || (t > 0.14f && t < 0.22f); b[i] = on ? (float)Math.Sin(TAU * 1800 * t) * 0.6f : 0; }
                FadeEnds(b, 0.001f, 0.002f); break;
            }
            case "creak":
            {
                b = Buf(0.9f); LP lp = default; float ph = 0, f = 230 + v * 40;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; f += r.N() * 3f; f = Math.Max(150, Math.Min(380, f)); ph += f / SR; float saw = 2 * (ph - (float)Math.Floor(ph)) - 1; b[i] = lp.Do(saw, 1400) * (float)Math.Sin(Math.PI * t / 0.9f) * (0.6f + 0.4f * (float)Math.Sin(t * 37 + v)); }
                break;
            }
            case "breath":
            {
                b = Buf(3.3f); BP bp = default; LP lp = default;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / SR, n = r.N();
                    float inh = t < 1.2f ? (float)Math.Sin(Math.PI * t / 1.2f) : 0, exh = t > 1.4f && t < 3.1f ? (float)Math.Sin(Math.PI * (t - 1.4f) / 1.7f) : 0;
                    b[i] = bp.Do(n, 900, 1f) * inh * 0.8f + lp.Do(n, 700) * exh * 1.6f;
                }
                Normalize(b, 0.7f); return MakeLoop(b, 0.15f);
            }
            case "growl":
            {
                b = Buf(1.7f); LP lp = default, lp2 = default; float ph = 0;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR, f = 62 + 6 * (float)Math.Sin(TAU * 5.5f * t); ph += f / SR; float saw = 2 * (ph - (float)Math.Floor(ph)) - 1; float env = Att(t, 0.25f) * (t > 1.2f ? (1.7f - t) / 0.5f : 1); b[i] = (lp.Do(saw, 500) + lp2.Do(r.N(), 350) * 1.5f) * env * (0.7f + 0.3f * (float)Math.Sin(TAU * 11 * t)); }
                break;
            }
            case "bstep":
            {
                b = Buf(0.45f); LP lp = default; BP bp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR, n = r.N(); b[i] = lp.Do(n, 300) * Exp(t, 0.07f) * 3f + bp.Do(n, 600 + v * 150, 1f) * Exp(t - 0.09f, 0.05f) * (t > 0.09f ? 0.6f : 0); }
                Thump(b, 0, 70, 42, 0.09f, 1f); break;
            }
            case "heartbeat":
            {
                b = Buf(0.6f);
                Thump(b, 0, 58, 38, 0.06f, 1f); Thump(b, 0.27f, 68, 44, 0.05f, 0.7f);
                LP lp = default; for (int i = 0; i < b.Length; i++) b[i] = lp.Do(b[i], 300) * 2f;
                break;
            }
            case "drone":
            {
                // all partials are multiples of 1/8 Hz, so the 8 s buffer loops seamlessly
                b = Buf(8f); LP lp = default, lp2 = default;
                (float f, float a, float am)[] parts = { (41.25f, 0.5f, 0.125f), (55f, 0.4f, 0.25f), (61.875f, 0.25f, 0.375f), (82.5f, 0.2f, 0.125f), (110.125f, 0.08f, 0.5f) };
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / SR, s = 0;
                    foreach (var p in parts) s += (float)Math.Sin(TAU * p.f * t) * p.a * (0.6f + 0.4f * (float)Math.Sin(TAU * p.am * t));
                    b[i] = s + lp2.Do(lp.Do(r.N(), 160), 160) * 1.5f;
                }
                Normalize(b, 0.8f); return MakeLoop(b, 0.3f);
            }
            case "hum":
            {
                b = Buf(1f);
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] = 0.5f * (float)Math.Sin(TAU * 120 * t) + 0.25f * (float)Math.Sin(TAU * 240 * t) + 0.12f * (float)Math.Sin(TAU * 360 * t) + 0.05f * r.N(); }
                Normalize(b, 0.8f); return b;
            }
            case "amb_creak":
            {
                b = Buf(2.6f); LP lp = default;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / SR, f = 140 - 45 * t / 2.6f, am = 0.5f + 0.5f * (float)Math.Sin(TAU * (6 + 2 * (float)Math.Sin(t * 3)) * t);
                    float s = (float)Math.Sin(TAU * f * t) + 0.5f * (float)Math.Sin(TAU * f * 2 * t) + 0.3f * (float)Math.Sin(TAU * f * 3.1f * t);
                    b[i] = lp.Do(s * am, 900) * (float)Math.Sin(Math.PI * t / 2.6f);
                }
                break;
            }
            case "amb_knock":
            {
                b = Buf(1.3f);
                foreach (var at in new[] { 0f, 0.33f, 0.68f }) { Thump(b, at, 110, 70, 0.05f, 1f); LP lp = default; for (int i = (int)(at * SR); i < b.Length; i++) { float t = (float)i / SR - at; b[i] += lp.Do(r.N(), 400) * Exp(t, 0.03f) * 1.5f; } }
                AddEcho(b, (0.09f, 0.3f), (0.17f, 0.15f)); break;
            }
            case "amb_vent":
            {
                b = Buf(3.2f); LP lp = default; BP bp = default;
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR, n = r.N(); b[i] = (lp.Do(n, 450) * 2 + bp.Do(n, 300 + 200 * (float)Math.Sin(t * 2), 2f) * 0.5f) * (float)Math.Pow(Math.Sin(Math.PI * t / 3.2f), 2); }
                break;
            }
            case "amb_steps":
            {
                b = Buf(2.2f);
                for (int k = 0; k < 4; k++) { float at = k * 0.48f; Thump(b, at, 90, 55, 0.05f, 0.8f); LP lp = default; for (int i = (int)(at * SR); i < b.Length; i++) { float t = (float)i / SR - at; b[i] += lp.Do(r.N(), 500) * Exp(t, 0.03f) * 1.2f; } }
                AddEcho(b, (0.11f, 0.35f), (0.23f, 0.2f)); break;
            }
            case "stinger":
            {
                b = Buf(2.4f); LP lp = default;
                Thump(b, 0, 60, 30, 0.25f, 1.2f);
                foreach (var f in new[] { 440f, 466.2f, 523.3f, 622.3f })
                    for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] += (float)Math.Sin(TAU * f * t + 3 * (float)Math.Sin(TAU * 6.3f * t)) * Exp(t, 0.7f) * Att(t, 0.01f) * 0.25f; }
                for (int i = 0; i < b.Length; i++) { float t = (float)i / SR; b[i] += lp.Do(r.N(), 3000) * Exp(t, 0.15f) * 0.9f; }
                break;
            }
            default: b = Buf(0.1f); break;
        }
        Normalize(b); FadeEnds(b);
        return b;
    }
}
