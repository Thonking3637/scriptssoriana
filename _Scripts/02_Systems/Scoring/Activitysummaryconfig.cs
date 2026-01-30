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
    [Header("Configuración por Actividad")]
    [SerializeField] private List<ActivityMessages> activityConfigs = new List<ActivityMessages>();

    [Header("Mensajes por Defecto (si no hay config específica)")]
    [SerializeField]
    private StarMessages defaultMessages = new StarMessages
    {
        star3Message = "¡Excelente trabajo!",
        star2Message = "¡Muy bien! Sigue practicando.",
        star1Message = "Buen intento. Puedes mejorar.",
        star0Message = "Necesitas más práctica."
    };

    // ═══════════════════════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene el mensaje apropiado según la actividad y las estrellas obtenidas.
    /// </summary>
    public string GetMessage(string activityId, int stars, int errors = 0)
    {
        // Buscar configuración específica de la actividad
        var config = activityConfigs.Find(c => c.activityId == activityId);

        if (config != null)
        {
            return GetMessageFromConfig(config, stars, errors);
        }

        // Usar mensajes por defecto
        return GetMessageByStars(defaultMessages, stars);
    }

    /// <summary>
    /// Obtiene el mensaje considerando errores específicos si están configurados.
    /// </summary>
    private string GetMessageFromConfig(ActivityMessages config, int stars, int errors)
    {
        // Si hay mensajes específicos por tipo de error y hubo errores
        if (errors > 0 && config.errorSpecificMessages != null && config.errorSpecificMessages.Count > 0)
        {
            // Buscar mensaje específico para este rango de errores
            foreach (var errorMsg in config.errorSpecificMessages)
            {
                if (errors >= errorMsg.minErrors && errors <= errorMsg.maxErrors)
                {
                    return errorMsg.message;
                }
            }
        }

        // Usar mensajes por estrellas
        return GetMessageByStars(config.starMessages, stars);
    }

    private string GetMessageByStars(StarMessages messages, int stars)
    {
        return stars switch
        {
            3 => messages.star3Message,
            2 => messages.star2Message,
            1 => messages.star1Message,
            _ => messages.star0Message
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
// CLASES DE DATOS
// ═══════════════════════════════════════════════════════════════════════════════

[Serializable]
public class ActivityMessages
{
    [Header("Identificación")]
    [Tooltip("ID de la actividad (debe coincidir con ActivityIdMap)")]
    public string activityId;

    [Tooltip("Nombre para mostrar en el editor")]
    public string displayName;

    [Header("Mensajes por Estrellas")]
    public StarMessages starMessages = new StarMessages
    {
        star3Message = "¡Excelente trabajo!",
        star2Message = "¡Muy bien! Sigue practicando.",
        star1Message = "Buen intento. Puedes mejorar.",
        star0Message = "Necesitas más práctica."
    };

    [Header("Mensajes por Errores (Opcional)")]
    [Tooltip("Mensajes específicos cuando hay cierto número de errores")]
    public List<ErrorSpecificMessage> errorSpecificMessages = new List<ErrorSpecificMessage>();
}

[Serializable]
public class StarMessages
{
    [TextArea(1, 3)]
    public string star3Message = "¡Excelente trabajo!";

    [TextArea(1, 3)]
    public string star2Message = "¡Muy bien! Sigue practicando.";

    [TextArea(1, 3)]
    public string star1Message = "Buen intento. Puedes mejorar.";

    [TextArea(1, 3)]
    public string star0Message = "Necesitas más práctica.";
}

[Serializable]
public class ErrorSpecificMessage
{
    [Tooltip("Mínimo de errores para mostrar este mensaje")]
    public int minErrors = 1;

    [Tooltip("Máximo de errores para mostrar este mensaje (usar 999 para 'sin límite')")]
    public int maxErrors = 999;

    [Tooltip("Tipo de error (para referencia, no afecta la lógica)")]
    public string errorType = "General";

    [TextArea(2, 4)]
    public string message = "Cometiste algunos errores. ¡Sigue practicando!";
}