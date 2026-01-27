using UnityEngine;

public class DefectHighlighter : MonoBehaviour
{
    [SerializeField] GameObject overlay; // malla duplicada/material unlit
    public void SetActive(bool v) { if (overlay) overlay.SetActive(v); }
}
