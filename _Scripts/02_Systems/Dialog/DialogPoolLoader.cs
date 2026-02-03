using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DialogPoolLoader - Carga y sirve preguntas aleatorias desde un JSON.
/// 
/// USO:
///   // Cargar 6 preguntas aleatorias de la categoría "frustrated"
///   List<CustomerComment> questions = DialogPoolLoader.GetRandom("frustrated", 6);
/// 
///   // Registrar en DialogSystem
///   DialogPoolLoader.RegisterInDialogSystem("frustrated", 6);
/// 
/// SETUP:
///   1. Colocar el JSON en: Assets/Resources/Dialogs/client_dialogs.json
///   2. Llamar desde cualquier actividad con una línea
///   3. Funciona en Android, iOS y WebGL
/// 
/// CATEGORÍAS DISPONIBLES:
///   - "impatient"  → Cliente apurado (15 preguntas)
///   - "frustrated" → Cliente frustrado (15 preguntas)
///   - "rude"       → Cliente grosero (15 preguntas)
///   - "confused"   → Cliente confundido (15 preguntas)
/// 
/// NOTAS:
///   - El JSON se cachea en memoria después de la primera carga
///   - GetRandom devuelve preguntas sin repetir (si hay suficientes)
///   - Las opciones se barajan automáticamente por DialogSystem, no aquí
/// </summary>
public static class DialogPoolLoader
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CLASES DE SERIALIZACIÓN (para JsonUtility)
    // ══════════════════════════════════════════════════════════════════════════════

    [System.Serializable]
    private class DialogPool
    {
        public List<DialogCategory> categories;
    }

    [System.Serializable]
    private class DialogCategory
    {
        public string id;
        public string description;
        public List<DialogQuestion> questions;
    }

    [System.Serializable]
    private class DialogQuestion
    {
        public string clientText;
        public string question;
        public List<string> options;
        public string correctAnswer;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CACHE
    // ══════════════════════════════════════════════════════════════════════════════

    private static DialogPool _cachedPool;
    private const string JSON_PATH = "Dialogs/client_dialogs";

    // ══════════════════════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene una lista aleatoria de preguntas de una categoría.
    /// </summary>
    /// <param name="categoryId">ID de la categoría (ej: "frustrated", "impatient")</param>
    /// <param name="count">Cantidad de preguntas a obtener</param>
    /// <returns>Lista de CustomerComment listos para usar con DialogSystem</returns>
    public static List<CustomerComment> GetRandom(string categoryId, int count)
    {
        var pool = LoadPool();
        if (pool == null) return new List<CustomerComment>();

        // Buscar la categoría
        var category = pool.categories.Find(c => c.id == categoryId);
        if (category == null || category.questions == null || category.questions.Count == 0)
        {
            Debug.LogWarning($"[DialogPoolLoader] Categoría '{categoryId}' no encontrada o vacía.");
            return new List<CustomerComment>();
        }

        // Crear copia y barajar
        var shuffled = new List<DialogQuestion>(category.questions);
        Shuffle(shuffled);

        // Tomar las primeras 'count' preguntas
        int take = Mathf.Min(count, shuffled.Count);
        var result = new List<CustomerComment>();

        for (int i = 0; i < take; i++)
        {
            var q = shuffled[i];
            result.Add(new CustomerComment
            {
                category = categoryId,
                clientText = q.clientText,
                question = q.question,
                options = new List<string>(q.options),
                correctAnswer = q.correctAnswer
            });
        }

        Debug.Log($"[DialogPoolLoader] Cargadas {result.Count} preguntas de '{categoryId}' (pool: {category.questions.Count})");
        return result;
    }

    /// <summary>
    /// Carga preguntas aleatorias y las registra directamente en DialogSystem.
    /// Reemplaza el método Register___Dialogs() de cada actividad.
    /// </summary>
    /// <param name="categoryId">ID de la categoría</param>
    /// <param name="count">Cantidad de preguntas</param>
    public static void RegisterInDialogSystem(string categoryId, int count)
    {
        if (DialogSystem.Instance == null)
        {
            Debug.LogError("[DialogPoolLoader] DialogSystem.Instance es null.");
            return;
        }

        // Limpiar preguntas anteriores de esta categoría
        DialogSystem.Instance.customerComments.RemoveAll(c => c.category == categoryId);
        DialogSystem.Instance.ResetCategoryIndex(categoryId);

        // Cargar nuevas preguntas aleatorias
        var questions = GetRandom(categoryId, count);
        DialogSystem.Instance.customerComments.AddRange(questions);

        Debug.Log($"[DialogPoolLoader] Registradas {questions.Count} preguntas de '{categoryId}' en DialogSystem.");
    }

    /// <summary>
    /// Obtiene las categorías disponibles en el pool.
    /// </summary>
    public static List<string> GetAvailableCategories()
    {
        var pool = LoadPool();
        if (pool == null) return new List<string>();

        var result = new List<string>();
        foreach (var cat in pool.categories)
            result.Add(cat.id);

        return result;
    }

    /// <summary>
    /// Obtiene la cantidad total de preguntas en una categoría.
    /// </summary>
    public static int GetQuestionCount(string categoryId)
    {
        var pool = LoadPool();
        if (pool == null) return 0;

        var category = pool.categories.Find(c => c.id == categoryId);
        return category?.questions?.Count ?? 0;
    }

    /// <summary>
    /// Fuerza la recarga del JSON (útil si se actualizó en runtime).
    /// </summary>
    public static void ClearCache()
    {
        _cachedPool = null;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INTERNOS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Carga el JSON desde Resources y lo cachea.
    /// </summary>
    private static DialogPool LoadPool()
    {
        if (_cachedPool != null) return _cachedPool;

        var textAsset = Resources.Load<TextAsset>(JSON_PATH);
        if (textAsset == null)
        {
            Debug.LogError($"[DialogPoolLoader] No se encontró el JSON en Resources/{JSON_PATH}.json");
            return null;
        }

        _cachedPool = JsonUtility.FromJson<DialogPool>(textAsset.text);

        if (_cachedPool == null || _cachedPool.categories == null)
        {
            Debug.LogError("[DialogPoolLoader] Error al parsear el JSON de diálogos.");
            return null;
        }

        int totalQuestions = 0;
        foreach (var cat in _cachedPool.categories)
            totalQuestions += cat.questions?.Count ?? 0;

        Debug.Log($"[DialogPoolLoader] Pool cargado: {_cachedPool.categories.Count} categorías, {totalQuestions} preguntas totales.");

        return _cachedPool;
    }

    /// <summary>
    /// Baraja una lista (Fisher-Yates).
    /// </summary>
    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}