using System;

namespace Project.AI.Cover
{
public enum CoverHeight : byte
{
    Low,
    High
}

[Flags]
public enum CoverPeekDirection : byte
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Over = 1 << 2
}
}
