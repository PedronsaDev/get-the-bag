using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Controla todo o HUD. Chame os métodos Set* a partir do seu jogo.
public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Vida")]
    public Image healthFill;          // barra (escala em X, pivot à esquerda)
    public TMP_Text healthText;
    public Image damageVignette;      // imagem vermelha em tela cheia
    [Range(0f, 1f)] public float lowHealthThreshold = 0.3f;

    [Header("Munição")]
    public TMP_Text magText;
    public TMP_Text reserveText;
    public TMP_Text statusText;

    [Header("Interação")]
    public CanvasGroup interactGroup;
    public Image interactFill;        // barra (escala em X, pivot à esquerda)
    public TMP_Text interactText;

    [Header("Relógio de pulso")]
    public TMP_Text watchTime;
    public TMP_Text watchCenti;
    public Color watchNormal = new Color(0.11f, 0.16f, 0.08f);
    public Color watchUrgent = new Color(0.55f, 0.06f, 0.06f);

    static readonly Color Green  = new Color(0.24f, 0.86f, 0.52f);
    static readonly Color Yellow = new Color(1f, 0.7f, 0f);
    static readonly Color Red    = new Color(1f, 0.24f, 0.29f);
    static readonly Color Dim    = new Color(0.55f, 0.62f, 0.67f);

    float targetHealth = 1f, shownHealth = 1f, vignetteAlpha;
    int mag; bool reloading;

    void Awake()
    {
        Instance = this;
        HideInteract();
    }

    void Update()
    {
        // vida suavizada + cor
        shownHealth = Mathf.MoveTowards(shownHealth, targetHealth, Time.unscaledDeltaTime * 1.5f);
        healthFill.rectTransform.localScale = new Vector3(shownHealth, 1f, 1f);
        healthFill.color = shownHealth > 0.5f
            ? Color.Lerp(Yellow, Green, (shownHealth - 0.5f) * 2f)
            : Color.Lerp(Red, Yellow, shownHealth * 2f);

        // vinheta de dano + pulso com vida baixa
        bool low = targetHealth > 0f && targetHealth <= lowHealthThreshold;
        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.unscaledDeltaTime * 2f);
        float pulse = low ? 0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
        var c = damageVignette.color; c.a = Mathf.Max(vignetteAlpha, pulse); damageVignette.color = c;
        healthText.color = low ? Red : Color.white;
    }

    // ---------- VIDA ----------
    public void SetHealth(float current, float max)
    {
        targetHealth = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        healthText.text = Mathf.CeilToInt(current).ToString();
    }
    public void FlashDamage(float intensity = 0.5f) => vignetteAlpha = Mathf.Max(vignetteAlpha, intensity);

    // ---------- MUNIÇÃO ----------
    public void SetAmmo(int magazine, int reserve)
    {
        mag = magazine;
        magText.text = magazine.ToString("00");
        magText.color = magazine == 0 ? Red : Color.white;
        reserveText.text = "/ " + reserve;
        RefreshStatus();
    }
    public void SetReloading(bool value) { reloading = value; RefreshStatus(); }
    void RefreshStatus()
    {
        if (reloading)      { statusText.text = "RECARREGANDO";   statusText.color = Yellow; }
        else if (mag == 0)  { statusText.text = "RECARREGUE [R]"; statusText.color = Red; }
        else                { statusText.text = "PRONTA";         statusText.color = Dim; }
    }

    // ---------- INTERAÇÃO ----------
    public void ShowInteract(string prompt) { interactText.text = prompt; interactGroup.alpha = 1f; SetInteractProgress(0f); }
    public void HideInteract() { interactGroup.alpha = 0f; SetInteractProgress(0f); }
    public void SetInteractProgress(float t01) =>
        interactFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(t01), 1f, 1f);

    // ---------- RELÓGIO ----------
    public void SetMissionTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int m = (int)(seconds / 60f), s = (int)(seconds % 60f), cs = (int)((seconds % 1f) * 100f);
        string sep = (Time.unscaledTime % 1f) < 0.5f ? ":" : " ";   // dois-pontos piscando
        watchTime.text = $"{m:00}{sep}{s:00}";
        watchCenti.text = cs.ToString("00");
        Color col = seconds < 60f ? watchUrgent : watchNormal;
        watchTime.color = col; watchCenti.color = col;
    }
}
