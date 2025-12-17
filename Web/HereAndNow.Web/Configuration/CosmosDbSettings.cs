namespace HereAndNowService.Configuration;

/// <summary>
/// Configuration settings for Azure Cosmos DB connection.
/// </summary>
public class CosmosDbSettings
{
    /// <summary>
    /// The Cosmos DB account endpoint URL.
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// The Cosmos DB primary key for authentication.
    /// </summary>
    public required string PrimaryKey { get; set; }

    /// <summary>
    /// The name of the Cosmos DB database.
    /// </summary>
    public required string DatabaseName { get; set; }

    /// <summary>
    /// The name of the Cosmos DB container for reminders.
    /// </summary>
    public required string ContainerName { get; set; }
}
