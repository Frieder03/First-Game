using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Bewegung")]
    [Tooltip("Beschleunigung bei gedrückter Leertaste (Kraft pro Sekunde).")]
    public float thrustForce = 12f;

    [Tooltip("Maximale Fluggeschwindigkeit.")]
    public float maxSpeed = 10f;

    [Tooltip("Wie schnell die Nase Richtung Maus dreht (Grad/Sek.).")]
    public float turnSpeed = 720f;

    [Tooltip("Leichter passiver Bremsfaktor (0 = kein Bremsen).")]
    [Range(0f, 10f)] public float passiveBrake = 1.0f;

    [Header("Optionale Zusatzsteuerung (WASD/Stick)")]
    public InputAction MoveAction; // liefert Vector2 (-1..1). Optional.

    Rigidbody2D rb;
    Camera cam;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;

        // Empfohlene Rigidbody2D-Defaults für Space:
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;          // Bremsen steuern wir selbst über passiveBrake
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void OnEnable()
    {
        if (MoveAction != null) MoveAction.Enable();
    }

    void OnDisable()
    {
        if (MoveAction != null) MoveAction.Disable();
    }

    void Update()
    {
        // 1) Blickrichtung zur Maus drehen
        Vector3 mouseScreen = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
        Vector3 mouseWorld = cam != null ? cam.ScreenToWorldPoint(mouseScreen) : Vector3.zero;
        mouseWorld.z = 0f;

        Vector2 toMouse = (mouseWorld - transform.position);
        if (toMouse.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg - 90f; // Sprite-Nase zeigt +Y
            float current = transform.eulerAngles.z;
            float next = Mathf.MoveTowardsAngle(current, targetAngle, turnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, next);
        }

        // 2) Thrust per Space
        bool thrusting = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        if (thrusting)
        {
            rb.AddForce(transform.up * thrustForce, ForceMode2D.Force);
        }

        // 3) Optionale Zusatzsteuerung (WASD/Stick) als feine Korrektur
        if (MoveAction != null && MoveAction.enabled)
        {
            Vector2 move = MoveAction.ReadValue<Vector2>();  // -1..1
            Vector2 local = new Vector2(move.x, move.y) * thrustForce * 0.6f;
            Vector2 world = transform.TransformVector(local);
            rb.AddForce(world * Time.deltaTime, ForceMode2D.Impulse);
        }

        // 4) Passives, sanftes Abbremsen (im „Weltraum“-Stil)
        if (!thrusting && passiveBrake > 0f && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            float reduce = passiveBrake * Time.deltaTime;
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, reduce);
        }

        // 5) Maximalgeschwindigkeit begrenzen
        float speed = rb.linearVelocity.magnitude;
        if (speed > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }

    // Optional: kleiner Ziel-Gizmo zur Orientierung
    void OnDrawGizmosSelected()
    {
        if (Camera.main == null || Mouse.current == null) return;
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint((Vector3)Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;
        Gizmos.DrawWireSphere(mouseWorld, 0.2f);
        Gizmos.DrawLine(transform.position, mouseWorld);
    }
}
