// First-person player: WASD + mouse look, SHIFT run (loud), flashlight. Hiding is handled by VaultGame.
using UnityEngine;

public class FirstPersonPlayer : MonoBehaviour
{
    public VaultGame game;
    public CharacterController cc;
    public Camera cam;
    public Light flashlight;
    public bool hidden, running, moving, seenEnteringLocker;
    public Locker locker;
    public Vector3 hideReturnPos;
    float pitch, yaw;
    public float walkSpeed = 4.2f, runSpeed = 7.2f, mouseSens = 2.2f;

    public Tile T => game.TileOf(transform.position);

    public void Build(VaultGame g, Vector3 pos)
    {
        game = g;
        transform.position = pos + Vector3.up * 1f;
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.7f; cc.radius = 0.35f; cc.center = Vector3.zero;
        var camGo = new GameObject("PlayerCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0, 0.65f, 0);
        cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f; cam.fieldOfView = 70; cam.backgroundColor = Color.black; cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.AddComponent<AudioListener>();
        var fl = new GameObject("Flashlight");
        fl.transform.SetParent(camGo.transform, false);
        flashlight = fl.AddComponent<Light>();
        flashlight.type = LightType.Spot; flashlight.range = 16; flashlight.spotAngle = 60; flashlight.intensity = 2.2f;
        flashlight.color = new Color(1f, 0.95f, 0.85f); flashlight.shadows = LightShadows.Soft;
    }

    public void Look(float yawDeg) { yaw = yawDeg; transform.rotation = Quaternion.Euler(0, yaw, 0); }

    public void Tick(float dt)
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxisRaw("Mouse X") * mouseSens;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * mouseSens, -80, 80);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
        if (hidden) { moving = running = false; return; }
        Vector3 input = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), 0,
                                    (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        moving = input.sqrMagnitude > 0.01f;
        running = moving && Input.GetKey(KeyCode.LeftShift);
        Vector3 move = transform.TransformDirection(input.normalized) * (running ? runSpeed : walkSpeed);
        move.y = -9f;
        cc.Move(move * dt);
        // head bob
        float bob = moving ? Mathf.Sin(Time.time * (running ? 14 : 9)) * (running ? 0.06f : 0.03f) : 0;
        cam.transform.localPosition = new Vector3(0, 0.65f + bob, 0);
    }

    // dominant grid direction the player is facing (for throwing)
    public Tile FacingDir()
    {
        Vector3 f = transform.forward;
        if (Mathf.Abs(f.x) > Mathf.Abs(f.z)) return new Tile(f.x > 0 ? 1 : -1, 0);
        return new Tile(0, f.z > 0 ? -1 : 1); // world +z = grid -y (north)
    }

    public void Teleport(Vector3 p) { cc.enabled = false; transform.position = p; cc.enabled = true; }
}
