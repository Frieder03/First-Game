using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Bewegung")]
    [Tooltip("Beschleunigung bei gedrückter Taste (Kraft pro Sekunde).")]
    public float thrustForce = 12f;

    [Tooltip("Maximale Fluggeschwindigkeit.")]
    public float maxSpeed = 10f;

    [Tooltip("Wie schnell die Nase Richtung Maus dreht (Grad/Sek.).")]
    public float turnSpeed = 720f;

    [Tooltip("Leichter passiver Bremsfaktor (0 = kein Bremsen).")]
    [Range(0f, 10f)] public float passiveBrake = 1.0f;

    [Header("Eingaben")]
    [Tooltip("InputAction für Schub (z. B. Taste W / Space / Gamepad).")]
    public InputAction ThrustAction; // Button oder 1D Axis

    [Tooltip("Optionale Zusatzsteuerung (WASD/Stick)")]
    public InputAction MoveAction; // Vector2 (-1..1). Optional.

    [Tooltip("1D Axis Composite: Negative = A, Positive = D. Triggert Dash links/rechts.")]
    public InputAction DashAction; // float: -1 (links) ... +1 (rechts)

    [Header("Dash")]
    [Tooltip("Reale Strecke des Dashs in Welt-Einheiten.")]
    public float dashDistance = 4.0f;

    [Tooltip("Dauer des Dashs in Sekunden.")]
    public float dashTime = 0.12f;

    [Tooltip("Sekunden bis Dash wieder verfügbar ist.")]
    public float dashCooldown = 0.35f;

    [Tooltip("Unverwundbar während dieses Anteils der Dash-Zeit.")]
    public float dashInvulnerability = 0.10f;

    [Tooltip("Bewegungskurve (0..1 über die Dash-Zeit).")]
    public AnimationCurve dashEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Optional: TrailRenderer für Nachzieh-Effekt (wird während Dash kurz aktiviert).")]
    public TrailRenderer dashTrail;

    Rigidbody2D rb;
    Camera cam;

    // intern
    bool isDashing = false;
    float lastDashEnd = -999f;
    bool invulnerable = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;

        // Empfohlene Defaults
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;          // Bremsen steuern wir selbst über passiveBrake
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (dashTrail != null) dashTrail.emitting = false;
    }

    void OnEnable()
    {
        if (MoveAction != null) MoveAction.Enable();
        if (ThrustAction != null) ThrustAction.Enable();
        if (DashAction != null) DashAction.Enable();
    }

    void OnDisable()
    {
        if (MoveAction != null) MoveAction.Disable();
        if (ThrustAction != null) ThrustAction.Disable();
        if (DashAction != null) DashAction.Disable();
    }

    void Update()
    {
        // === 1) Blickrichtung zur Maus drehen (alles als Vector2 rechnen) ===
        Vector2 mouseScreen = Mouse.current != null ? (Vector2)Mouse.current.position.ReadValue() : Vector2.zero;
        Vector3 mouseWorld3 = cam != null ? cam.ScreenToWorldPoint((Vector3)mouseScreen) : Vector3.zero;
        Vector2 mouseWorld = (Vector2)mouseWorld3;

        Vector2 toMouse = mouseWorld - rb.position; // reine 2D-Rechnung, keine V2/V3-Mischung
        if (toMouse.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg - 90f; // Sprite-Nase zeigt +Y
            float current = transform.eulerAngles.z;
            float next = Mathf.MoveTowardsAngle(current, targetAngle, turnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, next);
        }

        // === 2) Dash-Eingabe (Edge-Trigger) ===
        if (!isDashing && DashAction != null && DashAction.triggered)
        {
            float dir = Mathf.Sign(DashAction.ReadValue<float>()); // -1 = links, +1 = rechts
            if (Mathf.Abs(dir) > 0.1f && (Time.time >= lastDashEnd + dashCooldown))
            {
                StartCoroutine(DashRoutine(dir));
            }
        }

        // === 3) Thrust über InputAction (wenn nicht dashing) ===
        bool thrusting = !isDashing && ThrustAction != null && ThrustAction.ReadValue<float>() > 0.5f;
        if (thrusting)
        {
            rb.AddForce((Vector2)transform.up * thrustForce, ForceMode2D.Force); // transform.up -> Vector2
        }

        // === 4) Optionale Zusatzsteuerung (WASD/Stick) als feine Korrektur (wenn nicht dashing) ===
        if (!isDashing && MoveAction != null && MoveAction.enabled)
        {
            Vector2 move = MoveAction.ReadValue<Vector2>();  // -1..1
            Vector2 local = new Vector2(move.x, move.y) * thrustForce * 0.6f;

            // In Welt transformieren, dann als Vector2 weiterverwenden
            Vector2 world = (Vector2)transform.TransformVector((Vector3)local);
            rb.AddForce(world * Time.deltaTime, ForceMode2D.Impulse);
        }

        // === 5) Passives, sanftes Abbremsen (im „Weltraum“-Stil) ===
        if (!isDashing && !thrusting && passiveBrake > 0f && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            float reduce = passiveBrake * Time.deltaTime;
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, reduce);
        }

        // === 6) Maximalgeschwindigkeit begrenzen (außer während Dash) ===
        if (!isDashing)
        {
            float speed = rb.linearVelocity.magnitude;
            if (speed > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }
        }
    }

    IEnumerator DashRoutine(float dir)
    {
        isDashing = true;
        invulnerable = true;

        if (dashTrail != null) dashTrail.emitting = true;

        Vector2 startPos = rb.position;
        Vector2 right = (Vector2)transform.right;                 // << fix
        Vector2 dashVec = right * Mathf.Sign(dir) * dashDistance;

        // vorhandene Geschwindigkeit "merken", dann nullen
        Vector2 preVel = rb.linearVelocity;
        rb.linearVelocity = Vector2.zero;

        float t = 0f;
        while (t < dashTime)
        {
            float u = t / dashTime;             // 0..1
            float eased = dashEase.Evaluate(u); // Kurve
            Vector2 target = startPos + dashVec * eased;  // alles Vector2

            rb.MovePosition(target);
            t += Time.deltaTime;

            if (t >= dashInvulnerability) invulnerable = false;

            yield return null;
        }

        // final exakt setzen
        rb.MovePosition(startPos + dashVec);

        // leichte Restgeschwindigkeit quer + etwas der alten beibehalten (Juice)
        rb.linearVelocity = (Vector2)transform.right * Mathf.Sign(dir) * (dashDistance / dashTime) * 0.25f
                            + preVel * 0.15f;

        if (dashTrail != null) dashTrail.emitting = false;

        isDashing = false;
        lastDashEnd = Time.time;
        invulnerable = false;
    }

    // Beispiel: fürs Damage-System
    public bool IsInvulnerable() => invulnerable;

    // Gizmos (nur Editor)
    void OnDrawGizmosSelected()
    {
        if (Camera.main == null || Mouse.current == null) return;
        Vector2 mouseScreen = (Vector2)Mouse.current.position.ReadValue();
        Vector3 mw3 = Camera.main.ScreenToWorldPoint((Vector3)mouseScreen);
        Vector2 mouseWorld = (Vector2)mw3;

        Gizmos.DrawWireSphere((Vector3)mouseWorld, 0.2f);
        Gizmos.DrawLine(transform.position, (Vector3)mouseWorld);
    }
}
