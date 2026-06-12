namespace HereAndNowService.Models;

/// <summary>
/// Generates deterministic IDs for derived reminder documents (Pattern V3-1).
/// Format: rtr_{configId}_{yyyy-MM-ddTHH:mm:ssZ}
/// </summary>
public static class DerivedReminderId
{
    /// <summary>
    /// Generates a derived reminder ID from a config ID and UTC recurrence datetime.
    /// </summary>
    /// <param name="configId">The recurring task config ID.</param>
    /// <param name="recurrenceDateAndTime">UTC recurrence datetime (must have DateTimeKind.Utc).</param>
    /// <returns>The derived reminder ID string.</returns>
    /// <exception cref="ArgumentException">Thrown if recurrenceDateAndTime is not UTC.</exception>
    public static string Generate(string configId, DateTime recurrenceDateAndTime)
    {
        if (recurrenceDateAndTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                $"DateTime must be UTC (Kind was {recurrenceDateAndTime.Kind})",
                nameof(recurrenceDateAndTime));
        }

        return $"rtr_{configId}_{recurrenceDateAndTime:yyyy-MM-ddTHH:mm:ssZ}";
    }
}
