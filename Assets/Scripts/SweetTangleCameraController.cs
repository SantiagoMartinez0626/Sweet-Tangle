using UnityEngine;
using UnityEngine.InputSystem;

public class SweetTangleCameraController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minZoom = 3.5f;
    [SerializeField] private float maxZoom = 12f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }
    }

    public void ConfigureZoomLimits(float min, float max)
    {
        minZoom = min;
        maxZoom = max;
    }

    private void Update()
    {
        HandleMovement();
        HandleZoom();
    }

    private void HandleMovement()
    {
        float x = 0f;
        float y = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
        }

        Vector3 delta = new Vector3(x, y, 0f) * (moveSpeed * Time.deltaTime);
        transform.position += delta;
    }

    private void HandleZoom()
    {
        if (cam == null || !cam.orthographic)
        {
            return;
        }

        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y * 0.01f : 0f;
        if (Keyboard.current != null && Keyboard.current.qKey.isPressed)
        {
            scroll += 1f;
        }
        if (Keyboard.current != null && Keyboard.current.eKey.isPressed)
        {
            scroll -= 1f;
        }

        if (Mathf.Abs(scroll) < 0.01f)
        {
            return;
        }

        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize - scroll * zoomSpeed * Time.deltaTime * 8f,
            minZoom,
            maxZoom
        );
    }
}
