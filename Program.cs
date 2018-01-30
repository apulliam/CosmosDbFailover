namespace CosmosDbBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    public sealed class Program
    {
        private TestConfig _config = null;
        private CosmosDbAccount _cosmosDbAccount = null;
        private bool _exit = false;
        private bool _failover = false;
        private Program(TestConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Main method for the sample.
        /// </summary>
        /// <param name="args">command line arguments.</param>
        public static void Main(string[] args)
        {
            try
            {
                if (args.Count() == 1)
                {
                    TestConfig config = null;
                    var benchmark = args[0];
                    if (File.Exists(benchmark))
                    {
                        try
                        {
                            config = JsonConvert.DeserializeObject<TestConfig>(File.ReadAllText(benchmark));
                        }
                        catch
                        {
                        }
                    }
                
                    if (config != null)
                    {
                        var program = new Program(config);

                        program.RunAsync().Wait();
                        return;
                    }
                
                }
             
                Console.WriteLine("Useage:");
                Console.WriteLine("CosmosDbFailover <JSON config file>");
                
            }

#if !DEBUG
            catch (Exception e)
            {
                // If the Exception is a DocumentClientException, the "StatusCode" value might help identity 
                // the source of the problem. 
                Console.WriteLine("CosmosDbFailover exception:{0}", e);
            }
#endif

            finally
            {
            
            }
        }

        private async Task RunAsync()
        {
            await GetCosmosDbAccount(_config.ResourceGroup, _config.CosmosDbName, _config.Regions);
          
            Console.WriteLine($"CosmosDbFailover starting...");
            Console.WriteLine($"Cosmos DB: {_config.CosmosDbName}");
            Console.WriteLine($"Regions:");
            foreach (var region in _cosmosDbAccount.Regions)
                Console.WriteLine($"\t{region.LocationName}");
            Console.WriteLine($"Collection: {_config.DatabaseName}.{_config.CollectionName}");
            Console.WriteLine($"RU's: {_config.CollectionThroughput}");
            

            Console.WriteLine("Hit Enter to start failover");
            Console.WriteLine("Hit Esc to exit test");

            var cosmosDbEndpoint = new Uri(_cosmosDbAccount.EndPoint);

            var replicaClients = new List<FailoverTestClient>();

            var defaultClient = await FailoverTestClient.GetFailoverTestClient(cosmosDbEndpoint, _cosmosDbAccount.MasterKey, _config.DatabaseName, _config.CollectionName, _config.PartitionKey, _config.CollectionThroughput);

            
            // Skip the first region, which is the primary region, to get replica regions 
            var replicaRegions = _cosmosDbAccount.Regions.OrderBy(r => r.FailoverPriority).Skip(1);

            // Create DocumentDb API client in each replica region
            foreach (var replicaRegion in replicaRegions)
            {
                var replicaClient = await FailoverTestClient.GetFailoverTestClient(cosmosDbEndpoint, _cosmosDbAccount.MasterKey, _config.DatabaseName, _config.CollectionName, _config.PartitionKey, _config.CollectionThroughput, new List<string>() { replicaRegion.LocationName });
                replicaClients.Add(replicaClient);
            }

            try
            {
                // Create DocumentDb API client using default region
             

                var tasks = new List<Task>();

                var readConsoleTask = ReadConsoleKeys();
                tasks.Add(readConsoleTask);

                var writeTask = UpdateDocument(defaultClient, replicaClients);
                tasks.Add(writeTask);

                var failoverTask = Failover();
                tasks.Add(failoverTask);

                await Task.WhenAll(tasks.ToArray());
            }
            finally
            {
                if (_config.CleanupDatabaseOnFinish)
                    await DocumentDbUtility.DeleteDatabase(defaultClient.DocumentClient, _config.DatabaseName);
                defaultClient.Dispose();
                foreach (var replicaClient in replicaClients)
                    replicaClient.Dispose();
                if (_config.CleanupAcountOnFinish)
                {
                    Console.WriteLine($"Deleting {_cosmosDbAccount.EndPoint}");
                    await _cosmosDbAccount.DeleteAsync();
                }
            }
        }

        private async Task GetCosmosDbAccount(string resourceGroup, string cosmosDbName, string[] regions)
        {
            _cosmosDbAccount = new CosmosDbAccount();
            await _cosmosDbAccount.GetOrCreateAsync(resourceGroup, cosmosDbName, regions.First(), regions.Skip(1));
       
        }

        private async Task ReadConsoleKeys()
        {
            while (!_exit)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Enter)
                    {
                        _failover = true;
                    }

                    if (key.Key == ConsoleKey.Escape)
                    {
                        Console.WriteLine("Exiting CosmosDbFailover");
                        _exit = true;
                    }
                }
                else
                    await Task.Delay(100);
            }
        }

        private async Task Failover()
        {
            while (!_exit)
            {
                if (_failover == true)
                {
                    Console.WriteLine($"{DateTime.Now}: >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Starting failover");
                    try
                    {
                        await _cosmosDbAccount.FailoverAsync(_config.ResourceGroup, _config.CosmosDbName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{DateTime.Now}: Failover exception: {ex.Message}"); 
                        throw;
                    }
                    Console.WriteLine($"{DateTime.Now}: >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Failover complete");
                    Console.WriteLine("Exiting CosmosDbFailover");
                    _exit = true;
                }
                await Task.Delay(100);
            }
        }

        private async Task UpdateDocument(FailoverTestClient defaultClient, IEnumerable<FailoverTestClient> replicaClients)
        {
            while (!_exit)
            {
                var response1 = await defaultClient.UpsertItem("ATL", "A", "11", "S123");
                Console.WriteLine($"{DateTime.Now}: Upsert to region: {defaultClient.WriteRegion,-15}; latency: {response1.Item1}");
                foreach (var replicaClient in replicaClients)
                {
                    // read replica region
                    var response2 = await replicaClient.GetItem("ATL", "A", "11", "S123");
                    Console.WriteLine($"{DateTime.Now}: Read from region: {replicaClient.ReadRegion,-15}; latency: {response2.Item1}");
                    var region = replicaClient.ReadRegion;
                    if (response1.Item2.text != response2.Item2.text)
                        throw new Exception("Replication failed!!!");
                }
                // read default region (primary)
                var response3 = await defaultClient.GetItem("ATL", "A", "11", "S123");
                Console.WriteLine($"{DateTime.Now}: Read from region: {defaultClient.ReadRegion,-15}; latency: {response3.Item1}");
            }
        }
     }
}
