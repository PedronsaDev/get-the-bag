using UnityEngine;

namespace Project.AI.Perception
{
public interface IAISensoryReceiver
{
    public Vector3 SensoryOrigin { get; }
    public void ReceiveStimulus(in StimulusPayload stimulus);
}
}