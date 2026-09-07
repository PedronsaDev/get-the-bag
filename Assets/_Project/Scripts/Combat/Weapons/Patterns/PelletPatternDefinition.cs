using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    public enum PatternShape
    {
        HorizontalLine, // Sand Hawk, Tidal Wave
        VerticalLine,   // Vertical slash
        Grid3x3,        // Blockhead (9 pellets)
        Grid2x2,        // 4 pellets
        Ring,           // Donut / circle
        Cross,          // Plus (+) shape
        XShape,         // Diagonal X
        Heart,          // Heart Breaker cardioid
        Custom          // Designer-defined (X, Y) points
    }

    /// <summary>
    /// ScriptableObject defining a deterministic firing pattern for multi-pellet weapons
    /// (e.g. Sand Hawk horizontal line, Blockhead 3x3 square matrix, Heart Breaker, etc.).
    /// </summary>
    [CreateAssetMenu(fileName = "PAT_Shape_CountP_New", menuName = "Combat/Pellet Pattern")]
    public class PelletPatternDefinition : ScriptableObject
    {
        [SerializeField] private string _patternId = "pat_new";
        [SerializeField] private string _displayName = "New Pattern";
        [TextArea(2, 4)]
        [SerializeField] private string _description;

        [Header("Pattern Geometry")]
        [SerializeField] private PatternShape _shape = PatternShape.HorizontalLine;
        [Tooltip("Number of pellets this pattern discharges. For fixed grids (e.g. 3x3 = 9, 2x2 = 4), this is automatically enforced.")]
        [Range(1, 32)]
        [SerializeField] private int _pelletCount = 5;

        [Tooltip("Angular spacing between pellets in degrees.")]
        [Range(0.1f, 15f)]
        [SerializeField] private float _spacingDegrees = 1.2f;

        [Tooltip("Small random deviation (0 to 0.3) around each pattern point so it feels like authentic gunfire.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _microJitter = 0.03f;

        [Tooltip("If true, the pattern scales up as accuracy bloom/spread increases. If false, the pattern retains its exact fixed angular size.")]
        [SerializeField] private bool _scaleWithSpread = true;

        [Tooltip("Multiplier for spread expansion. At 1.0, the pattern's outer boundary expands directly with the weapon's spread cone.")]
        [Range(0.1f, 5f)]
        [SerializeField] private float _spreadMultiplier = 1.0f;

        [Tooltip("Additional random pellet scatter that grows with weapon bloom, adding authentic bullet dispersion during rapid fire.")]
        [Range(0f, 1f)]
        [SerializeField] private float _bloomScatter = 0.1f;

        [Tooltip("Custom 2D coordinate offsets (in normalized -1 to +1 space) used when Shape is set to Custom.")]
        [SerializeField] private List<Vector2> _customPoints = new();

        public string PatternId => string.IsNullOrEmpty(_patternId) ? name : _patternId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Description => _description;
        public PatternShape Shape => _shape;
        public int PelletCount => GetEnforcedPelletCount();
        public float SpacingDegrees => _spacingDegrees;
        public float MicroJitter => _microJitter;
        public bool ScaleWithSpread => _scaleWithSpread;
        public float SpreadMultiplier => _spreadMultiplier;
        public float BloomScatter => _bloomScatter;

        private int GetEnforcedPelletCount()
        {
            return _shape switch
            {
                PatternShape.Grid3x3 => 9,
                PatternShape.Grid2x2 => 4,
                PatternShape.Custom when _customPoints != null && _customPoints.Count > 0 => _customPoints.Count,
                _ => Mathf.Max(1, _pelletCount)
            };
        }

        /// <summary>
        /// Calculates the (yaw, pitch) angular offset in degrees for a given pellet index.
        /// </summary>
        public Vector2 GetPointOffset(int pelletIndex, int totalPellets, float currentSpread, float baseSpread)
        {
            Vector2 rawCoord = CalculateRawCoordinate(pelletIndex, totalPellets);

            float spreadScale = _spacingDegrees;

            if (_scaleWithSpread)
            {
                currentSpread = Mathf.Max(0f, currentSpread);
                baseSpread = Mathf.Max(0.001f, baseSpread);
                float extraBloom = Mathf.Max(0f, currentSpread - baseSpread);

                // 1. Direct cone mapping: outermost pellets expand directly with the weapon's spread angle
                float coneScale = currentSpread * _spreadMultiplier;

                // 2. Dynamic bloom expansion: extra bloom expands authored pattern spacing
                float bloomScale = _spacingDegrees + (extraBloom * _spreadMultiplier);

                // 3. Proportional ratio scaling: handles relative growth
                float ratioScale = _spacingDegrees * (currentSpread / baseSpread) * _spreadMultiplier;

                // Use the maximum of all scaling methods so the pattern ALWAYS expands noticeably
                // whether base spread is large, extra bloom is large, or ratio is large!
                spreadScale = Mathf.Max(_spacingDegrees, Mathf.Max(coneScale, Mathf.Max(bloomScale, ratioScale)));
            }

            Vector2 offsetDegrees = rawCoord * spreadScale;

            // Apply organic micro-jitter and bloom scatter
            float totalJitter = _microJitter;
            if (_scaleWithSpread && _bloomScatter > 0.0001f)
            {
                // Dynamic scatter increases as spread expands
                totalJitter += _bloomScatter * Mathf.Clamp01(currentSpread / 8f);
            }

            if (totalJitter > 0.0001f)
            {
                float jitterAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float jitterMag = UnityEngine.Random.Range(0f, totalJitter * spreadScale);
                offsetDegrees.x += Mathf.Cos(jitterAngle) * jitterMag;
                offsetDegrees.y += Mathf.Sin(jitterAngle) * jitterMag;
            }

            return offsetDegrees;
        }

        private Vector2 CalculateRawCoordinate(int index, int total)
        {
            total = Mathf.Max(1, total);

            switch (_shape)
            {
                case PatternShape.HorizontalLine:
                    if (total == 1) return Vector2.zero;
                    float tHoriz = (float)index / (total - 1);
                    return new Vector2(Mathf.Lerp(-1f, 1f, tHoriz), 0f);

                case PatternShape.VerticalLine:
                    if (total == 1) return Vector2.zero;
                    float tVert = (float)index / (total - 1);
                    return new Vector2(0f, Mathf.Lerp(-1f, 1f, tVert));

                case PatternShape.Grid3x3:
                    int col3 = (index % 3) - 1;
                    int row3 = 1 - (index / 3);
                    return new Vector2(col3, row3);

                case PatternShape.Grid2x2:
                    int col2 = (index % 2 == 0) ? -1 : 1;
                    int row2 = (index < 2) ? 1 : -1;
                    return new Vector2(col2, row2);

                case PatternShape.Ring:
                    float angleRad = (index / (float)total) * Mathf.PI * 2f;
                    return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

                case PatternShape.Cross:
                    if (index == 0) return Vector2.zero;
                    int arm = (index - 1) % 4;
                    int tier = ((index - 1) / 4) + 1;
                    float dist = tier * 0.5f;
                    return arm switch
                    {
                        0 => new Vector2(0f, dist),
                        1 => new Vector2(dist, 0f),
                        2 => new Vector2(0f, -dist),
                        _ => new Vector2(-dist, 0f)
                    };

                case PatternShape.XShape:
                    if (index == 0) return Vector2.zero;
                    int xArm = (index - 1) % 4;
                    int xTier = ((index - 1) / 4) + 1;
                    float xDist = xTier * 0.5f;
                    return xArm switch
                    {
                        0 => new Vector2(xDist, xDist),
                        1 => new Vector2(xDist, -xDist),
                        2 => new Vector2(-xDist, -xDist),
                        _ => new Vector2(-xDist, xDist)
                    };

                case PatternShape.Heart:
                    float tHeart = (index / (float)total) * Mathf.PI * 2f;
                    float sinT = Mathf.Sin(tHeart);
                    float hx = 16f * sinT * sinT * sinT;
                    float hy = 13f * Mathf.Cos(tHeart) - 5f * Mathf.Cos(2f * tHeart) - 2f * Mathf.Cos(3f * tHeart) - Mathf.Cos(4f * tHeart);
                    return new Vector2(hx / 16f, (hy + 2.5f) / 14.5f);

                case PatternShape.Custom:
                    if (_customPoints != null && _customPoints.Count > 0)
                    {
                        return _customPoints[index % _customPoints.Count];
                    }
                    return Vector2.zero;

                default:
                    return Vector2.zero;
            }
        }

        /// <summary>
        /// Converts a base forward vector and 2D angular offsets (in degrees) into a 3D discharge direction.
        /// </summary>
        public static Vector3 CalculatePatternDirection(Vector3 baseDirection, Vector3 aimRight, Vector3 aimUp, Vector2 angularOffsetDegrees)
        {
            Quaternion yaw = Quaternion.AngleAxis(angularOffsetDegrees.x, aimUp);
            Quaternion pitch = Quaternion.AngleAxis(-angularOffsetDegrees.y, aimRight);
            return (yaw * pitch) * baseDirection;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_patternId))
            {
                _patternId = name;
            }
        }
#endif
    }
}
