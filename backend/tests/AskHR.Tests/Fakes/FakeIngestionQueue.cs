using System.Collections.Concurrent;
using AskHR.Application.Abstractions;

namespace AskHR.Tests.Fakes;

/// <summary>Records what would have been sent to Hangfire, so API tests need no job server.</summary>
public sealed class FakeIngestionQueue : IIngestionQueue
{
    public ConcurrentQueue<Guid> Enqueued { get; } = new();

    public string Enqueue(Guid documentId)
    {
        Enqueued.Enqueue(documentId);
        return $"fake-job-{Enqueued.Count}";
    }
}
