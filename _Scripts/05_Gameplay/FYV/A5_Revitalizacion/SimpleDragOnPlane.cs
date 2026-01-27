using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class SimpleDragOnPlane : MonoBehaviour
{
    [Header("Plano")]
    public float yHeight = 0f;      // altura de la tina/mesa

    [Header("Ejes permitidos")]
    public bool allowX = true;      // ← Remojo: true
    public bool allowZ = false;     // ← Remojo: false (queda fija)

    [Header("Límites (opcionales)")]
    public bool clampX = false;
    public float xMin = -1f;
    public float xMax = 1f;
    public bool clampZ = false;
    public float zMin = -1f;
    public float zMax = 1f;

    [Header("Suavizado")]
    public float followSpeed = 18f;

    [Header("Input")]
    public bool ignoreUI = true;
    public Camera raycastCamera;
    public LayerMask hitMask = ~0;

    [Header("Bloqueo externo")]
    public bool lockWhileTimer = false;

    Camera _cam;
    bool _drag;
    Vector3 _grabOffset; // offset desde el punto proyectado al objeto

    void Awake() => _cam = raycastCamera ? raycastCamera : Camera.main;

    void Update()
    {
        if (lockWhileTimer) { _drag = false; return; }

        if (InputDown())
        {
            if (ignoreUI && IsPointerOverUI()) return;

            _cam = raycastCamera ? raycastCamera : (_cam ? _cam : Camera.main);
            if (_cam == null) return;

            if (RayHitSelf())
            {
                _drag = true;

                // offset entre posición actual y proyección al plano
                var p = ProjectToPlane(GetPointerPos());
                _grabOffset = transform.position - p;
            }
        }

        if (InputUp()) _drag = false;
        if (!_drag) return;

        var target = ProjectToPlane(GetPointerPos()) + _grabOffset;

        // fijar Y del plano
        target.y = yHeight;

        // bloquear ejes
        if (!allowX) target.x = transform.position.x;
        if (!allowZ) target.z = transform.position.z;

        // clamps
        if (clampX) target.x = Mathf.Clamp(target.x, xMin, xMax);
        if (clampZ) target.z = Mathf.Clamp(target.z, zMin, zMax);

        // seguir suave
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * followSpeed);
    }

    // ---------- helpers ----------
    bool InputDown()
    {
        if (Input.GetMouseButtonDown(0)) return true;
#if ENABLE_INPUT_SYSTEM
        var m = UnityEngine.InputSystem.Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) return true;
        var t = UnityEngine.InputSystem.Touchscreen.current;
        if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
#endif
        return false;
    }

    bool InputUp()
    {
        if (Input.GetMouseButtonUp(0)) return true;
#if ENABLE_INPUT_SYSTEM
        var m = UnityEngine.InputSystem.Mouse.current;
        if (m != null && m.leftButton.wasReleasedThisFrame) return true;
        var t = UnityEngine.InputSystem.Touchscreen.current;
        if (t != null && t.primaryTouch.press.wasReleasedThisFrame) return true;
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended) return true;
#endif
        return false;
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
#if ENABLE_INPUT_SYSTEM
        return EventSystem.current.IsPointerOverGameObject();
#else
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
#endif
    }

    Vector2 GetPointerPos()
    {
#if ENABLE_INPUT_SYSTEM
        var t = UnityEngine.InputSystem.Touchscreen.current;
        if (t != null && t.primaryTouch.press.isPressed)
            return t.primaryTouch.position.ReadValue();
        var m = UnityEngine.InputSystem.Mouse.current;
        if (m != null)
            return m.position.ReadValue();
#else
        if (Input.touchCount > 0)
            return Input.GetTouch(0).position;
#endif
        return Input.mousePosition;
    }

    Vector3 ProjectToPlane(Vector2 screenPos)
    {
        var cam = _cam ? _cam : Camera.main;
        if (!cam) return transform.position;

        var ray = cam.ScreenPointToRay(screenPos);
        var plane = new Plane(Vector3.up, new Vector3(0f, yHeight, 0f));
        if (plane.Raycast(ray, out float t)) return ray.GetPoint(t);
        return transform.position;
    }

    bool RayHitSelf()
    {
        var cam = _cam ? _cam : Camera.main;
        if (!cam) return true; // sin cámara, permitimos

        var ray = cam.ScreenPointToRay(GetPointerPos());
        if (Physics.Raycast(ray, out var hit, 100f, hitMask, QueryTriggerInteraction.Collide))
            return hit.collider && hit.collider.transform.IsChildOf(transform);
        return false;
    }
}
