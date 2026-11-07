// Visible sweeping security camera with a light cone. THE BROKER only learns your position from these,
// its hunter's own eyes, sounds and escape-room alerts. Crouching gets you under the cone.
using UnityEngine;

public class SecurityCamera : MonoBehaviour
{
    public string id; public Tile tile; public float baseYaw, sweep = 60f, speed = 0.6f, fov = 50f; public int rangeTiles = 5;
    public Light cone; public float alertUntil, cooldownUntil; public int spots;
    float phase;

    public void Build(string camId, Tile t, Vector3 pos, float yaw)
    {
        id = camId; tile = t; baseYaw = yaw; phase = Random.value * 10f;
        transform.position = pos;
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false); body.transform.localScale = new Vector3(0.25f, 0.2f, 0.45f);
        body.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.17f);
        var led = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(led.GetComponent<Collider>());
        led.transform.SetParent(transform, false); led.transform.localPosition = new Vector3(0, 0, 0.24f); led.transform.localScale = Vector3.one * 0.08f;
        var m = led.GetComponent<Renderer>().material; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(3, 0, 0));
        cone = new GameObject("cone").AddComponent<Light>(); cone.transform.SetParent(transform, false);
        cone.type = LightType.Spot; cone.spotAngle = fov; cone.range = rangeTiles * VaultGame.TILE + 2; cone.intensity = 2.2f; cone.color = new Color(1f, 0.55f, 0.4f);
    }

    public Vector3 Forward => transform.forward;

    void Update()
    {
        float yaw = baseYaw + Mathf.Sin(Time.time * speed + phase) * sweep;
        transform.rotation = Quaternion.Euler(28f, yaw, 0);
        cone.color = Time.time < alertUntil ? new Color(1f, 0.05f, 0.05f) : new Color(1f, 0.55f, 0.4f);
        cone.intensity = Time.time < alertUntil ? 4f : 2.2f;
    }

    public bool CanSee(VaultMaze maze, Tile p, Vector3 playerPos, bool crouched, bool hidden)
    {
        if (hidden || crouched) return false;
        int d = Mathf.Abs(p.x - tile.x) + Mathf.Abs(p.y - tile.y);
        if (d > rangeTiles || d == 0 && false) return false;
        Vector3 to = playerPos - transform.position; to.y = 0;
        Vector3 f = Forward; f.y = 0;
        if (to.sqrMagnitude > 0.01f && Vector3.Angle(f, to) > fov * 0.5f + 8f) return false;
        return maze.LineOfSight(tile.x, tile.y, p.x, p.y);
    }
}
