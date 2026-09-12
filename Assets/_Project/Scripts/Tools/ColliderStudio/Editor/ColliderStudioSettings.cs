using System;
using UnityEditor;
using UnityEngine;

namespace Project.Tools.ColliderStudio.Editor
{
    public enum ColliderColorMode
    {
        AutoByType,
        SolidColor,
        TriggerColor,
        CustomColor
    }

    public enum HandleInteractionMode
    {
        All,
        CornersOnly,
        FacesOnly,
        CenterOnly
    }

    public static class ColliderStudioSettings
    {
        public static event Action OnSettingsChanged;

        private const string PrefPrefix = "ColliderStudio_";

        public static bool ToolEnabled
        {
            get => EditorPrefs.GetBool(PrefPrefix + "ToolEnabled", true);
            set { EditorPrefs.SetBool(PrefPrefix + "ToolEnabled", value); NotifyChanged(); }
        }

        public static HandleInteractionMode InteractionMode
        {
            get => (HandleInteractionMode)EditorPrefs.GetInt(PrefPrefix + "InteractionMode", (int)HandleInteractionMode.All);
            set { EditorPrefs.SetInt(PrefPrefix + "InteractionMode", (int)value); NotifyChanged(); }
        }

        public static float HandleScale
        {
            get => EditorPrefs.GetFloat(PrefPrefix + "HandleScale", 1.0f);
            set { EditorPrefs.SetFloat(PrefPrefix + "HandleScale", Mathf.Clamp(value, 0.1f, 3f)); NotifyChanged(); }
        }

        public static bool CornerSymmetric
        {
            get => EditorPrefs.GetBool(PrefPrefix + "CornerSymmetric", false);
            set { EditorPrefs.SetBool(PrefPrefix + "CornerSymmetric", value); NotifyChanged(); }
        }

        public static bool FaceSymmetric
        {
            get => EditorPrefs.GetBool(PrefPrefix + "FaceSymmetric", false);
            set { EditorPrefs.SetBool(PrefPrefix + "FaceSymmetric", value); NotifyChanged(); }
        }

        public static bool SnapEnabled
        {
            get => EditorPrefs.GetBool(PrefPrefix + "SnapEnabled", false);
            set { EditorPrefs.SetBool(PrefPrefix + "SnapEnabled", value); NotifyChanged(); }
        }

        public static float SnapValue
        {
            get => EditorPrefs.GetFloat(PrefPrefix + "SnapValue", 0.1f);
            set { EditorPrefs.SetFloat(PrefPrefix + "SnapValue", Mathf.Max(0.001f, value)); NotifyChanged(); }
        }

        public static bool FillFaces
        {
            get => EditorPrefs.GetBool(PrefPrefix + "FillFaces", true);
            set { EditorPrefs.SetBool(PrefPrefix + "FillFaces", value); NotifyChanged(); }
        }

        public static float FillAlpha
        {
            get => EditorPrefs.GetFloat(PrefPrefix + "FillAlpha", 0.18f);
            set { EditorPrefs.SetFloat(PrefPrefix + "FillAlpha", Mathf.Clamp01(value)); NotifyChanged(); }
        }

        public static float WireframeAlpha
        {
            get => EditorPrefs.GetFloat(PrefPrefix + "WireframeAlpha", 0.95f);
            set { EditorPrefs.SetFloat(PrefPrefix + "WireframeAlpha", Mathf.Clamp01(value)); NotifyChanged(); }
        }

        public static bool XRay
        {
            get => EditorPrefs.GetBool(PrefPrefix + "XRay", false);
            set { EditorPrefs.SetBool(PrefPrefix + "XRay", value); NotifyChanged(); }
        }

        public static ColliderColorMode ColorMode
        {
            get => (ColliderColorMode)EditorPrefs.GetInt(PrefPrefix + "ColorMode", (int)ColliderColorMode.AutoByType);
            set { EditorPrefs.SetInt(PrefPrefix + "ColorMode", (int)value); NotifyChanged(); }
        }

        public static Color SolidColor
        {
            get => GetColor(PrefPrefix + "SolidColor", new Color(0f, 0.9f, 1f, 1f)); // Electric Cyan
            set { SetColor(PrefPrefix + "SolidColor", value); NotifyChanged(); }
        }

        public static Color TriggerColor
        {
            get => GetColor(PrefPrefix + "TriggerColor", new Color(1f, 0.57f, 0f, 1f)); // Vibrant Amber
            set { SetColor(PrefPrefix + "TriggerColor", value); NotifyChanged(); }
        }

        public static Color CustomColor
        {
            get => GetColor(PrefPrefix + "CustomColor", new Color(0.46f, 1f, 0.01f, 1f)); // Lime
            set { SetColor(PrefPrefix + "CustomColor", value); NotifyChanged(); }
        }

        public static Color GetEffectiveColor(Collider collider)
        {
            Color baseColor;
            switch (ColorMode)
            {
                case ColliderColorMode.SolidColor:
                    baseColor = SolidColor;
                    break;
                case ColliderColorMode.TriggerColor:
                    baseColor = TriggerColor;
                    break;
                case ColliderColorMode.CustomColor:
                    baseColor = CustomColor;
                    break;
                case ColliderColorMode.AutoByType:
                default:
                    baseColor = (collider != null && collider.isTrigger) ? TriggerColor : SolidColor;
                    break;
            }
            return baseColor;
        }

        private static Color GetColor(string key, Color defaultVal)
        {
            string hex = EditorPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out Color c))
            {
                return c;
            }
            return defaultVal;
        }

        private static void SetColor(string key, Color c)
        {
            EditorPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(c));
        }

        private static void NotifyChanged()
        {
            OnSettingsChanged?.Invoke();
            SceneView.RepaintAll();
        }
    }
}
