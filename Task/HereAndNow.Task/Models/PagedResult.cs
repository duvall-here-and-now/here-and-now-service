namespace HereAndNowService.Models;

/// <summary>
/// Wrapper for paginated query results
/// </summary>
/// <typeparam name="T">The type of items in the result</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// The items in the current page
    /// </summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Total count of all items matching the query (across all pages)
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Indicates whether more items exist beyond the current page
    /// </summary>
    public bool HasMore { get; set; }
}
