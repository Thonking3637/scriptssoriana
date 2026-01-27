// CameraAAByQuality.cs
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class CameraAAByQuality : MonoBehaviour
{
    void OnEnable() => Apply();

    public void Apply()
    {
        var cam = GetComponent<Camera>();
        var data = cam ? cam.GetUniversalAdditionalCameraData() : null;
        if (!data) return;

        int q = QualitySettings.GetQualityLevel(); // 0=Low,1=Med,2=High (ajusta según tus nombres)
        // Low: FXAA (barato). Med/High: None (usan MSAA del URP Asset)
        data.antialiasing = (q == 0) ? AntialiasingMode.FastApproximateAntialiasing
                                     : AntialiasingMode.None;
    }
}
