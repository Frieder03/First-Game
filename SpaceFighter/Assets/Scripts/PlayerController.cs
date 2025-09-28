using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Bewegung")]
    public float thrustForce = 12f;
    public float maxSpeed = 10f;
    public float turnSpeed = 720f;
    [Range(0f, 10f)] public float passiveBrake = 1.0f;

    [Header("Eingaben")]
    public InputAction ThrustAction;
    public InputAction MoveAction;
    public InputAction DashAction; // -1..+1

    [Header("Dash")]
    public float dashDistance = 4.0f;
    public float dashTime = 0.12f;
    public float dashCooldown = 0.35f;
    public float dashInvulnerability = 0.10f;
    public AnimationCurve dashEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public TrailRenderer dashTrail;

    Rigidbody2D rb;
    Camera cam;

    bool isDashing = false;
    float lastDashEnd = -999f;
    bool invulnerable = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;

        rb.gravityScale = 0f;
        rb.linearDamping = 0f;                 // statt linearDamping
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (dashTrail != null) dashTrail.emitting = false;
    }

    void OnEnable()
    {
        MoveAction?.Enable();
        ThrustAction?.Enable();
        DashAction?.Enable();
    }

    void OnDisable()
    {
        MoveAction?.Disable();
        ThrustAction?.Disable();
        DashAction?.Disable();
    }

    void Update()
    {
        // 1) Blick zur Maus
        Vector2 mouseScreen = Mouse.current != null ? (Vector2)Mouse.current.position.ReadValue() : Vector2.zero;
        Vector3 mouseWorld3 = cam != null ? cam.ScreenToWorldPoint((Vector3)mouseScreen) : Vector3.zero;
        Vector2 mouseWorld = (Vector2)mouseWorld3;

        Vector2 toMouse = mouseWorld - rb.position;
        if (toMouse.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg - 90f;
            float current = transform.eulerAngles.z;
            float next = Mathf.MoveTowardsAngle(current, targetAngle, turnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, next);
        }

        // 2) Dash-Trigger
        if (!isDashing && DashAction != null && DashAction.triggered)
        {
            float dir = Mathf.Sign(DashAction.ReadValue<float>());
            if (Mathf.Abs(dir) > 0.1f && (Time.time >= lastDashEnd + dashCooldown))
            {
                StartCoroutine(DashRoutine(dir));
            }
        }

        // passives Bremsen + Clamp (nur au�erhalb Dash)
        if (!isDashing)
        {
            if (passiveBrake > 0f && rb.linearVelocity.sqrMagnitude > 0.0001f && !(ThrustAction?.ReadValue<float>() > 0.5f))
            {
                float reduce = passiveBrake * Time.deltaTime;
                rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, reduce);
            }

            float speed = rb.linearVelocity.magnitude;
            if (speed > maxSpeed) rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }

    void FixedUpdate()
    {
        if (isDashing) return;

        // Thrust
        bool thrusting = ThrustAction != null && ThrustAction.ReadValue<float>() > 0.5f;
        if (thrusting)
        {
            rb.AddForce((Vector2)transform.up * thrustForce, ForceMode2D.Force);
        }

        // Optionale Zusatzsteuerung
        if (MoveAction != null && MoveAction.enabled)
        {
            Vector2 move = MoveAction.ReadValue<Vector2>();
            Vector2 local = move * thrustForce * 0.6f;
            Vector2 world = (Vector2)transform.TransformVector((Vector3)local);
            rb.AddForce(world * Time.fixedDeltaTime, ForceMode2D.Impulse);
        }
    }

    IEnumerator DashRoutine(float dir)
    {
        isDashing = true;
        invulnerable = true;
        if (dashTrail != null) dashTrail.emitting = true;

        Vector2 startPos = rb.position;
        Vector2 right = (Vector2)transform.right;
        Vector2 dashVec = right * Mathf.Sign(dir) * dashDistance;

        Vector2 preVel = rb.linearVelocity;
        rb.linearVelocity = Vector2.zero;

        float t = 0f;
        while (t < dashTime)
        {
            float u = t / dashTime;
            float eased = dashEase.Evaluate(u);
            Vector2 target = startPos + dashVec * eased;

            rb.MovePosition(target);
            t += Time.fixedDeltaTime;

            if (t >= dashInvulnerability) invulnerable = false;
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(startPos + dashVec);

        rb.linearVelocity = (Vector2)transform.right * Mathf.Sign(dir) * (dashDistance / dashTime) * 0.25f
                      + preVel * 0.15f;

        if (dashTrail != null) dashTrail.emitting = false;

        isDashing = false;
        lastDashEnd = Time.time;
        invulnerable = false;
    }

    public bool IsInvulnerable() => invulnerable;

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
