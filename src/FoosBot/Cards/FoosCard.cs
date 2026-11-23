using System.Text.Json;
using FoosBot.Domain;

namespace FoosBot.Cards;

public static class FoosCard
{
    public static JsonElement Create(TableSnapshot state)
    {
        string table = state.IsOccupied ? "🔴 ZAJĘTY — mecz trwa" : "🟢 WOLNY";
        string playing = state.IsOccupied
            ? string.Join(", ", state.Playing.Select(p => p.DisplayName))
            : "Nikt obecnie nie gra";
        var lineup = state.Waiting.Take(4).Select(p => p.DisplayName).ToArray();
        string waiting = lineup.Length == 0 ? "Brak chętnych" : string.Join(", ", lineup);
        string extra = state.Waiting.Count > 4
            ? $" (+{state.Waiting.Count - 4} w dalszej kolejce)" : string.Empty;

        // Anonymous objects serialize directly to valid Adaptive Card 1.5 JSON.
        // 'isEnabled' is UI guidance only: all constraints are rechecked on the server.
        var payload = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new { type = "TextBlock", text = "⚽ FoosBot · piłkarzyki", size = "Large", weight = "Bolder" },
                new { type = "TextBlock", text = $"Stół: {table}", wrap = true, weight = "Bolder" },
                new { type = "TextBlock", text = $"Grają: {playing}", wrap = true },
                new { type = "TextBlock", text = $"Następny skład ({Math.Min(4, state.Waiting.Count)}/4): {waiting}{extra}", wrap = true },
                new { type = "TextBlock", text = state.IsOccupied
                    ? "GO zablokowane, aż aktualni gracze zakończą mecz."
                    : state.CanStart ? "Komplet! GO należy kliknąć ręcznie."
                    : "Zapiszcie się przyciskiem +1.", wrap = true, isSubtle = true },
                new { type = "TextBlock", text = $"Wersja stanu: {state.Revision}", size = "Small", isSubtle = true }
            },
            actions = new object[]
            {
                Action("+1", "join", true),
                Action("−1", "leave", true),
                Action("GO!", "go", state.CanStart),
                Action("Zakończ mecz / zwolnij stół", "finish", state.IsOccupied),
                Action("Odśwież", "status", true)
            }
        };
        return JsonSerializer.SerializeToElement(payload);
    }

    private static object Action(string title, string action, bool enabled) => new
    {
        type = "Action.Execute",
        title,
        verb = "foosbot",
        data = new { action },
        isEnabled = enabled
    };
}
