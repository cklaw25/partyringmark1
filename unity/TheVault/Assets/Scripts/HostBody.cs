// THE HOST's body: a tall glossy-black robot with big red ring eyes (headphone-like ear pods, jaw grille, exposed neck
// rods and spine), built from code like every other model. Animated procedurally: a stalking walk tied to its real
// speed, arms reaching when it chases, the head tracking you in a chase and scanning otherwise, eyes flaring.
using UnityEngine;
using UnityEngine.Rendering;
using A = VaultArt;

public class HostBody : MonoBehaviour
{
    BrokerAgent agent; FirstPersonPlayer player;
    Transform body, neck, head, jaw, hipL, hipR, kneeL, kneeR, shL, shR, elL, elR;
    Material eyeMat, haloMat, coreMat; Light eyeLight;
    Vector3 prevPos; float speed, phase, headYaw, headPitch, headRoll, scanT, flare;

    const float HIP_Y = 0.92f, LEG = 0.44f;

    public static HostBody Build(Transform parent)
    {
        var g = new GameObject("THE_HOST"); g.transform.SetParent(parent, false);
        var hb = g.AddComponent<HostBody>(); hb.Construct();
        g.transform.localScale = Vector3.one * 0.92f; // ~2.1 m to the top of the head
        return hb;
    }

    public void Init(BrokerAgent a, FirstPersonPlayer p) { agent = a; player = p; prevPos = transform.position; }

    static Transform Pivot(string n, Transform parent, Vector3 pos)
    {
        var t = new GameObject(n).transform; t.SetParent(parent, false); t.localPosition = pos; return t;
    }

    void Construct()
    {
        var shell = A.Plain(new Color(0.025f, 0.025f, 0.03f), 0.9f, 0.55f);        // glossy black armour
        var joint = A.DarkSteel; var rubber = A.Rubber;
        var socket = A.Plain(new Color(0.01f, 0.01f, 0.012f), 0.95f, 0.2f);
        var redDim = A.Plain(new Color(0.2f, 0f, 0f), 0.5f, 0f, new Color(0.9f, 0.05f, 0.02f));
        coreMat = A.Plain(new Color(0.3f, 0f, 0f), 0.5f, 0f, new Color(1.6f, 0.08f, 0.04f));

        body = Pivot("body", transform, new Vector3(0, HIP_Y, 0)); // everything above the hips moves with this (bob + lean)

        // ---- pelvis + exposed spine ----
        A.Part("pelvis", body, A.Ball, shell, new Vector3(0, 0.03f, 0), new Vector3(0.34f, 0.16f, 0.22f));
        for (int i = 0; i < 3; i++) A.Cyl("spine" + i, body, joint, new Vector3((i - 1) * 0.05f, 0.14f, -0.02f), 0.018f, 0.2f);
        A.Cyl("spineCable", body, rubber, new Vector3(0.09f, 0.14f, 0.03f), 0.012f, 0.22f, Quaternion.Euler(0, 0, 8));

        // ---- torso: tapered chest, flattened front-to-back, with a lit gap down the middle ----
        var chest = A.Lathe(new[] { new Vector2(0.0f, 0f), new Vector2(0.15f, 0f), new Vector2(0.19f, 0.12f), new Vector2(0.25f, 0.36f), new Vector2(0.27f, 0.48f), new Vector2(0.22f, 0.6f), new Vector2(0.1f, 0.66f), new Vector2(0f, 0.67f) }, 20);
        A.Part("torso", body, chest, shell, new Vector3(0, 0.2f, 0), new Vector3(1f, 1f, 0.62f));
        A.Cube("chestGap", body, socket, new Vector3(0, 0.58f, 0.15f), new Vector3(0.05f, 0.3f, 0.04f));
        A.Cube("chestCore", body, coreMat, new Vector3(0, 0.58f, 0.168f), new Vector3(0.018f, 0.24f, 0.01f));
        for (int i = 0; i < 4; i++) A.Cube("rib" + i, body, joint, new Vector3(0, 0.36f + i * 0.045f, 0.1f), new Vector3(0.2f - i * 0.01f, 0.012f, 0.06f));
        A.Cube("collar", body, joint, new Vector3(0, 0.82f, 0), new Vector3(0.22f, 0.04f, 0.14f));

        // ---- neck: bundle of rods + cables ----
        neck = Pivot("neck", body, new Vector3(0, 0.85f, 0));
        for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2 + 0.4f; A.Cyl("neckRod" + i, neck, joint, new Vector3(Mathf.Cos(a) * 0.035f, 0.06f, Mathf.Sin(a) * 0.03f), 0.012f, 0.13f); }
        A.Cyl("neckCable", neck, rubber, new Vector3(-0.04f, 0.06f, -0.03f), 0.014f, 0.14f, Quaternion.Euler(0, 0, -10));
        A.Cyl("neckCore", neck, socket, new Vector3(0, 0.06f, 0), 0.025f, 0.14f);

        // ---- head (the reference's big glossy helmet with ring eyes) ----
        head = Pivot("head", neck, new Vector3(0, 0.12f, 0)); head.localScale = Vector3.one * 1.15f; // big helmet head, like the reference
        var hc = new Vector3(0, 0.2f, 0.01f); // head centre, relative to the head pivot (the neck joint)
        A.Part("skull", head, A.Ball, shell, hc, new Vector3(0.36f, 0.42f, 0.38f));
        A.Part("brow", head, A.Ball, shell, hc + new Vector3(0, 0.075f, 0.1f), new Vector3(0.33f, 0.09f, 0.2f));             // visor ridge over the eyes
        A.Part("faceplate", head, A.Ball, shell, hc + new Vector3(0, -0.04f, 0.07f), new Vector3(0.3f, 0.3f, 0.27f));
        A.Part("seam", head, A.Ball, redDim, hc + new Vector3(0, 0.0f, 0), new Vector3(0.362f, 0.01f, 0.382f));              // faint red seam round the helmet
        A.Part("nose", head, A.Ball, joint, hc + new Vector3(0, -0.04f, 0.19f), new Vector3(0.03f, 0.05f, 0.03f));
        // ear pods
        foreach (float sx in new[] { -1f, 1f })
        {
            A.Cyl("earPod", head, shell, hc + new Vector3(sx * 0.18f, 0.0f, -0.01f), 0.085f, 0.07f, Quaternion.Euler(0, 0, 90));
            A.Cyl("earRing", head, joint, hc + new Vector3(sx * 0.218f, 0.0f, -0.01f), 0.06f, 0.012f, Quaternion.Euler(0, 0, 90));
            A.Cyl("earLed", head, redDim, hc + new Vector3(sx * 0.226f, 0.0f, -0.01f), 0.018f, 0.006f, Quaternion.Euler(0, 0, 90));
        }
        // eyes: dark glass sockets, a glowing ring texture, and an additive halo so they read through fog
        eyeMat = A.Fade(A.HostEyeTex, Color.white, 0.6f, new Color(4f, 0.35f, 0.25f));
        haloMat = A.Additive(A.DotTex, new Color(1f, 0.08f, 0.04f, 0.5f));
        foreach (float sx in new[] { -1f, 1f })
        {
            var eyeRot = Quaternion.Euler(0, -sx * 14f, 0);
            var ep = hc + new Vector3(sx * 0.085f, 0.02f, 0.175f);
            A.Cyl("socket", head, socket, ep - eyeRot * Vector3.forward * 0.005f, 0.075f, 0.03f, eyeRot * Quaternion.Euler(90, 0, 0));
            var eye = A.Part("eye", head, A.Quad, eyeMat, ep + eyeRot * Vector3.forward * 0.013f, new Vector3(0.14f, 0.14f, 1f), eyeRot * Quaternion.Euler(0, 180, 0));
            eye.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var halo = A.Part("halo", head, A.Quad, haloMat, ep + eyeRot * Vector3.forward * 0.03f, new Vector3(0.34f, 0.34f, 1f), eyeRot * Quaternion.Euler(0, 180, 0));
            halo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        // jaw with a grille, hinged so it can hang open
        jaw = Pivot("jaw", head, hc + new Vector3(0, -0.1f, 0.02f));
        A.Part("jawShell", jaw, A.Ball, shell, new Vector3(0, -0.06f, 0.08f), new Vector3(0.22f, 0.11f, 0.17f));
        for (int i = 0; i < 4; i++) A.Cube("grille" + i, jaw, joint, new Vector3(0, -0.035f - i * 0.022f, 0.16f - i * 0.006f), new Vector3(0.13f - i * 0.015f, 0.008f, 0.02f));
        A.Cube("jawHinge", jaw, joint, new Vector3(0, -0.02f, 0.0f), new Vector3(0.24f, 0.03f, 0.03f));

        eyeLight = new GameObject("eyeLight").AddComponent<Light>(); eyeLight.transform.SetParent(head, false);
        eyeLight.transform.localPosition = hc + new Vector3(0, 0.05f, 0.8f); // ahead of the face, so it lights the world, not its own nose
        eyeLight.type = LightType.Point; eyeLight.range = 6; eyeLight.intensity = 2.5f; eyeLight.color = new Color(1f, 0.08f, 0.04f);

        // ---- arms ----
        foreach (float sx in new[] { -1f, 1f })
        {
            var sh = Pivot(sx < 0 ? "shoulderL" : "shoulderR", body, new Vector3(sx * 0.31f, 0.74f, 0));
            A.Part("pad", sh, A.Ball, shell, new Vector3(sx * 0.02f, 0.03f, 0), new Vector3(0.2f, 0.15f, 0.2f));
            A.Part("shJoint", sh, A.Ball, joint, Vector3.zero, Vector3.one * 0.1f);
            A.Cyl("upper", sh, joint, new Vector3(0, -0.17f, 0), 0.032f, 0.3f);
            A.Part("upperArmour", sh, A.Ball, shell, new Vector3(0, -0.15f, 0), new Vector3(0.1f, 0.24f, 0.1f));
            var el = Pivot("elbow", sh, new Vector3(0, -0.33f, 0));
            A.Part("elJoint", el, A.Ball, joint, Vector3.zero, Vector3.one * 0.08f);
            A.Cyl("fore", el, joint, new Vector3(0, -0.15f, 0), 0.026f, 0.28f);
            A.Part("foreArmour", el, A.Ball, shell, new Vector3(0, -0.13f, 0.005f), new Vector3(0.085f, 0.24f, 0.09f));
            A.Cyl("foreCable", el, rubber, new Vector3(sx * 0.035f, -0.14f, -0.03f), 0.008f, 0.26f);
            var hand = Pivot("hand", el, new Vector3(0, -0.31f, 0));
            A.Cube("palm", hand, shell, new Vector3(0, -0.03f, 0), new Vector3(0.06f, 0.07f, 0.08f));
            for (int f = 0; f < 3; f++) A.Cube("finger" + f, hand, joint, new Vector3(0, -0.1f, (f - 1) * 0.026f), new Vector3(0.016f, 0.09f, 0.016f), Quaternion.Euler(0, 0, sx * -12f));
            A.Cube("thumb", hand, joint, new Vector3(-sx * 0.03f, -0.07f, 0.03f), new Vector3(0.015f, 0.06f, 0.015f), Quaternion.Euler(20, 0, sx * 25f));
            if (sx < 0) { shL = sh; elL = el; } else { shR = sh; elR = el; }
        }

        // ---- legs (children of the root, not the bobbing body, so the feet stay planted) ----
        foreach (float sx in new[] { -1f, 1f })
        {
            var hip = Pivot(sx < 0 ? "hipL" : "hipR", transform, new Vector3(sx * 0.12f, HIP_Y, 0));
            A.Part("hipJoint", hip, A.Ball, joint, Vector3.zero, Vector3.one * 0.11f);
            A.Cyl("thighRod", hip, joint, new Vector3(0, -LEG / 2, 0), 0.035f, LEG);
            A.Part("thighArmour", hip, A.Ball, shell, new Vector3(0, -LEG * 0.42f, 0.01f), new Vector3(0.13f, LEG * 0.75f, 0.14f));
            var knee = Pivot("knee", hip, new Vector3(0, -LEG, 0));
            A.Part("kneeJoint", knee, A.Ball, joint, Vector3.zero, Vector3.one * 0.09f);
            A.Part("kneeCap", knee, A.Ball, shell, new Vector3(0, 0.01f, 0.045f), new Vector3(0.08f, 0.09f, 0.05f));
            A.Cyl("shinRod", knee, joint, new Vector3(0, -LEG / 2, 0), 0.028f, LEG);
            A.Part("shinArmour", knee, A.Ball, shell, new Vector3(0, -LEG * 0.4f, 0.02f), new Vector3(0.1f, LEG * 0.72f, 0.11f));
            A.Cyl("shinCable", knee, rubber, new Vector3(sx * 0.04f, -LEG * 0.5f, -0.03f), 0.008f, LEG * 0.8f);
            A.Cube("foot", knee, shell, new Vector3(0, -LEG + 0.02f, 0.05f), new Vector3(0.11f, 0.05f, 0.24f));
            A.Cube("toe", knee, joint, new Vector3(0, -LEG + 0.015f, 0.18f), new Vector3(0.1f, 0.03f, 0.04f));
            if (sx < 0) { hipL = hip; kneeL = knee; } else { hipR = hip; kneeR = knee; }
        }
        foreach (var c in GetComponentsInChildren<Collider>()) Destroy(c);
    }

    void Update()
    {
        if (agent == null) return;
        float dt = Mathf.Max(1e-4f, Time.deltaTime);
        var p = transform.position; var d = p - prevPos; d.y = 0; prevPos = p;
        float inst = d.magnitude / dt; if (inst > 20f) inst = 0; // teleports (new day) aren't walking
        speed = Mathf.Lerp(speed, inst, 1f - Mathf.Exp(-dt * 8f));
        string st = agent.state; bool chase = st == "CHASE";

        // ---- walk cycle: stride length grows a little with speed ----
        float amp = Mathf.Clamp01(speed / 3.5f);
        phase += speed * dt / Mathf.Lerp(0.9f, 1.4f, Mathf.Clamp01(speed / 7f)) * Mathf.PI;
        float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
        float swing = 34f * amp;
        hipL.localRotation = Quaternion.Euler(s * swing, 0, 0); hipR.localRotation = Quaternion.Euler(-s * swing, 0, 0);
        kneeL.localRotation = Quaternion.Euler(Mathf.Max(0, c) * 55f * amp + 4f, 0, 0); kneeR.localRotation = Quaternion.Euler(Mathf.Max(0, -c) * 55f * amp + 4f, 0, 0);
        float breathe = Mathf.Sin(Time.time * 1.3f);
        float lean = Mathf.Lerp(2f, chase ? 16f : 9f, amp);
        body.localPosition = new Vector3(0, HIP_Y - Mathf.Abs(c) * 0.035f * amp + breathe * 0.006f, 0);
        body.localRotation = Quaternion.Euler(lean, 0, -s * 3f * amp);
        // arms: swing opposite the legs; in a chase they come up and forward, reaching
        float reach = chase ? 1f : 0f;
        float armSwing = 28f * amp * (1f - reach * 0.6f);
        shL.localRotation = Quaternion.Euler(-s * armSwing - reach * 55f, 0, -6f); shR.localRotation = Quaternion.Euler(s * armSwing - reach * 55f, 0, 6f);
        elL.localRotation = Quaternion.Euler(-(18f + reach * 25f + Mathf.Max(0, s) * 15f * amp), 0, 0);
        elR.localRotation = Quaternion.Euler(-(18f + reach * 25f + Mathf.Max(0, -s) * 15f * amp), 0, 0);

        // ---- head: locks onto you in a chase, scans while hunting, tilts while it listens ----
        float wantYaw = 0, wantPitch = 0, wantRoll = 0;
        if (chase && player != null)
        {
            var to = player.cam.transform.position - head.position;
            var local = Quaternion.Inverse(transform.rotation) * to;
            wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
            wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg - lean, -35f, 35f);
        }
        else
        {
            scanT += dt * (st == "SEARCH" ? 1.6f : st == "INVESTIGATE" ? 1.1f : 0.5f);
            wantYaw = Mathf.Sin(scanT) * (st == "PATROL" ? 35f : 55f) + Mathf.Sin(scanT * 2.7f) * 6f;
            wantPitch = 6f - lean * 0.6f;
            if (st == "INVESTIGATE") wantRoll = 16f * Mathf.Sin(scanT * 0.5f);
        }
        float hk = 1f - Mathf.Exp(-dt * (chase ? 10f : 3f));
        headYaw = Mathf.Lerp(headYaw, wantYaw, hk); headPitch = Mathf.Lerp(headPitch, wantPitch, hk); headRoll = Mathf.Lerp(headRoll, wantRoll, hk);
        neck.localRotation = Quaternion.Euler(headPitch * 0.3f, headYaw * 0.3f, 0);
        head.localRotation = Quaternion.Euler(headPitch * 0.7f, headYaw * 0.7f, headRoll);
        jaw.localRotation = Quaternion.Euler(chase ? 14f + Mathf.PerlinNoise(Time.time * 9f, 1f) * 10f : 2f + breathe * 1.5f, 0, 0);

        // ---- eyes: steady glow, flaring and flickering in a chase ----
        flare = Mathf.MoveTowards(flare, chase ? 1f : st == "PATROL" ? 0f : 0.4f, dt * 2f);
        float flick = chase ? (Mathf.PerlinNoise(Time.time * 14f, 0.5f) > 0.85f ? 0.5f : 1f) : 1f;
        float e = (1f + flare * 1.2f) * flick;
        eyeMat.SetColor("_EmissionColor", new Color(4f, 0.35f, 0.25f) * e);
        haloMat.color = new Color(1f, 0.08f, 0.04f, (0.45f + 0.35f * flare) * flick);
        coreMat.SetColor("_EmissionColor", new Color(1.6f, 0.08f, 0.04f) * (0.7f + 0.3f * Mathf.Sin(Time.time * (chase ? 9f : 2f)) + flare * 0.8f));
        eyeLight.intensity = (2.5f + 2f * flare) * flick; eyeLight.range = 6f + 2f * flare;
    }
}
