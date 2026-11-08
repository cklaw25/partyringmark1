// THE HOST's memory: short-term events + long-term behavioural profile. Persists across captures.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameEvent
{
    public float t; public string name; public Dictionary<string, object> data = new Dictionary<string, object>();
    public Dictionary<string, object> ToDict()
    {
        var d = new Dictionary<string, object>(data) { ["event"] = name, ["t"] = Mathf.Round(t) };
        return d;
    }
}

public class DecisionRecord
{
    public float t; public string action, target, reason, prediction, dialogue, source, note, outcome = "pending";
    public float confidence; public bool valid; public int latencyMs;
    public Dictionary<string, object> ToDict() => new Dictionary<string, object> {
        ["t"] = Mathf.Round(t), ["action"] = action, ["target"] = target, ["reason"] = reason, ["prediction"] = prediction,
        ["valid"] = valid, ["outcome"] = outcome };
}

public class BrokerMemory
{
    public List<GameEvent> events = new List<GameEvent>();
    public List<GameEvent> log = new List<GameEvent>();
    public Dictionary<string, float> zoneVisits = new Dictionary<string, float>();
    public Dictionary<string, int> doorPasses = new Dictionary<string, int>();
    public Dictionary<string, int> lockerUses = new Dictionary<string, int>();
    public float moveTime, runTime;
    public int hides, throws, distractionsCaught, sightings;
    public float noiseTrust = 1f;
    public List<(int x, int y, string zone, string how, float t)> captures = new List<(int, int, string, string, float)>();
    public Tile? lastSeen; public float lastSeenTime;
    public List<DecisionRecord> decisions = new List<DecisionRecord>();

    public void Push(GameEvent e) { events.Add(e); if (events.Count > 40) events.RemoveAt(0); log.Add(e); }

    public void Tick(float dt, string zone, bool moving, bool running)
    {
        zoneVisits.TryGetValue(zone, out var v); zoneVisits[zone] = v + dt;
        if (moving) { moveTime += dt; if (running) runTime += dt; }
    }

    static (string key, float share, float count) Top<T>(Dictionary<string, T> d) where T : System.IConvertible
    {
        string best = null; float bv = 0, total = 0;
        foreach (var kv in d) { float f = System.Convert.ToSingle(kv.Value); total += f; if (f > bv) { bv = f; best = kv.Key; } }
        return (best, total > 0 ? bv / total : 0, bv);
    }

    public float RunRatio => moveTime > 1 ? runTime / moveTime : 0;

    public string Playstyle => RunRatio > 0.45f ? "aggressive" : (hides >= 2 || throws >= 2 ? "stealth" : "cautious");

    public Dictionary<string, object> Profile()
    {
        var z = Top(zoneVisits); var d = Top(doorPasses); var l = Top(lockerUses);
        return new Dictionary<string, object> {
            ["running_frequency"] = R(RunRatio), ["hiding_count"] = hides,
            ["preferred_zone"] = z.key, ["preferred_zone_confidence"] = R(z.share),
            ["preferred_door"] = d.key, ["preferred_door_uses"] = (int)d.count,
            ["favourite_locker"] = l.key, ["favourite_locker_uses"] = (int)l.count,
            ["distractions_thrown"] = throws, ["distractions_detected"] = distractionsCaught,
            ["noise_trust"] = R(noiseTrust), ["capture_count"] = captures.Count, ["sightings"] = sightings,
            ["playstyle"] = Playstyle };
    }

    public string PreferredZone => Top(zoneVisits).key;
    public float PreferredZoneShare => Top(zoneVisits).share;
    public string PreferredDoor => Top(doorPasses).key;
    public int PreferredDoorUses => (int)Top(doorPasses).count;
    public string FavouriteLocker => Top(lockerUses).key;

    // deception mechanic: each bottle it falls for lowers its trust in noises
    public void OnDistractionFound() { distractionsCaught++; noiseTrust = Mathf.Max(0.2f, noiseTrust - 0.2f); }
    public void OnRealNoiseConfirmed() { noiseTrust = Mathf.Min(1f, noiseTrust + 0.1f); }

    static float R(float v) => Mathf.Round(v * 100f) / 100f;
}
