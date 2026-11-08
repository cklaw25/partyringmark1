// First-person player: momentum-based movement (walk / run / crouch, jump, air-slide chaining), camera feel, flashlight.
// Hiding is handled by VaultGame.
//
// Slide chaining: jump, press C in mid-air for a small boost, keep C held to land into a slide (friction slowly eats
// the speed), jump again quickly to keep it. Landing WITHOUT a slide drops you back to run speed. The boost shrinks as
// you approach maxSpeed, so good chaining settles around ~8.5-9.5 u/s versus 7.2 for a plain run - and it is loud.
using UnityEngine;

public class FirstPersonPlayer : MonoBehaviour
{
    public VaultGame game;
    public CharacterController cc;
    public Camera cam;
    public Light flashlight;
    public bool hidden, running, moving, crouched, frozen, seenEnteringLocker;
    public Locker locker;
    public Vector3 hideReturnPos;
    float pitch, yaw;

    [Header("Ground")]
    public float walkSpeed = 4.2f, runSpeed = 7.2f, crouchSpeed = 2.2f, mouseSens = 2.2f;
    public float groundAccel = 50f, groundDecel = 40f;
    [Header("Air / jump")]
    public float gravity = 22f, jumpVelocity = 6.5f, airAccel = 8f, coyoteTime = 0.1f, jumpBuffer = 0.15f;
    [Header("Slide")]
    public float slideBoost = 1.5f, slideFriction = 7f, slideSteer = 2f, maxSpeed = 10f;
    [Header("Camera feel")]
    [Range(0, 1)] public float headBobScale = 1f;
    public float baseFov = 70f, maxFovBoost = 10f;
    public bool debugSpeed; // F3 toggles a speed readout

    public enum MoveState { Ground, Air, AirSlide, GroundSlide }
    public MoveState state;
    public Vector3 velocity;
    public bool Grounded => state == MoveState.Ground || state == MoveState.GroundSlide;
    public bool Sliding => state == MoveState.AirSlide || state == MoveState.GroundSlide;
    public float HorizontalSpeed => new Vector3(velocity.x, 0, velocity.z).magnitude;
    // read by VaultGame after Tick: landing makes noise
    public bool landedThisFrame; public float landSpeed;
    public System.Action onStep; // fires on each footstep (hook for future footstep audio)

    float coyote, buffer, bobPhase, bobBlend, landDip, landDipVel, roll, fovKick, shake, eyeY = 0.65f, peakSpeed;
    bool slideUsed;

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
        cam.nearClipPlane = 0.05f; cam.fieldOfView = baseFov; cam.backgroundColor = Color.black; cam.clearFlags = CameraClearFlags.SolidColor;
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
        landedThisFrame = false;
        if (Input.GetKeyDown(KeyCode.F3)) debugSpeed = !debugSpeed;
        if (Cursor.lockState == CursorLockMode.Locked && !frozen)
        {
            yaw += Input.GetAxisRaw("Mouse X") * mouseSens;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * mouseSens, -80, 80);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
        if (hidden || frozen)
        {
            moving = running = crouched = false; velocity = Vector3.zero; state = MoveState.Ground; buffer = 0;
            cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            return;
        }

        // ---- input ----
        bool cHeld = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
        bool cDown = Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl);
        bool shift = Input.GetKey(KeyCode.LeftShift);
        Vector3 input = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), 0,
                                    (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        moving = input.sqrMagnitude > 0.01f;
        Vector3 wishDir = transform.TransformDirection(input.normalized);
        if (Input.GetKeyDown(KeyCode.Space)) buffer = jumpBuffer;
        buffer -= dt; coyote -= dt;

        Vector3 h = new Vector3(velocity.x, 0, velocity.z);
        float spd = h.magnitude;

        // ---- air slide: one boost per airtime, shrinking as you near maxSpeed ----
        if (state == MoveState.Air && cDown && !slideUsed)
        {
            slideUsed = true; state = MoveState.AirSlide;
            Vector3 dir = spd > 0.1f ? h / spd : (moving ? wishDir : transform.forward);
            float room = Mathf.Clamp01((maxSpeed - spd) / (maxSpeed - runSpeed));
            spd = Mathf.Min(maxSpeed, spd + slideBoost * Mathf.Max(room, spd < runSpeed ? 1f : 0f));
            h = dir * spd;
            fovKick = 4f;
        }

        // ---- horizontal velocity per state ----
        switch (state)
        {
            case MoveState.Ground:
            {
                bool crouch = cHeld;
                float target = crouch ? crouchSpeed : (shift ? runSpeed : walkSpeed);
                Vector3 want = moving ? wishDir * target : Vector3.zero;
                float rate = moving && spd <= target + 0.01f ? groundAccel : groundDecel;
                h = Vector3.MoveTowards(h, want, rate * dt);
                break;
            }
            case MoveState.GroundSlide:
            {
                spd = Mathf.Max(0, spd - slideFriction * dt);
                Vector3 dir = spd > 0.01f ? h.normalized : transform.forward;
                if (moving) dir = Vector3.MoveTowards(dir, wishDir, slideSteer * dt).normalized;
                h = dir * spd;
                if (!cHeld || spd <= runSpeed) state = MoveState.Ground;
                break;
            }
            default: // Air / AirSlide: keep momentum, a little steering, never gain speed from steering
                if (moving)
                {
                    float cap = Mathf.Max(spd, shift ? runSpeed : walkSpeed);
                    h = Vector3.MoveTowards(h, wishDir * cap, airAccel * dt);
                    if (h.magnitude > cap) h = h.normalized * cap;
                }
                break;
        }

        // ---- jump (buffered, with coyote time) ----
        if (buffer > 0 && (Grounded || coyote > 0))
        {
            velocity.y = jumpVelocity;
            state = MoveState.Air; slideUsed = false; buffer = 0; coyote = 0;
        }

        // ---- vertical ----
        if (Grounded) velocity.y = -2f;
        else velocity.y -= gravity * dt;
        float fallSpeed = -velocity.y;

        velocity = new Vector3(h.x, velocity.y, h.z);
        Vector3 before = transform.position;
        var flags = cc.Move(velocity * dt);
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
        if ((flags & CollisionFlags.Sides) != 0 && dt > 0)
        {
            // walls eat momentum: keep only the movement that actually happened
            Vector3 actual = (transform.position - before) / dt; actual.y = 0;
            if (actual.magnitude < h.magnitude) { velocity.x = actual.x; velocity.z = actual.z; }
        }

        // ---- ground transitions ----
        if (!Grounded && cc.isGrounded && velocity.y <= 0)
        {
            landedThisFrame = true; landSpeed = HorizontalSpeed;
            if (state == MoveState.AirSlide && cHeld) state = MoveState.GroundSlide;
            else
            {
                state = MoveState.Ground;
                // a plain landing loses the slide bonus
                Vector3 hv = new Vector3(velocity.x, 0, velocity.z);
                if (hv.magnitude > runSpeed) { hv = hv.normalized * runSpeed; velocity.x = hv.x; velocity.z = hv.z; }
            }
            velocity.y = -2f;
            landDipVel = -Mathf.Clamp(fallSpeed * 0.06f, 0, 0.9f);
            shake = Mathf.Clamp01((fallSpeed - 5f) / 5f) * 0.04f;
        }
        else if (Grounded && !cc.isGrounded)
        {
            state = MoveState.Air; coyote = coyoteTime; slideUsed = false; velocity.y = 0;
        }

        // ---- flags the rest of the game reads ----
        float hs = HorizontalSpeed;
        crouched = state == MoveState.Ground && cHeld;
        // jumping, sliding and going faster than a run are all loud
        running = (state == MoveState.Ground && moving && shift && !crouched) || state != MoveState.Ground || hs > runSpeed + 0.1f;
        peakSpeed = Mathf.Max(peakSpeed * Mathf.Exp(-dt * 0.5f), hs);

        UpdateCamera(dt, input, hs);
    }

    void UpdateCamera(float dt, Vector3 input, float hs)
    {
        float k = headBobScale;
        // head bob driven by distance travelled, so it can never drift out of step with the feet
        bool bobbing = state == MoveState.Ground && moving && hs > 0.3f;
        bobBlend = Mathf.MoveTowards(bobBlend, bobbing ? 1 : 0, dt * 6f);
        if (bobbing)
        {
            int step = Mathf.FloorToInt(bobPhase / Mathf.PI);
            bobPhase += hs * dt;
            if (Mathf.FloorToInt(bobPhase / Mathf.PI) != step) onStep?.Invoke();
        }
        float run01 = Mathf.Clamp01((hs - walkSpeed) / (runSpeed - walkSpeed));
        float amp = (crouched ? 0.015f : Mathf.Lerp(0.03f, 0.07f, run01)) * bobBlend * k;
        float bobY = Mathf.Sin(bobPhase * 2f) * amp;
        float bobX = Mathf.Sin(bobPhase) * amp * 0.7f;

        // landing dip: damped spring
        landDipVel += (-landDip * 140f - landDipVel * 16f) * dt;
        landDip += landDipVel * dt;
        shake = Mathf.MoveTowards(shake, 0, dt * 0.15f);
        Vector3 shakeOff = shake > 0 ? Random.insideUnitSphere * shake * k : Vector3.zero;

        // eye height: slide lowest, crouch low, stand normal
        float eyeTarget = Sliding ? -0.35f : crouched ? -0.25f : 0.65f;
        eyeY = Mathf.Lerp(eyeY, eyeTarget, (Sliding ? 16f : 12f) * dt);

        // roll: lean into strafes, tilt while sliding
        float targetRoll = (-input.x * 2f + (Sliding ? 4f : 0f)) * k;
        roll = Mathf.Lerp(roll, targetRoll, 8f * dt);

        cam.transform.localPosition = new Vector3(bobX, eyeY + bobY + landDip * k, 0) + shakeOff;
        cam.transform.localRotation = Quaternion.Euler(pitch, 0, roll);

        // FOV: widens with speed so it reads on screen, plus a kick on each slide
        fovKick = Mathf.MoveTowards(fovKick, 0, dt * 8f);
        float speed01 = Mathf.Clamp01((hs - walkSpeed) / (maxSpeed - walkSpeed));
        float fovTarget = baseFov + (maxFovBoost * speed01 + fovKick) * k;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovTarget, 10f * dt);
    }

    void OnGUI()
    {
        if (!debugSpeed) return;
        GUI.Label(new Rect(Screen.width / 2 - 120, Screen.height - 60, 240, 50),
            $"<b>{HorizontalSpeed:0.0} u/s</b>  (peak {peakSpeed:0.0})\n{state}",
            new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 16, normal = { textColor = Color.white } });
    }

    // dominant grid direction the player is facing (for throwing)
    public Tile FacingDir()
    {
        Vector3 f = transform.forward;
        if (Mathf.Abs(f.x) > Mathf.Abs(f.z)) return new Tile(f.x > 0 ? 1 : -1, 0);
        return new Tile(0, f.z > 0 ? -1 : 1); // world +z = grid -y (north)
    }

    public void Teleport(Vector3 p)
    {
        cc.enabled = false; transform.position = p; cc.enabled = true;
        velocity = Vector3.zero; state = MoveState.Ground; buffer = 0;
    }
}
