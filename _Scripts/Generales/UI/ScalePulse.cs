// Assets/Scripts/Generales/UI/ScalePulse.cs
using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems; // <- IMPORTANTE

[AddComponentMenu("FX/Scale Pulse (DOTween)")]
public class ScalePulse : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Target")]
    public Transform target;
    public Vector3 baseScale = Vector3.zero;

    [Header("Pulso")]
    [Range(0f, 2f)] public float intensity = 0.15f;
    public float duration = 0.35f;
    public Ease easeOut = Ease.OutBack;
    public Ease easeIn = Ease.InBack;
    public bool loop = true;
    public bool useUnscaledTime = true;
    public bool playOnEnable = true;
    public bool resetOnDisable = true;

    [Header("UI Opcional")]
    public bool pointerHoverControls = false;
    public bool pointerPressBump = true;
    public float pressBumpMultiplier = 1.4f;

    private Sequence _seq;
    private bool _paused;

    void Awake()
    {
        if (!target) target = transform;
        if (baseScale == Vector3.zero) baseScale = target.localScale;
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        Stop(resetOnDisable);
    }

    public void Play()
    {
        KillSequence();

        _seq = DOTween.Sequence().SetUpdate(useUnscaledTime);
        _seq.Append(target.DOScale(baseScale * (1f + intensity), Mathf.Max(0.01f, duration)).SetEase(easeOut));
        _seq.Append(target.DOScale(baseScale, Mathf.Max(0.01f, duration)).SetEase(easeIn));
        _seq.SetLoops(loop ? -1 : 1, LoopType.Restart);
        _seq.Play();
        _paused = false;
    }

    public void Stop() => Stop(resetOnDisable);
    public void Stop(bool resetScale)
    {
        if (resetScale && target) target.localScale = baseScale;
        KillSequence();
    }

    public void Pause()
    {
        if (_seq != null && _seq.IsActive() && _seq.IsPlaying())
        {
            _seq.Pause();
            _paused = true;
        }
    }

    public void Resume()
    {
        if (_seq != null && _seq.IsActive() && _paused)
        {
            _seq.Play();
            _paused = false;
        }
    }

    public void SetIntensity(float newIntensity)
    {
        intensity = Mathf.Max(0f, newIntensity);
        RebuildKeepingState();
    }

    public void SetDuration(float newDuration)
    {
        duration = Mathf.Max(0.01f, newDuration);
        RebuildKeepingState();
    }

    private void RebuildKeepingState()
    {
        bool wasPlaying = _seq != null && _seq.IsActive() && _seq.IsPlaying();
        bool prevLoop = loop;
        Play();
        loop = prevLoop;
        if (!wasPlaying) Pause();
    }

    private void KillSequence()
    {
        if (_seq != null)
        {
            _seq.Kill();
            _seq = null;
        }
        _paused = false;
    }

    // ===== Implementaciones EXACTAS de las interfaces =====
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!pointerHoverControls) return;
        Play();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!pointerHoverControls) return;
        Stop(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!pointerPressBump) return;

        if (_seq == null || !_seq.IsActive()) target.localScale = baseScale;

        float bumpInt = Mathf.Max(0f, intensity * pressBumpMultiplier);
        float bumpDur = Mathf.Max(0.05f, duration * 0.55f);

        Sequence bump = DOTween.Sequence().SetUpdate(useUnscaledTime);
        bump.Append(target.DOScale(baseScale * (1f + bumpInt), bumpDur).SetEase(Ease.OutBack));
        bump.Append(target.DOScale(baseScale * (1f + intensity), bumpDur * 0.9f).SetEase(Ease.InOutSine));
        bump.Play();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // opcional
    }
}
