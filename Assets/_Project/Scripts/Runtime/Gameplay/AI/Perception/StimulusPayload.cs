using UnityEngine;

namespace Project.AI.Perception
{

public enum StimulusType : byte
{
    Sight,       
    Sound,      
    Damage,     
    Tactical
}

public readonly struct StimulusPayload
{
    public readonly StimulusType Type;
    public readonly Vector3 Position;
    public readonly GameObject Source;
    public readonly float Intensity;    //Loudness, damage amount, or visual contrast
    public readonly float TimeStamp;

    public StimulusPayload(StimulusType type, Vector3 position, GameObject source, float intensity, float timeStamp = -1)
    {
        Type = type;
        Position = position;
        Source = source;
        Intensity = intensity;
        TimeStamp = timeStamp >= 0 ? timeStamp : Time.time;
    }
}
}