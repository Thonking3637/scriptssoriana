// LevelItemCompletionBadge.cs (v2)
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(LevelItemMeta))]
public class LevelItemCompletionBadge : MonoBehaviour
{
    [Header("UI")]
    public Image checkIcon;           // ✔ cuando está completado
    public Image lockIcon;            // opcional (mostrar si pendiente)
    public TextMeshProUGUI subLabel;  // opcional: "Completada" o "X/Y" en escenas
    [Range(0, 1f)] public float dimIncomplete = 0.85f;

    [Header("Contexto para actividades")]
    [Tooltip("Si este item representa una ACTIVIDAD, usa esta escena como contexto. " +
             "Si está vacío, se usará LevelItemMeta.sceneName.")]
    public string activitySceneNameOverride;

    LevelItemMeta meta;
    Graphic selfGraphic;

    void Awake()
    {
        meta = GetComponent<LevelItemMeta>();
        selfGraphic = GetComponent<Graphic>();
    }

    void OnEnable() => Refresh();

    public void Refresh()
    {
        bool done = false;

        switch (meta.launchType)
        {
            case LevelLaunchType.ActivityIndex:
                {
                    // ✔ PARA ACTIVIDADES (SETC):
                    // necesitamos escena + índice de actividad
                    string sceneCtx = string.IsNullOrEmpty(activitySceneNameOverride)
                                      ? meta.sceneName
                                      : activitySceneNameOverride;

                    if (string.IsNullOrEmpty(sceneCtx))
                    {
                        Debug.LogWarning("[LevelItemCompletionBadge] Falta sceneName para actividad.");
                        break;
                    }

                    done = CompletionService.IsActivityDone(sceneCtx, meta.activityIndex);

                    if (subLabel) subLabel.text = done ? "Completada" : "";
                    break;
                }

            case LevelLaunchType.SceneByName:
            default:
                {
                    // ✔ PARA ESCENAS/DÍAS (Línea de Cajas):
                    // check cuando X/Y == 100%
                    if (string.IsNullOrEmpty(meta.sceneName))
                    {
                        Debug.LogWarning("[LevelItemCompletionBadge] Falta sceneName para escena/día.");
                        break;
                    }

                    var (x, y) = CompletionService.GetSceneProgress(meta.sceneName);
                    done = CompletionService.IsSceneCompleted(meta.sceneName) || (y > 0 && x >= y);

                    if (subLabel) subLabel.text = y > 0 ? $"{x}/{y}" : "";
                    break;
                }
        }

        if (checkIcon) checkIcon.enabled = done;
        if (lockIcon) lockIcon.enabled = !done;

        // Atenuar el card cuando no está completo (opcional)
        if (selfGraphic) selfGraphic.canvasRenderer.SetAlpha(done ? 1f : dimIncomplete);
    }
}
