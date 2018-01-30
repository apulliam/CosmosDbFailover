using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.CosmosDB.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

namespace CosmosDbBenchmark
{
    class CosmosDbAccount
    {
        private IAzure _azure;
        private ICosmosDBAccount _cosmosDBAccount;


        public CosmosDbAccount()
        {
            var credentials = SdkContext.AzureCredentialsFactory.FromFile(Environment.GetEnvironmentVariable("AZURE_AUTH_LOCATION"));

            _azure = Azure
                //.Configure()
                //.WithLogLevel(HttpLoggingDelegatingHandler.Level.None)
                .Authenticate(credentials)
                .WithDefaultSubscription();

        }

        public async Task GetOrCreateAsync(string resourceGroup, string cosmosDbName, string primaryRegion, IEnumerable<string> replicaRegions, string accountRegion = null)
        {
            var resourceGroupRegion = accountRegion != null ? accountRegion : primaryRegion;
            _cosmosDBAccount = await _azure.CosmosDBAccounts.GetByResourceGroupAsync(resourceGroup, cosmosDbName);

            if (_cosmosDBAccount == null)
            {
                if (!await _azure.ResourceGroups.ContainAsync(resourceGroup))
                    await _azure.ResourceGroups.Define(resourceGroup).WithRegion(resourceGroupRegion).CreateAsync();

                var create = _azure.CosmosDBAccounts.Define(cosmosDbName)
                    .WithRegion(primaryRegion)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithKind(DatabaseAccountKind.GlobalDocumentDB)
                    .WithStrongConsistency()
                    .WithReadReplication(Region.Create(primaryRegion));

                foreach (var secondary in replicaRegions)
                    create.WithReadReplication(Region.Create(secondary));

                _cosmosDBAccount = await create.CreateAsync();
            }
        }
           
        public string MasterKey
        {
            get
            {
                if (_cosmosDBAccount == null)
                    return null;
                var databaseAccountListKeysResult = _cosmosDBAccount.ListKeys();
                return databaseAccountListKeysResult.PrimaryMasterKey;
            }
        }

        public string EndPoint
        {
            get
            {
                if (_cosmosDBAccount == null)
                    return null;
                return _cosmosDBAccount.DocumentEndpoint;
            }
        }
        public IEnumerable<Location> Regions
        {
            get
            {
                if (_cosmosDBAccount == null)
                    return null;
                return _cosmosDBAccount.ReadableReplications;
            }
        }

        public async Task DeleteAsync()
        {
            await _azure.CosmosDBAccounts.DeleteByIdAsync(_cosmosDBAccount.Id);
        }
        

        public async Task FailoverAsync(string resourceGroup, string cosmosDbName)
        {

            var cosmosDbAccount = _azure.CosmosDBAccounts.GetByResourceGroup(resourceGroup, cosmosDbName);
            var currentLocations = cosmosDbAccount.ReadableReplications
                .OrderBy(l => (int)l.FailoverPriority);
            var failoverLocations = new List<Location>(currentLocations.Skip(1));

            failoverLocations.Add(currentLocations.First());
            for (int i = 0; i < failoverLocations.Count(); i++)
            {
                var failoverLocation = failoverLocations[i];
                failoverLocation.FailoverPriority = i;
            }
            
            await _azure.CosmosDBAccounts.FailoverPriorityChangeAsync(resourceGroup, cosmosDbName, failoverLocations);

        }
    }
}
