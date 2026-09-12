namespace OrchardCore.Indexing.Core;

/// <summary>Reports a single index's observed processing outcome without exposing provider exception details.</summary>
public sealed class IndexProcessingResult
{
    /// <summary>Gets the tenant-local index profile identifier.</summary>
    public string IndexId { get; init; }

    /// <summary>Gets whether processing completed or why it did not.</summary>
    public IndexProcessingStatus Status { get; internal set; }

    /// <summary>Gets the last confirmed provider cursor, or null when processing never read it.</summary>
    public long? LastTaskId { get; internal set; }
}

/// <summary>Distinguishes completed indexing from work that was skipped or failed.</summary>
public enum IndexProcessingStatus
{
    /// <summary>Processing failed; the last confirmed cursor remains available for retry.</summary>
    Failed,
    /// <summary>No further queued tasks were observed after successful processing.</summary>
    Completed,
    /// <summary>The index was already locked by another operation.</summary>
    Busy,
    /// <summary>A required provider service is not registered.</summary>
    ProviderUnavailable,
    /// <summary>The provider does not contain the requested index.</summary>
    ProviderMissing,
    /// <summary>The provider rejected a requested lifecycle operation.</summary>
    ProviderRejected,
    /// <summary>The profile is absent from this indexing source.</summary>
    NotFound,
    /// <summary>The profile's source is not supported by this indexing service.</summary>
    Unsupported,
}
