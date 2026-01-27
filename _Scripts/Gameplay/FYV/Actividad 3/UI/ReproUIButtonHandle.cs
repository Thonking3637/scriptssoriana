using UnityEngine;

public class ReproUIButtonHandle : MonoBehaviour
{
    [HideInInspector] public ReproSpawnerUI owner;
    public void RecycleSelf()
    {
        if (owner) owner.Recycle(gameObject);
        else Destroy(gameObject);
    }
}
