using FoosBot.Domain;
using Xunit;

namespace FoosBot.Tests;

public sealed class TableQueueTests
{
    private static Player P(int i) => new($"id-{i}", $"Gracz {i}");
    private static void Add(TableQueue q, int first, int last)
    {
        for (int i = first; i <= last; i++) Assert.True(q.Apply(QueueAction.Join, P(i)).Ok);
    }

    [Fact]
    public void FullLineupDoesNotAutoStart()
    {
        var q = new TableQueue(); Add(q, 1, 4);
        Assert.True(q.Snapshot().CanStart);
        Assert.Empty(q.Snapshot().Playing);
        Assert.Equal(4, q.Snapshot().Waiting.Count);
    }

    [Fact]
    public void MinusOnePreventsGoEvenAfterFourWerePresent()
    {
        var q = new TableQueue(); Add(q, 1, 4);
        Assert.True(q.Apply(QueueAction.Leave, P(4)).Ok);
        Assert.False(q.Apply(QueueAction.Go, P(1)).Ok);
        Assert.Equal(3, q.Snapshot().Waiting.Count);
    }

    [Fact]
    public void SecondTeamCannotGoWhileFirstTeamIsPlaying()
    {
        var q = new TableQueue(); Add(q, 1, 8);
        Assert.True(q.Apply(QueueAction.Go, P(1)).Ok);
        Assert.True(q.Snapshot().IsOccupied);
        Assert.Equal(new[] { "id-1", "id-2", "id-3", "id-4" },
            q.Snapshot().Playing.Select(p => p.Id));
        Assert.Equal(4, q.Snapshot().Waiting.Count);

        var forbiddenGo = q.Apply(QueueAction.Go, P(5));
        Assert.False(forbiddenGo.Ok);
        Assert.Equal(4, q.Snapshot().Playing.Count);
        Assert.Equal(4, q.Snapshot().Waiting.Count);
    }

    [Fact]
    public void FinishDoesNotAutomaticallyStartWaitingTeam()
    {
        var q = new TableQueue(); Add(q, 1, 8);
        Assert.True(q.Apply(QueueAction.Go, P(1)).Ok);
        Assert.True(q.Apply(QueueAction.Finish, P(2)).Ok);
        Assert.Empty(q.Snapshot().Playing);
        Assert.Equal(4, q.Snapshot().Waiting.Count);
        Assert.True(q.Snapshot().CanStart);
        Assert.True(q.Apply(QueueAction.Go, P(5)).Ok);
        Assert.Equal("id-5", q.Snapshot().Playing[0].Id);
    }

    [Fact]
    public void OnlyActivePlayerCanFinish()
    {
        var q = new TableQueue(); Add(q, 1, 5);
        q.Apply(QueueAction.Go, P(1));
        Assert.False(q.Apply(QueueAction.Finish, P(5)).Ok);
        Assert.True(q.Snapshot().IsOccupied);
    }

    [Fact]
    public void NoDoubleSignup()
    {
        var q = new TableQueue();
        Assert.True(q.Apply(QueueAction.Join, P(1)).Ok);
        Assert.False(q.Apply(QueueAction.Join, P(1)).Ok);
        Assert.Single(q.Snapshot().Waiting);
    }

    [Fact]
    public void OutOfOrderGoIsRejected()
    {
        var q = new TableQueue(); Add(q, 1, 5);
        Assert.False(q.Apply(QueueAction.Go, P(5)).Ok);
    }

    [Fact]
    public async Task ConcurrentGosCannotStartTwoMatches()
    {
        var q = new TableQueue(); Add(q, 1, 8);
        var results = await Task.WhenAll(
            Task.Run(() => q.Apply(QueueAction.Go, P(1))),
            Task.Run(() => q.Apply(QueueAction.Go, P(5))));
        Assert.Single(results, r => r.Ok);
        Assert.Equal(4, q.Snapshot().Playing.Count);
        Assert.Equal(4, q.Snapshot().Waiting.Count);
    }

    [Fact]
    public void DifferentConversationsHaveDifferentQueues()
    {
        var r = new TableRegistry();
        r.ForConversation("chatA").Apply(QueueAction.Join, P(1));
        Assert.Empty(r.ForConversation("chatB").Snapshot().Waiting);
    }
}
