using PhoenixHttp.Enums;

namespace PhoenixHttp.Models;

/// <summary>
/// A single request's lifecycle state, stored in the <see cref="Http.RequestStore"/> and identified
/// by <see cref="Id"/>. It carries the request being built, the response once it is ready, and the
/// timestamps that let the logs report end-to-end latency.
/// </summary>
public sealed class Transaction
{
    /// <summary>Unique id (a GUID) the script uses to reference this transaction across calls.</summary>
    public required string Id { get; init; }

    /// <summary>The request being assembled and then executed.</summary>
    public required Request Request { get; init; }

    /// <summary>Serialized response split into engine-sized chunks, or null until it is ready.</summary>
    public IReadOnlyList<string>? ResponseChunks { get; set; }

    /// <summary>Where the transaction is in its lifecycle.</summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    /// <summary>When the transaction was created.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>When the request was queued for sending, or null if it has not been sent.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>When the response was fully assembled, or null if it is not yet complete.</summary>
    public DateTime? CompletedAt { get; set; }
}
