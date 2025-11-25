using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Positions a UI health bar above a target's head. Supports Screen Space (Overlay/Camera) and World Space canvases.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HealthBarFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);
    public bool isScreenSpaceCanvas = true;
    public Camera targetCamera;

    RectTransform _rect;
    Canvas _canvas;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    void LateUpdate()
    {
        if (target == null || _rect == null || _canvas == null) return;
        if (isScreenSpaceCanvas && targetCamera == null) return;

        Vector3 worldPos = target.position + worldOffset;

        if (isScreenSpaceCanvas)
        {
            Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPos);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, screenPos, targetCamera, out Vector2 anchoredPos))
            {
                _rect.anchoredPosition = anchoredPos;
            }
        }
        else
        {
            _rect.position = worldPos;
            // Optional billboard: face camera if desired
            if (targetCamera != null)
            {
                _rect.rotation = targetCamera.transform.rotation;
            }
        }
    }
}
