using UnityEngine;
using UnityEngine.UI;

public class ActivityCarouselSelector : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Scrollbar scrollbar;

    private float[] pos;
    private float scrollPos;
    private int itemCount;

    [Header("Escalado")]
    public float scaleMax = 1f;
    public float scaleMin = 0.8f;
    public float lerpSpeed = 0.1f;

    [Header("Snap")]
    [Tooltip("Qué tan rápido se centra al soltar/ajustar")]
    public float snapSpeed = 10f;

    private bool isSnapping = false;

    private void OnEnable()
    {
        if (scrollbar != null)
            scrollbar.onValueChanged.AddListener(OnScrollValueChanged);
    }

    private void OnDisable()
    {
        if (scrollbar != null)
            scrollbar.onValueChanged.RemoveListener(OnScrollValueChanged);
    }

    private void Start()
    {
        RebuildPositions();
        // inicializa scrollPos con el valor actual
        if (scrollbar != null) scrollPos = scrollbar.value;
    }

    // Si agregas/quitas items en runtime, esto lo detecta y reconstruye pos[]
    private void OnTransformChildrenChanged()
    {
        RebuildPositions();
    }

    private void RebuildPositions()
    {
        itemCount = transform.childCount;

        if (itemCount <= 0)
        {
            pos = null;
            return;
        }

        pos = new float[itemCount];

        // Distribuye 0..1
        if (itemCount == 1)
        {
            pos[0] = 0.5f;
            return;
        }

        float distance = 1f / (itemCount - 1f);
        for (int i = 0; i < itemCount; i++)
            pos[i] = distance * i;
    }

    private void OnScrollValueChanged(float value)
    {
        scrollPos = value;
    }

    private void Update()
    {
        if (scrollbar == null || pos == null || pos.Length == 0) return;

        // Encuentra el índice más cercano al scrollPos
        int nearestIndex = GetNearestIndex(scrollPos);

        // Snap suave hacia el nearest cuando no estás moviendo manualmente (si quieres snap siempre)
        // Si prefieres snap SOLO cuando sueltan drag, esto se conecta a eventos de drag del ScrollRect.
        if (!isSnapping)
        {
            float target = pos[nearestIndex];
            scrollbar.value = Mathf.Lerp(scrollbar.value, target, Time.deltaTime * snapSpeed);
            scrollPos = scrollbar.value;
        }

        // Escalado visual
        for (int i = 0; i < itemCount; i++)
        {
            var child = transform.GetChild(i);

            Vector3 targetScale = (i == nearestIndex)
                ? new Vector3(scaleMax, scaleMax, 1f)
                : new Vector3(scaleMin, scaleMin, 1f);

            child.localScale = Vector3.Lerp(child.localScale, targetScale, lerpSpeed);
        }
    }

    private int GetNearestIndex(float value)
    {
        int nearest = 0;
        float minDist = Mathf.Abs(value - pos[0]);

        for (int i = 1; i < pos.Length; i++)
        {
            float d = Mathf.Abs(value - pos[i]);
            if (d < minDist)
            {
                minDist = d;
                nearest = i;
            }
        }

        return nearest;
    }
}
