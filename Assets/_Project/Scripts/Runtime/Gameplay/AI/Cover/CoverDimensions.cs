namespace Project.AI.Cover
{
public static class CoverDimensions
{
    public const float AgentRadius = 0.5f;

    public const float LowObstacleHeight = 1.1f;
    public const float HighObstacleHeight = 2.0f;

    public const float CrouchBodyHeight = 0.9f;
    public const float StandBodyHeight = 1.8f;

    public const float LowStanceProbeHeight = 0.75f;
    public const float HighStanceProbeHeight = 1.5f;

    public const float PeekProbeHeight = 1.0f;
    public const float MaxPeekDistance = 1.5f;
    public const float PeekMargin = 0.1f;

    public static float StanceProbeHeight(CoverHeight height) =>
        height == CoverHeight.Low ? LowStanceProbeHeight : HighStanceProbeHeight;

    public static float BodyHeight(CoverHeight height) =>
        height == CoverHeight.Low ? CrouchBodyHeight : StandBodyHeight;
}
}
