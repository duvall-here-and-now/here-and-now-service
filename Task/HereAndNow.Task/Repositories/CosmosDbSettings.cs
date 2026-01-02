namespace HereAndNowService.Repositories;

/// <summary>
/// Configuration settings for Cosmos DB connection
/// </summary>
public class CosmosDbSettings
{
    /// <summary>
    /// The Cosmos DB connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The name of the Cosmos DB database
    /// </summary>
    public string DatabaseName { get; set; } = "HereAndNow";

    /// <summary>
    /// The name of the Tasks container
    /// </summary>
    public string ContainerName { get; set; } = "Tasks";
}
