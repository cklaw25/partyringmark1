// The three escape rooms + the three-lock mechanic + their puzzles (drawn with IMGUI).
// Entering seals the room and alerts THE BROKER. Every mistake breaks a lock. 3 broken = THE BROKER can get in.
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

    // Power: breaker sequence memory
    public int[] sequence; public int seqPos; public float watchStart; public bool watching;
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
        active = true; timer = TIME_LIMIT; entry = ""; seqPos = 0; leverHits = 0; watching = true; watchStart = Time.time;
    }

    public void ResetAfterCapture() { active = false; breached = false; locks = 3; entry = ""; seqPos = 0; leverHits = 0; }

    public void NewLeverZone() { zoneStart = Random.Range(0.1f, 0.9f - zoneWidth); }

    static readonly Color[] BREAKER = { new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.85f, 0.3f), new Color(0.25f, 0.45f, 1f), new Color(1f, 0.85f, 0.2f) };
    static readonly string[] BREAKER_NAME = { "RED", "GREEN", "BLUE", "YELLOW" };

    // returns: 0 = nothing, 1 = mistake, 2 = solved
    public int DrawPuzzle(Rect area, GUIStyle title, GUIStyle body)
    {
        int result = 0;
        GUILayout.BeginArea(area);
        GUILayout.Label($"<b>{this.title}</b>   locks: {new string('■', locks)}{new string('□', 3 - locks)}   time: {Mathf.CeilToInt(timer)} s", title);
        if (name == "power")
        {
            float t = Time.time - watchStart; int lit = watching ? Mathf.FloorToInt(t / 0.7f) : -1;
            if (watching && lit >= sequence.Length + 1) { watching = false; }
            GUILayout.Label(watching ? "Watch the breaker sequence..." : $"Repeat the sequence: step {seqPos + 1}/{sequence.Length}. A wrong breaker breaks a lock.", body);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 4; i++)
            {
                bool on = watching && lit < sequence.Length && lit >= 0 && sequence[lit] == i && (t % 0.7f) < 0.5f;
                GUI.backgroundColor = on ? Color.white : BREAKER[i] * 0.8f;
                if (GUILayout.Button(BREAKER_NAME[i], GUILayout.Height(90)) && !watching)
                {
                    Sfx?.Invoke("click");
                    if (sequence[seqPos] == i) { seqPos++; if (seqPos >= sequence.Length) result = 2; }
                    else { result = 1; seqPos = 0; watching = true; watchStart = Time.time; }
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }
        else if (name == "surveillance")
        {
            string ord = ""; foreach (var o in order) ord += "CAM " + camLabels[o] + "  →  ";
            GUILayout.Label("Read the 4 camera monitors in this room. Enter their digits in this order:", body);
            GUILayout.Label("<b>" + ord.TrimEnd(' ', '→') + "</b>", body);
            GUILayout.Label($"Code: <b>{entry.PadRight(4, '_')}</b>", title);
            for (int row = 0; row < 4; row++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 3; c++)
                {
                    int k = row * 3 + c; string lbl = k < 9 ? (k + 1).ToString() : k == 9 ? "CLR" : k == 10 ? "0" : "ENTER";
                    if (GUILayout.Button(lbl, GUILayout.Height(44), GUILayout.Width(110)))
                    {
                        Sfx?.Invoke("click");
                        if (lbl == "CLR") entry = "";
                        else if (lbl == "ENTER") { if (entry == code) result = 2; else { result = 1; entry = ""; } }
                        else if (entry.Length < 4) entry += lbl;
                    }
                }
                GUILayout.EndHorizontal();
            }
        }
        else
        {
            if (Event.current.type == EventType.Repaint) bar += barDir * Time.deltaTime * (0.55f + 0.25f * leverHits);
            if (bar > 1) { bar = 1; barDir = -1; } if (bar < 0) { bar = 0; barDir = 1; }
            GUILayout.Label($"Pull each override lever while the needle is in the GREEN zone. Levers: {leverHits}/3. A miss breaks a lock.", body);
            var r = GUILayoutUtility.GetRect(area.width - 20, 40);
            GUI.color = new Color(0.15f, 0.15f, 0.18f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.85f, 0.3f); GUI.DrawTexture(new Rect(r.x + r.width * zoneStart, r.y, r.width * zoneWidth, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white; GUI.DrawTexture(new Rect(r.x + r.width * bar - 3, r.y - 4, 6, r.height + 8), Texture2D.whiteTexture);
            if (GUILayout.Button("PULL LEVER  (SPACE)", GUILayout.Height(60)) || Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            {
                Sfx?.Invoke("click");
                if (bar >= zoneStart && bar <= zoneStart + zoneWidth) { leverHits++; NewLeverZone(); if (leverHits >= 3) result = 2; }
                else { result = 1; NewLeverZone(); }
                if (Event.current.type == EventType.KeyDown) Event.current.Use();
            }
        }
        GUILayout.Space(8);
        GUILayout.Label("<color=#888>E / ESC to step away from the terminal (the room stays sealed)</color>", body);
        GUILayout.EndArea();
        return result;
    }
}
