// Turns TexGen images into textures/materials (cached), plus mesh and decal helpers shared by the world builders.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class VaultArt
{
    static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    static Shader std, add;
    static Shader Std => std != null ? std : (std = Shader.Find("Standard"));
    static Shader Add => add != null ? add : (add = Shader.Find("Vault/Additive") ?? Shader.Find("Legacy Shaders/Particles/Additive"));

    // ---------- textures ----------
    public static Texture2D Tex(Img img, bool linear = false, TextureWrapMode wrap = TextureWrapMode.Repeat, bool mips = true)
    {
        var t = new Texture2D(img.w, img.h, TextureFormat.RGBA32, mips, linear) { wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        var cols = new Color[img.px.Length];
        for (int i = 0; i < cols.Length; i++) { var p = img.px[i]; cols[i] = new Color(Mathf.Clamp01(p.r), Mathf.Clamp01(p.g), Mathf.Clamp01(p.b), Mathf.Clamp01(p.a)); }
        t.SetPixels(cols); t.Apply(mips, true);
        return t;
    }

    public static Texture2D NormalTex(float[] h, int w, int hh, float strength)
    {
        var img = new Img(w, hh, new C4(0.5f, 0.5f, 1f)); var n = TexGen.NormalFromHeight(h, w, hh, strength);
        Array.Copy(n, img.px, n.Length);
        return Tex(img, linear: true);
    }

    static Texture2D T(string key, Func<Texture2D> make) { if (!texCache.TryGetValue(key, out var t)) texCache[key] = t = make(); return t; }
    static Material M(string key, Func<Material> make) { if (!matCache.TryGetValue(key, out var m)) matCache[key] = m = make(); return m; }

    // ---------- material builders ----------
    public static Material Lit(Texture2D albedo, Texture2D normal = null, float smooth = 0.2f, float metal = 0f, Vector2? tiling = null,
                               Color? tint = null, bool smoothFromAlpha = false, float bump = 1f, Texture2D emissionMap = null, Color? emission = null)
    {
        var m = new Material(Std) { mainTexture = albedo, color = tint ?? Color.white };
        m.SetFloat("_Glossiness", smooth); m.SetFloat("_Metallic", metal);
        if (tiling.HasValue) m.mainTextureScale = tiling.Value;
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
        if (smoothFromAlpha) { m.SetFloat("_SmoothnessTextureChannel", 1); m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"); m.SetFloat("_GlossMapScale", smooth); }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value);
            if (emissionMap != null) m.SetTexture("_EmissionMap", emissionMap);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        return m;
    }

    public static Material Plain(Color c, float smooth = 0.2f, float metal = 0f, Color? emission = null)
    {
        var m = new Material(Std) { color = c }; m.SetFloat("_Glossiness", smooth); m.SetFloat("_Metallic", metal);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        return m;
    }

    // lit, alpha-blended (decals, paper, graffiti)
    public static Material Fade(Texture2D tex, Color? tint = null, float smooth = 0.1f, Color? emission = null)
    {
        var m = Lit(tex, smooth: smooth, tint: tint, emissionMap: emission.HasValue ? tex : null, emission: emission);
        m.SetFloat("_Mode", 2); m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        return m;
    }

    public static Material Glass(Color c)
    {
        var m = Plain(c, 0.95f, 0.1f, new Color(c.r, c.g, c.b) * 0.08f);
        m.SetFloat("_Mode", 3); m.SetInt("_SrcBlend", (int)BlendMode.One); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        return m;
    }

    // unlit additive with vertex colours (light shafts, camera cones, dust)
    public static Material Additive(Texture2D tex, Color c) { var m = new Material(Add) { mainTexture = tex, color = c }; return m; }

    // ---------- the game's materials ----------
    public static Material Wallpaper(int v) => M("wp" + v, () => { var img = TexGen.Wallpaper(v, out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 2.5f), 0.12f, bump: 0.35f); });
    public static Material ConcreteWall(int v) => M("cw" + v, () => { var img = TexGen.ConcreteWall(v, out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 4f), 0.18f, bump: 0.9f); });
    public static Material Carpet(Vector2 tiling) => M("carpet", () => { var img = TexGen.Carpet(out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 1.5f), 0.05f, tiling: tiling, bump: 0.5f); });
    public static Material Ceiling(Vector2 tiling) => M("ceiling", () => Lit(Tex(TexGen.Ceiling()), null, 0.05f, tiling: tiling));
    public static Material ConcreteFloor => M("cfloor", () => { var img = TexGen.ConcreteFloor(out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 3f), 0.9f, smoothFromAlpha: true, bump: 0.4f); });
    public static Material Steel => M("steel", () => { var img = TexGen.Metal(new C4(0.36f, 0.38f, 0.4f), 5, out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 2f), 0.45f, 0.3f); });
    public static Material DarkSteel => M("dsteel", () => { var img = TexGen.Metal(new C4(0.16f, 0.17f, 0.18f), 6, out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 2f), 0.4f, 0.35f); });
    public static Material LockerBody => M("lockerbody", () => { var img = TexGen.Metal(new C4(0.33f, 0.4f, 0.36f), 7, out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 2f), 0.35f, 0.25f); });
    public static Material LockerDoor(string num) => M("lockerdoor" + num, () => { var img = TexGen.LockerDoor(new C4(0.33f, 0.4f, 0.36f), num, out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 3f), 0.35f, 0.4f); });
    public static Material Slats => M("slats", () => { var img = TexGen.Slats(out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 4f), 0.35f, 0.5f); });
    public static Material Corrugated => M("corr", () => { var img = TexGen.Corrugated(out var h); return Lit(Tex(img), NormalTex(h, img.w, img.h, 4f), 0.25f, 0.3f); });
    public static Material Hazard => M("hazard", () => Lit(Tex(TexGen.Hazard()), null, 0.3f, 0.3f));
    public static Material Cardboard => M("card", () => Lit(Tex(TexGen.Cardboard()), null, 0.05f));
    public static Material PanelOn => M("panelon", () => { var t = Tex(TexGen.PanelDiffuser()); return Lit(t, null, 0.3f, emissionMap: t, emission: new Color(1.7f, 1.6f, 1.25f)); });
    public static Material PanelOff => M("paneloff", () => Lit(Tex(TexGen.PanelDiffuser()), null, 0.3f, tint: new Color(0.35f, 0.35f, 0.33f)));
    public static Material BottleGlass => M("glass", () => Glass(new Color(0.18f, 0.42f, 0.2f, 0.55f)));
    public static Material Rubber => M("rubber", () => Plain(new Color(0.05f, 0.05f, 0.05f), 0.25f));
    public static Material Pipe => M("pipe", () => Plain(new Color(0.3f, 0.3f, 0.28f), 0.45f, 0.6f));
    public static Material RustPipe => M("rustpipe", () => Plain(new Color(0.35f, 0.22f, 0.12f), 0.25f, 0.4f));
    public static Material Plastic => M("plastic", () => Plain(new Color(0.12f, 0.12f, 0.13f), 0.35f));
    public static Material Yellow => M("yellow", () => Plain(new Color(0.85f, 0.65f, 0.08f), 0.35f));
    public static Material Paper(int v) => M("paper" + v, () => Fade(Tex(TexGen.Paper(v * 13 + 1)), null, 0.05f));

    public static Material Glow(Color c, float k = 2f) => M($"glow{c}{k}", () => Plain(c * 0.6f, 0.4f, 0f, c * k));

    // decals
    public static Material Stain(int v) => M("stain" + v, () => Fade(Tex(TexGen.StainDecal(10 + v), wrap: TextureWrapMode.Clamp)));
    public static Material Mould(int v) => M("mould" + v, () => Fade(Tex(TexGen.MouldDecal(20 + v), wrap: TextureWrapMode.Clamp)));
    public static Material Scuff => M("scuff", () => Fade(Tex(TexGen.ScuffDecal(5), wrap: TextureWrapMode.Clamp)));
    public static Material Puddle(int v) => M("puddle" + v, () => Fade(Tex(TexGen.PuddleDecal(30 + v), wrap: TextureWrapMode.Clamp), null, 0.97f));
    public static Material Crack(int v) => M("crack" + v, () => Fade(Tex(TexGen.CrackDecal(40 + v), wrap: TextureWrapMode.Clamp)));
    public static Material Vent => M("vent", () => Fade(Tex(TexGen.VentDecal(), wrap: TextureWrapMode.Clamp), null, 0.3f));
    public static Material Outlet => M("outlet", () => Fade(Tex(TexGen.OutletDecal(), wrap: TextureWrapMode.Clamp), null, 0.3f));
    public static Material HazardDecal => M("hazdecal", () => Fade(Tex(TexGen.Hazard()), new Color(1, 1, 1, 0.85f), 0.25f));
    public static Material ChevronDim => M("chevdim", () => Fade(Tex(TexGen.Chevron(), wrap: TextureWrapMode.Clamp), new Color(0.1f, 0.35f, 0.15f, 0.7f), 0.2f));
    public static Material ChevronLit => M("chevlit", () => Fade(Tex(TexGen.Chevron(), wrap: TextureWrapMode.Clamp), new Color(0.2f, 1f, 0.35f, 0.95f), 0.2f, new Color(0.3f, 2.2f, 0.6f)));

    public static Texture2D DotTex => T("dot", () => Tex(TexGen.Dot(), wrap: TextureWrapMode.Clamp));
    public static Texture2D ShaftTex => T("shaft", () => Tex(TexGen.ShaftGradient(), wrap: TextureWrapMode.Clamp));
    public static Texture2D HostEyeTex => T("hosteye", () => Tex(TexGen.HostEye(), wrap: TextureWrapMode.Clamp));
    public static Texture2D Cookie => T("cookie", () => Tex(TexGen.FlashCookie(), wrap: TextureWrapMode.Clamp, mips: false));
    // a dim, warm environment to reflect instead of Unity's default blue sky
    public static Cubemap DarkReflection
    {
        get
        {
            var cm = new Cubemap(16, TextureFormat.RGBA32, false); var col = new Color[16 * 16];
            foreach (CubemapFace f in System.Enum.GetValues(typeof(CubemapFace)))
            {
                if (f == CubemapFace.Unknown) continue;
                var c = f == CubemapFace.PositiveY ? new Color(0.09f, 0.085f, 0.07f) : f == CubemapFace.NegativeY ? new Color(0.02f, 0.018f, 0.015f) : new Color(0.05f, 0.045f, 0.035f);
                for (int i = 0; i < col.Length; i++) col[i] = c;
                cm.SetPixels(col, f);
            }
            cm.Apply(); return cm;
        }
    }

    // text materials: size comes back so quads keep the right aspect ratio
    public static Material Graffiti(string text, Color c, int seed, out float aspect)
    {
        var img = TexGen.Graffiti(text, new C4(c.r, c.g, c.b), seed); aspect = (float)img.w / img.h;
        return Fade(Tex(img, wrap: TextureWrapMode.Clamp), null, 0.15f);
    }
    public static Material Stencil(string text, Color c, int seed, out float aspect)
    {
        var img = TexGen.Stencil(text, new C4(c.r, c.g, c.b), seed); aspect = (float)img.w / img.h;
        return Fade(Tex(img, wrap: TextureWrapMode.Clamp), null, 0.15f);
    }
    public static Material Sign(string text, Color plate, Color ink, float glow, out float aspect)
    {
        var img = TexGen.Sign(text, new C4(plate.r, plate.g, plate.b), new C4(ink.r, ink.g, ink.b)); aspect = (float)img.w / img.h;
        var t = Tex(img, wrap: TextureWrapMode.Clamp);
        return Lit(t, null, 0.4f, emissionMap: t, emission: Color.white * glow);
    }
    public static Material Screen(string[] lines, Color ink, float glow = 1.6f, int w = 128, int h = 96, int scale = 2)
    {
        var t = Tex(TexGen.Screen(lines, new C4(ink.r, ink.g, ink.b), w, h, scale), wrap: TextureWrapMode.Clamp);
        return Lit(t, null, 0.85f, emissionMap: t, emission: Color.white * glow);
    }

    // ---------- meshes ----------
    static Mesh block, quad;
    // unit cube with upright UVs on all four sides (so wallpaper is never sideways) and tangents for normal maps
    public static Mesh Block
    {
        get
        {
            if (block != null) return block;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            void Face(Vector3 c, Vector3 right, Vector3 up)
            {
                int i = v.Count; Vector3 nn = Vector3.Cross(right, up).normalized * -1f;
                v.Add(c - right - up); v.Add(c + right - up); v.Add(c + right + up); v.Add(c - right + up);
                for (int k = 0; k < 4; k++) n.Add(nn);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            float hh = 0.5f;
            Face(new Vector3(0, 0, -hh), Vector3.right * hh, Vector3.up * hh);    // -Z
            Face(new Vector3(0, 0, hh), Vector3.left * hh, Vector3.up * hh);      // +Z
            Face(new Vector3(hh, 0, 0), Vector3.forward * hh, Vector3.up * hh);   // +X
            Face(new Vector3(-hh, 0, 0), Vector3.back * hh, Vector3.up * hh);     // -X
            Face(new Vector3(0, hh, 0), Vector3.right * hh, Vector3.forward * hh); // top
            Face(new Vector3(0, -hh, 0), Vector3.right * hh, Vector3.back * hh);  // bottom
            block = new Mesh { name = "VaultBlock" }; block.SetVertices(v); block.SetNormals(n); block.SetUVs(0, uv); block.SetTriangles(tri, 0);
            block.RecalculateTangents(); block.RecalculateBounds();
            return block;
        }
    }

    public static Mesh Quad
    {
        get
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "VaultQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateTangents(); quad.RecalculateBounds();
            return quad;
        }
    }

    // surface of revolution around +Y; profile = (radius, height) from bottom to top
    public static Mesh Lathe(Vector2[] profile, int seg = 16)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int i = 0; i < profile.Length; i++)
            for (int s = 0; s <= seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2;
                v.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x)); uv.Add(new Vector2(s / (float)seg, i / (float)(profile.Length - 1)));
            }
        for (int i = 0; i < profile.Length - 1; i++)
            for (int s = 0; s < seg; s++)
            {
                int a = i * (seg + 1) + s, b = a + seg + 1;
                tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        var m = new Mesh { name = "Lathe" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    // open frustum along -Y from a top rectangle to a bigger bottom rectangle; vertex alpha fades downward (light shafts)
    public static Mesh Shaft(Vector2 top, Vector2 bottom, float height)
    {
        var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var tri = new List<int>();
        Vector2[] dirs = { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
        for (int k = 0; k < 4; k++)
        {
            var d0 = dirs[k]; var d1 = dirs[(k + 1) % 4]; int i = v.Count;
            v.Add(new Vector3(d0.x * top.x / 2, 0, d0.y * top.y / 2)); v.Add(new Vector3(d1.x * top.x / 2, 0, d1.y * top.y / 2));
            v.Add(new Vector3(d1.x * bottom.x / 2, -height, d1.y * bottom.y / 2)); v.Add(new Vector3(d0.x * bottom.x / 2, -height, d0.y * bottom.y / 2));
            c.Add(Color.white); c.Add(Color.white); c.Add(new Color(1, 1, 1, 0)); c.Add(new Color(1, 1, 1, 0));
            uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 0));
            tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        var m = new Mesh { name = "Shaft" }; m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
        return m;
    }

    // open cone along +Z with the apex at the origin; vertex alpha fades toward the far end (camera beams)
    public static Mesh Cone(float length, float radius, int seg = 20)
    {
        var v = new List<Vector3> { Vector3.zero }; var c = new List<Color> { Color.white }; var uv = new List<Vector2> { new Vector2(0.5f, 1) }; var tri = new List<int>();
        for (int s = 0; s <= seg; s++)
        {
            float a = s / (float)seg * Mathf.PI * 2;
            v.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, length)); c.Add(new Color(1, 1, 1, 0)); uv.Add(new Vector2(s / (float)seg, 0));
            if (s > 0) tri.AddRange(new[] { 0, s, s + 1 });
        }
        var m = new Mesh { name = "Cone" }; m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
        return m;
    }

    // ---------- object helpers ----------
    public static GameObject Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Vector3 scale, Quaternion? rot = null, bool collider = false)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos; g.transform.localRotation = rot ?? Quaternion.identity; g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) g.AddComponent<BoxCollider>();
        return g;
    }
    public static GameObject Cube(string name, Transform parent, Material mat, Vector3 localPos, Vector3 size, Quaternion? rot = null, bool collider = false)
        => Part(name, parent, Block, mat, localPos, size, rot, collider);

    static Mesh ball;
    // unit-diameter sphere (scale it into eggs, pads and joints)
    public static Mesh Ball
    {
        get
        {
            if (ball != null) return ball;
            var prof = new Vector2[13];
            for (int i = 0; i < prof.Length; i++) { float t = i / (float)(prof.Length - 1) * Mathf.PI; prof[i] = new Vector2(Mathf.Sin(t) * 0.5f, -Mathf.Cos(t) * 0.5f); }
            return ball = Lathe(prof, 20);
        }
    }
    static Mesh cyl;
    public static Mesh Cylinder => cyl != null ? cyl : (cyl = Lathe(new[] { new Vector2(0, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f) }, 14));
    public static GameObject Cyl(string name, Transform parent, Material mat, Vector3 localPos, float radius, float length, Quaternion? rot = null)
        => Part(name, parent, Cylinder, mat, localPos, new Vector3(radius * 2, length, radius * 2), rot);

    // flat decal facing `normal` (pushed slightly off the surface to avoid z-fighting); roll in degrees
    public static GameObject Decal(Transform parent, Material mat, Vector3 pos, Vector3 normal, Vector2 size, float roll = 0f, float lift = 0.012f)
    {
        var g = new GameObject("Decal"); g.transform.SetParent(parent, false);
        g.transform.position = pos + normal * lift;
        g.transform.rotation = Quaternion.LookRotation(-normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up) * Quaternion.Euler(0, 0, roll);
        g.transform.localScale = new Vector3(size.x, size.y, 1);
        g.AddComponent<MeshFilter>().sharedMesh = Quad; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }
}
