using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat.Presentation
{
/// <summary>
/// Cosmetic bullet tracers: a short glowing streak that flies from the muzzle to where the shot ended.
/// Pooled, no allocations per shot. Call <see cref="Spawn"/> from presentation code only.
/// </summary>
public sealed class BulletTracers : MonoBehaviour
{
    private const string MATERIAL_PATH = "VFX/M_Tracer";
    private const int POOL_SIZE = 32;

    // Tuning. Static so it can be changed from one place without a prefab.
    public static float Speed = 260f;
    public static float StreakLength = 3.5f;
    public static float Width = 0.035f;
    public static float MinDistance = 2f;

    private sealed class Tracer
    {
        public LineRenderer Line;
        public Vector3 Start;
        public Vector3 Direction;
        public float Distance;
        public float Travelled;
    }

    private static BulletTracers _instance;

    private readonly Stack<Tracer> _free = new(POOL_SIZE);
    private readonly List<Tracer> _active = new(POOL_SIZE);
    private Material _material;

    public static void Spawn(Vector3 start, Vector3 end)
    {
        if (!Application.isPlaying) return;

        var offset = end - start;
        float distance = offset.magnitude;
        if (!float.IsFinite(distance) || distance < MinDistance) return;

        if (!_instance) _instance = Create();
        _instance.Play(start, offset / distance, distance);
    }

    private static BulletTracers Create()
    {
        var go = new GameObject("[BulletTracers]");
        DontDestroyOnLoad(go);
        return go.AddComponent<BulletTracers>();
    }

    private void Awake()
    {
        _material = Resources.Load<Material>(MATERIAL_PATH);
        if (!_material)
            Debug.LogWarning($"BulletTracers: missing material at Resources/{MATERIAL_PATH}; tracers will not show.");

        for (int i = 0; i < POOL_SIZE; i++)
            _free.Push(CreateTracer());
    }

    private Tracer CreateTracer()
    {
        var go = new GameObject("Tracer");
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = _material;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.allowOcclusionWhenDynamic = false;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
        line.colorGradient = TailFadeGradient();
        line.enabled = false;

        return new Tracer { Line = line };
    }

    // Point 0 is the head (opaque), point 1 is the tail (fully faded).
    private static Gradient TailFadeGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    private void Play(Vector3 start, Vector3 direction, float distance)
    {
        if (!_material || _free.Count == 0) return;

        var tracer = _free.Pop();
        tracer.Start = start;
        tracer.Direction = direction;
        tracer.Distance = distance;
        tracer.Travelled = 0f;
        tracer.Line.widthMultiplier = Width;
        tracer.Line.SetPosition(0, start);
        tracer.Line.SetPosition(1, start);
        tracer.Line.enabled = true;
        _active.Add(tracer);
    }

    private void Update()
    {
        float step = Speed * Time.deltaTime;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var tracer = _active[i];
            tracer.Travelled += step;

            float tail = tracer.Travelled - StreakLength;
            if (tail >= tracer.Distance)
            {
                tracer.Line.enabled = false;
                _active.RemoveAt(i);
                _free.Push(tracer);
                continue;
            }

            float head = Mathf.Min(tracer.Travelled, tracer.Distance);
            tail = Mathf.Max(tail, 0f);
            tracer.Line.SetPosition(0, tracer.Start + tracer.Direction * head);
            tracer.Line.SetPosition(1, tracer.Start + tracer.Direction * tail);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
}
