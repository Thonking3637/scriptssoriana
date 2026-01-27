using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;

public class SmoothCameraController : MonoBehaviour
{
    [Serializable]
    public class UIElementConfig
    {
        public RectTransform element;
        public Vector2 startPos;
        public Vector2 endPos;
        public float transitionTime = 0.5f;
        public Ease easeType = Ease.OutBack;
    }

    [Serializable]
    public class CameraPosition
    {
        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public float transitionTime = 1.5f;
        public List<UIElementConfig> uiElements = new List<UIElementConfig>();
    }

    public List<CameraPosition> cameraPositions = new List<CameraPosition>();
    private CameraPosition currentTarget;

    public bool isMoving { get; private set; } = false; // Flag público
    private List<UIElementConfig> activeUIElements = new List<UIElementConfig>();

    // ✅ HARDENING: cola de 1 (última orden gana)
    private bool _hasPendingMove = false;
    private string _pendingMoveName = null;
    private Action _pendingMoveCallback = null;

    /// <summary>
    /// Llamado por GameManager para mover la cámara al iniciar una actividad.
    /// </summary>
    public void InitializeCameraPosition(string startPosition)
    {
        if (!string.IsNullOrEmpty(startPosition))
        {
            MoveToPosition(startPosition);
        }
    }

    public void MoveToPosition(string positionName, Action onComplete = null)
    {
        if (string.IsNullOrEmpty(positionName))
        {
            onComplete?.Invoke();
            return;
        }

        // ✅ Si se está moviendo, NO pierdas la orden: guárdala como pendiente
        if (isMoving)
        {
            _hasPendingMove = true;
            _pendingMoveName = positionName;
            _pendingMoveCallback = onComplete;
            return;
        }

        CameraPosition target = cameraPositions.Find(pos => pos.name == positionName);
        if (target == null)
        {
            Debug.Log($"No se encontró la posición de cámara con el nombre: {positionName}");
            onComplete?.Invoke(); // ✅ no cuelgues a quien espera callback
            return;
        }

        isMoving = true;
        currentTarget = target;

        // 🔹 Mueve la cámara con DoTween
        transform.DOMove(target.position, target.transitionTime).SetEase(Ease.InOutSine);
        transform.DORotate(target.rotation, target.transitionTime).SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                isMoving = false;

                ActivateUIElements(target.uiElements);
                UpdateCameraChildName();

                onComplete?.Invoke();

                // ✅ Ejecutar movimiento pendiente si existe (último gana)
                if (_hasPendingMove)
                {
                    _hasPendingMove = false;
                    string next = _pendingMoveName;
                    Action cb = _pendingMoveCallback;

                    _pendingMoveName = null;
                    _pendingMoveCallback = null;

                    MoveToPosition(next, cb);
                }
            });
    }

    private void UpdateCameraChildName()
    {
        if (currentTarget == null) return;

        if (transform.childCount > 0)
        {
            Transform firstChild = transform.GetChild(0);
            firstChild.name = currentTarget.name;
            Debug.Log($"📷 Se cambió el nombre del hijo de la cámara a: {firstChild.name}");
        }
        else
        {
            Debug.LogWarning("⚠️ La cámara no tiene hijos para renombrar.");
        }
    }

    public void MoveToNext(Action onComplete = null)
    {
        if (cameraPositions == null || cameraPositions.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        int currentIndex = cameraPositions.IndexOf(currentTarget);
        if (currentIndex < 0) currentIndex = 0;

        int nextIndex = (currentIndex + 1) % cameraPositions.Count;
        MoveToPosition(cameraPositions[nextIndex].name, onComplete);
    }

    public void MoveToPrevious(Action onComplete = null)
    {
        if (cameraPositions == null || cameraPositions.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        int currentIndex = cameraPositions.IndexOf(currentTarget);
        if (currentIndex < 0) currentIndex = 0;

        int prevIndex = (currentIndex - 1 + cameraPositions.Count) % cameraPositions.Count;
        MoveToPosition(cameraPositions[prevIndex].name, onComplete);
    }

    private void ActivateUIElements(List<UIElementConfig> uiElements)
    {
        List<UIElementConfig> elementsToKeep = new List<UIElementConfig>();

        foreach (var ui in activeUIElements)
        {
            if (uiElements != null && uiElements.Exists(e => e.element == ui.element))
            {
                elementsToKeep.Add(ui);
            }
            else
            {
                if (ui.element != null)
                {
                    ui.element.DOAnchorPos(ui.startPos, ui.transitionTime).SetEase(Ease.InOutSine)
                        .OnComplete(() => ui.element.gameObject.SetActive(false));
                }
            }
        }

        activeUIElements.Clear();
        activeUIElements.AddRange(elementsToKeep);

        if (uiElements == null) return;

        foreach (var ui in uiElements)
        {
            if (!elementsToKeep.Exists(e => e.element == ui.element))
            {
                if (ui.element != null)
                {
                    ui.element.gameObject.SetActive(true);
                    ui.element.anchoredPosition = ui.startPos;
                    ui.element.DOAnchorPos(ui.endPos, ui.transitionTime).SetEase(ui.easeType);
                    activeUIElements.Add(ui);
                }
            }
        }
    }

    public void DetenerMovimiento()
    {
        isMoving = false;
        _hasPendingMove = false;
        _pendingMoveName = null;
        _pendingMoveCallback = null;

        DOTween.Kill(transform);
    }
}
