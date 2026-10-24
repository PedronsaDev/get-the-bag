namespace Project.AI.Cover
{
public enum CoverPeekFailure
{
    InvalidCoverPoint = 0,
    InvalidPeekDirection = 1,
    UnavailablePeekDirection = 2,
    ExceededMaxLateralPeekDistance = 3,
    ExceededMaxVerticalPeekDistance = 4,
    None = 5,
    InvalidConfiguration = 6,
    InvalidPosition = 7,
    InvalidAgent = 8,
    NavigationSampleFailed = 9,
    NavigationSnapExceeded = 10,
    NavigationRouteBlocked = 11,
    BodyBlocked = 12,
    MovementBlocked = 13,
    QueryBufferFull = 14,
    InvalidFiringGeometry = 15,
    FiringLineBlocked = 16
}
}
