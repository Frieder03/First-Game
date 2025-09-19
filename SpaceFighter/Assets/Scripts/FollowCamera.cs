using UnityEngine;

public class FollowCam : MonoBehaviour
{
    public Transform target;         
    public Vector3 offset = new Vector3(0, 0, -10);
    public float smooth = 10f;       

    void LateUpdate()
    {
        if (!target) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(
            transform.position,
            desired,
            1f - Mathf.Exp(-smooth * Time.deltaTime)
        );
    }
}