namespace Project.AI.Alarm
{
public enum AlarmState : byte
{
    Stealth,
    Alarmed
}

public enum AlarmReason : byte
{
    GuardAlerted,
    BodyFound,
    CivilianCall,
    CivilianKilled
}
}
