using System.Text.Json;
using System.Text.RegularExpressions;
using FoosBot.Cards;
using FoosBot.Domain;
using Microsoft.Teams.Apps;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTeamsBotApplication();
builder.Services.AddSingleton<TableRegistry>();

var webApp = builder.Build();
webApp.MapGet("/health", () => Results.Ok(new { status = "ok", app = "FoosBot" }));

var registry = webApp.Services.GetRequiredService<TableRegistry>();
TeamsBotApplication teams = webApp.UseTeamsBotApplication();

teams.OnMessage(async (context, ct) =>
{
    var conversationId = context.Activity.Conversation?.Id;
    var senderId = context.Activity.From?.Id;
    if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(senderId))
    {
        await context.SendAsync("Nie udało się rozpoznać rozmowy lub użytkownika Teams.", ct);
        return;
    }

    var actor = new Player(senderId, context.Activity.From?.Name ?? "Gracz");
    string text = Regex.Replace(context.Activity.Text ?? "", @"<at>.*?</at>", "",
        RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();

    if (!TryParseAction(text, out var command))
    {
        await context.SendAsync("Komendy: +1, -1, go, koniec, status. Wpisz 'status', aby zobaczyć kartę z przyciskami.", ct);
        return;
    }

    var result = registry.ForConversation(conversationId).Apply(command, actor);
    await context.SendAsync(
        new MessageActivityInput().WithText(result.Message)
            .WithAdaptiveCardAttachment(FoosCard.Create(result.Snapshot)), ct);
});

teams.OnAdaptiveCardAction(async (context, ct) =>
{
    var data = context.Activity.Value?.Action?.Data;
    if (data is null || !data.TryGetValue("action", out var rawAction) ||
        !TryParseAction(rawAction is JsonElement element ? element.ToString() : rawAction?.ToString(), out var command))
        return AdaptiveCardResponse.CreateMessageResponse("Nieznana akcja FoosBot.", 400);

    var conversationId = context.Activity.Conversation?.Id;
    var senderId = context.Activity.From?.Id;
    if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(senderId))
        return AdaptiveCardResponse.CreateMessageResponse("Nie udało się rozpoznać użytkownika Teams.", 400);

    var actor = new Player(senderId, context.Activity.From?.Name ?? "Gracz");
    var result = registry.ForConversation(conversationId).Apply(command, actor);

    // MVP: send a NEW current status card. Old cards can still be clicked, but
    // never bypass server-side state checks. Next step: update one pinned card.
    await context.SendAsync(new MessageActivityInput().WithText(result.Message)
        .WithAdaptiveCardAttachment(FoosCard.Create(result.Snapshot)), ct);

    return AdaptiveCardResponse.CreateMessageResponse(result.Message);
});

webApp.Run();

static bool TryParseAction(string? text, out QueueAction action)
{
    switch ((text ?? "").Trim().ToLowerInvariant())
    {
        case "+1": case "join": case "dolacz": case "dołącz":
            action = QueueAction.Join; return true;
        case "-1": case "−1": case "leave": case "wypisz":
            action = QueueAction.Leave; return true;
        case "go": case "start":
            action = QueueAction.Go; return true;
        case "koniec": case "finish": case "stop":
            action = QueueAction.Finish; return true;
        case "status": case "stan": case "foosbot":
            action = QueueAction.Status; return true;
        default:
            action = QueueAction.Status; return false;
    }
}
