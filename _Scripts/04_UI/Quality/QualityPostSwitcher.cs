using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // para controlar post en la cámara

public class QualityPostSwitcher : MonoBehaviour
{
    public Volume globalVolume;          // arrástralo; si no, lo busca
    public VolumeProfile postMed;        // perfil para Medium
    public VolumeProfile postHigh;       // perfil para High

    void Awake()
    {
        if (!globalVolume)
        {
            // intenta encontrar un Volume global en escena (aunque esté desactivado)
            foreach (var v in Resources.FindObjectsOfTypeAll<Volume>())
                if (v.isGlobal) { globalVolume = v; break; }
        }
    }

    void OnEnable() => ApplyForCurrent();  // aplica al entrar a la escena

    public void ApplyForCurrent()
    {
        int i = QualitySettings.GetQualityLevel(); // 0=Low, 1=Med, 2=High
        if (!globalVolume) { Debug.LogWarning("[QualityPostSwitcher] No hay Volume global."); return; }

        // Asegurar que el volume sea global y completo
        globalVolume.isGlobal = true;
        globalVolume.weight = 1f;

        // Cámara: activar post solo en Med/High
        var cad = Camera.main ? Camera.main.GetUniversalAdditionalCameraData() : null;
        if (cad) cad.renderPostProcessing = (i != 0);

        if (i == 0)
        {
            // Low: sin post
            globalVolume.enabled = false;           // (alternativa: dejar enabled y poner weight=0)
            return;
        }

        // Med/High: encender y escoger perfil
        globalVolume.enabled = true;
        if (i == 2 && postHigh) globalVolume.sharedProfile = postHigh;
        else if (postMed) globalVolume.sharedProfile = postMed;
        // si faltara algún perfil, deja el que ya tenga asignado
    }
}
