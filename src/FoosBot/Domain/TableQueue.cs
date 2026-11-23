using System.Collections.Concurrent;

namespace FoosBot.Domain;

public enum QueueAction { Join, Leave, Go, Finish, Status }

public sealed record Player(string Id, string DisplayName);

public sealed record TableSnapshot(
    IReadOnlyList<Player> Playing,
    IReadOnlyList<Player> Waiting,
    long Revision)
{
    public bool IsOccupied => Playing.Count != 0;
    public bool HasCompleteNextLineup => Waiting.Count >= 4;
    public bool CanStart => !IsOccupied && HasCompleteNextLineup;
    public IReadOnlyList<Player> NextLineup => Waiting.Take(4).ToArray();
}

public sealed record QueueResult(bool Ok, string Message, TableSnapshot Snapshot);

/// <summary>
/// One lock serializes the WHOLE transition, not just each collection operation.
/// This prevents GO racing against -1 or Finish, and prevents two simultaneous GOs.
/// This implementation is in-memory and single-instance only.
/// </summary>
public sealed class TableQueue
{
    private readonly object _sync = new();
    private readonly List<Player> _waiting = new();
    private readonly List<Player> _playing = new();
    private long _revision;

    public TableSnapshot Snapshot()
    {
        lock (_sync) return TakeSnapshot();
    }

    public QueueResult Apply(QueueAction action, Player actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor.Id);
        lock (_sync)
        {
            switch (action)
            {
                case QueueAction.Join:
                    if (_waiting.Any(p => p.Id == actor.Id))
                        return Fail("Jesteś już zapisany(-a) do kolejki.");
                    _waiting.Add(actor);
                    _revision++;
                    return Success(_playing.Count > 0
                        ? $"{actor.DisplayName} dołącza do kolejki na kolejny mecz."
                        : $"{actor.DisplayName} zapisuje się na piłkarzyki (+1).");

                case QueueAction.Leave:
                    var index = _waiting.FindIndex(p => p.Id == actor.Id);
                    if (index < 0)
                        return Fail(_playing.Any(p => p.Id == actor.Id)
                            ? "Mecz już trwa. −1 dotyczy tylko oczekujących; zakończcie mecz przyciskiem «Zakończ»."
                            : "Nie ma Cię w kolejce oczekujących.");
                    _waiting.RemoveAt(index);
                    _revision++;
                    return Success($"{actor.DisplayName} wypisuje się (−1).");

                case QueueAction.Go:
                    // HARD SERVER-SIDE GUARANTEE: a second lineup cannot GO while occupied.
                    if (_playing.Count > 0)
                        return Fail("Stół jest zajęty! Drugi skład nie może kliknąć GO, dopóki poprzedni mecz się nie zakończy.");
                    if (_waiting.Count < 4)
                        return Fail($"Za mało chętnych na GO: {_waiting.Count}/4.");
                    if (!_waiting.Take(4).Any(p => p.Id == actor.Id))
                        return Fail("GO może kliknąć osoba z pierwszej czwórki w kolejce.");
                    _playing.AddRange(_waiting.Take(4));
                    _waiting.RemoveRange(0, 4);
                    _revision++;
                    return Success("GO! Mecz rozpoczęty. Stół jest teraz zajęty.");

                case QueueAction.Finish:
                    if (_playing.Count == 0)
                        return Fail("Stół jest już wolny — nie ma meczu do zakończenia.");
                    if (!_playing.Any(p => p.Id == actor.Id))
                        return Fail("Mecz może zakończyć tylko jedna z czterech grających osób.");
                    _playing.Clear();
                    _revision++;
                    // DO NOT start next game automatically, even if 4+ people are waiting.
                    return Success("Mecz zakończony — stół wolny. Następny skład może ręcznie kliknąć GO!");

                case QueueAction.Status:
                    return new QueueResult(true, "Aktualny stan stołu i kolejki:", TakeSnapshot());

                default:
                    return Fail("Nieznana operacja.");
            }
        }
    }

    private QueueResult Success(string message) => new(true, message, TakeSnapshot());
    private QueueResult Fail(string message) => new(false, message, TakeSnapshot());
    private TableSnapshot TakeSnapshot() =>
        new(_playing.ToArray(), _waiting.ToArray(), _revision);
}

/// <summary>
/// A separate queue per Teams conversation. For multiple tables add a tableId to the key.
/// State disappears on restart and is not safe across multiple server instances.
/// </summary>
public sealed class TableRegistry
{
    private readonly ConcurrentDictionary<string, TableQueue> _tables = new();
    public TableQueue ForConversation(string conversationId) =>
        _tables.GetOrAdd(conversationId, _ => new TableQueue());
}
