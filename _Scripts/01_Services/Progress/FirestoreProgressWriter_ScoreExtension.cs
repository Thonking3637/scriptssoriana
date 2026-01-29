// ═══════════════════════════════════════════════════════════════════════════════
// FirestoreProgressWriter_ScoreExtension.cs
// Extensión para guardar scores detallados en Firebase
// Se agrega como partial class o como extensión del writer existente
// ═══════════════════════════════════════════════════════════════════════════════

using Firebase.Firestore;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Extensión del FirestoreProgressWriter para guardar scores detallados
/// NOTA: Si tu FirestoreProgressWriter ya existe, agrega estos métodos ahí
/// </summary>
public partial class FirestoreProgressWriter
{
    /// <summary>
    /// Guarda un score detallado de actividad en Firebase
    /// Estructura: users/{uid}/activities/{activityId}/attempts/{attemptId}
    /// </summary>
    public async Task RecordScoreAsync(string uid, ActivityScoreData scoreData)
    {
        if (string.IsNullOrEmpty(uid) || scoreData == null)
        {
            Debug.LogWarning("[FirestoreWriter] uid o scoreData nulo, skip");
            return;
        }

        try
        {
            var db = FirebaseFirestore.DefaultInstance;

            // Generar ID único para este intento
            string attemptId = System.Guid.NewGuid().ToString("N");

            // Crear documento del intento
            var attemptRef = db
                .Collection("users").Document(uid)
                .Collection("activities").Document(scoreData.activityId)
                .Collection("attempts").Document(attemptId);

            var attemptData = new Dictionary<string, object>
            {
                { "score", scoreData.score },
                { "stars", scoreData.stars },
                { "accuracy", scoreData.accuracy },
                { "timeSeconds", scoreData.timeSeconds },
                { "successes", scoreData.successes },
                { "errors", scoreData.errors },
                { "dispatches", scoreData.dispatches },
                { "customMessage", scoreData.customMessage ?? "" },
                { "timestamp", FieldValue.ServerTimestamp },
                { "isPersonalBest", false } // Se actualizará después
            };

            await attemptRef.SetAsync(attemptData);

            // Actualizar el personal best si aplica
            await UpdatePersonalBestAsync(uid, scoreData, attemptId);

            Debug.Log($"[FirestoreWriter] Score guardado: {scoreData.activityId} → {scoreData.score}/100");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[FirestoreWriter] Error guardando score: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Actualiza el personal best del usuario para esta actividad
    /// </summary>
    private async Task UpdatePersonalBestAsync(string uid, ActivityScoreData newScore, string attemptId)
    {
        try
        {
            var db = FirebaseFirestore.DefaultInstance;

            var pbRef = db
                .Collection("users").Document(uid)
                .Collection("activities").Document(newScore.activityId);

            // Leer el best actual
            var snapshot = await pbRef.GetSnapshotAsync();

            bool isNewBest = false;

            if (!snapshot.Exists)
            {
                // Primera vez - es el best
                isNewBest = true;
            }
            else
            {
                // Comparar con el best actual
                var currentBest = snapshot.ToDictionary();

                int currentStars = currentBest.ContainsKey("bestStars") ?
                    System.Convert.ToInt32(currentBest["bestStars"]) : 0;

                int currentScore = currentBest.ContainsKey("bestScore") ?
                    System.Convert.ToInt32(currentBest["bestScore"]) : 0;

                // Nuevo best si: más estrellas O mismas estrellas pero más score
                isNewBest = (newScore.stars > currentStars) ||
                           (newScore.stars == currentStars && newScore.score > currentScore);
            }

            if (isNewBest)
            {
                // Actualizar documento principal de la actividad
                var pbData = new Dictionary<string, object>
                {
                    { "bestScore", newScore.score },
                    { "bestStars", newScore.stars },
                    { "bestAccuracy", newScore.accuracy },
                    { "bestTime", newScore.timeSeconds },
                    { "bestAttemptId", attemptId },
                    { "lastUpdated", FieldValue.ServerTimestamp }
                };

                await pbRef.SetAsync(pbData, SetOptions.MergeAll);

                // Marcar el intento como personal best
                var attemptRef = pbRef.Collection("attempts").Document(attemptId);
                await attemptRef.UpdateAsync("isPersonalBest", true);

                Debug.Log($"[FirestoreWriter] ¡Nuevo personal best! {newScore.activityId}: {newScore.score}/100 ({newScore.stars}★)");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[FirestoreWriter] Error actualizando personal best: {ex.Message}");
        }
    }

    /// <summary>
    /// Lee el personal best de una actividad desde Firebase
    /// </summary>
    public async Task<ActivityScoreData> LoadPersonalBestAsync(string uid, string activityId)
    {
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(activityId))
            return null;

        try
        {
            var db = FirebaseFirestore.DefaultInstance;

            var pbRef = db
                .Collection("users").Document(uid)
                .Collection("activities").Document(activityId);

            var snapshot = await pbRef.GetSnapshotAsync();

            if (!snapshot.Exists)
                return null;

            var data = snapshot.ToDictionary();

            return new ActivityScoreData
            {
                activityId = activityId,
                score = data.ContainsKey("bestScore") ? System.Convert.ToInt32(data["bestScore"]) : 0,
                stars = data.ContainsKey("bestStars") ? System.Convert.ToInt32(data["bestStars"]) : 0,
                accuracy = data.ContainsKey("bestAccuracy") ? System.Convert.ToSingle(data["bestAccuracy"]) : 0f,
                timeSeconds = data.ContainsKey("bestTime") ? System.Convert.ToSingle(data["bestTime"]) : 0f
            };
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[FirestoreWriter] Error cargando personal best: {ex.Message}");
            return null;
        }
    }
}