using UnityEngine;
using UnityEngine.Events;

/// Contagem regressiva da missão, exibida no relógio de pulso.
public class MissionTimer : MonoBehaviour
{
    public float missionSeconds = 600f;   // 10:00
    public bool autoStart = true;
    public UnityEvent onTimeUp;

    public float Remaining { get; private set; }
    public bool Running { get; private set; }

    void Start() { Remaining = missionSeconds; Running = autoStart; Push(); }

    void Update()
    {
        if (!Running) return;
        Remaining -= Time.deltaTime;
        if (Remaining <= 0f) { Remaining = 0f; Running = false; onTimeUp?.Invoke(); }
        Push();
    }

    public void StartTimer() => Running = true;
    public void Pause()      => Running = false;
    public void AddTime(float seconds) => Remaining += seconds;

    void Push() { if (HUDManager.Instance) HUDManager.Instance.SetMissionTime(Remaining); }
}
