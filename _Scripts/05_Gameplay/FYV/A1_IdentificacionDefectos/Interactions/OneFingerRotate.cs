// OneFingerRotate.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Permite rotar un objeto 3D con un solo toque/clic.
/// Requiere que el toque inicie sobre el Collider del objeto.
/// </summary>
[RequireComponent(typeof(MeshRenderer), typeof(Collider))]
public class OneFingerRotate : MonoBehaviour
{
    [Header("Input System")]
    [SerializeField] private InputActionAsset actions;   // ← arrastra tu InputActionAsset aquí

    private InputAction leftClickAction;
    private InputAction mouseLookAction;

    [Header("Rotación")]
    [SerializeField] private float speed = 120f;
    [SerializeField] private bool inverted = false;

    private bool rotateAllowed;
    private Transform tr;

    public event Action OnFirstRotate;
    private bool emitted;

    // Campos necesarios para Raycast
    private Camera mainCamera;
    private Collider myCollider;

    private void Awake()
    {
        tr = transform;

        // Inicialización de referencias necesarias
        mainCamera = Camera.main;
        myCollider = GetComponent<Collider>();

        InitializeInputSystem();
    }

    private void InitializeInputSystem()
    {
        // Busca las acciones dentro del asset
        leftClickAction = actions.FindAction("Left Click", true);
        mouseLookAction = actions.FindAction("Mouse Look", true);

        // 📢 MODIFICADO: Conecta el evento 'started' a un método que hace Raycast
        leftClickAction.started += OnLeftClickStarted;

        // Conecta el evento 'canceled' para desactivar la rotación al levantar el dedo/ratón
        leftClickAction.canceled += ctx => rotateAllowed = false;

        actions.Enable();
    }

    private void OnDestroy()
    {
        // Desuscribir el evento para evitar errores
        leftClickAction.started -= OnLeftClickStarted;
        actions.Disable();
    }

    /// <summary>
    /// Maneja el evento de inicio de clic/toque, solo permitiendo la rotación
    /// si un Raycast golpea este objeto.
    /// </summary>
    private void OnLeftClickStarted(InputAction.CallbackContext ctx)
    {
        // 1. Obtener la posición del toque/ratón de forma multiplataforma
        Vector2 screenPosition;

        if (Touchscreen.current != null)
        {
            // Posición del toque primario (móvil)
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Mouse.current != null)
        {
            // Posición del ratón (PC/WebGL)
            screenPosition = Mouse.current.position.ReadValue();
        }
        else
        {
            // No hay entrada
            return;
        }

        // 2. Ejecutar Raycast y verificar el golpe
        if (CheckForHit(screenPosition))
        {
            // Solo si golpea, permitimos la rotación.
            rotateAllowed = true;
            if (!emitted)
            {
                emitted = true;
                OnFirstRotate?.Invoke();
            }
        }
        else
        {
            // Si el Raycast falla, la rotación queda DESHABILITADA
            rotateAllowed = false;
        }
    }


    private void Update()
    {
        if (!rotateAllowed) return;

        // Lee el movimiento del ratón/dedo
        Vector2 delta = mouseLookAction.ReadValue<Vector2>();
        ApplyRotation(delta);
    }

    private void ApplyRotation(Vector2 delta)
    {
        float yaw = delta.x * speed * Time.deltaTime;
        float pitch = delta.y * speed * Time.deltaTime;

        if (inverted)
        {
            yaw = -yaw;
            pitch = -pitch;
        }

        // Giro horizontal (Y) global + vertical (X) local
        tr.Rotate(Vector3.up, -yaw, Space.World);
        tr.Rotate(Vector3.right, pitch, Space.Self);
    }

    /// <summary>
    /// Crea un Raycast desde la posición de la pantalla y verifica si golpea el collider de esta fruta.
    /// </summary>
    private bool CheckForHit(Vector2 screenPosition)
    {
        if (mainCamera == null || myCollider == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        // Usamos Collider.Raycast para eficiencia, solo comprueba este objeto
        return myCollider.Raycast(ray, out RaycastHit hitInfo, 100f);
    }

    public void ResetEmit() => emitted = false;
}