using UnityEngine;

[ExecuteAlways]
public class BackgroundAdjustment : MonoBehaviour
{
    public Camera mainCamera;
    public SpriteRenderer spriteRenderer;

    private void Start()
    {
        float worldSpaceHeight = mainCamera.orthographicSize * 2;
        float worldSpaceWidth = worldSpaceHeight * mainCamera.aspect;

        spriteRenderer.size = new Vector2(worldSpaceWidth, worldSpaceHeight);
    }
}