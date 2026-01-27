using System.Collections.Generic;
using UnityEngine;
using DG.Tweening; // opcional, solo para matar tweens en la cámara

/// <summary>
/// Free camera independiente. Actívala con toggleKey (F1).
/// - WASD: mover en plano
/// - Q/E o Ctrl/Espacio: bajar/subir
/// - RMB: look (bloquea cursor mientras esté pulsado)
/// - Shift: rápido | Ctrl: lento
/// - Scroll: ajusta velocidad base
/// Usa unscaledDeltaTime para que funcione en pausa.
/// </summary>
[DisallowMultipleComponent]
public class FreeFlyCamera : MonoBehaviour
{
    [Header("Activación")]
    public KeyCode toggleKey = KeyCode.F1;
    public bool startEnabled = false;

    [Header("Movimiento")]
    public float baseSpeed = 5f;
    public float fastMultiplier = 3f;     // Shift
    public float slowMultiplier = 0.35f;  // Ctrl
    public float verticalSpeedMultiplier = 1f; // para Q/E o Space/Ctrl
    public float scrollSpeedStep = 1f;    // cuánto sube/baja el baseSpeed por notch
    public float minBaseSpeed = 0.5f;
    public float maxBaseSpeed = 50f;

    [Header("Rotación (RMB)")]
    public float lookSensitivity = 0.15f; // grados por pixel
    public float pitchMin = -89f;
    public float pitchMax = 89f;

    [Header("Límites opcionales")]
    public bool useBounds = false;
    public Vector3 minBounds = new Vector3(-100, 0, -100);
    public Vector3 maxBounds = new Vector3(100, 50, 100);

    // Estado
    public bool IsActive { get; private set; }

    float yaw;
    float pitch;

    // Guardar/pausar controladores de cámara ajenos
    readonly List<Behaviour> disabledBehaviours = new();
    bool cursorWasLocked = false;
    bool cursorWasVisible = true;

    void Start()
    {
        var rot = transform.rotation.eulerAngles;
        yaw = rot.y;
        pitch = rot.x;

        if (startEnabled)
            EnableFreeCam();
        else
            DisableFreeCam(); // asegúrate del estado consistente
    }

    void Update()
    {
        // Toggle
        if (Input.GetKeyDown(toggleKey))
        {
            if (IsActive) DisableFreeCam();
            else EnableFreeCam();
        }

        if (!IsActive) return;

        float dt = Time.unscaledDeltaTime;

        // Rotación con RMB
        bool rmb = Input.GetMouseButton(1);
        if (rmb)
        {
            LockCursor(true);
            float dx = Input.GetAxisRaw("Mouse X");
            float dy = Input.GetAxisRaw("Mouse Y");
            yaw += dx * lookSensitivity * 10f;    // factor para sensación agradable
            pitch -= dy * lookSensitivity * 10f;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        else
        {
            LockCursor(false);
        }

        // Ajuste de velocidad con scroll
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            baseSpeed = Mathf.Clamp(baseSpeed + scroll * scrollSpeedStep, minBaseSpeed, maxBaseSpeed);
        }

        // Multiplicadores de velocidad
        float mult = 1f;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) mult *= fastMultiplier;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) mult *= slowMultiplier;

        float speed = baseSpeed * mult;

        // Movimiento local
        Vector3 move = Vector3.zero;
        move += GetAxis(KeyCode.W, KeyCode.S) * Vector3.forward;   // Z
        move += GetAxis(KeyCode.D, KeyCode.A) * Vector3.right;     // X

        // Vertical: E/Q o Space/Ctrl
        float upDown = 0f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) upDown += 1f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) upDown -= 1f;
        move += upDown * verticalSpeedMultiplier * Vector3.up;

        // Aplicar
        if (move.sqrMagnitude > 0f)
        {
            Vector3 worldMove = transform.TransformDirection(move.normalized) * speed * dt;
            transform.position += worldMove;

            if (useBounds)
            {
                var p = transform.position;
                p.x = Mathf.Clamp(p.x, minBounds.x, maxBounds.x);
                p.y = Mathf.Clamp(p.y, minBounds.y, maxBounds.y);
                p.z = Mathf.Clamp(p.z, minBounds.z, maxBounds.z);
                transform.position = p;
            }
        }
    }

    float GetAxis(KeyCode positive, KeyCode negative)
    {
        float v = 0f;
        if (Input.GetKey(positive)) v += 1f;
        if (Input.GetKey(negative)) v -= 1f;
        return v;
    }

    void EnableFreeCam()
    {
        if (IsActive) return;
        IsActive = true;

        // Matar tweens sobre la cámara (para evitar que DOMove/DORotate interfieran)
        DOTween.Kill(transform);

        // Deshabilitar temporalmente controladores de cámara conocidos en el mismo GO
        TryDisableOtherControllers();
    }

    void DisableFreeCam()
    {
        if (!IsActive) return;
        IsActive = false;

        // Restaurar controladores
        foreach (var b in disabledBehaviours)
            if (b) b.enabled = true;
        disabledBehaviours.Clear();

        // Restaurar cursor si quedó bloqueado
        LockCursor(false);
    }

    void TryDisableOtherControllers()
    {
        // Busca behaviours que suelan mover la cámara y deshabilítalos mientras la freecam esté activa
        var behaviours = GetComponents<Behaviour>();
        foreach (var b in behaviours)
        {
            if (b == this) continue;
            if (!b.enabled) continue;

            // Heurística simple: desactiva controladores de cámara/pathed
            string n = b.GetType().Name;
            bool isCameraMover =
                n.Contains("SmoothCameraController") ||
                n.Contains("CameraPathfinding") ||
                n.Contains("Cinemachine") ||
                n.Contains("FirstPerson") ||
                n.Contains("ThirdPerson");

            if (isCameraMover)
            {
                b.enabled = false;
                disabledBehaviours.Add(b);
            }
        }
    }

    void LockCursor(bool locked)
    {
        if (locked)
        {
            // guarda estado previo solo la primera vez
            if (!Input.GetMouseButton(1))
                return;

            cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            cursorWasVisible = Cursor.visible;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // solo si no se está manteniendo RMB
            if (Input.GetMouseButton(1)) return;

            Cursor.lockState = cursorWasLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = cursorWasVisible;
        }
    }
}
