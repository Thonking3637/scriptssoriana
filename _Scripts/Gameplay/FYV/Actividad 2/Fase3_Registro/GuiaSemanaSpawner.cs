using UnityEngine;
using TMPro;

public class GuiaSemanaSpawner : MonoBehaviour
{
    [Header("Fuente de datos")]
    public SemanaGuiaSO guia;

    [Header("Scroll target")]
    public RectTransform content;

    [Header("Prefabs")]
    public CaseCardUI caseCardPrefab;
    [Tooltip("Opcional: encabezado por d�a")]
    public GameObject headerPrefab;

    /// <summary> Limpia el Content e instancia tarjetas seg�n la gu�a. </summary>
    public void ClearAndPopulate(SemanaGuiaSO custom = null)
    {
        if (custom != null) guia = custom;
        if (!content || !caseCardPrefab || guia == null) return;

        // Limpiar hijos
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // Agrupar por d�a (simple: recorre en orden y coloca header cuando cambia el d�a)
        string diaActual = null;

        foreach (var caso in guia.casos)
        {
            // Encabezado por d�a (opcional)
            if (headerPrefab && diaActual != caso.dia)
            {
                diaActual = caso.dia;
                var hdr = Instantiate(headerPrefab, content);
                var txt = hdr.GetComponentInChildren<TMP_Text>();
                if (txt) txt.text = diaActual.ToUpper();
            }

            // Tarjeta
            var card = Instantiate(caseCardPrefab, content);
            card.Configurar(caso);
            // Puedes suscribir card.OnSeleccionado si quieres enfoque de tablero/c�mara
        }
    }
}
