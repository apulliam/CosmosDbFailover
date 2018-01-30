# CosmosDbFailover

Sample application to demonstrate Cosmos DB writes during a programatically triggered manual region failover of Cosmos DB with strong consistency.

#### Note: Geo replication with strong consistency is a preview feature and may not be available on all accounts. 

To run this application, you first need to create a Service Principal and setup an azureauth.properties file using instructions here: https://docs.microsoft.com/en-us/dotnet/azure/dotnet-sdk-azure-get-started?view=azure-dotnet

CosmosDbFailover takes the name of a JSON configuration file as its runtime argument:

    CosmosDbFailover test.json

A sample test.json is included.  The properties are as follows:

        public string ResourceGroup;         // resource group to create the Cosmos DB account if it doesn't already exist
        public string CosmosDbName;          // Cosmos DB account name.
        public string[] Regions;             // Regions listed by priority.  The first region is the write region.  Additional regions are replica regions.  All regions must be within 5000 miles of one another.
        public string DatabaseName;          // Database name for test
        public string CollectionName;        // Collection name for test
        public int CollectionThroughput;     // Throughput in RU's for test
        public bool CleanupDatabaseOnStart;  // Option to delete and recreate database if it already exists
        public bool CleanupDatabaseOnFinish; // Option to delete database after test
        public bool CleanupAcountOnFinish;   // Option to delete Cosmos DB account after test

Once CosmosDbFailover is launched, it will setup the Cosmos DB account and collection if needed.  Then it will continuously update a single document "text" property with a GUID.
The replication is verified by reading the document from replica regions and verifying the "text" property.

To initiate a failover, hit Enter.  The primary (write) region will eventually switch over to the highest priority replica region.  The replica clients will continue reading from the same region. 

CosmosDbFailover will exit after the failover operation is complete.  This occurs some time after the writes have switched over to the new primary region.

CosmosDbFailover can also exited with the Esc key if a failover operation is not in progress. 