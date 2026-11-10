#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Menu: Tools > Shooter HUD > Criar HUD na cena
/// Monta Canvas, barras, munição, prompt de interação e relógio de pulso, já ligados ao HUDManager.
public static class HUDBuilder
{
    static readonly Color Panel  = new Color(0.03f, 0.05f, 0.06f, 0.75f);
    static readonly Color Dim    = new Color(0.55f, 0.62f, 0.67f);
    static readonly Color Accent = new Color(1f, 0.7f, 0f);
    static readonly Color Ink    = new Color(0.11f, 0.16f, 0.08f);

    static Vector2 V(float x, float y) => new Vector2(x, y);

    [MenuItem("Tools/Shooter HUD/Criar HUD na cena")]
    static void Build()
    {
        var root = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Criar HUD");
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = V(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var hud = root.AddComponent<HUDManager>();
        root.AddComponent<MissionTimer>();

        // Flash/vinheta de dano
        hud.damageVignette = Stretch("DamageVignette", root.transform, new Color(1f, 0f, 0.05f, 0f));

        // ---- VIDA (inferior esquerdo) ----
        var hp = Img("Vida", root.transform, V(0, 0), V(0, 0), V(40, 40), V(520, 130), Panel);
        Txt("Label", hp.transform, V(0, 1), V(0, 1), V(20, -12), V(300, 24), "INTEGRIDADE", 18, Dim, TextAlignmentOptions.TopLeft);
        hud.healthText = Txt("HP", hp.transform, V(0, 1), V(0, 1), V(20, -34), V(300, 64), "100", 52, Color.white, TextAlignmentOptions.Left);
        var hpBg = Img("BarraBg", hp.transform, V(0, 0), V(0, 0), V(20, 18), V(480, 22), new Color(1, 1, 1, 0.1f));
        hud.healthFill = Fill(hpBg.transform, Color.green);

        // ---- MUNIÇÃO (inferior direito) ----
        var am = Img("Municao", root.transform, V(1, 0), V(1, 0), V(-40, 40), V(400, 150), Panel);
        Txt("Label", am.transform, V(1, 1), V(1, 1), V(-20, -12), V(300, 24), "MUNIÇÃO", 18, Dim, TextAlignmentOptions.TopRight);
        hud.magText = Txt("Pente", am.transform, V(1, 1), V(1, 1), V(-145, -30), V(240, 100), "30", 80, Color.white, TextAlignmentOptions.Right);
        hud.reserveText = Txt("Reserva", am.transform, V(1, 1), V(1, 1), V(-15, -62), V(130, 50), "/ 120", 36, Dim, TextAlignmentOptions.Left);
        hud.statusText = Txt("Status", am.transform, V(1, 0), V(1, 0), V(-20, 14), V(360, 28), "PRONTA", 20, Dim, TextAlignmentOptions.Right);

        // ---- INTERAÇÃO (centro inferior) ----
        var it = Img("Interacao", root.transform, V(0.5f, 0), V(0.5f, 0), V(0, 40), V(460, 100), Panel);
        hud.interactGroup = it.gameObject.AddComponent<CanvasGroup>();
        hud.interactText = Txt("Prompt", it.transform, V(0.5f, 1), V(0.5f, 1), V(0, -14), V(430, 40), "[E] Interagir", 26, Color.white, TextAlignmentOptions.Center);
        var ibBg = Img("BarraBg", it.transform, V(0.5f, 0), V(0.5f, 0), V(0, 20), V(420, 14), new Color(1, 1, 1, 0.12f));
        hud.interactFill = Fill(ibBg.transform, Accent);

        // ---- RELÓGIO DE PULSO (superior direito) ----
        var watch = Rect("RelogioPulso", root.transform, V(1, 1), V(1, 1), V(-60, -110), V(280, 150));
        var strapCol = new Color(0.08f, 0.09f, 0.10f);
        Img("PulseiraCima",  watch, V(0.5f, 1), V(0.5f, 0), V(0, -8), V(130, 90), strapCol);
        Img("PulseiraBaixo", watch, V(0.5f, 0), V(0.5f, 1), V(0,  8), V(130, 90), strapCol);
        var cs = Img("Caixa", watch, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0, 0), V(280, 150), new Color(0.20f, 0.22f, 0.25f));
        var lcd = Img("Visor", cs.transform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0, 0), V(244, 116), new Color(0.72f, 0.82f, 0.60f));
        Txt("Titulo", lcd.transform, V(0, 1), V(0, 1), V(12, -8), V(140, 22), "MISSÃO", 16, Ink, TextAlignmentOptions.TopLeft);
        hud.watchTime = Txt("Tempo", lcd.transform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0, 4), V(230, 70), "10:00", 62, Ink, TextAlignmentOptions.Center);
        hud.watchCenti = Txt("Centesimos", lcd.transform, V(1, 0), V(1, 0), V(-12, 6), V(80, 22), "00", 16, Ink, TextAlignmentOptions.BottomRight);

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }

    // ---------- helpers ----------
    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = anchor; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    static Image Img(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c)
    {
        var i = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<Image>();
        i.color = c; i.raycastTarget = false;
        return i;
    }

    static Image Stretch(string name, Transform parent, Color c)
    {
        var i = Img(name, parent, V(0, 0), V(0.5f, 0.5f), V(0, 0), V(0, 0), c);
        i.rectTransform.anchorMin = V(0, 0); i.rectTransform.anchorMax = V(1, 1);
        i.rectTransform.offsetMin = i.rectTransform.offsetMax = V(0, 0);
        return i;
    }

    static Image Fill(Transform parent, Color c)
    {
        var i = Stretch("Fill", parent, c);
        i.rectTransform.pivot = V(0, 0.5f);   // escala a partir da esquerda
        return i;
    }

    static TextMeshProUGUI Txt(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                               string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        var t = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = fontSize; t.color = color; t.alignment = align;
        t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
        return t;
    }
}
#endif
