namespace PhoenixHttp.Enums;

/// <summary>The stage a <see cref="Models.Transaction"/> has reached in its lifecycle.</summary>
public enum TransactionStatus
{
    /// <summary>Created and being assembled, but not yet sent.</summary>
    Pending,

    /// <summary>Queued or in flight.</summary>
    Processing,

    /// <summary>Completed with a response ready to read.</summary>
    Done
}
