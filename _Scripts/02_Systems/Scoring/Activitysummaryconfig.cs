// ═══════════════════════════════════════════════════════════════════════════════
// ActivitySummaryConfig.cs
// ScriptableObject para configurar mensajes del panel de resumen por actividad
// ═══════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Training/Activity Summary Config", fileName = "ActivitySummaryConfig")]
public class ActivitySummaryConfig : ScriptableObject
{
    [Header("═══ MENSAJES GLOBALES (2★, 1★, 0★) ═══")]
    [Tooltip("Estos mensajes se usan para TODAS las actividades según las estrellas obtenidas")]

    [TextArea(1, 3)]
    [SerializeField] private string global2StarMessage = "¡Muy bien! Sigue practicando.";

    [TextArea(1, 3)]
    [SerializeField] private string global1StarMessage = "Buen intento. Puedes mejorar.";

    [TextArea(1, 3)]
    [SerializeField] private string global0StarMessage = "Necesitas más práctica.";

    [Header("═══ MENSAJES DE 3 ESTRELLAS POR ACTIVIDAD ═══")]
    [Tooltip("Solo define el mensaje de éxito (3★) para cada actividad")]
    [SerializeField] private List<ActivityThreeStarMessage> activityConfigs = new List<ActivityThreeStarMessage>();

    [Header("═══ MENSAJE DE 3★ POR DEFECTO ═══")]
    [Tooltip("Se usa si una actividad no tiene mensaje de 3★ configurado")]
    [TextArea(1, 3)]
    [SerializeField] private string defaultThreeStarMessage = "¡Excelente trabajo!";

    // ═══════════════════════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene el mensaje apropiado según la actividad y las estrellas obtenidas.
    /// </summary>
    public string GetMessage(string activityId, int stars, int errors = 0)
    {
        // Si es 3 estrellas → buscar mensaje específico de la actividad
        if (stars == 3)
        {
            var config = activityConfigs.Find(c => c.activityId == activityId);

            if (config != null && !string.IsNullOrEmpty(config.threeStarMessage))
            {
                return config.threeStarMessage;
            }

            // Si no hay config específica, usar el default de 3★
            return defaultThreeStarMessage;
        }

        // Para 2, 1, 0 estrellas → usar mensajes globales
        return stars switch
        {
            2 => global2StarMessage,
            1 => global1StarMessage,
            _ => global0StarMessage
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // VALIDACIÓN EN EDITOR
    // ═══════════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Verificar IDs duplicados
        var ids = new HashSet<string>();
        foreach (var config in activityConfigs)
        {
            if (!string.IsNullOrEmpty(config.activityId))
            {
                if (!ids.Add(config.activityId))
                {
                    Debug.LogWarning($"[ActivitySummaryConfig] ID duplicado: {config.activityId}");
                }
            }
        }
    }
#endif
}

// ═══════════════════════════════════════════════════════════════════════════════
// CLASE DE DATOS SIMPLIFICADA
// ═══════════════════════════════════════════════════════════════════════════════

[Serializable]
public class ActivityThreeStarMessage
{
    [Header("Identificación")]
    [Tooltip("ID de la actividad (debe coincidir con ActivityIdMap)")]
    public string activityId;

    [Tooltip("Nombre para mostrar en el editor (solo referencia)")]
    public string displayName;

    [Header("Mensaje de Éxito")]
    [Tooltip("Mensaje que se muestra cuando el usuario obtiene 3 estrellas")]
    [TextArea(2, 4)]
    public string threeStarMessage = "¡Excelente trabajo!";
}