// InfiniteBG2D.cs
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class InfiniteBG2D : MonoBehaviour
{
    public Transform target;               // Player oder Kamera
    public Camera cam;                     // Main Camera
    [Tooltip("Wie viele Weltmeter entsprechen 1 Texturkachel?")]
    public Vector2 worldTileSize = new(10, 10);
    [Tooltip("0 = klebt an Kamera, 1 = folgt exakt dem Player")]
    public Vector2 parallax = new(0.5f, 0.5f);
    [Tooltip("Wie weit hinter dem Player (größere Zahl = weiter hinten)")]
    public float depthBehindTarget = 1f;   // Player meist z=0 -> BG bei z=+1

    MeshRenderer mr;
    Material mat;

    void Awake()
    {
        mr = GetComponent<MeshRenderer>();
        mat = mr.material;                 // Instanz, nicht sharedMaterial
        if (!cam) cam = Camera.main;
        FitToCamera();
    }

    void LateUpdate()
    {
        if (!target || !cam) return;

        Vector3 p = target.position;
        // UV-Offset aus Weltposition ableiten
        Vector2 uv = new(
            (p.x * parallax.x) / Mathf.Max(0.0001f, worldTileSize.x),
            (p.y * parallax.y) / Mathf.Max(0.0001f, worldTileSize.y)
        );

        // Built-in
        mat.mainTextureOffset = uv;
        // URP (falls Shader _BaseMap nutzt)
        if (mat.HasProperty("_BaseMap"))
            mat.SetTextureOffset("_BaseMap", uv);

        // Quad immer mittig vor der Kamera halten (füllt Screen)
        var c = cam.transform.position;
        float z = target ? target.position.z + depthBehindTarget : 0f;
        transform.position = new Vector3(c.x, c.y, z);
    }

    // Skaliert das Quad, sodass es den ganzen View füllt (Orthographic)
    void FitToCamera()
    {
        if (!cam || !cam.orthographic) return;
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        transform.localScale = new Vector3(w, h, 1f);
    }

    void OnValidate()
    {
        if (cam && cam.orthographic) FitToCamera();
    }
}
