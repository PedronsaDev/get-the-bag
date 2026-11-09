using UnityEngine;

namespace Plugins.Outline
{
    [CreateAssetMenu(menuName = "Presentation/Mesh Outline Style")]
    public sealed class MeshOutlineStyle : ScriptableObject
    {
        [SerializeField] internal MeshOutline.Mode outlineMode = MeshOutline.Mode.OutlineVisible;
        [SerializeField, ColorUsage(true, true)] internal Color outlineColor = Color.white;
        [SerializeField, Min(0f)] internal float outlineWidth = 5f;
        [SerializeField] internal MeshOutline.WidthMode widthMode = MeshOutline.WidthMode.ScreenSpacePixels;
        [SerializeField] internal bool enableSurfaceFill = false;
        [SerializeField, ColorUsage(true, true)] internal Color fillColor = new Color(0.2f, 0.8f, 1f, 0.4f);
        [SerializeField, ColorUsage(true, true)] internal Color fillSecondaryColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] internal MeshOutline.PatternType fillPattern = MeshOutline.PatternType.Stripes;
        [SerializeField] internal Texture2D fillTexture;
        [SerializeField] internal Vector2 fillTiling = new Vector2(10f, 10f);
        [SerializeField, Range(0f, 360f)] internal float fillAngle = 45f;
        [SerializeField] internal float fillScrollSpeed = 1f;
        [SerializeField, Min(0.1f)] internal float fresnelPower = 2.5f;
        [SerializeField] internal MeshOutline.CoordSpace fillSpace = MeshOutline.CoordSpace.ObjectUV;
        [SerializeField] internal MeshOutline.FillOcclusion fillOcclusion = MeshOutline.FillOcclusion.Always;
        [SerializeField] internal bool pulseEnabled = false;
        [SerializeField] internal float pulseSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)] internal float pulseAmplitude = 0.35f;
        [SerializeField] internal bool distanceFadeEnabled = false;
        [SerializeField, Min(0f)] internal float fadeMinDistance = 15f;
        [SerializeField, Min(0.1f)] internal float fadeMaxDistance = 45f;
        [SerializeField] internal bool jitterEnabled = false;
        [SerializeField] internal float jitterSpeed = 2.0f;
        [SerializeField] internal float jitterFrequency = 3.0f;
        [SerializeField, Range(0f, 1f)] internal float jitterScale = 0.5f;
        [SerializeField] internal float jitterFramerate = 5.0f;
        [SerializeField] internal bool unscaledEffects;
        [SerializeField] internal bool twoSidedSurface;
        [SerializeField, Range(0.01f, 0.49f)] internal float patternLineWidth = 0.12f;
        [SerializeField] internal float animationPhase;
    }
}
