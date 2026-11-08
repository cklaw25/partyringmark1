// Camera image effect driving Resources/VaultDanger.shader (built-in pipeline OnRenderImage).
// VaultGame sets danger (0..1, how close THE BROKER is), pulse (heartbeat) and flash (spotted).
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class DangerFX : MonoBehaviour
{
    [Range(0, 1)] public float danger, pulse, flash;
    public float grain = 0.045f, vignette = 0.35f;
    public bool reduceEffects; // tones down blur/fringe (and the zoom, applied by the player)
    Material mat;

    void Awake()
    {
        var sh = Shader.Find("Hidden/VaultDanger");
        if (sh != null && sh.isSupported) mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
    }

    void Update() { flash = Mathf.MoveTowards(flash, 0, Time.deltaTime * 1.5f); pulse = Mathf.MoveTowards(pulse, 0, Time.deltaTime * 3f); }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null) { Graphics.Blit(src, dst); return; }
        mat.SetFloat("_Danger", danger);
        mat.SetFloat("_Blur", danger * (reduceEffects ? 0.3f : 1f));
        mat.SetFloat("_Pulse", pulse);
        mat.SetFloat("_Flash", flash);
        mat.SetFloat("_Grain", grain);
        mat.SetFloat("_Vig", vignette);
        Graphics.Blit(src, dst, mat);
    }
}
