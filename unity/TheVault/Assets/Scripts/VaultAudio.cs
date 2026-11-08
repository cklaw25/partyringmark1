// Sound for THE VAULT: every clip is synthesised by SynthGen at startup, unless a real recording is dropped into
// Assets/Resources/Audio/<name>.wav (or <name>_1, <name>_2 ... for variants) - then that is used instead.
// Pooled one-shots (2D or positional), ambience loops, the threat drone, distant noises, the heartbeat, and
// muffling while hidden.
using System.Collections.Generic;
using UnityEngine;

public class VaultAudio : MonoBehaviour
{
    readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
    readonly List<AudioSource> pool = new List<AudioSource>(); int poolNext;
    AudioSource hum, drone, slide, breath, klaxon;
    AudioLowPassFilter muffle;
    Transform listener;

    // driven by VaultGame every frame
    [Range(0, 1)] public float danger, threat, slideLevel;
    public bool hidden, alarm;
    public float humVolume = 0.03f, masterVolume = 1f;
    public System.Action OnBeat;
    float nextBeat, nextAmbient;
    static readonly string[] AMBIENT = { "amb_creak", "amb_knock", "amb_vent", "amb_steps" };

    public void Init(Transform listenerTransform)
    {
        listener = listenerTransform;
        foreach (var (n, vars, _) in SynthGen.SOUNDS) clips[n] = Load(n, vars);
        for (int i = 0; i < 28; i++)
        {
            var go = new GameObject("sfx" + i); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.dopplerLevel = 0; s.rolloffMode = AudioRolloffMode.Logarithmic;
            pool.Add(s);
        }
        hum = Loop("hum"); drone = Loop("drone"); slide = Loop("slide"); breath = Loop("breath"); klaxon = Loop("klaxon");
        muffle = listener.gameObject.AddComponent<AudioLowPassFilter>(); muffle.cutoffFrequency = 22000;
        nextAmbient = Time.time + 12f;
    }

    AudioClip[] Load(string n, int vars)
    {
        var list = new List<AudioClip>();
        var single = Resources.Load<AudioClip>("Audio/" + n); if (single != null) list.Add(single);
        for (int i = 1; i <= 8; i++) { var c = Resources.Load<AudioClip>($"Audio/{n}_{i}"); if (c != null) list.Add(c); }
        if (list.Count > 0) return list.ToArray();
        var arr = new AudioClip[vars];
        for (int v = 0; v < vars; v++)
        {
            var data = SynthGen.Make(n, v);
            arr[v] = AudioClip.Create($"{n}_{v}", data.Length, 1, SynthGen.SR, false); arr[v].SetData(data, 0);
        }
        return arr;
    }

    AudioSource Loop(string n)
    {
        var s = gameObject.AddComponent<AudioSource>(); s.clip = clips[n][0]; s.loop = true; s.volume = 0; s.spatialBlend = 0; s.playOnAwake = false; s.Play();
        return s;
    }

    // one-shot; pos = null plays it "in your head" (2D), otherwise positional out to maxDist metres
    public AudioSource Play(string n, Vector3? pos = null, float vol = 1f, float pitchJitter = 0.06f, float maxDist = 30f, float pitch = 1f)
    {
        if (!clips.TryGetValue(n, out var arr) || arr.Length == 0 || pool.Count == 0) return null;
        AudioSource s = null;
        for (int k = 0; k < pool.Count; k++) { var c = pool[(poolNext + k) % pool.Count]; if (!c.isPlaying) { s = c; poolNext = (poolNext + k + 1) % pool.Count; break; } }
        if (s == null) { s = pool[poolNext]; poolNext = (poolNext + 1) % pool.Count; }
        s.Stop(); s.clip = arr[Random.Range(0, arr.Length)];
        s.volume = vol * masterVolume; s.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        if (pos.HasValue) { s.transform.position = pos.Value; s.spatialBlend = 1f; s.minDistance = 1.5f; s.maxDistance = maxDist; }
        else s.spatialBlend = 0f;
        s.Play();
        return s;
    }

    void Update()
    {
        if (listener == null) return;
        float dt = Time.deltaTime;
        // heartbeat: starts quiet and slow, gets loud and fast as THE BROKER closes in
        if (danger > 0.03f && Time.time >= nextBeat)
        {
            float bpm = Mathf.Lerp(60f, 160f, danger);
            nextBeat = Time.time + 60f / bpm;
            Play("heartbeat", null, Mathf.Lerp(0.12f, 1f, danger), 0.02f);
            OnBeat?.Invoke();
        }
        else if (danger <= 0.03f) nextBeat = Mathf.Min(nextBeat, Time.time + 0.3f);

        float k = 1f - Mathf.Exp(-dt * 3f);
        hum.volume = Mathf.Lerp(hum.volume, humVolume * masterVolume, k);
        drone.volume = Mathf.Lerp(drone.volume, (0.05f + 0.18f * threat + 0.15f * danger) * masterVolume, k * 0.5f);
        slide.volume = Mathf.Lerp(slide.volume, slideLevel * 0.45f * masterVolume, 1f - Mathf.Exp(-dt * 14f)); slide.pitch = 0.8f + slideLevel * 0.45f;
        breath.volume = Mathf.Lerp(breath.volume, (hidden ? 0.3f + 0.3f * danger : 0f) * masterVolume, k);
        breath.pitch = 1f + danger * 0.35f;
        klaxon.volume = Mathf.Lerp(klaxon.volume, (alarm ? 0.16f : 0f) * masterVolume, k * 2f);
        muffle.cutoffFrequency = Mathf.Lerp(muffle.cutoffFrequency, hidden ? 1600f : 22000f, 1f - Mathf.Exp(-dt * 6f));

        // something, somewhere, in the dark
        if (Time.time > nextAmbient)
        {
            nextAmbient = Time.time + Random.Range(15f, 40f);
            float ang = Random.value * Mathf.PI * 2, dist = Random.Range(14f, 24f);
            var p = listener.position + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * dist;
            Play(AMBIENT[Random.Range(0, AMBIENT.Length)], p, 0.55f, 0.12f, 45f);
        }
    }
}
