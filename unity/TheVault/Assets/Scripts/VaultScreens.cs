// Full-screen flow and menus: intro title -> start screen -> wake-up (eyelids, blur, opening text) -> play -> post-mortem.
// Menus are laid out for a 1280x720 reference and scaled to the real screen (fonts are sized directly, so text stays sharp).
using System.Linq;
using UnityEngine;

public partial class VaultGame
{
    // ---------- flow state ----------
    static bool introSeen; // the intro plays once per launch, not again on "Load seed" / "Play again"
    bool introDone = introSeen; float introStart = -1, startFadeAt = -9;
    const float INTRO_LEN = 5.2f, WAKE_LEN = 2.6f, MOVE_AT = 1.4f, WAKE_TEXT_END = 9f;
    float wakeStart = -99; bool wakeActive, welcomePending;
    string[] wakeLines = new string[0];

    // ---------- scaling + styles ----------
    float MenuScale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.6f, 3f);
    int Ms(float px) => Mathf.Max(9, Mathf.RoundToInt(px * MenuScale));
    GUIStyle btnStyle, fieldStyle; float stylesScale = -1;
    Texture2D lidTex;
    static Texture2D Solid(Color c) { var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; t.SetPixel(0, 0, c); t.Apply(); return t; }

    GUIStyle Btn(int size = 20)
    {
        if (btnStyle == null || stylesScale != MenuScale)
        {
            stylesScale = MenuScale;
            btnStyle = new GUIStyle(GUI.skin.button) { richText = true, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
            btnStyle.padding = new RectOffset(Ms(14), Ms(14), Ms(8), Ms(8));
            btnStyle.normal.background = Solid(new Color(0.13f, 0.07f, 0.07f, 0.95f)); btnStyle.normal.textColor = new Color(0.95f, 0.85f, 0.82f);
            btnStyle.hover.background = Solid(new Color(0.38f, 0.08f, 0.07f)); btnStyle.hover.textColor = Color.white;
            btnStyle.active.background = Solid(new Color(0.6f, 0.1f, 0.08f)); btnStyle.active.textColor = Color.white;
            btnStyle.focused = btnStyle.normal;
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = Ms(19), alignment = TextAnchor.MiddleLeft, padding = new RectOffset(Ms(10), Ms(10), Ms(6), Ms(6)) };
            fieldStyle.normal.background = fieldStyle.focused.background = fieldStyle.hover.background = Solid(new Color(0.06f, 0.06f, 0.07f));
            fieldStyle.normal.textColor = fieldStyle.focused.textColor = fieldStyle.hover.textColor = Color.white;
        }
        var s = new GUIStyle(btnStyle) { fontSize = Ms(size) };
        return s;
    }

    // ---------- intro: the title fades up over the sting, then away ----------
    void TickIntro()
    {
        if (introStart < 0) { introStart = Time.time; sfx.Play("sting_intro", null, 0.9f, 0f); }
        float t = Time.time - introStart;
        if (t > 0.3f && t < INTRO_LEN - 0.6f && Input.anyKeyDown) introStart = Time.time - (INTRO_LEN - 0.6f); // skip: quick fade out
        if (t >= INTRO_LEN) { introDone = introSeen = true; startFadeAt = Time.time; }
    }

    void DrawIntro()
    {
        float sw = Screen.width, sh = Screen.height, t = introStart < 0 ? 0 : Time.time - introStart;
        GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture); GUI.color = Color.white;
        float fadeOut = 1f - Mathf.Clamp01((t - (INTRO_LEN - 1.3f)) / 1.1f);
        float a = Mathf.SmoothStep(0, 1, (t - 0.45f) / 1.4f) * fadeOut;
        if (t > 0.45f && t < 2.4f && Mathf.PerlinNoise(t * 18f, 0.3f) > 0.72f) a *= 0.45f; // fluorescent flicker as it comes on
        float a2 = Mathf.SmoothStep(0, 1, (t - 1.9f) / 0.9f) * fadeOut;
        var title = "<b>THE LIVING LABYRINTH</b>";
        float th = Ms(120), ty = sh * 0.5f - th * 0.7f;
        // soft red glow: the title drawn faintly a few times around itself
        var glow = S(Ms(78), new Color(0.8f, 0.05f, 0.03f, a * 0.16f), false, TextAnchor.MiddleCenter);
        for (int i = 0; i < 8; i++) { float ang = i * Mathf.PI / 4, r = Ms(4); GUI.Label(new Rect(Mathf.Cos(ang) * r, ty + Mathf.Sin(ang) * r, sw, th), title, glow); }
        GUI.Label(new Rect(0, ty, sw, th), title, S(Ms(78), new Color(0.95f, 0.22f, 0.18f, a), false, TextAnchor.MiddleCenter));
        GUI.Label(new Rect(0, ty + th * 0.95f, sw, Ms(50)), "<i>The maze isn't just trapping you. It's learning how you escape.</i>", S(Ms(22), new Color(0.85f, 0.72f, 0.68f, a2), false, TextAnchor.MiddleCenter));
    }

    // ---------- start screen ----------
    void StartScreen()
    {
        float sw = Screen.width, sh = Screen.height, w = Mathf.Min(Ms(960), sw - 40), x = (sw - w) / 2, my = Ms(30);
        GUI.color = new Color(0, 0, 0, 0.9f); GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture); GUI.color = Color.white;
        var btn = Btn(19); var white = S(Ms(21), Color.white); var dim = S(Ms(17), DIM);
        GUILayout.BeginArea(new Rect(x, my, w, sh - 2 * my));
        GUILayout.FlexibleSpace();
        GUILayout.Label("<b>THE LIVING LABYRINTH</b>", S(Ms(46), RED));
        GUILayout.Label("<i>The maze isn't just trapping you. It's learning how you escape.</i>", S(Ms(20), new Color(1f, 0.6f, 0.6f)));
        GUILayout.Space(Ms(14));
        GUILayout.Label("<b>HOW TO ESCAPE</b>", S(Ms(23), new Color(1f, 0.85f, 0.2f)));
        GUILayout.Label("<b>1.</b>  Find the <b>3 puzzle rooms</b>. Follow the yellow arrow ▲", white);
        GUILayout.Label("<b>2.</b>  Solve each room's puzzle. <b>3 mistakes</b> and the door opens for the monster", white);
        GUILayout.Label("<b>3.</b>  Reach the <b>exit in the centre</b> of the maze", white);
        GUILayout.Space(Ms(8));
        GUILayout.Label("<color=#ff6b6b><b>THE HOST</b></color> hunts you and <b>learns your habits</b>. Getting caught costs 1 of your <b>3 days</b>.", S(Ms(19), Color.white));
        GUILayout.Label("SHIFT run (fast but loud) · SPACE jump · tap C while sprinting or mid-air to slide, SPACE while sliding to hop (chain them to go faster, loudly) · hold C to crouch under cameras · E hide in lockers / use terminals · CLICK throw a bottle to distract it", dim);
        GUILayout.Space(Ms(10));
        GUILayout.Label($"Strategist link: {(client != null && client.online ? "<color=#6bd68a>online (Claude)</color>" : "<color=#ff6b6b>offline: fallback rules</color>")}", dim);
        GUILayout.Space(Ms(6));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Maze seed (same seed = same maze):", dim, GUILayout.ExpandWidth(false));
        GUILayout.Space(Ms(8));
        seedText = GUILayout.TextField(seedText, fieldStyle, GUILayout.Width(Ms(130)), GUILayout.Height(Ms(38)));
        if (GUILayout.Button("Load", btn, GUILayout.Width(Ms(90)), GUILayout.Height(Ms(38))) && uint.TryParse(seedText, out var ns) && ns != seed) { var go = new GameObject("VAULT"); go.AddComponent<VaultGame>().seed = ns; Destroy(gameObject); }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(demoMode ? "Mode: Demo (7 min)" : "Mode: Full (30 min)", btn, GUILayout.Width(Ms(240)), GUILayout.Height(Ms(38)))) demoMode = !demoMode;
        GUILayout.EndHorizontal();
        GUILayout.Space(Ms(18));
        if (GUILayout.Button("ENTER THE LABYRINTH", Btn(28), GUILayout.Height(Ms(70))))
        {
            started = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            BeginWake(true);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
        // fade in from the intro's black
        float f = 1f - Mathf.Clamp01((Time.time - startFadeAt) / 0.8f);
        if (f > 0) { GUI.color = new Color(0, 0, 0, f); GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture); GUI.color = Color.white; }
    }

    // ---------- wake-up: eyes open in blinks, the blur clears, you get up off the floor ----------
    void BeginWake(bool firstDay)
    {
        wakeStart = Time.time + (firstDay ? 0f : 0.7f); // after a capture, the eyes stay shut a moment longer
        wakeActive = true; welcomePending = firstDay;
        player.lookOnly = true; fx.wake = 1f; fx.danger = 0; danger = 0; sfx.danger = 0; player.fovOffset = 0;
        sfx.Play("wake", null, 0.8f, 0f);
        wakeLines = day == 1
            ? new[] { "You wake on damp carpet, somewhere you don't remember.", "Something in these halls is watching, and learning how you move.", "<b><color=#ff5a4a>Get out before it finds you.</color></b>" }
            : day == 2
            ? new[] { "You wake again, back where you started.", "It caught you once. It remembers how.", "<b><color=#ff5a4a>DAY 2 OF 3</color></b>" }
            : new[] { "You wake again. The lights hum louder now.", "It knows your habits. This is your last chance.", "<b><color=#ff5a4a>DAY 3 OF 3</color></b>" };
    }

    // true while the wake-up owns the frame (the simulation waits; you can look around, then move)
    bool TickWake(float dt)
    {
        if (!wakeActive) return false;
        float t = Time.time - wakeStart;
        if (t >= WAKE_LEN)
        {
            wakeActive = false; fx.wake = 0; player.lookOnly = false; player.wakeDrop = player.wakeRoll = 0;
            if (welcomePending) { welcomePending = false; Say("Welcome to my labyrinth. I will be studying you.", false); }
            return false;
        }
        fx.wake = Mathf.Pow(1f - Mathf.Clamp01(t / 2.1f), 1.6f);
        player.lookOnly = t < MOVE_AT;
        player.wakeDrop = 0.6f * (1f - Mathf.SmoothStep(0, 1, (t - 0.3f) / 2.1f));
        player.wakeRoll = 14f * (1f - Mathf.SmoothStep(0, 1, (t - 0.2f) / 2.0f));
        player.Tick(dt);
        var pt = player.T; if (pt.x != lastTile.x || pt.y != lastTile.y) { OnTile(pt); lastTile = pt; }
        foreach (var s in signs) s.rotation = Quaternion.LookRotation(s.position - player.cam.transform.position);
        return true;
    }

    static readonly (float t, float open)[] LIDS = { (0f, 0f), (0.25f, 0f), (0.55f, 0.38f), (0.78f, 0.06f), (1.15f, 0.72f), (1.35f, 0.3f), (1.9f, 1f) };
    static float LidOpen(float t)
    {
        if (t <= LIDS[0].t) return 0;
        for (int i = 1; i < LIDS.Length; i++)
            if (t < LIDS[i].t) return Mathf.Lerp(LIDS[i - 1].open, LIDS[i].open, Mathf.SmoothStep(0, 1, (t - LIDS[i - 1].t) / (LIDS[i].t - LIDS[i - 1].t)));
        return 1;
    }

    void DrawWake()
    {
        float sw = Screen.width, sh = Screen.height, t = Time.time - wakeStart;
        if (wakeActive)
        {
            float open = LidOpen(t);
            if (open < 1f)
            {
                if (lidTex == null)
                {
                    lidTex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                    for (int i = 0; i < 64; i++) { float k = i / 63f; lidTex.SetPixel(0, i, new Color(0, 0, 0, k * k * (3 - 2 * k))); } // row 63 (top of the texture) opaque
                    lidTex.Apply();
                }
                float cover = sh * 0.5f * (1f - open), feather = sh * 0.16f, fa = Mathf.Clamp01((1f - open) * 5f);
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, sw, cover), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, sh - cover, sw, cover), Texture2D.whiteTexture);
                GUI.color = new Color(1, 1, 1, fa);
                GUI.DrawTextureWithTexCoords(new Rect(0, cover, sw, feather), lidTex, new Rect(0, 0, 1, 1));       // opaque at the lid edge, fading down
                GUI.DrawTextureWithTexCoords(new Rect(0, sh - cover - feather, sw, feather), lidTex, new Rect(0, 1, 1, -1)); // and fading up
                GUI.color = Color.white;
            }
        }
        // the opening lines, one at a time, then gone
        if (t > 0.8f && t < WAKE_TEXT_END && wakeLines.Length > 0)
        {
            float outA = 1f - Mathf.Clamp01((t - (WAKE_TEXT_END - 1.2f)) / 1.2f);
            float y = sh * 0.56f, w = Mathf.Min(sw - 40, Fs(1100));
            for (int i = 0; i < wakeLines.Length; i++)
            {
                float a = Mathf.SmoothStep(0, 1, (t - (0.9f + i * 1.4f)) / 0.9f) * outA;
                if (a <= 0) break;
                bool last = i == wakeLines.Length - 1;
                var st = S(last ? Fs(36) : Fs(25), new Color(0.92f, 0.88f, 0.82f, a), false, TextAnchor.MiddleCenter);
                string txt = last ? wakeLines[i] : "<i>" + wakeLines[i] + "</i>";
                float h = st.CalcHeight(new GUIContent(txt), w);
                var sh2 = S(st.fontSize, new Color(0, 0, 0, a * 0.85f), false, TextAnchor.MiddleCenter);
                GUI.Label(new Rect((sw - w) / 2 + 2, y + 2, w, h), System.Text.RegularExpressions.Regex.Replace(txt, "</?color[^>]*>", ""), sh2);
                GUI.Label(new Rect((sw - w) / 2, y, w, h), txt, st);
                y += h + Fs(10) + (last ? 0 : 0);
                if (i == wakeLines.Length - 2) y += Fs(12);
            }
        }
    }

    // ---------- terminal puzzle panel ----------
    void DrawPuzzlePanel(EscapeRoom ar, float pw, float sh)
    {
        float k = MenuScale, w = Mathf.Min(Ms(860), pw - 40), h = Mathf.Min(Ms(600), sh - 40), x = (pw - w) / 2, py = (sh - h) / 2;
        GUI.color = new Color(0.02f, 0.03f, 0.04f, 0.96f); GUI.DrawTexture(new Rect(x - Ms(18), py - Ms(18), w + Ms(36), h + Ms(36)), Texture2D.whiteTexture);
        GUI.color = new Color(0.6f, 0.12f, 0.1f, 0.8f); GUI.DrawTexture(new Rect(x - Ms(18), py - Ms(18), w + Ms(36), Ms(3)), Texture2D.whiteTexture); GUI.color = Color.white;
        int res = ar.DrawPuzzle(new Rect(x, py, w, h), k, S(Ms(26), RED), S(Ms(20), Color.white), Btn(20));
        if (res == 1) RoomMistake(ar, ar.name == "power" ? "wrong breaker" : ar.name == "surveillance" ? "wrong code" : "missed the lever");
        else if (res == 2) RoomSolved(ar);
    }

    // ---------- post-mortem ----------
    void PostMortem()
    {
        float sw = Screen.width, sh = Screen.height, w = Mathf.Min(Ms(1080), sw - 40), x = (sw - w) / 2, my = Ms(30);
        GUI.color = new Color(0, 0, 0, 0.92f); GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(x, my, w, sh - 2 * my));
        GUILayout.Label($"<b>{(won ? "ESCAPE SUCCESSFUL: YOU OUTSMARTED THE HOST" : "CAPTURED")}</b>", S(Ms(40), RED));
        GUILayout.Label("<b>INCIDENT POST-MORTEM</b>  ·  " + (won ? "You got out. THE HOST has filed a report." : "THE HOST has filed a report on how it beat you."), S(Ms(20), new Color(1f, 0.6f, 0.6f)));
        GUILayout.Space(Ms(10));
        int good = mem.decisions.Count(d => d.outcome.StartsWith("SUCCESS") || d.outcome.StartsWith("partial"));
        int deception = mem.throws > 0 ? Mathf.RoundToInt(100f * (mem.throws - mem.distractionsCaught) / mem.throws) : 0;
        var body = S(Ms(19), Color.white);
        GUILayout.Label($"<b>Escape day</b> {day} · <b>Captures</b> {mem.captures.Count} · <b>Rooms completed</b> {RoomsDone}/3 · <b>Puzzle mistakes</b> {rooms.Values.Sum(r => r.mistakes)} · <b>Camera spots</b> {cams.Sum(c => c.spots)} · <b>Deception score</b> {deception}% · <b>HOST decisions that worked</b> {good}/{mem.decisions.Count}", body);
        GUILayout.Space(Ms(4));
        GUILayout.Label($"<b>What it learned about you:</b> playstyle {mem.Playstyle} · runs {Mathf.RoundToInt(mem.RunRatio * 100)}% of the time · favourite zone {mem.PreferredZone ?? "-"} · favourite door {mem.PreferredDoor ?? "-"} · favourite locker {mem.FavouriteLocker ?? "-"} · trust in your noises ended at {Mathf.RoundToInt(mem.noiseTrust * 100)}%", body);
        GUILayout.Space(Ms(12));
        GUILayout.Label("INCIDENT TIMELINE", S(Ms(19), BLUE, true));
        pmScroll = GUILayout.BeginScrollView(pmScroll, GUILayout.ExpandHeight(true));
        var keyEvents = new[] { "player_spotted", "camera_spotted", "player_captured", "room_entered", "puzzle_mistake", "room_breached", "room_completed", "central_exit_unlocked", "player_hiding", "distraction_detected", "broker_decision", "player_escaped", "game_over" };
        var line = S(Ms(17), Color.white);
        foreach (var e in mem.log.Where(e => keyEvents.Contains(e.name))) GUILayout.Label($"<color=#888>{Mathf.RoundToInt(e.t)}s</color>  {Describe(e)}", line);
        GUILayout.EndScrollView();
        GUILayout.Space(Ms(10));
        if (GUILayout.Button("PLAY AGAIN (same maze)", Btn(24), GUILayout.Height(Ms(60)))) { var go = new GameObject("VAULT"); go.AddComponent<VaultGame>().seed = seed; Destroy(gameObject); }
        GUILayout.EndArea();
    }
}
