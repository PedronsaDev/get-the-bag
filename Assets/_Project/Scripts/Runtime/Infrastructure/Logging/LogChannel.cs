using System;

namespace Project.Logging
{
    [Flags]
    public enum LogChannel
    {
        None        = 0,
        General     = 1 << 0,
        Network     = 1 << 1,
        Match       = 1 << 2,
        Combat      = 1 << 3,
        Player      = 1 << 4,
        AI          = 1 << 5,
        UI          = 1 << 6,
        Persistence = 1 << 7,
        Audio       = 1 << 8,
        Editor       = 1 << 9,
        Animation   = 1 << 10,
        All         = ~0
    }
}
