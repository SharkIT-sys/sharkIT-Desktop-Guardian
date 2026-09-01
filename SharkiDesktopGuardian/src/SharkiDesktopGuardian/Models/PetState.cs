namespace SharkiDesktopGuardian.Models;

public enum PetState
{
    Idle,
    MovingLeft,
    MovingRight,
    Greeting,
    CommandNotRecognized,
    HighLoad,
    HighMemory,
    LowDisk,
    ThermalAlert,
    Waiting,
    Paused
}

public sealed record AlertStatus(
    PetState State,
    string Title,
    string Message,
    bool IsCritical,
    IReadOnlyList<string> Reasons)
{
    public static AlertStatus Normal(string petName) => new(
        PetState.Idle,
        "Estado normal",
        $"{petName} vigila el equipo.",
        false,
        []);
}
