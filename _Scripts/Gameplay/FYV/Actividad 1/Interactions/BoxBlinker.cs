using UnityEngine;

public class BoxBlinker : MonoBehaviour
{
    [SerializeField] Renderer[] renderers;
    [SerializeField] Color emissionColor = Color.white;
    [SerializeField] float minIntensity = 0.0f;
    [SerializeField] float maxIntensity = 1.2f;
    [SerializeField] float speed = 2f;

    Material[] mats;
    bool playing;
    float t;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        // Materiales instanciados
        var list = new System.Collections.Generic.List<Material>();
        foreach (var r in renderers)
        {
            if (!r) continue;
            foreach (var m in r.materials)
            {
                if (!m) continue;
                m.EnableKeyword("_EMISSION");
                list.Add(m);
            }
        }
        mats = list.ToArray();
    }

    void Update()
    {
        if (!playing || mats == null || mats.Length == 0) return;

        t += Time.unscaledDeltaTime * speed;
        float pulse = (Mathf.Sin(t) * 0.5f + 0.5f); // 0..1
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, pulse);
        Color c = emissionColor * Mathf.LinearToGammaSpace(intensity);

        for (int i = 0; i < mats.Length; i++)
            mats[i].SetColor("_EmissionColor", c);
    }

    public void Play() { playing = true; }
    public void Stop()
    {
        playing = false;
        // apaga emisión
        if (mats != null)
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetColor("_EmissionColor", Color.black);
    }
}
