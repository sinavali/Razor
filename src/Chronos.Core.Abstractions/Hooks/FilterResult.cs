namespace Chronos.Core.Abstractions.Hooks;

/// <summary>
/// Factory methods for creating <see cref="FilterResult{T}"/> instances.
/// </summary>
public static class FilterResult
{
    /// <summary>Creates a result that allows the data to pass through.</summary>
    public static FilterResult<T> Allow<T>(T data) => FilterResult<T>.Allow(data);

    /// <summary>Creates a result that rejects the data with a reason.</summary>
    public static FilterResult<T> Reject<T>(string reason) => FilterResult<T>.Reject(reason);
}

/// <summary>
/// Result of a filter hook. Indicates whether to allow the data through
/// (possibly modified) or to reject it.
/// </summary>
/// <typeparam name="T">The type of data being filtered.</typeparam>
public readonly struct FilterResult<T> : IEquatable<FilterResult<T>>
{
    /// <summary>Whether the data is allowed to continue through the pipeline.</summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// The (possibly modified) data. Valid only when <see cref="IsAllowed"/> is <c>true</c>.
    /// </summary>
    public T? Data { get; }

    /// <summary>
    /// The reason for rejection. Valid only when <see cref="IsAllowed"/> is <c>false</c>.
    /// </summary>
    public string? RejectionReason { get; }

    internal FilterResult(bool isAllowed, T? data, string? rejectionReason)
    {
        IsAllowed = isAllowed;
        Data = data;
        RejectionReason = rejectionReason;
    }

    /// <summary>Creates a result that allows the data to pass through.</summary>
    internal static FilterResult<T> Allow(T data) => new(true, data, null);

    /// <summary>Creates a result that rejects the data with a reason.</summary>
    internal static FilterResult<T> Reject(string reason) => new(false, default, reason);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FilterResult<T> other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(FilterResult<T> other) =>
        IsAllowed == other.IsAllowed &&
        EqualityComparer<T?>.Default.Equals(Data, other.Data) &&
        RejectionReason == other.RejectionReason;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(IsAllowed, Data, RejectionReason);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(FilterResult<T> left, FilterResult<T> right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(FilterResult<T> left, FilterResult<T> right) => !left.Equals(right);
}
