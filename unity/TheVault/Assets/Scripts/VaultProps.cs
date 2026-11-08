// Builders for every object in the vault except THE HOST: doors of each kind, lockers, bottles, terminals,
// monitors, ceiling lights, pipes, signs and clutter. Pure construction; VaultWorld decides where things go.
using UnityEngine;
using UnityEngine.Rendering;
using A = VaultArt;

public static class VaultProps
{
    const float T = VaultGame.TILE, H = VaultGame.WALL_H;

    // ---------- doors ----------
    public class DoorVisual { public GameObject root; public Renderer[] lamps; public Material lampOn, lampOff; public float speed = 1f; }

    // root pivot sits at mid-height (the game slides it between WALL_H/2 = closed and below the floor = open).
    // alongX: the passage runs along world X, so the door panel spans world Z.
    public static DoorVisual Door(Door d, Vector3 tileCenter, bool alongX, Transform moving, Transform statics)
    {
        var dv = new DoorVisual();
        var root = new GameObject(d.id); root.transform.SetParent(moving, false);
        root.transform.position = tileCenter + Vector3.up * (d.closed ? H / 2 : -H / 2 + 0.05f);
        root.transform.rotation = Quaternion.Euler(0, alongX ? 90 : 0, 0);
        var rt = root.transform; var col = root.AddComponent<BoxCollider>();
        var frameRot = Quaternion.Euler(0, alongX ? 90 : 0, 0);
        switch (d.kind)
        {
            case "door": // industrial roller shutter
                A.Cube("slats", rt, A.Slats, Vector3.zero, new Vector3(T * 0.96f, H, 0.1f));
                A.Cube("bottomBar", rt, A.Hazard, new Vector3(0, -H / 2 + 0.07f, 0), new Vector3(T * 0.96f, 0.14f, 0.16f));
                A.Cube("handleA", rt, A.DarkSteel, new Vector3(0, -H / 2 + 0.35f, 0.08f), new Vector3(0.32f, 0.05f, 0.05f));
                A.Cube("handleB", rt, A.DarkSteel, new Vector3(0, -H / 2 + 0.35f, -0.08f), new Vector3(0.32f, 0.05f, 0.05f));
                col.size = new Vector3(T, H, 0.3f);
                dv.lamps = Frame(statics, tileCenter, frameRot, A.DarkSteel, 0.32f);
                dv.lampOn = A.Glow(new Color(1f, 0.1f, 0.05f), 4f); dv.lampOff = A.Glow(new Color(0.35f, 0.05f, 0.02f), 0.3f);
                break;
            case "shutter": // a corrugated wall panel THE HOST can drop
                A.Cube("panel", rt, A.Corrugated, Vector3.zero, new Vector3(T * 0.98f, H, T * 0.98f));
                var la = A.Cube("lampA", rt, A.Glow(new Color(1f, 0.55f, 0.1f), 3f), new Vector3(0, H / 2 - 0.3f, T * 0.495f), new Vector3(0.5f, 0.06f, 0.03f));
                var lb = A.Cube("lampB", rt, A.Glow(new Color(1f, 0.55f, 0.1f), 3f), new Vector3(0, H / 2 - 0.3f, -T * 0.495f), new Vector3(0.5f, 0.06f, 0.03f));
                col.size = new Vector3(T * 0.98f, H, T * 0.98f);
                dv.lamps = new[] { la.GetComponent<Renderer>(), lb.GetComponent<Renderer>() };
                dv.lampOn = A.Glow(new Color(1f, 0.55f, 0.1f), 2.5f); dv.lampOff = A.Glow(new Color(0.3f, 0.15f, 0.03f), 0.2f);
                break;
            case "gate": // escape-room lockdown cage
                for (int i = 0; i < 12; i++) A.Cyl("bar", rt, A.DarkSteel, new Vector3(-T * 0.45f + i * T * 0.9f / 11f, 0, 0), 0.028f, H);
                A.Cube("railLow", rt, A.DarkSteel, new Vector3(0, -H / 2 + 0.45f, 0), new Vector3(T * 0.94f, 0.08f, 0.08f));
                A.Cube("railHigh", rt, A.DarkSteel, new Vector3(0, H / 2 - 0.45f, 0), new Vector3(T * 0.94f, 0.08f, 0.08f));
                var led = A.Cube("led", rt, A.Glow(new Color(0.25f, 0.4f, 1f), 3f), new Vector3(0, H / 2 - 0.15f, 0), new Vector3(T * 0.9f, 0.05f, 0.12f));
                col.size = new Vector3(T, H, 0.25f);
                var fl = Frame(statics, tileCenter, frameRot, A.Hazard, 0.25f);
                dv.lamps = new[] { led.GetComponent<Renderer>(), fl[0], fl[1] };
                dv.lampOn = A.Glow(new Color(1f, 0.08f, 0.05f), 4f); dv.lampOff = A.Glow(new Color(0.25f, 0.4f, 1f), 2f);
                break;
            default: // "blast": central exit bulkhead
                A.Cube("slab", rt, A.DarkSteel, Vector3.zero, new Vector3(T * 0.96f, H, 0.5f));
                A.Cube("bandLow", rt, A.Hazard, new Vector3(0, -H / 2 + 0.55f, 0), new Vector3(T * 0.97f, 0.45f, 0.52f));
                A.Cube("bandHigh", rt, A.Hazard, new Vector3(0, H / 2 - 0.35f, 0), new Vector3(T * 0.97f, 0.3f, 0.52f));
                foreach (float side in new[] { 1f, -1f })
                {
                    A.Part("wheel", rt, Torus, A.Steel, new Vector3(0, 0.15f, side * 0.29f), Vector3.one, Quaternion.Euler(90, 0, 0));
                    A.Cube("spokeA", rt, A.Steel, new Vector3(0, 0.15f, side * 0.29f), new Vector3(0.66f, 0.05f, 0.04f), Quaternion.Euler(0, 0, 45));
                    A.Cube("spokeB", rt, A.Steel, new Vector3(0, 0.15f, side * 0.29f), new Vector3(0.66f, 0.05f, 0.04f), Quaternion.Euler(0, 0, -45));
                    for (int i = 0; i < 6; i++) A.Cyl("bolt", rt, A.Steel, new Vector3(-T * 0.42f + i * T * 0.84f / 5f, -H / 2 + 0.95f, side * 0.26f), 0.04f, 0.04f, Quaternion.Euler(90, 0, 0));
                }
                col.size = new Vector3(T, H, 0.5f);
                dv.lamps = Frame(statics, tileCenter, frameRot, A.Hazard, 0.5f);
                dv.lampOn = A.Glow(new Color(1f, 0.6f, 0.05f), 4f); dv.lampOff = A.Glow(new Color(0.3f, 0.15f, 0.02f), 0.2f);
                dv.speed = 0.35f; // heavy
                break;
        }
        dv.root = root;
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
        return dv;
    }

    // static door frame: two posts, a header housing and a warning lamp on each side
    static Renderer[] Frame(Transform statics, Vector3 c, Quaternion rot, Material postM, float depth)
    {
        var f = new GameObject("frame"); f.transform.SetParent(statics, false); f.transform.position = c; f.transform.rotation = rot;
        A.Cube("postL", f.transform, postM, new Vector3(-T / 2 + 0.06f, H / 2, 0), new Vector3(0.12f, H, depth + 0.08f));
        A.Cube("postR", f.transform, postM, new Vector3(T / 2 - 0.06f, H / 2, 0), new Vector3(0.12f, H, depth + 0.08f));
        A.Cube("header", f.transform, A.DarkSteel, new Vector3(0, H - 0.16f, 0), new Vector3(T, 0.32f, depth + 0.2f));
        var a = A.Cube("lampA", f.transform, A.Glow(Color.red), new Vector3(0, H - 0.4f, depth / 2 + 0.11f), new Vector3(0.2f, 0.08f, 0.06f));
        var b = A.Cube("lampB", f.transform, A.Glow(Color.red), new Vector3(0, H - 0.4f, -depth / 2 - 0.11f), new Vector3(0.2f, 0.08f, 0.06f));
        return new[] { a.GetComponent<Renderer>(), b.GetComponent<Renderer>() };
    }

    static Mesh torus;
    static Mesh Torus
    {
        get
        {
            if (torus != null) return torus;
            var prof = new Vector2[13]; // circle (minor radius 0.04) around major radius 0.33, counter-clockwise = faces outward
            for (int i = 0; i < 13; i++) { float a = -Mathf.PI / 2 + i / 12f * Mathf.PI * 2; prof[i] = new Vector2(0.33f + Mathf.Cos(a) * 0.04f, Mathf.Sin(a) * 0.04f); }
            return torus = A.Lathe(prof, 24);
        }
    }

    // ---------- lockers ----------
    // back against the wall in direction wallDir; returns the root (+Z = front), the door hinge and where the hider stands
    public static Transform Locker(string number, Vector3 tileCenter, Vector3 wallDir, Transform parent, out Transform hinge, out Vector3 inside)
    {
        var root = new GameObject("locker_" + number).transform; root.SetParent(parent, false);
        root.position = tileCenter + wallDir * (T / 2 - 0.3f); root.rotation = Quaternion.LookRotation(-wallDir);
        A.Cube("body", root, A.LockerBody, new Vector3(0, 1.0f, 0), new Vector3(0.85f, 2.0f, 0.55f), collider: true);
        A.Cube("cap", root, A.DarkSteel, new Vector3(0, 2.02f, 0), new Vector3(0.88f, 0.04f, 0.58f));
        foreach (var (fx, fz) in new[] { (-0.38f, -0.22f), (0.38f, -0.22f), (-0.38f, 0.22f), (0.38f, 0.22f) }) A.Cube("foot", root, A.DarkSteel, new Vector3(fx, 0.02f, fz), new Vector3(0.05f, 0.04f, 0.05f));
        hinge = new GameObject("hinge").transform; hinge.SetParent(root, false); hinge.localPosition = new Vector3(-0.42f, 1.0f, 0.285f);
        A.Cube("door", hinge, A.LockerDoor(number), new Vector3(0.41f, 0, 0.0f), new Vector3(0.82f, 1.94f, 0.025f));
        A.Cube("handle", hinge, A.DarkSteel, new Vector3(0.72f, 0.05f, 0.03f), new Vector3(0.04f, 0.18f, 0.04f));
        inside = root.position + Vector3.up * 1f - root.forward * 0.02f;
        return root;
    }

    // ---------- bottle ----------
    static Mesh bottleMesh, labelMesh;
    public static GameObject Bottle(Transform parent)
    {
        if (bottleMesh == null)
        {
            bottleMesh = A.Lathe(new[] { new Vector2(0, 0), new Vector2(0.035f, 0), new Vector2(0.037f, 0.01f), new Vector2(0.037f, 0.15f), new Vector2(0.03f, 0.18f),
                                         new Vector2(0.015f, 0.21f), new Vector2(0.012f, 0.26f), new Vector2(0.015f, 0.265f), new Vector2(0, 0.268f) }, 16);
            labelMesh = A.Lathe(new[] { new Vector2(0.0385f, 0.06f), new Vector2(0.0385f, 0.115f) }, 16);
        }
        var g = new GameObject("bottle"); g.transform.SetParent(parent, false);
        A.Part("glass", g.transform, bottleMesh, A.BottleGlass, Vector3.zero, Vector3.one);
        A.Part("label", g.transform, labelMesh, A.Paper(1), Vector3.zero, Vector3.one);
        A.Part("cap", g.transform, A.Cylinder, A.Glow(new Color(0.8f, 0.7f, 0.3f), 0.4f), new Vector3(0, 0.27f, 0), new Vector3(0.03f, 0.012f, 0.03f));
        return g;
    }

    // ---------- terminals, monitors, cabinets ----------
    // double-sided electrical cabinet (both faces show the screen); returns the two screen renderers
    public static Renderer[] Terminal(Vector3 pos, Transform parent, Material screen)
    {
        var root = new GameObject("terminal").transform; root.SetParent(parent, false); root.position = pos;
        A.Cube("body", root, A.Steel, new Vector3(0, 0.95f, 0), new Vector3(1.0f, 1.9f, 0.55f), collider: true);
        A.Cube("plinth", root, A.DarkSteel, new Vector3(0, 0.05f, 0), new Vector3(1.05f, 0.1f, 0.6f));
        A.Cyl("conduit", root, A.Pipe, new Vector3(0.3f, (1.9f + H) / 2, 0), 0.05f, H - 1.9f);
        var screens = new Renderer[2];
        for (int s = 0; s < 2; s++)
        {
            float z = s == 0 ? 0.28f : -0.28f; var n = s == 0 ? Vector3.forward : Vector3.back;
            A.Cube("bezel", root, A.DarkSteel, new Vector3(0, 1.38f, z), new Vector3(0.66f, 0.5f, 0.02f));
            screens[s] = A.Decal(root, screen, pos + new Vector3(0, 1.38f, z), n, new Vector2(0.58f, 0.42f), 0, 0.012f).GetComponent<Renderer>();
            Color[] bc = { new Color(1, 0.15f, 0.1f), new Color(0.2f, 1, 0.3f), new Color(1, 0.7f, 0.1f) };
            for (int i = 0; i < 3; i++) A.Cyl("button", root, A.Glow(bc[i], 2f), new Vector3(-0.2f + i * 0.2f, 1.0f, z + (s == 0 ? 0.02f : -0.02f)), 0.035f, 0.03f, Quaternion.Euler(90, 0, 0));
            A.Decal(root, A.HazardDecal, pos + new Vector3(0, 0.55f, z), n, new Vector2(0.7f, 0.12f), 0, 0.012f);
        }
        return screens;
    }

    public static Renderer Monitor(Vector3 pos, Vector3 face, Transform parent, Material screen)
    {
        var root = new GameObject("monitor").transform; root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.LookRotation(face);
        A.Cube("crt", root, A.Plastic, new Vector3(0, 0, -0.1f), new Vector3(0.62f, 0.5f, 0.5f));
        A.Cube("bezel", root, A.Plastic, new Vector3(0, 0, 0.16f), new Vector3(0.66f, 0.54f, 0.04f));
        A.Cyl("arm", root, A.DarkSteel, new Vector3(0, (H - pos.y) / 2 + 0.2f, -0.1f), 0.025f, H - pos.y - 0.2f);
        return A.Decal(root, screen, root.TransformPoint(new Vector3(0, 0, 0.18f)), face, new Vector2(0.54f, 0.42f), 0, 0.005f).GetComponent<Renderer>();
    }

    public static void Cabinet(Vector3 wallPoint, Vector3 normal, Transform parent, int seed)
    {
        var root = new GameObject("cabinet").transform; root.SetParent(parent, false);
        root.position = wallPoint + normal * 0.23f; root.rotation = Quaternion.LookRotation(normal);
        A.Cube("body", root, A.Steel, new Vector3(0, 1.0f, 0), new Vector3(1.0f, 2.0f, 0.45f), collider: true);
        A.Cube("seam", root, A.DarkSteel, new Vector3(0, 1.0f, 0.226f), new Vector3(0.015f, 1.9f, 0.01f));
        A.Cube("handle", root, A.DarkSteel, new Vector3(0.08f, 1.05f, 0.24f), new Vector3(0.03f, 0.2f, 0.03f));
        var col = seed % 3 == 0 ? new Color(0.2f, 1f, 0.4f) : seed % 3 == 1 ? new Color(1f, 0.6f, 0.1f) : new Color(0.3f, 0.7f, 1f);
        A.Decal(root, A.Screen(new[] { "SYS", seed % 2 == 0 ? "OK" : "ERR" }, col, 1.2f, 64, 48, 1), root.TransformPoint(new Vector3(-0.25f, 1.5f, 0.226f)), normal, new Vector2(0.3f, 0.22f), 0, 0.004f);
        A.Decal(root, A.Vent, root.TransformPoint(new Vector3(0, 0.35f, 0.226f)), normal, new Vector2(0.6f, 0.3f), 0, 0.004f);
        A.Cyl("conduit", root, A.Pipe, new Vector3(0.35f, (2.0f + H) / 2, -0.05f), 0.04f, H - 2.0f);
    }

    // ---------- ceiling light panel ----------
    public class Panel { public Renderer diffuser; public Light light; public Renderer shaft; public bool on, flicker, broken; public float next; }

    public static Panel CeilingPanel(Vector3 floorPos, Transform parent, bool broken, bool withLight)
    {
        var p = new Panel { broken = broken };
        var root = new GameObject("panel").transform; root.SetParent(parent, false); root.position = floorPos + Vector3.up * H;
        var hang = root;
        if (broken)
        {
            // hangs from one edge on a wire
            hang = new GameObject("hang").transform; hang.SetParent(root, false); hang.localPosition = new Vector3(0.7f, -0.02f, 0);
            hang.localRotation = Quaternion.Euler(0, 0, -28f);
            A.Cyl("wire", root, A.Rubber, new Vector3(-0.3f, -0.25f, 0), 0.006f, 0.5f);
        }
        float off = broken ? -0.7f : 0f;
        A.Cube("frame", hang, A.DarkSteel, new Vector3(off, -0.03f, 0), new Vector3(1.4f, 0.06f, 0.72f));
        p.diffuser = A.Decal(hang, A.PanelOff, hang.TransformPoint(new Vector3(off, -0.061f, 0)), -hang.up, new Vector2(1.3f, 0.62f), 0, 0.001f).GetComponent<Renderer>();
        if (withLight && !broken)
        {
            var lg = new GameObject("light"); lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(0, -0.35f, 0);
            p.light = lg.AddComponent<Light>(); p.light.type = LightType.Point; p.light.range = 9f; p.light.intensity = 1.0f;
            p.light.color = new Color(1f, 0.92f, 0.7f); p.light.shadows = LightShadows.None; p.light.renderMode = LightRenderMode.Auto;
            var sh = A.Part("shaft", root, A.Shaft(new Vector2(1.3f, 0.62f), new Vector2(2.6f, 1.9f), H - 0.1f), A.Additive(Texture2D.whiteTexture, new Color(1f, 0.88f, 0.6f, 0.03f)), new Vector3(0, -0.07f, 0), Vector3.one);
            p.shaft = sh.GetComponent<Renderer>(); p.shaft.shadowCastingMode = ShadowCastingMode.Off;
        }
        return p;
    }

    // ---------- signs ----------
    public static void HangingSign(string text, Color ink, Vector3 pos, float yaw, float height, Transform parent, float glow = 0.9f)
    {
        var mat = A.Sign(text, new Color(0.06f, 0.06f, 0.07f), ink, glow, out float aspect);
        var root = new GameObject("sign_" + text).transform; root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.Euler(0, yaw, 0);
        A.Cube("plate", root, A.DarkSteel, Vector3.zero, new Vector3(height * aspect + 0.04f, height + 0.04f, 0.03f));
        A.Decal(root, mat, root.TransformPoint(new Vector3(0, 0, 0.016f)), root.forward, new Vector2(height * aspect, height), 0, 0.001f);
        A.Decal(root, mat, root.TransformPoint(new Vector3(0, 0, -0.016f)), -root.forward, new Vector2(height * aspect, height), 0, 0.001f);
        float chain = H - pos.y - height / 2;
        if (chain > 0.02f) foreach (float sx in new[] { -0.4f, 0.4f }) A.Cyl("chain", root, A.DarkSteel, new Vector3(sx * height * aspect, height / 2 + chain / 2, 0), 0.008f, chain);
    }

    // ---------- pipes & clutter ----------
    public static void PipeRun(Vector3 a, Vector3 b, float radius, Material m, Transform parent)
    {
        var d = b - a; if (d.sqrMagnitude < 0.0001f) return;
        A.Cyl("pipe", parent, m, (a + b) / 2, radius, d.magnitude, Quaternion.FromToRotation(Vector3.up, d.normalized));
    }

    public static void Box(Vector3 pos, float size, float yaw, Transform parent)
    {
        A.Cube("box", parent, A.Cardboard, pos + Vector3.up * size * 0.4f, new Vector3(size, size * 0.8f, size * 0.9f), Quaternion.Euler(0, yaw, 0), collider: true);
    }

    public static void WetFloorSign(Vector3 pos, float yaw, Transform parent)
    {
        var root = new GameObject("wetfloor").transform; root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.Euler(0, yaw, 0);
        var mat = A.Sign("CAUTION", new Color(0.85f, 0.65f, 0.08f), new Color(0.05f, 0.05f, 0.05f), 0f, out float aspect);
        foreach (float s in new[] { 1f, -1f })
        {
            var leaf = new GameObject("leaf").transform; leaf.SetParent(root, false); leaf.localPosition = new Vector3(0, 0.3f, s * 0.08f); leaf.localRotation = Quaternion.Euler(s * 14f, 0, 0);
            A.Cube("board", leaf, A.Yellow, Vector3.zero, new Vector3(0.3f, 0.62f, 0.015f));
            A.Decal(leaf, mat, leaf.TransformPoint(new Vector3(0, 0.1f, s * 0.009f)), leaf.forward * s, new Vector2(0.26f, 0.26f / aspect), 0, 0.001f);
        }
    }
}
