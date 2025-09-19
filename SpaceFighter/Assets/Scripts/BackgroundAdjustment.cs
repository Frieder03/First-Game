using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundAdjustment : MonoBehaviour
{
    public Camera mainCamera;
    public SpriteRenderer spriteRenderer;
    [Tooltip("Optional: Scrollgeschwindigkeit für endloses Parallax-Feeling.")]
    public Vector2 scrollSpeed = Vector2.zero; // z.B. (0.1f, 0.1f)

    // Für URP/Built-in unterschiedliche Property-Namen (_BaseMap vs _MainTex)
    static readonly int BaseMap_ST = Shader.PropertyToID("_BaseMap_ST");
    static readonly int MainTex_ST  = Shader.PropertyToID("_MainTex_ST");

    void OnEnable()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (!mainCamera) mainCamera = Camera.main;

        // WICHTIG: Tiled-Mode aktivieren, sonst ignoriert Unity 'size'
        spriteRenderer.drawMode = SpriteDrawMode.Tiled;

        // WrapMode auf Repeat (damit die Textur gekachelt werden kann)
        if (spriteRenderer.sprite && spriteRenderer.sprite.texture)
        {
            spriteRenderer.sprite.texture.wrapMode = TextureWrapMode.Repeat;
        }
    }

    void LateUpdate()
    {
        if (!mainCamera || !spriteRenderer) return;

        FitToCamera();
        if (scrollSpeed != Vector2.zero)
            UpdateTilingOffset();
    }

    void FitToCamera()
    {
        // Sichtbare Weltgröße der Orthographic-Kamera
        float worldSpaceHeight = mainCamera.orthographicSize * 2f;
        float worldSpaceWidth  = worldSpaceHeight * mainCamera.aspect;

        // Auf Bildschirmgröße setzen (kachelt automatisch in alle Richtungen)
        spriteRenderer.size = new Vector2(worldSpaceWidth, worldSpaceHeight);

        // Optional: Sprite-Transform auf Kamera zentrieren (falls gewünscht)
        // transform.position = new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, transform.position.z);
    }

    void UpdateTilingOffset()
    {
        // „Unendlicher“ Eindruck: Offset basierend auf Weltposition/Time
        // Du kannst auch mainCamera.transform.position verwenden, um das Muster
        // im Gegentakt zur Kamera zu schieben (Parallax).
        Vector2 offset = scrollSpeed * Time.time;

        // Für SpriteRenderer geht das über Material-Tiling/Offset
        var mat = spriteRenderer.sharedMaterial; // shared: kein Material-Spam im Editor
        if (!mat) return;

        // _ST ist (tiling.x, tiling.y, offset.x, offset.y)
        // Wir lassen Tiling = 1 und ändern nur Offset.
        if (mat.HasProperty(BaseMap_ST))
        {
            var st = mat.GetVector(BaseMap_ST);
            st.z = offset.x;
            st.w = offset.y;
            mat.SetVector(BaseMap_ST, st);
        }
        else if (mat.HasProperty(MainTex_ST))
        {
            var st = mat.GetVector(MainTex_ST);
            st.z = offset.x;
            st.w = offset.y;
            mat.SetVector(MainTex_ST, st);
        }
    }
}
