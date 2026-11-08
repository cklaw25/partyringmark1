// Visible sweeping security camera with a light cone and a faint visible beam. THE BROKER only learns your position
// from these, its hunter's own eyes, sounds and escape-room alerts. Crouching gets you under the cone.
using UnityEngine;
using A = VaultArt;

public class SecurityCamera : MonoBehaviour
{
    public string id; public Tile tile; public float baseYaw, sweep = 60f, speed = 0.6f, fov = 50f; public int rangeTiles = 5;
    public Light cone; public float alertUntil, cooldownUntil; public int spots;
    Transform pivot; Material beamMat, ledMat; Renderer led;
    float phase;

    public void Build(string camId, Tile t, Vector3 pos, float yaw)
    {
        id = camId; tile = t; baseYaw = yaw; phase = Random.value * 10f;
        transform.position = pos;
        // ceiling mount (static) + pivoting head
        A.Cyl("pole", transform, A.DarkSteel, new Vector3(0, (VaultGame.WALL_H - pos.y) / 2, 0), 0.025f, VaultGame.WALL_H - pos.y);
        A.Cube("plate", transform, A.DarkSteel, new Vector3(0, VaultGame.WALL_H - pos.y - 0.01f, 0), new Vector3(0.16f, 0.02f, 0.16f));
        pivot = new GameObject("pivot").transform; pivot.SetParent(transform, false);
        A.Cube("housing", pivot, A.Plain(new Color(0.75f, 0.74f, 0.7f), 0.4f), new Vector3(0, 0, 0.08f), new Vector3(0.17f, 0.15f, 0.36f));
        A.Cube("hood", pivot, A.Plain(new Color(0.65f, 0.64f, 0.6f), 0.4f), new Vector3(0, 0.09f, 0.12f), new Vector3(0.21f, 0.025f, 0.42f));
        A.Cyl("lens", pivot, A.Plastic, new Vector3(0, 0, 0.27f), 0.05f, 0.04f, Quaternion.Euler(90, 0, 0));
        A.Cyl("glass", pivot, A.Glow(new Color(0.2f, 0.25f, 0.35f), 0.5f), new Vector3(0, 0, 0.292f), 0.035f, 0.005f, Quaternion.Euler(90, 0, 0));
        ledMat = A.Plain(new Color(0.5f, 0, 0), 0.5f, 0, new Color(3, 0, 0));
        led = A.Cube("led", pivot, ledMat, new Vector3(0.06f, 0.05f, 0.265f), new Vector3(0.025f, 0.025f, 0.01f)).GetComponent<Renderer>();
        float len = rangeTiles * VaultGame.TILE * 0.85f;
        beamMat = A.Additive(Texture2D.whiteTexture, new Color(1f, 0.55f, 0.4f, 0.045f));
        var beam = A.Part("beam", pivot, A.Cone(len, Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * len * 0.85f), beamMat, new Vector3(0, 0, 0.3f), Vector3.one);
        beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cone = new GameObject("cone").AddComponent<Light>(); cone.transform.SetParent(pivot, false); cone.transform.localPosition = new Vector3(0, 0, 0.3f);
        cone.type = LightType.Spot; cone.spotAngle = fov; cone.range = rangeTiles * VaultGame.TILE + 2; cone.intensity = 2.2f; cone.color = new Color(1f, 0.55f, 0.4f);
    }

    public Vector3 Forward => pivot.forward;

    void Update()
    {
        float yaw = baseYaw + Mathf.Sin(Time.time * speed + phase) * sweep;
        pivot.rotation = Quaternion.Euler(28f, yaw, 0);
        bool alert = Time.time < alertUntil;
        cone.color = alert ? new Color(1f, 0.05f, 0.05f) : new Color(1f, 0.55f, 0.4f);
        cone.intensity = alert ? 4f : 2.2f;
        beamMat.color = alert ? new Color(1f, 0.1f, 0.05f, 0.09f) : new Color(1f, 0.55f, 0.4f, 0.045f);
        led.enabled = alert || (Time.time * 1.2f + phase) % 1f < 0.5f; // idle blink
    }

    public bool CanSee(VaultMaze maze, Tile p, Vector3 playerPos, bool crouched, bool hidden)
    {
        if (hidden || crouched) return false;
        int d = Mathf.Abs(p.x - tile.x) + Mathf.Abs(p.y - tile.y);
        if (d > rangeTiles) return false;
        Vector3 to = playerPos - transform.position; to.y = 0;
        Vector3 f = Forward; f.y = 0;
        if (to.sqrMagnitude > 0.01f && Vector3.Angle(f, to) > fov * 0.5f + 8f) return false;
        return maze.LineOfSight(tile.x, tile.y, p.x, p.y);
    }
}
