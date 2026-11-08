// THE HOST - the fast path. Sees, hears, moves and reacts every frame with plain code.
// The slow path (Claude strategist) only sets its strategy + maze changes; it never drives a frame.
using System.Collections.Generic;
using UnityEngine;

public class Decision
{
    public string action, target, reason, prediction, dialogue, source = "claude";
    public float confidence; public int latencyMs;
}

public class NoiseEvent { public int x, y; public float radius; public string kind; public int fromX, fromY; }

public class BrokerAgent : MonoBehaviour
{
    public static readonly string[] ACTIONS = { "PATROL", "INVESTIGATE_ZONE", "SEARCH_HIDING_PLACES", "INTERCEPT_ROUTE", "BLOCK_CORRIDOR", "OPEN_PASSAGE", "GUARD_OBJECTIVE", "ACTIVATE_ALARM" };

    public VaultGame game; VaultMaze maze; BrokerMemory mem;
    public string state = "PATROL", stateReason = "starting patrol";
    public Decision strategy = new Decision { action = "PATROL" };
    List<Tile> path = new List<Tile>();
    Queue<Locker> searchQueue = new Queue<Locker>();
    Locker currentLocker;
    NoiseEvent investigation;
    float loseSight, repath, alarmUntil;
    Tile? guardPoint;
    bool lastSeenPlayer;
    public bool AlarmOn => Time.time < alarmUntil;

    public void Init(VaultGame g)
    {
        game = g; maze = g.maze; mem = g.mem;
        transform.position = g.World(maze.ZoneCentre("south-east")); // starts far away: no hunting at the start
    }

    public Tile T => game.TileOf(transform.position);

    float Speed()
    {
        // a fraction of the player's run speed, so tuning the player keeps the balance: always slower than a run,
        // its danger is prediction + maze control, not speed
        float b = state switch { "PATROL" => 0.42f, "INVESTIGATE" => 0.58f, "SEARCH" => 0.5f, "CHASE" => 0.81f, "INTERCEPT" => 0.69f, "GUARD" => 0.39f, _ => 0.5f };
        if (game.day >= 3) b *= 1.1f;
        return (AlarmOn ? b * 1.2f : b) * game.player.runSpeed;
    }

    void GoTo(Tile t, bool ignoreDoors = false)
    {
        var p = maze.Path(T, t, ignoreDoors) ?? maze.Path(T, t, true);
        path = p ?? new List<Tile>(); if (path.Count > 0) path.RemoveAt(0);
    }

    public void SetState(string s, string reason) { state = s; stateReason = reason; }

    // ---------- perception ----------
    public bool CanSee(Tile pt, bool hidden, bool running, bool powerOn, bool crouched = false)
    {
        if (hidden) return false;
        var t = T; float range = (powerOn ? 7 : 5) + (running ? 1 : 0) - (crouched ? 2 : 0);
        float d = Mathf.Sqrt((pt.x - t.x) * (pt.x - t.x) + (pt.y - t.y) * (pt.y - t.y));
        return d <= range && maze.LineOfSight(t.x, t.y, pt.x, pt.y);
    }

    public void Hear(NoiseEvent n)
    {
        if (state == "CHASE") return;
        var t = T; float d = Mathf.Sqrt((n.x - t.x) * (n.x - t.x) + (n.y - t.y) * (n.y - t.y));
        if (d > n.radius) return;
        if (n.kind == "throw")
        {
            // DECEPTION LEARNING: once fooled, it checks where the bottle came FROM
            if (mem.noiseTrust >= 0.5f)
            {
                investigation = n; SetState("INVESTIGATE", $"heard a bottle (trust in noises {Mathf.RoundToInt(mem.noiseTrust * 100)}%)");
                GoTo(new Tile(n.x, n.y)); game.Say(Pick("What was that?", "I hear you.", "Something fell..."), true);
            }
            else
            {
                investigation = new NoiseEvent { x = n.fromX, y = n.fromY, kind = "throw_origin" };
                SetState("INVESTIGATE", $"ignores the landing spot, checks where it was thrown FROM (trust {Mathf.RoundToInt(mem.noiseTrust * 100)}%)");
                GoTo(new Tile(n.fromX, n.fromY)); game.Say(Pick("Another bottle? I know where you threw it from.", "Not falling for that again."), true);
            }
            return;
        }
        investigation = n; SetState("INVESTIGATE", $"heard {n.kind} noise"); GoTo(new Tile(n.x, n.y));
        if (n.kind != "run" && n.kind != "land") game.Say(Pick("I heard that.", "Coming.", "There you are..."), true);
    }

    // ---------- strategy from the slow path (already validated by VaultGame) ----------
    public bool ApplyStrategy(Decision dec)
    {
        strategy = dec;
        switch (dec.action)
        {
            case "PATROL": SetState("PATROL", dec.reason ?? "patrolling"); path.Clear(); break;
            case "INVESTIGATE_ZONE":
                var c = maze.ZoneCentre(string.IsNullOrEmpty(dec.target) ? "centre" : dec.target);
                investigation = new NoiseEvent { x = c.x, y = c.y, kind = "zone" };
                SetState("INVESTIGATE", dec.reason ?? "checking a zone"); GoTo(c); break;
            case "SEARCH_HIDING_PLACES":
                var ls = new List<Locker>(maze.lockers);
                ls.Sort((a, b) => Uses(b).CompareTo(Uses(a)));
                if (!string.IsNullOrEmpty(dec.target)) { var tl = ls.Find(l => l.id == dec.target); if (tl != null) { ls.Remove(tl); ls.Insert(0, tl); } }
                searchQueue = new Queue<Locker>(ls.GetRange(0, Mathf.Min(3, ls.Count)));
                SetState("SEARCH", dec.reason ?? "searching hiding places"); NextSearch(); break;
            case "INTERCEPT_ROUTE":
            case "GUARD_OBJECTIVE":
                var pt = game.ObjectivePoint(dec.target); if (pt == null) return false;
                guardPoint = pt; SetState(dec.action == "GUARD_OBJECTIVE" ? "GUARD" : "INTERCEPT", dec.reason ?? "cutting you off");
                GoTo(pt.Value, true); break;
            case "ACTIVATE_ALARM": alarmUntil = Time.time + 8f; break;
        }
        return true;
    }

    int Uses(Locker l) => mem.lockerUses.TryGetValue(l.id, out var u) ? u : 0;

    void NextSearch()
    {
        if (searchQueue.Count == 0) { SetState("PATROL", "search finished"); path.Clear(); currentLocker = null; return; }
        currentLocker = searchQueue.Dequeue(); GoTo(new Tile(currentLocker.x, currentLocker.y));
    }

    // ---------- per-frame ----------
    public void Tick(float dt, FirstPersonPlayer p, bool powerOn)
    {
        var pt = p.T;
        bool sees = CanSee(pt, p.hidden, p.running, powerOn, p.crouched);
        if (sees)
        {
            if (!lastSeenPlayer)
            {
                mem.sightings++;
                game.Emit("player_spotted", ("zone", maze.ZoneOf(pt.x, pt.y)), ("confidence", 1f));
                game.Say(Pick("Found you.", "There you are.", "Run."), true);
            }
            mem.lastSeen = pt; mem.lastSeenTime = Time.time; loseSight = 0;
            SetState("CHASE", "player in sight");
            repath -= dt; if (repath <= 0 || path.Count == 0) { GoTo(pt); repath = 0.25f; }
        }
        else if (state == "CHASE")
        {
            loseSight += dt;
            if (p.hidden && p.seenEnteringLocker && p.locker != null) { currentLocker = p.locker; SetState("SEARCH", "watched you get into " + p.locker.id); GoTo(new Tile(p.locker.x, p.locker.y)); searchQueue.Clear(); }
            else if (loseSight > 1.5f)
            {
                var near = maze.lockers.FindAll(l => mem.lastSeen.HasValue && Mathf.Abs(l.x - mem.lastSeen.Value.x) + Mathf.Abs(l.y - mem.lastSeen.Value.y) < 10);
                near.Sort((a, b) => Uses(b).CompareTo(Uses(a)));
                searchQueue = new Queue<Locker>(near.GetRange(0, Mathf.Min(2, near.Count)));
                SetState("SEARCH", "lost sight, searching nearby hiding places");
                if (mem.lastSeen.HasValue) GoTo(mem.lastSeen.Value); else NextSearch();
            }
        }
        lastSeenPlayer = sees;

        if (path.Count > 0)
        {
            var n = path[0];
            if (maze.doorAt.TryGetValue(maze.Key(n.x, n.y), out var door) && door.closed) { path.Clear(); return; }
            Vector3 target = game.World(n); target.y = transform.position.y;
            Vector3 dir = target - transform.position; float d = dir.magnitude, step = Speed() * dt;
            if (d > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * dt);
            if (d <= step) { transform.position = target; path.RemoveAt(0); }
            else transform.position += dir / d * step;
        }
        else OnArrive(p);
    }

    void OnArrive(FirstPersonPlayer p)
    {
        var t = T; var pt = p.T;
        if (state == "INVESTIGATE")
        {
            bool near = Mathf.Abs(pt.x - t.x) + Mathf.Abs(pt.y - t.y) <= 4 && !p.hidden;
            if (investigation != null && investigation.kind == "throw")
            {
                if (!near) { mem.OnDistractionFound(); game.Emit("distraction_detected", ("zone", maze.ZoneOf(t.x, t.y)), ("noise_trust", mem.noiseTrust)); game.Say(Pick("A bottle. Clever.", "You think that will work twice?"), true); }
            }
            else if (investigation != null && near) mem.OnRealNoiseConfirmed();
            investigation = null;
            var nearL = maze.lockers.FindAll(l => Mathf.Abs(l.x - t.x) + Mathf.Abs(l.y - t.y) < 9);
            searchQueue = new Queue<Locker>(nearL.GetRange(0, Mathf.Min(2, nearL.Count)));
            SetState("SEARCH", "nothing here, checking hiding places"); NextSearch(); return;
        }
        if (state == "SEARCH")
        {
            if (currentLocker != null && p.hidden && p.locker != null && p.locker.id == currentLocker.id) { game.Capture("found you in " + p.locker.id); return; }
            NextSearch(); return;
        }
        if (state == "GUARD" || state == "INTERCEPT")
        {
            var g = guardPoint ?? t;
            GoTo(maze.NearestFloor(g.x + Random.Range(-3, 4), g.y + Random.Range(-3, 4))); return;
        }
        if (state == "CHASE") return;
        var targets = new List<Tile>();
        foreach (var r in maze.rooms.Values) targets.Add(new Tile(r.cx, r.cy));
        foreach (var z in new[] { "north", "south", "east", "west", "centre" }) targets.Add(maze.ZoneCentre(z));
        GoTo(targets[Random.Range(0, targets.Count)]);
    }

    public void ResetAfterCapture() { SetState("PATROL", "new day: lost track of you"); path.Clear(); transform.position = game.World(maze.ZoneCentre("east")); }

    // escape room breached / alert: go straight there
    public void Hunt(Tile t, string reason) { SetState("INTERCEPT", reason); guardPoint = t; GoTo(t); }

    public string Threat => state == "CHASE" ? "ACTIVE HUNT" : (state == "PATROL" || state == "GUARD") ? "EXPLORATION" : "INVESTIGATION";

    // rule-based fallback strategist (backend offline / slow)
    public static Decision Fallback(BrokerMemory mem, int roomsDone, string activeRoom, string nextRoom, List<string> closable)
    {
        if (roomsDone >= 3) return D("GUARD_OBJECTIVE", "central", "all three rooms done, guarding the central exit", "heading for the central exit", 0.8f, "Three rooms. One exit. And I am standing in front of it.");
        if (activeRoom != null) return D("GUARD_OBJECTIVE", activeRoom, $"player sealed inside the {activeRoom} room", "will leave the room when the puzzle is solved", 0.7f, "Take your time. I will be waiting outside.");
        if (mem.PreferredDoor != null && mem.PreferredDoorUses >= 3 && closable.Contains(mem.PreferredDoor))
            return D("BLOCK_CORRIDOR", mem.PreferredDoor, $"player used {mem.PreferredDoor} {mem.PreferredDoorUses} times", $"will try {mem.PreferredDoor} again", 0.6f, "Your favourite door is closed. Predictable.");
        if (mem.hides >= 2) return D("SEARCH_HIDING_PLACES", mem.FavouriteLocker, "player hides often", "hiding in a locker", 0.55f, "I know you like the lockers.");
        if (mem.RunRatio > 0.45f && nextRoom != null) return D("INTERCEPT_ROUTE", nextRoom, "fast player rushing the next room", "rushing the next escape room", 0.5f, "Running again? I can hear every step.");
        return D("INVESTIGATE_ZONE", mem.PreferredZone ?? "centre", "checking the player's favourite zone", $"player in {mem.PreferredZone ?? "centre"}", Mathf.Max(0.3f, mem.PreferredZoneShare), "I am watching.");
    }

    static Decision D(string a, string t, string r, string p, float c, string dl) => new Decision { action = a, target = t, reason = r, prediction = p, confidence = c, dialogue = dl, source = "fallback-rules" };
    static string Pick(params string[] a) => a[Random.Range(0, a.Length)];
}
