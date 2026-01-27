using System.Collections;
using UnityEngine;

public class MenuRemoteProgressBinder : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MenuCompletionBinder menuCompletionBinder;

    [Header("Behavior")]
    [SerializeField] private bool refreshRemoteOnEnable = true;

    private ProgressService _ps;
    private Coroutine _cr;

    private void OnEnable()
    {
        CompletionService.OnProgressChanged += RefreshUI;
        RefreshUI();

        if (_cr == null)
            _cr = StartCoroutine(EnsureProgressServiceAndRefresh());
    }

    private void OnDisable()
    {
        CompletionService.OnProgressChanged -= RefreshUI;
        if (_cr != null)
        {
            StopCoroutine(_cr);
            _cr = null;
        }
    }

    private IEnumerator EnsureProgressServiceAndRefresh()
    {
        while (_ps == null)
        {
            _ps = ProgressService.Instance;
            if (_ps == null)
                _ps = FindObjectOfType<ProgressService>();

            yield return null;
        }

        if (refreshRemoteOnEnable)
            yield return _ps.LoadRemoteMedalsIntoCompletionService();

        CompletionService.NotifyChanged();
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (menuCompletionBinder != null)
            menuCompletionBinder.RefreshAll();
    }
}
