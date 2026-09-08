using System.Text.Json;

namespace Wolpertinger.Edge.Journal;

public enum JournalEventKind
{
    Unknown = 0,
    FileHeader,
    Commander,
    LoadGame,
    FsdJump,
}

public static class JournalEventClassifier
{
    public static JournalEventKind Classify(JsonElement root)
    {
        if (!root.TryGetProperty("event", out var eventName) || eventName.ValueKind != JsonValueKind.String)
        {
            return JournalEventKind.Unknown;
        }

        return eventName.GetString() switch
        {
            "Fileheader" => JournalEventKind.FileHeader,
            "Commander" => JournalEventKind.Commander,
            "LoadGame" => JournalEventKind.LoadGame,
            "FSDJump" => JournalEventKind.FsdJump,
            _ => JournalEventKind.Unknown,
        };
    }
}
