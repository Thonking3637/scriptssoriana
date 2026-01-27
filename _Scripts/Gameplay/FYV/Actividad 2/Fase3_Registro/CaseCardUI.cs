using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CaseCardUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text txtResumen;        // Un solo texto
    public Image iconEstado;           // Circulito estado
    public Button btnFocus;            // Opcional

    [Header("Estilo")]
    public bool mayusculasDia = true;
    public bool resaltarRegistro = true;
    public Color colorSi = new Color(0.18f, 0.62f, 0.12f);
    public Color colorNo = new Color(0.85f, 0.15f, 0.15f);
    public bool atenuarAlCompletar = true;
    [Range(0.2f, 1f)] public float alphaCompletado = 0.6f;

    [HideInInspector] public SemanaGuiaSO.CasoDia data;
    [HideInInspector] public bool completado = false;

    public System.Action<CaseCardUI> OnSeleccionado;

    void Awake()
    {
        // Auto-wire seguro
        if (!txtResumen) txtResumen = GetComponentInChildren<TMP_Text>(true);
        if (!iconEstado) iconEstado = GetComponentInChildren<Image>(true);
        if (!btnFocus) btnFocus = GetComponentInChildren<Button>(true);
    }

    void OnValidate()
    {
        // También en editor
        if (!txtResumen) txtResumen = GetComponentInChildren<TMP_Text>(true);
        if (!iconEstado) iconEstado = GetComponentInChildren<Image>(true);
        if (!btnFocus) btnFocus = GetComponentInChildren<Button>(true);
    }

    void Start()
    {
        if (btnFocus) btnFocus.onClick.AddListener(() => OnSeleccionado?.Invoke(this));
    }

    public void Configurar(SemanaGuiaSO.CasoDia caso)
    {
        data = caso;

        string dia = mayusculasDia ? (caso.dia ?? "").ToUpperInvariant() : (caso.dia ?? "");
        string producto = caso.producto ?? "";
        string kg = $"{caso.kg} kg";

        string regPlano = caso.registradoApp ? "REGISTRADO" : "NO REGISTRADO";
        string regTxt = regPlano;

        if (resaltarRegistro)
        {
            string hex = ColorUtility.ToHtmlStringRGB(caso.registradoApp ? colorSi : colorNo);
            regTxt = $"<color=#{hex}>{regPlano}</color>";
        }

        if (txtResumen)
            txtResumen.text = $"{dia}:\n{producto}, {kg}, {regTxt}";

        SetEstado(false);
    }


    public void SetEstado(bool done)
    {
        completado = done;

        if (iconEstado)
            iconEstado.color = done ? colorSi : Color.white;

        if (txtResumen && atenuarAlCompletar)
        {
            var c = txtResumen.color;
            c.a = done ? alphaCompletado : 1f;
            txtResumen.color = c;
        }
    }
}
