using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

public class FirestoreProgressWriter : MonoBehaviour
{
    private FirebaseFirestore _db;

    private async void Start()
    {
        // ✅ Espera a que tu bootstrap termine (auth/firestore quedan listos)
        await FirebaseBootstrap.EnsureFirebaseReady();
        _db = FirebaseFirestore.DefaultInstance;
    }

    public async Task RecordMedalAsync(string uid, string medalId)
    {
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(medalId)) return;

        if (_db == null)
        {
            await FirebaseBootstrap.EnsureFirebaseReady();
            _db = FirebaseFirestore.DefaultInstance;
        }

        // ✅ users/{uid}/medals/{medalId}
        var doc = _db.Collection("users").Document(uid).Collection("medals").Document(medalId);

        var data = new Dictionary<string, object>
        {
            { "activityId", medalId },
            { "earnedAt", Timestamp.GetCurrentTimestamp() }
        };

        await doc.SetAsync(data, SetOptions.MergeAll);
    }

    public async Task<List<string>> LoadUserMedalsAsync(string uid)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(uid)) return result;

        if (_db == null)
        {
            await FirebaseBootstrap.EnsureFirebaseReady();
            _db = FirebaseFirestore.DefaultInstance;
        }

        // ✅ MISMA ruta que RecordMedalAsync
        var col = _db.Collection("users").Document(uid).Collection("medals");
        var snap = await col.GetSnapshotAsync();

        foreach (var doc in snap.Documents)
            if (doc.Exists) result.Add(doc.Id);

        return result;
    }
}
