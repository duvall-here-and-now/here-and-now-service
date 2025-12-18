namespace HereAndNowService.Configuration;

/// <summary>
/// Configuration settings for Azure Cosmos DB connection.
/// </summary>
public class CosmosDbSettings
{
    /// <summary>
    /// The Cosmos DB account endpoint URL.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// The primary key for authentication.
    /// </summary>
    public required string PrimaryKey { get; init; }

    /// <summary>
    /// The database name.
    /// </summary>
    public required string DatabaseName { get; init; }

    /// <summary>
    /// The container name for reminder instances.
    /// </summary>
    public required string ContainerName { get; init; }
}
