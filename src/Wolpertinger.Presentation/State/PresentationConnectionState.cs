namespace Wolpertinger.Presentation.State;

public enum PresentationConnectionState : byte
{
    Connecting = 0,
    Live = 1,
    Disconnected = 2,
    Incompatible = 3
}
