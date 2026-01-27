using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class RevitaErrorTap : MonoBehaviour
{
    [Header("Input")]
    public Camera raycastCamera;
    public LayerMask tapMask = ~0;
    public bool ignoreTapOverUI = true;

    private Camera _cam;

    private void Awake()
    {
        _cam = raycastCamera ? raycastCamera : Camera.main;
    }

    private void Update()
    {
        if (!TapDown()) return;

        if (RayHitMe())
        {
            SoundManager.Instance?.PlaySound("success");
            Destroy(gameObject);
        }
    }

    private bool TapDown()
    {
        if (ignoreTapOverUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            return false;

        if (Input.GetMouseButtonDown(0)) return true;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame == true) return true;
        if (UnityEngine.InputSystem.Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true) return true;
#endif
        return false;
    }

    private bool RayHitMe()
    {
        var cam = _cam ? _cam : Camera.main;
        if (!cam) return false;

        Vector3 sp = Input.mousePosition;
#if ENABLE_INPUT_SYSTEM
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        var ms = UnityEngine.InputSystem.Mouse.current;
        if (ts != null && ts.primaryTouch.press.isPressed)
            sp = ts.primaryTouch.position.ReadValue();
        else if (ms != null)
            sp = ms.position.ReadValue();
#endif
        var ray = cam.ScreenPointToRay(sp);
        if (Physics.Raycast(ray, out var hit, 100f, tapMask, QueryTriggerInteraction.Collide))
            return hit.collider != null && hit.collider.gameObject == this.gameObject;

        return false;
    }
}
