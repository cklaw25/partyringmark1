// Procedural textures in plain C# (no UnityEngine), so they can be generated and previewed outside Unity too.
// Two looks: the BACKROOMS maze (mustard wallpaper, damp carpet, drop ceiling) and the FACILITY rooms
// (stained concrete, wet floors, steel). Rows run bottom-to-top, like Texture2D.SetPixels.
using System;
using System.Collections.Generic;

public struct C4
{
    public float r, g, b, a;
    public C4(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
    public static C4 operator *(C4 c, float k) => new C4(c.r * k, c.g * k, c.b * k, c.a);
    public static C4 Lerp(C4 x, C4 y, float t)
    {
        t = TexGen.Clamp01(t);
        return new C4(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
    }
    public C4 WithA(float na) => new C4(r, g, b, na);
}

public class Img
{
    public readonly int w, h; public readonly C4[] px;
    public Img(int w, int h, C4 fill) { this.w = w; this.h = h; px = new C4[w * h]; for (int i = 0; i < px.Length; i++) px[i] = fill; }
    public C4 this[int x, int y] { get => px[y * w + x]; set => px[y * w + x] = value; }
    public bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
    // paint colour c with coverage k: rgb lerps toward c, alpha accumulates (works for opaque and decal images)
    public void Paint(int x, int y, C4 c, float k)
    {
        if (!In(x, y) || k <= 0) return;
        var p = this[x, y]; k = TexGen.Clamp01(k);
        this[x, y] = new C4(p.r + (c.r - p.r) * k, p.g + (c.g - p.g) * k, p.b + (c.b - p.b) * k, p.a + (1 - p.a) * k * c.a);
    }
    public void Disc(float cx, float cy, float rad, C4 c, float k, float soft = 0.5f)
    {
        int r = (int)Math.Ceiling(rad + 1);
        for (int y = (int)cy - r; y <= (int)cy + r; y++) for (int x = (int)cx - r; x <= (int)cx + r; x++)
        {
            float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            Paint(x, y, c, k * TexGen.Clamp01((rad - d) / Math.Max(0.01f, rad * soft) + 0.5f));
        }
    }
    public void Rect(int x0, int y0, int x1, int y1, C4 c, float k = 1) { for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) Paint(x, y, c, k); }
}

public static class TexGen
{
    // ---------- math / noise ----------
    public static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float Smooth(float e0, float e1, float x) { float t = Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
    public static float Hash(int x, int y, int s)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + s * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }
    // tileable value noise (lattice periods px, py)
    public static float Noise(float x, float y, int px, int py, int s)
    {
        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y); float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int x0 = Mod(xi, px), x1 = Mod(xi + 1, px), y0 = Mod(yi, py), y1 = Mod(yi + 1, py);
        float a = Hash(x0, y0, s), b = Hash(x1, y0, s), c = Hash(x0, y1, s), d = Hash(x1, y1, s);
        return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
    }
    // fractal noise over a 0..1 tile; fx/fy = base frequency per axis (tiles seamlessly)
    public static float Fbm(float u, float v, int fx, int fy, int oct, int s)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int o = 0; o < oct; o++) { sum += amp * Noise(u * fx, v * fy, fx, fy, s + o * 31); norm += amp; amp *= 0.5f; fx *= 2; fy *= 2; }
        return sum / norm;
    }
    public static float Ridged(float u, float v, int f, int oct, int s) => 1f - Math.Abs(Fbm(u, v, f, f, oct, s) * 2f - 1f);

    public static C4[] NormalFromHeight(float[] src, int w, int h, float strength)
    {
        // soften first (3x3 box) so single-pixel details don't turn into hard "+" shapes
        var hgt = new float[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float sum = 0; for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) sum += src[Mod(y + dy, h) * w + Mod(x + dx, w)];
            hgt[y * w + x] = sum / 9f;
        }
        var o = new C4[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float dx = hgt[y * w + Mod(x + 1, w)] - hgt[y * w + Mod(x - 1, w)];
            float dy = hgt[Mod(y + 1, h) * w + x] - hgt[Mod(y - 1, h) * w + x];
            float nx = -dx * strength, ny = -dy * strength, nz = 1f, l = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            o[y * w + x] = new C4(nx / l * 0.5f + 0.5f, ny / l * 0.5f + 0.5f, nz / l * 0.5f + 0.5f, 1f);
        }
        return o;
    }

    // ---------- BACKROOMS ----------
    // wallpaper for one wall face (2.5 m x 3 m). variant 0 clean, 1 water-stained, 2 peeling, 3 mouldy
    public static Img Wallpaper(int variant, out float[] height)
    {
        const int W = 256, H = 320; var img = new Img(W, H, new C4(0, 0, 0)); height = new float[W * H];
        int s = 100 + variant * 7; var rnd = new Random(s);
        var baseC = new C4(0.68f, 0.6f, 0.33f);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            float u = (float)x / W, v = (float)y / H;
            float stripe = (x / 16) % 2 == 0 ? 1f : 0.95f;
            // damask-ish diamond motif on an offset lattice
            int row = y / 40; int cx = (x + (row % 2) * 16) % 32, cy = y % 40;
            float dx = (cx - 16) / 16f, dy = (cy - 20) / 20f, d = Math.Abs(dx) + Math.Abs(dy);
            float ring = Smooth(0.5f, 0.58f, d) * (1 - Smooth(0.66f, 0.74f, d));
            float dot = 1 - Smooth(0.12f, 0.2f, d);
            float motif = 1f - 0.1f * ring - 0.08f * dot;
            float paper = 0.93f + 0.07f * Fbm(u, v, 8, 10, 4, s) + (Hash(x, y, s) - 0.5f) * 0.05f;
            float tint = 0.94f + 0.12f * Fbm(u, v, 2, 2, 3, s + 1);
            var c = baseC * (stripe * motif * paper * tint);
            float hgt = 0.4f + ring * 0.3f + dot * 0.2f + Hash(x, y, s + 2) * 0.08f;
            // grime toward the ceiling and above the skirting
            float grime = Smooth(0.82f, 1f, v) * (0.25f + 0.25f * Fbm(u, v, 6, 3, 3, s + 3)) + (1 - Smooth(0.05f, 0.14f, v)) * 0.18f * Fbm(u, v, 10, 2, 3, s + 4);
            c = c * (1 - grime);
            // skirting board
            if (y < 16)
            {
                float g = 0.85f + 0.15f * Fbm(u, v, 16, 1, 3, s + 5);
                c = new C4(0.3f, 0.23f, 0.12f) * g; if (y >= 13) c = c * 1.35f; if (y < 2) c = c * 0.6f;
                hgt = y >= 13 ? 1f : 0.9f;
            }
            img[x, y] = c; height[y * W + x] = hgt;
        }
        if (variant == 1 || variant == 0)
        {
            // water stains seeping from the ceiling, with darker tide lines, and drips
            float amt = variant == 1 ? 1f : 0.35f;
            for (int y = 16; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                float m = Fbm(u, v, 3, 3, 4, s + 9) + (v - 0.55f) * 1.1f;
                float stain = Smooth(0.42f, 0.6f, m), tide = Smooth(0.4f, 0.44f, m) * (1 - Smooth(0.46f, 0.5f, m));
                img.Paint(x, y, new C4(0.42f, 0.32f, 0.15f), stain * 0.45f * amt);
                if (variant == 1) img.Paint(x, y, new C4(0.3f, 0.22f, 0.1f), tide * 0.5f);
            }
            int drips = variant == 1 ? 9 : 3;
            for (int i = 0; i < drips; i++) Drip(img, rnd.Next(W), H - 1 - rnd.Next(60), 60 + rnd.Next(180), new C4(0.33f, 0.24f, 0.11f), 0.35f * amt, rnd);
        }
        if (variant == 2)
        {
            // peeled wallpaper showing plaster
            for (int y = 18; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                float p = Fbm(u, v, 3, 4, 5, s + 11) + 0.25f * (float)Math.Sin(v * Math.PI) - 0.15f;
                if (p > 0.66f) { img[x, y] = new C4(0.55f, 0.53f, 0.47f) * (0.85f + 0.15f * Fbm(u, v, 12, 14, 3, s + 12)); height[y * W + x] = 0.1f; }
                else if (p > 0.63f) { img[x, y] = new C4(0.82f, 0.76f, 0.52f); height[y * W + x] = 1f; }
            }
            for (int i = 0; i < 3; i++) Drip(img, rnd.Next(W), H - 1, 80 + rnd.Next(120), new C4(0.33f, 0.24f, 0.11f), 0.3f, rnd);
        }
        if (variant == 3)
        {
            // black mould creeping up from the floor and down from the ceiling
            for (int y = 16; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                float zone = Math.Max(1 - Smooth(0.05f, 0.4f, v), Smooth(0.75f, 1f, v));
                float cl = Fbm(u, v, 5, 6, 4, s + 13) * zone, sp = Hash(x, y, s + 14);
                if (cl > 0.32f) img.Paint(x, y, new C4(0.1f, 0.12f, 0.07f), Smooth(0.32f, 0.45f, cl) * (0.5f + sp * 0.5f));
            }
        }
        return img;
    }

    static void Drip(Img img, int x0, int yTop, int len, C4 c, float k, Random rnd)
    {
        float x = x0, wdt = 1.5f + (float)rnd.NextDouble() * 2.5f;
        for (int i = 0; i < len; i++)
        {
            int y = yTop - i; if (y < 16) break;
            float f = 1 - (float)i / len; x += ((float)rnd.NextDouble() - 0.5f) * 0.6f;
            for (int dx = -4; dx <= 4; dx++) img.Paint((int)x + dx, y, c, k * f * Clamp01(1 - Math.Abs(dx) / wdt));
        }
        img.Disc(x, yTop - len, wdt * 0.9f, c, k * 0.5f);
    }

    // damp carpet, tiles every 2 maze tiles
    public static Img Carpet(out float[] height)
    {
        const int N = 256; var img = new Img(N, N, new C4(0, 0, 0)); height = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (float)x / N, v = (float)y / N;
            float fiber = Hash(x, y, 7) * 0.5f + Hash(x / 2, y / 2, 8) * 0.5f;
            float mid = Fbm(u, v, 8, 8, 4, 9), damp = Smooth(0.58f, 0.7f, Fbm(u, v, 3, 3, 4, 10));
            var c = new C4(0.4f, 0.32f, 0.17f) * (0.82f + fiber * 0.2f + (mid - 0.5f) * 0.18f);
            c = C4.Lerp(c, c * 0.6f, damp * 0.8f);
            img[x, y] = c; height[y * N + x] = fiber;
        }
        return img;
    }

    // drop ceiling: 2x2 tiles per maze tile, T-bar grid, speckle, a few stained tiles
    public static Img Ceiling()
    {
        const int N = 256; var img = new Img(N, N, new C4(0, 0, 0));
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (float)x / N, v = (float)y / N;
            var c = new C4(0.64f, 0.62f, 0.54f) * (0.9f + 0.1f * Fbm(u, v, 16, 16, 3, 20));
            if (Hash(x, y, 21) < 0.05f) c = c * 0.8f;
            int tx = x / 128, ty = y / 128; float lx = (x % 128) / 128f - 0.5f, ly = (y % 128) / 128f - 0.5f;
            if (Hash(tx, ty, 22) < 0.35f)
            {
                float r = (float)Math.Sqrt(lx * lx + ly * ly) + (Fbm(u, v, 6, 6, 3, 23) - 0.5f) * 0.2f;
                c = C4.Lerp(c, new C4(0.5f, 0.42f, 0.25f), (1 - Smooth(0.25f, 0.32f, r)) * 0.35f + Smooth(0.27f, 0.3f, r) * (1 - Smooth(0.31f, 0.34f, r)) * 0.4f);
            }
            int gx = x % 128, gy = y % 128;
            if (gx < 3 || gy < 3) c = new C4(0.5f, 0.49f, 0.45f);
            else if (gx < 5 || gy < 5) c = c * 0.8f;
            img[x, y] = c;
        }
        return img;
    }

    // ---------- FACILITY ----------
    public static Img ConcreteWall(int variant, out float[] height)
    {
        const int W = 256, H = 320; var img = new Img(W, H, new C4(0, 0, 0)); height = new float[W * H];
        int s = 300 + variant * 13; float dark = variant == 1 ? 0.85f : 1f;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            float u = (float)x / W, v = (float)y / H;
            float mott = Fbm(u, v, 4, 5, 4, s), fine = Fbm(u, v, 32, 40, 2, s + 1);
            var c = new C4(0.46f, 0.46f, 0.44f) * ((0.82f + mott * 0.3f + (fine - 0.5f) * 0.1f) * dark);
            float hgt = mott * 0.5f + fine * 0.3f;
            if (Hash(x, y, s + 2) < 0.012f) c = c * 0.6f;
            // vertical water streaks, heavier near the top
            float st = Fbm(u, v, 24, 2, 3, s + 3);
            c = c * (1 - Smooth(0.55f, 0.75f, st) * (0.15f + 0.25f * v));
            // formwork seams (wrap edge + every metre)
            if (x < 2 || y % 107 < 2) { c = c * 0.72f; hgt = 0.1f; }
            // painted band at waist height, chipped
            if (y >= 96 && y < 118 && Fbm(u, v, 20, 6, 3, s + 4) < 0.66f) { c = new C4(0.22f, 0.27f, 0.25f) * (0.9f + mott * 0.2f); hgt += 0.1f; }
            // damp line at the floor
            c = c * (1 - (1 - Smooth(0f, 0.12f, v)) * 0.35f);
            img[x, y] = c; height[y * W + x] = hgt;
        }
        // bolt holes with rust running down from them
        foreach (var by in new[] { 214, 107 })
            foreach (var bx in new[] { 32, 128, 224 })
            {
                img.Disc(bx, by + 10, 3.2f, new C4(0.12f, 0.11f, 0.1f), 1f);
                for (int i = 0; i < 50; i++) for (int dx = -2; dx <= 2; dx++) img.Paint(bx + dx, by + 6 - i, new C4(0.45f, 0.24f, 0.1f), 0.35f * (1 - i / 50f) * (1 - Math.Abs(dx) / 3f));
                for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++) if (dx * dx + dy * dy < 12) height[(by + 10 + dy) * W + bx + dx] = 0f;
            }
        if (variant == 1)
        {
            var rnd = new Random(s);
            for (int i = 0; i < 6; i++) Drip(img, rnd.Next(W), H - 1, 100 + rnd.Next(150), new C4(0.2f, 0.18f, 0.15f), 0.4f, rnd);
        }
        return img;
    }

    // concrete floor; alpha = smoothness (wet patches shine in the flashlight)
    public static Img ConcreteFloor(out float[] height)
    {
        const int N = 256; var img = new Img(N, N, new C4(0, 0, 0)); height = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (float)x / N, v = (float)y / N;
            float mott = Fbm(u, v, 4, 4, 4, 40), crack = Ridged(u, v, 5, 3, 141), wet = Smooth(0.52f, 0.62f, Fbm(u, v, 2, 2, 4, 42));
            var c = new C4(0.34f, 0.33f, 0.3f) * (0.8f + mott * 0.35f + (Hash(x, y, 43) - 0.5f) * 0.06f);
            float hgt = mott * 0.4f;
            if (crack > 0.988f && Fbm(u, v, 3, 3, 2, 143) > 0.5f) { c = c * 0.55f; hgt -= 0.15f; }
            if (Fbm(u, v, 5, 5, 3, 44) > 0.7f) c = C4.Lerp(c, new C4(0.2f, 0.16f, 0.1f), 0.35f); // oil
            c = C4.Lerp(c, c * 0.7f, wet);
            img[x, y] = c.WithA(0.15f + wet * 0.65f + Hash(x, y, 45) * 0.05f); height[y * N + x] = hgt;
        }
        return img;
    }

    public static Img Metal(C4 tint, int s, out float[] height)
    {
        const int N = 128; var img = new Img(N, N, new C4(0, 0, 0)); height = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (float)x / N, v = (float)y / N;
            float brush = Hash(0, y, s) * 0.06f + Fbm(u, v, 32, 2, 2, s + 1) * 0.06f, smudge = Fbm(u, v, 4, 4, 3, s + 2);
            var c = tint * (0.88f + brush + (smudge - 0.5f) * 0.2f);
            float hgt = 0.5f + brush;
            if (x < 3 || y < 3 || x > N - 4 || y > N - 4) { c = c * 0.65f; hgt = 0.2f; }
            if (Fbm(u, v, 3, 3, 4, s + 3) > 0.68f) c = C4.Lerp(c, new C4(0.4f, 0.22f, 0.1f), 0.5f); // rust
            img[x, y] = c; height[y * N + x] = hgt;
        }
        var rnd = new Random(s);
        for (int i = 0; i < 14; i++) // scratches
        {
            float x = rnd.Next(N), y = rnd.Next(N), ang = (float)(rnd.NextDouble() * Math.PI); int len = 6 + rnd.Next(18);
            for (int k = 0; k < len; k++) img.Paint((int)(x + Math.Cos(ang) * k), (int)(y + Math.Sin(ang) * k), tint * 1.5f, 0.5f);
        }
        return img;
    }

    public static Img LockerDoor(C4 tint, string number, out float[] height)
    {
        var img = Metal(tint, 600 + number.Length * 3 + (number.Length > 0 ? number[number.Length - 1] : 0), out var mh);
        // resample 128x128 metal into 64x160
        const int W = 64, H = 160; var o = new Img(W, H, new C4(0, 0, 0)); height = new float[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { o[x, y] = img[x * 2 % 128, y * 128 / H]; height[y * W + x] = 0.5f; }
        foreach (var (y0, y1) in new[] { (122, 150), (14, 38) })
            for (int y = y0; y < y1; y += 5)
            {
                o.Rect(12, y, 52, y + 2, new C4(0.03f, 0.03f, 0.03f)); o.Rect(12, y + 2, 52, y + 3, tint * 1.3f, 0.6f);
                for (int x = 12; x < 52; x++) { height[y * W + x] = 0f; height[(y + 2) * W + x] = 0.8f; }
            }
        o.Rect(20, 96, 44, 110, new C4(0.75f, 0.72f, 0.62f)); // number plate
        Text(o, number, 32, 99, 1, new C4(0.1f, 0.1f, 0.1f), center: true);
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (x < 2 || x > W - 3 || y < 2 || y > H - 3) { o[x, y] = o[x, y] * 0.6f; height[y * W + x] = 0.2f; }
        return o;
    }

    // roller shutter slats (HOST doors)
    public static Img Slats(out float[] height)
    {
        const int W = 128, H = 256; var img = new Img(W, H, new C4(0, 0, 0)); height = new float[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            float u = (float)x / W, v = (float)y / H, ph = (y % 16) / 16f;
            float rib = (float)Math.Sin(ph * Math.PI);
            var c = new C4(0.32f, 0.33f, 0.35f) * (0.75f + rib * 0.3f);
            float rust = Fbm(u, v, 4, 8, 4, 50) + (1 - v) * 0.25f;
            c = C4.Lerp(c, new C4(0.38f, 0.2f, 0.09f), Smooth(0.55f, 0.75f, rust) * 0.8f);
            c = c * (1 - Smooth(0.55f, 0.8f, Fbm(u, v, 16, 2, 3, 51)) * 0.25f);
            if (y % 16 == 0) c = c * 0.4f;
            img[x, y] = c; height[y * W + x] = rib;
        }
        return img;
    }

    // corrugated rusty panel (walls THE HOST can open)
    public static Img Corrugated(out float[] height)
    {
        const int W = 128, H = 256; var img = new Img(W, H, new C4(0, 0, 0)); height = new float[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            float u = (float)x / W, v = (float)y / H, rib = 0.5f + 0.5f * (float)Math.Sin(x / 16f * Math.PI * 2);
            var paint = new C4(0.36f, 0.42f, 0.44f); var rust = new C4(0.45f, 0.26f, 0.13f);
            float r = Smooth(0.45f, 0.6f, Fbm(u, v, 3, 6, 5, 60) + (1 - v) * 0.2f);
            var c = C4.Lerp(paint, rust, r) * (0.7f + rib * 0.35f);
            img[x, y] = c; height[y * W + x] = rib;
        }
        return img;
    }

    public static Img Hazard()
    {
        const int N = 128; var img = new Img(N, N, new C4(0, 0, 0));
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (float)x / N, v = (float)y / N;
            bool yellow = ((x + y) / 32) % 2 == 0;
            var c = yellow ? new C4(0.85f, 0.64f, 0.1f) : new C4(0.07f, 0.07f, 0.07f);
            float wear = Fbm(u, v, 6, 6, 4, 70);
            if (wear > 0.64f) c = C4.Lerp(c, new C4(0.4f, 0.4f, 0.4f), Smooth(0.64f, 0.75f, wear));
            c = c * (0.85f + Hash(x, y, 71) * 0.15f);
            img[x, y] = c;
        }
        return img;
    }

    public static Img Cardboard()
    {
        const int N = 128; var img = new Img(N, N, new C4(0, 0, 0));
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (float)x / N, v = (float)y / N;
            var c = new C4(0.55f, 0.42f, 0.26f) * (0.88f + 0.12f * Fbm(u, v, 6, 6, 3, 80) + ((x % 4 == 0) ? -0.03f : 0f));
            if (y > 56 && y < 72) c = new C4(0.62f, 0.52f, 0.34f);
            if (Fbm(u, v, 3, 3, 3, 81) > 0.68f) c = c * 0.75f; // water damage
            img[x, y] = c;
        }
        Text(img, "THIS SIDE UP", 64, 92, 1, new C4(0.25f, 0.17f, 0.1f), center: true);
        Text(img, "^ ^", 64, 26, 2, new C4(0.25f, 0.17f, 0.1f), center: true);
        return img;
    }

    public static Img Paper(int s)
    {
        var img = new Img(64, 80, new C4(0.86f, 0.85f, 0.79f)); var rnd = new Random(s);
        for (int y = 64; y > 8; y -= 5) { int len = 30 + rnd.Next(22); img.Rect(8, y, 8 + len, y + 1, new C4(0.35f, 0.35f, 0.38f), 0.8f); }
        img.Rect(8, 70, 40, 73, new C4(0.2f, 0.2f, 0.22f));
        for (int y = 0; y < 80; y++) for (int x = 0; x < 64; x++) if (Fbm(x / 64f, y / 80f, 3, 3, 3, s) > 0.66f) img.Paint(x, y, new C4(0.6f, 0.5f, 0.3f), 0.4f);
        return img;
    }

    public static Img PanelDiffuser()
    {
        var img = new Img(64, 32, new C4(0.95f, 0.93f, 0.86f));
        for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++)
        {
            if (x % 4 == 0 || y % 4 == 0) img[x, y] = img[x, y] * 0.88f;
            if (x < 2 || y < 2 || x > 61 || y > 29) img[x, y] = new C4(0.3f, 0.3f, 0.3f);
        }
        return img;
    }

    // ---------- decals (RGBA, alpha = coverage) ----------
    static Img Clear(int w, int h) => new Img(w, h, new C4(0, 0, 0, 0));

    public static Img StainDecal(int s)
    {
        var img = Clear(128, 128); var rnd = new Random(s);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float u = x / 128f, v = y / 128f;
            float m = Fbm(u, v, 3, 3, 4, s) + (v - 0.6f) * 1.2f - Math.Abs(u - 0.5f) * 0.9f;
            float a = Smooth(0.25f, 0.45f, m) * 0.5f + Smooth(0.23f, 0.27f, m) * (1 - Smooth(0.29f, 0.33f, m)) * 0.35f;
            img[x, y] = new C4(0.28f, 0.2f, 0.09f, Clamp01(a));
        }
        for (int i = 0; i < 4; i++) Drip(img, 30 + rnd.Next(68), 90, 40 + rnd.Next(80), new C4(0.25f, 0.18f, 0.08f, 1f), 0.45f, rnd);
        return img;
    }

    public static Img MouldDecal(int s)
    {
        var img = Clear(128, 128);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float u = x / 128f, v = y / 128f, r = (float)Math.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
            float cl = Fbm(u, v, 5, 5, 4, s) * (1 - Smooth(0.2f, 0.5f, r));
            float a = Smooth(0.25f, 0.42f, cl) * (0.6f + Hash(x, y, s) * 0.4f);
            img[x, y] = new C4(0.07f, 0.09f, 0.05f, a);
        }
        return img;
    }

    public static Img ScuffDecal(int s)
    {
        var img = Clear(128, 64); var rnd = new Random(s);
        for (int i = 0; i < 9; i++)
        {
            float y = 20 + rnd.Next(24), x = rnd.Next(40); int len = 30 + rnd.Next(60); float slope = ((float)rnd.NextDouble() - 0.5f) * 0.2f;
            for (int k = 0; k < len; k++) img.Disc(x + k, y + k * slope, 1.4f, new C4(0.05f, 0.05f, 0.05f, 1), 0.25f * (float)Math.Sin(k / (float)len * Math.PI));
        }
        return img;
    }

    public static Img PuddleDecal(int s)
    {
        var img = Clear(128, 128);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float u = x / 128f, v = y / 128f, r = (float)Math.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2;
            float m = 1 - r + (Fbm(u, v, 4, 4, 4, s) - 0.5f) * 0.7f;
            img[x, y] = new C4(0.04f, 0.04f, 0.035f, Smooth(0.2f, 0.32f, m) * 0.6f);
        }
        return img;
    }

    public static Img CrackDecal(int s)
    {
        var img = Clear(128, 128);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float u = x / 128f, v = y / 128f, r = (float)Math.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2;
            float c = Ridged(u, v, 3, 4, s);
            img[x, y] = new C4(0.06f, 0.05f, 0.05f, Smooth(0.94f, 0.985f, c) * (1 - Smooth(0.5f, 1f, r)) * 0.9f);
        }
        return img;
    }

    public static Img VentDecal()
    {
        var img = Clear(128, 96);
        img.Rect(0, 0, 128, 96, new C4(0.55f, 0.55f, 0.52f, 1));
        img.Rect(6, 6, 122, 90, new C4(0.06f, 0.06f, 0.06f, 1));
        for (int y = 10; y < 86; y += 8) { img.Rect(8, y, 120, y + 4, new C4(0.45f, 0.45f, 0.43f, 1)); img.Rect(8, y + 4, 120, y + 5, new C4(0.7f, 0.7f, 0.68f, 1)); }
        for (int y = 0; y < 96; y++) for (int x = 0; x < 128; x++) if (Fbm(x / 128f, y / 96f, 4, 3, 3, 90) > 0.65f) img.Paint(x, y, new C4(0.2f, 0.17f, 0.12f, 1), 0.5f);
        return img;
    }

    public static Img OutletDecal()
    {
        var img = Clear(32, 48);
        img.Rect(2, 2, 30, 46, new C4(0.75f, 0.72f, 0.64f, 1));
        img.Rect(10, 26, 13, 34, new C4(0.1f, 0.1f, 0.1f, 1)); img.Rect(19, 26, 22, 34, new C4(0.1f, 0.1f, 0.1f, 1));
        img.Rect(10, 12, 13, 20, new C4(0.1f, 0.1f, 0.1f, 1)); img.Rect(19, 12, 22, 20, new C4(0.1f, 0.1f, 0.1f, 1));
        return img;
    }

    // floor chevron pointing +y (toward the top of the texture)
    public static Img Chevron()
    {
        var img = Clear(64, 64);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float dx = Math.Abs(x - 31.5f), yy = y + dx * 0.9f - 14f;
            if ((yy > 12 && yy < 26) || (yy > 32 && yy < 46)) img[x, y] = new C4(1, 1, 1, dx < 26 ? 0.95f : 0f);
        }
        return img;
    }

    public static Img Dot()
    {
        var img = Clear(32, 32);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float r = (float)Math.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 16f; img[x, y] = new C4(1, 1, 1, (float)Math.Pow(Clamp01(1 - r), 2)); }
        return img;
    }

    // THE HOST's eye: a hot red ring around a dark-red glowing lens with a pinpoint centre; soft glow fading out past the ring.
    // Used as both albedo and emission map, alpha cut to a disc.
    public static Img HostEye()
    {
        const int N = 128; var img = Clear(N, N);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float r = (float)Math.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f)) / 64f;
            float ring = (float)Math.Exp(-Math.Pow((r - 0.68f) / 0.075f, 2));          // the bright ring
            float core = (float)Math.Exp(-Math.Pow((r - 0.68f) / 0.025f, 2));          // white-hot line inside it
            float lens = Smooth(0.6f, 0.0f, r) * 0.35f + 0.12f;                         // dark red lens, brighter toward the middle
            float pin = (float)Math.Exp(-r * r / 0.006f);                               // pinpoint pupil
            float halo = r > 0.68f ? (float)Math.Exp(-(r - 0.68f) / 0.09f) * 0.6f : 0;
            float red = Clamp01(lens + ring + pin + halo), gb = Clamp01(core * 0.75f + pin * 0.55f + ring * 0.12f);
            float a = r < 0.8f ? 1f : Clamp01(halo * 1.6f) * Smooth(1f, 0.85f, r);
            img[x, y] = new C4(red, gb * 0.8f, gb * 0.7f, a);
        }
        return img;
    }

    // vertical gradient for light shafts: bright at the top (v = 1), soft at the sides
    public static Img ShaftGradient()
    {
        var img = Clear(32, 64);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 32; x++) img[x, y] = new C4(1, 1, 1, (float)Math.Pow(y / 63f, 1.6f) * (float)Math.Sin(x / 31f * Math.PI));
        return img;
    }

    // flashlight cookie: hot centre, faint ring, smudges, hard-ish edge (alpha only)
    public static Img FlashCookie()
    {
        const int N = 128; var img = Clear(N, N);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N - 0.5f, v = (y + 0.5f) / N - 0.5f, r = (float)Math.Sqrt(u * u + v * v) * 2f;
            float hot = 1 - Smooth(0.05f, 0.6f, r);                         // broad bright hotspot
            float spill = 1 - Smooth(0.35f, 0.98f, r);                      // wide, dimmer, soft-edged spill
            float a = 0.35f * spill + 0.65f * hot;
            a += 0.04f * Smooth(0.4f, 0.46f, r) * (1 - Smooth(0.5f, 0.58f, r)); // faint reflector ring
            a *= 0.93f + 0.07f * Fbm((float)x / N, (float)y / N, 4, 4, 3, 95);  // lens smudges
            if (r > 0.99f) a = 0;
            img[x, y] = new C4(1, 1, 1, Clamp01(a));
        }
        return img;
    }

    // ---------- text: 5x7 bitmap font ----------
    static readonly Dictionary<char, string[]> FONT = new Dictionary<char, string[]>
    {
        ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
        ['C'] = new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" },
        ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
        ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
        ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
        ['G'] = new[] { ".####", "#....", "#....", "#.###", "#...#", "#...#", ".###." },
        ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
        ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
        ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
        ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
        ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
        ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
        ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
        ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
        ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
        ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
        ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
        ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
        ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
        ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
        ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
        ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
        ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
        ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
        ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
        ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
        ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
        ['6'] = new[] { ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###." },
        ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
        ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
        ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###." },
        ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
        ['>'] = new[] { "#....", ".#...", "..#..", "...#.", "..#..", ".#...", "#...." },
        ['<'] = new[] { "....#", "...#.", "..#..", ".#...", "..#..", "...#.", "....#" },
        ['^'] = new[] { "..#..", ".#.#.", "#...#", ".....", ".....", ".....", "....." },
        ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
        ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "..#.." },
        [':'] = new[] { ".....", "..#..", ".....", ".....", ".....", "..#..", "....." },
        ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
        ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
        ['\''] = new[] { "..#..", "..#..", ".....", ".....", ".....", ".....", "....." },
        ['|'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        ['#'] = new[] { "#####", "#####", "#####", "#####", "#####", "#####", "#####" },
        ['_'] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "#####" },
    };
    public const int GLYPH_ADV = 6, GLYPH_H = 7;
    public static int TextWidth(string t, int scale) => Math.Max(0, t.Length * GLYPH_ADV * scale - scale);

    // crisp text; (x, y) = left (or centre) and BOTTOM of the text
    public static void Text(Img img, string text, int x, int y, int scale, C4 c, bool center = false)
    {
        text = text.ToUpperInvariant(); if (center) x -= TextWidth(text, scale) / 2;
        foreach (var ch in text)
        {
            if (FONT.TryGetValue(ch, out var g))
                for (int gy = 0; gy < 7; gy++) for (int gx = 0; gx < 5; gx++)
                    if (g[gy][gx] == '#') img.Rect(x + gx * scale, y + (6 - gy) * scale, x + (gx + 1) * scale, y + (7 - gy) * scale, c);
            x += GLYPH_ADV * scale;
        }
    }

    // spray-painted text with wobble and drips (decal)
    public static Img Graffiti(string text, C4 c, int s)
    {
        text = text.ToUpperInvariant(); const int sc = 5; var rnd = new Random(s);
        int w = TextWidth(text, sc) + 24, h = 7 * sc + 60; var img = Clear(w, h);
        int x = 12, baseY = 44;
        foreach (var ch in text)
        {
            float wob = ((float)rnd.NextDouble() - 0.5f) * 4f, tilt = ((float)rnd.NextDouble() - 0.5f) * 0.15f;
            if (FONT.TryGetValue(ch, out var g))
                for (int gy = 0; gy < 7; gy++) for (int gx = 0; gx < 5; gx++)
                {
                    if (g[gy][gx] != '#') continue;
                    float px = x + gx * sc + sc / 2f + (6 - gy) * sc * tilt, py = baseY + (6 - gy) * sc + sc / 2f + wob;
                    img.Disc(px, py, sc * 0.75f, c.WithA(1), 0.85f, 0.8f);
                    if (gy == 6 && rnd.NextDouble() < 0.18) for (int k = 0; k < 8 + rnd.Next(30); k++) img.Disc(px, py - sc * 0.5f - k, 1.1f, c.WithA(1), 0.7f * (1 - k / 40f));
                }
            x += GLYPH_ADV * sc;
        }
        for (int i = 0; i < img.px.Length; i++) img.px[i].a *= 0.75f + 0.25f * Hash(i % w, i / w, s); // overspray grain
        return img;
    }

    // worn stencil lettering (decal)
    public static Img Stencil(string text, C4 c, int s)
    {
        const int sc = 6; int w = TextWidth(text, sc) + 24, h = 7 * sc + 24; var img = Clear(w, h);
        Text(img, text, 12, 12, sc, c.WithA(1));
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            var p = img[x, y]; if (p.a <= 0) continue;
            int ci = (x - 12) / (GLYPH_ADV * sc); char ch = ci >= 0 && ci < text.Length ? text[ci] : ' ';
            bool bridge = ch != '-' && ((x - 12) % (GLYPH_ADV * sc)) / sc == 2 && ((y - 12) / sc == 3); // stencil bridges
            p.a = bridge ? 0 : p.a * (0.55f + 0.45f * Smooth(0.3f, 0.6f, Fbm((float)x / w, (float)y / h, 12, 3, 3, s)));
            img[x, y] = p;
        }
        return img;
    }

    // sign plate: dark plate with a border and lettering (opaque; also used as the emission map)
    public static Img Sign(string text, C4 plate, C4 ink, int scale = 4)
    {
        int w = TextWidth(text, scale) + 16 * scale / 2 + 8, h = 7 * scale + 16; var img = new Img(w, h, plate);
        img.Rect(0, 0, w, 2, ink * 0.6f); img.Rect(0, h - 2, w, h, ink * 0.6f); img.Rect(0, 0, 2, h, ink * 0.6f); img.Rect(w - 2, 0, w, h, ink * 0.6f);
        Text(img, text, w / 2, 8, scale, ink, center: true);
        return img;
    }

    // glowing screen with scanlines (multi-line, top to bottom)
    public static Img Screen(string[] lines, C4 ink, int w = 128, int h = 96, int scale = 2)
    {
        var img = new Img(w, h, new C4(0.015f, 0.04f, 0.035f));
        int y = h - 8 - 7 * scale;
        foreach (var l in lines) { Text(img, l, w / 2, y, scale, ink, center: true); y -= 7 * scale + 4; }
        var glow = new C4[img.px.Length]; Array.Copy(img.px, glow, glow.Length);
        for (int yy = 1; yy < h - 1; yy++) for (int xx = 1; xx < w - 1; xx++)
        {
            float r = 0, g = 0, b = 0; for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { var p = glow[(yy + dy) * w + xx + dx]; r += p.r; g += p.g; b += p.b; }
            var o = img[xx, yy]; img[xx, yy] = new C4(Math.Max(o.r, r / 9 * 0.8f), Math.Max(o.g, g / 9 * 0.8f), Math.Max(o.b, b / 9 * 0.8f));
        }
        for (int yy = 0; yy < h; yy += 2) for (int xx = 0; xx < w; xx++) img[xx, yy] = img[xx, yy] * 0.75f;
        return img;
    }
}
