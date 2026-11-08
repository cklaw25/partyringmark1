// The three escape rooms + the three-lock mechanic + their puzzles (drawn with IMGUI).
// Entering seals the room and alerts THE HOST. Every mistake breaks a lock. 3 broken = THE HOST can get in.
using System.Collections.Generic;
using UnityEngine;

public class EscapeRoom
{
    public string name, title;
    public Room room;
    public List<Door> gates = new List<Door>();
    public int locks = 3, mistakes;
    public bool active, done, breached;
    public float timer;
    public const float TIME_LIMIT = 90f;
    public static System.Action<string> Sfx; // UI clicks (set by VaultGame)

    // Power: breaker sequence memory. The demo plays when the terminal is opened (after a short lead-in), not on entry.
    public int[] sequence; public int seqPos; public float watchStart; public bool watching;
    const float STEP = 0.8f, ON = 0.55f, LEAD = 0.9f;
    int lastLit = -1; readonly float[] pressedAt = { -9, -9, -9, -9 };
    // Surveillance: read the 4 monitors in the room
    public string[] camLabels = { "A", "B", "C", "D" }; public int[] camDigits = new int[4]; public int[] order = new int[4];
    public string code = "", fakeCode = "", entry = "";
    public List<GameObject> monitors = new List<GameObject>();
    // Emergency: timed lever override
    public int leverHits; public float bar, barDir = 1, zoneStart, zoneWidth = 0.16f;

    public EscapeRoom(string n, Room r, System.Random rng)
    {
        name = n; room = r;
        title = n == "power" ? "POWER CHAMBER" : n == "surveillance" ? "SURVEILLANCE CHAMBER" : "EMERGENCY RELEASE";
        sequence = new int[5]; for (int i = 0; i < 5; i++) sequence[i] = rng.Next(4);
        var perm = new List<int> { 0, 1, 2, 3 };
        for (int i = 0; i < 4; i++) { camDigits[i] = rng.Next(10); int j = rng.Next(perm.Count); order[i] = perm[j]; perm.RemoveAt(j); }
        foreach (var o in order) code += camDigits[o];
        do { fakeCode = ""; for (int i = 0; i < 4; i++) fakeCode += rng.Next(10); } while (fakeCode == code);
        NewLeverZone();
    }

    public string Status => done ? "DONE" : active ? $"LOCKDOWN, locks {locks}/3" : breached ? "breached" : "not started";

    public void Begin()
    {
        active = true; timer = TIME_LIMIT; entry = ""; seqPos = 0; leverHits = 0; watching = false;
    }

    // called whenever the terminal is opened: replays the breaker demo from the top
    public void OpenTerminal() { if (name == "power") StartDemo(); }
    void StartDemo() { seqPos = 0; watching = true; watchStart = Time.time + LEAD; lastLit = -1; }

    public void ResetAfterCapture() { active = false; breached = false; locks = 3; entry = ""; seqPos = 0; leverHits = 0; }

    public void NewLeverZone() { zoneStart = Random.Range(0.1f, 0.9f - zoneWidth); }

    static readonly Color[] BREAKER = { new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.85f, 0.3f), new Color(0.25f, 0.45f, 1f), new Color(1f, 0.85f, 0.2f) };
    static readonly string[] BREAKER_NAME = { "RED", "GREEN", "BLUE", "YELLOW" };

    // returns: 0 = nothing, 1 = mistake, 2 = solved. k = UI scale; styles come pre-scaled from VaultGame.
    public int DrawPuzzle(Rect area, float k, GUIStyle title, GUIStyle body, GUIStyle button)
    {
        int result = 0; var ev = Event.current;
        GUILayout.BeginArea(area);
        GUILayout.Label($"<b>{this.title}</b>   locks: {new string('■', locks)}{new string('□', 3 - locks)}   time: {Mathf.CeilToInt(timer)} s", title);
        GUILayout.Space(6 * k);
        if (name == "power")
        {
            float t = Time.time - watchStart; int lit = -1;
            if (watching)
            {
                if (t >= 0) { int idx = Mathf.FloorToInt(t / STEP); if (idx < sequence.Length && t - idx * STEP < ON) lit = idx; }
                if (t >= sequence.Length * STEP + 0.15f) watching = false;
                if (ev.type == EventType.Repaint && lit >= 0 && lit != lastLit) Sfx?.Invoke("tone" + sequence[lit]);
                if (ev.type == EventType.Repaint) lastLit = lit;
            }
            string msg = !watching ? $"Now repeat it: step <b>{seqPos + 1}/{sequence.Length}</b>. Click the breakers (or keys 1-4). A wrong breaker breaks a lock."
                       : t < 0 ? "<b>Watch closely...</b>" : "<b>Watch the breaker sequence...</b>";
            GUILayout.Label(msg, body);
            GUILayout.Space(8 * k);

            // progress dots: white while it plays, green as you repeat it
            var dots = GUILayoutUtility.GetRect(area.width, 22 * k);
            float ds = 16 * k, gap = 12 * k, dx = dots.x + (dots.width - (sequence.Length * ds + (sequence.Length - 1) * gap)) / 2;
            int shown = watching ? Mathf.Clamp(t < 0 ? 0 : Mathf.FloorToInt(t / STEP) + 1, 0, sequence.Length) : seqPos;
            for (int i = 0; i < sequence.Length; i++)
            {
                GUI.color = i < shown ? (watching ? Color.white : new Color(0.3f, 0.9f, 0.4f)) : new Color(1, 1, 1, 0.15f);
                GUI.DrawTexture(new Rect(dx + i * (ds + gap), dots.y + 3 * k, ds, ds), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUILayout.Space(10 * k);

            // the four breakers: dim in their own colour, light up full-bright (with a glow) when flashed or pressed
            var row = GUILayoutUtility.GetRect(area.width, 150 * k);
            float tg = 18 * k, tw = (row.width - 3 * tg) / 4f;
            int keyHit = -1;
            if (ev.type == EventType.KeyDown && ev.keyCode >= KeyCode.Alpha1 && ev.keyCode <= KeyCode.Alpha4) keyHit = ev.keyCode - KeyCode.Alpha1;
            if (ev.type == EventType.KeyDown && ev.keyCode >= KeyCode.Keypad1 && ev.keyCode <= KeyCode.Keypad4) keyHit = ev.keyCode - KeyCode.Keypad1;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(row.x + i * (tw + tg), row.y + 10 * k, tw, row.height - 20 * k);
                bool pressed = Time.time - pressedAt[i] < 0.22f;
                bool on = lit >= 0 && sequence[lit] == i || pressed;
                bool hover = !watching && r.Contains(ev.mousePosition);
                Color c = BREAKER[i];
                if (on)
                {
                    var g = new Rect(r.x - 12 * k, r.y - 12 * k, r.width + 24 * k, r.height + 24 * k);
                    GUI.color = new Color(c.r, c.g, c.b, 0.25f); GUI.DrawTexture(g, Texture2D.whiteTexture);
                    GUI.color = new Color(c.r, c.g, c.b, 0.45f); GUI.DrawTexture(new Rect(r.x - 6 * k, r.y - 6 * k, r.width + 12 * k, r.height + 12 * k), Texture2D.whiteTexture);
                    GUI.color = Color.Lerp(c, Color.white, 0.25f);
                }
                else GUI.color = c * (hover ? 0.45f : 0.26f) + new Color(0, 0, 0, 1);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = on ? new Color(1, 1, 1, 0.35f) : new Color(c.r, c.g, c.b, 0.5f); // bevel line
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3 * k), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var ls = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                ls.normal.textColor = on ? new Color(0.05f, 0.05f, 0.05f) : Color.Lerp(c, Color.white, 0.3f);
                GUI.Label(r, $"{BREAKER_NAME[i]}\n<size={Mathf.RoundToInt(ls.fontSize * 0.7f)}>{i + 1}</size>", ls);
                bool clicked = ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition);
                if ((clicked || keyHit == i) && !watching)
                {
                    ev.Use(); pressedAt[i] = Time.time;
                    Sfx?.Invoke("click"); Sfx?.Invoke("tone" + i);
                    if (sequence[seqPos] == i) { seqPos++; if (seqPos >= sequence.Length) result = 2; }
                    else { result = 1; StartDemo(); }
                }
            }
            GUILayout.Space(4 * k);
            if (!watching && GUILayout.Button("Replay the sequence (resets your input)", button, GUILayout.Height(40 * k))) StartDemo();
        }
        else if (name == "surveillance")
        {
            string ord = ""; foreach (var o in order) ord += "CAM " + camLabels[o] + "  →  ";
            GUILayout.Label("Read the 4 camera monitors in this room. Enter their digits in this order:", body);
            GUILayout.Label("<b>" + ord.TrimEnd(' ', '→') + "</b>", body);
            GUILayout.Label($"Code: <b>{entry.PadRight(4, '_')}</b>", title);
            GUILayout.Space(6 * k);
            for (int row = 0; row < 4; row++)
            {
                GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
                for (int c = 0; c < 3; c++)
                {
                    int kk = row * 3 + c; string lbl = kk < 9 ? (kk + 1).ToString() : kk == 9 ? "CLR" : kk == 10 ? "0" : "ENTER";
                    if (GUILayout.Button(lbl, button, GUILayout.Height(58 * k), GUILayout.Width(150 * k)))
                    {
                        Sfx?.Invoke("click");
                        if (lbl == "CLR") entry = "";
                        else if (lbl == "ENTER") { if (entry == code) result = 2; else { result = 1; entry = ""; } }
                        else if (entry.Length < 4) entry += lbl;
                    }
                }
                GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            }
        }
        else
        {
            if (ev.type == EventType.Repaint) bar += barDir * Time.deltaTime * (0.55f + 0.25f * leverHits);
            if (bar > 1) { bar = 1; barDir = -1; } if (bar < 0) { bar = 0; barDir = 1; }
            GUILayout.Label($"Pull each override lever while the needle is in the GREEN zone. Levers: <b>{leverHits}/3</b>. A miss breaks a lock.", body);
            GUILayout.Space(12 * k);
            var r = GUILayoutUtility.GetRect(area.width - 20, 56 * k);
            GUI.color = new Color(0.15f, 0.15f, 0.18f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.85f, 0.3f); GUI.DrawTexture(new Rect(r.x + r.width * zoneStart, r.y, r.width * zoneWidth, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white; GUI.DrawTexture(new Rect(r.x + r.width * bar - 4 * k, r.y - 6 * k, 8 * k, r.height + 12 * k), Texture2D.whiteTexture);
            GUILayout.Space(14 * k);
            if (GUILayout.Button("PULL LEVER  (SPACE)", button, GUILayout.Height(72 * k)) || ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Space)
            {
                Sfx?.Invoke("click");
                if (bar >= zoneStart && bar <= zoneStart + zoneWidth) { leverHits++; NewLeverZone(); if (leverHits >= 3) result = 2; }
                else { result = 1; NewLeverZone(); }
                if (ev.type == EventType.KeyDown) ev.Use();
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label("<color=#888>E / ESC to step away from the terminal (the room stays sealed)</color>", body);
        GUILayout.EndArea();
        return result;
    }
}
