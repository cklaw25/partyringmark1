// HTTP link to the Python strategist (slow path) + voice. Never blocks the game: coroutines with timeouts.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable] public class DecisionDto { public string action, target, reason, prediction, dialogue; public float confidence; }
[Serializable] public class DecideResponse { public bool ok; public DecisionDto decision; public string error; public int server_ms; public string model; }

public class StrategistClient : MonoBehaviour
{
    public string baseUrl = "http://localhost:8000";
    public bool online, busy;
    public string model = "-";
    public List<int> latencies = new List<int>();
    float nextHealth;

    void Update()
    {
        if (Time.time > nextHealth) { nextHealth = Time.time + 3f; StartCoroutine(Health()); }
    }

    IEnumerator Health()
    {
        using var req = UnityWebRequest.Get(baseUrl + "/health");
        req.timeout = 2;
        yield return req.SendWebRequest();
        online = req.result == UnityWebRequest.Result.Success && req.downloadHandler.text.Contains("\"key_set\":true");
    }

    public void Decide(string stateJson, float timeout, Action<Decision> done)
    {
        if (!online || busy) { done(null); return; }
        StartCoroutine(DecideCo(stateJson, timeout, done));
    }

    IEnumerator DecideCo(string body, float timeout, Action<Decision> done)
    {
        busy = true;
        float t0 = Time.realtimeSinceStartup;
        using var req = new UnityWebRequest(baseUrl + "/decide", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = Mathf.CeilToInt(timeout);
        yield return req.SendWebRequest();
        busy = false;
        int ms = Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000);
        if (req.result != UnityWebRequest.Result.Success) { done(null); yield break; }
        DecideResponse r = null;
        try { r = JsonUtility.FromJson<DecideResponse>(req.downloadHandler.text); } catch { }
        if (r == null || !r.ok || r.decision == null || string.IsNullOrEmpty(r.decision.action)) { done(null); yield break; }
        latencies.Add(ms); if (latencies.Count > 50) latencies.RemoveAt(0);
        model = r.model;
        done(new Decision { action = r.decision.action, target = string.IsNullOrEmpty(r.decision.target) ? null : r.decision.target, reason = r.decision.reason,
            prediction = r.decision.prediction, dialogue = r.decision.dialogue, confidence = r.decision.confidence, latencyMs = ms, source = "claude" });
    }

    // voice: the backend speaks on this laptop (Windows speech now; ElevenLabs later)
    public void Say(string text, bool interrupt)
    {
        StartCoroutine(SayCo(text, interrupt));
    }

    IEnumerator SayCo(string text, bool interrupt)
    {
        string body = MiniJson.Write(new Dictionary<string, object> { ["text"] = text, ["interrupt"] = interrupt });
        using var req = new UnityWebRequest(baseUrl + "/say", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 2;
        yield return req.SendWebRequest();
    }

    public int Last => latencies.Count > 0 ? latencies[latencies.Count - 1] : -1;
    public int P95
    {
        get
        {
            if (latencies.Count == 0) return -1;
            var s = new List<int>(latencies); s.Sort();
            return s[Mathf.Min(s.Count - 1, (int)(s.Count * 0.95f))];
        }
    }
}
