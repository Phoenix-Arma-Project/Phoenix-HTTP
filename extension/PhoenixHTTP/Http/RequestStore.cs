using System.Collections.Concurrent;
using PhoenixHttp.Models;

namespace PhoenixHttp.Http;

/// <summary>
/// Keeps transactions alive between the synchronous <c>callExtension</c> calls that build, send and
/// read a request. It is backed by a concurrent dictionary because requests are assembled on the
/// engine thread but completed on the thread pool, so both sides touch the store at once.
/// </summary>
public sealed class RequestStore
{
    /// <summary>Live transactions keyed by id; thread-safe for the engine/worker split.</summary>
    private readonly ConcurrentDictionary<string, Transaction> transactions = new();

    /// <summary>Stores a transaction, replacing any existing one with the same id.</summary>
    /// <param name="transaction">The transaction to store.</param>
    public void Add(Transaction transaction) => transactions[transaction.Id] = transaction;

    /// <summary>Looks up a transaction by id.</summary>
    /// <param name="id">The transaction id.</param>
    /// <param name="transaction">The found transaction, or null when absent.</param>
    /// <returns><see langword="true"/> if a transaction was found.</returns>
    public bool TryGet(string id, out Transaction transaction) => transactions.TryGetValue(id, out transaction!);

    /// <summary>Removes a transaction by id, ignoring ids that are not present.</summary>
    /// <param name="id">The transaction id.</param>
    public void Remove(string id) => transactions.TryRemove(id, out _);

    /// <summary>Removes every transaction.</summary>
    public void Clear() => transactions.Clear();
}
