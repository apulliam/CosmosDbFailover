using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CosmosDbBenchmark
{
    public static class DocumentDbUtility
    {
        public static DocumentClient GetDocumentDbClient(Uri cosmosDbEndpoint, string authorizationKey, IEnumerable<string> regions = null)
        {
            var connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = Microsoft.Azure.Documents.Client.ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
            };

            if (regions != null)
                foreach (var region in regions)
                    connectionPolicy.PreferredLocations.Add(region);
            
            return new DocumentClient(
                  cosmosDbEndpoint,
                  authorizationKey,
                  connectionPolicy);
        }

     
        public static async Task GetOrCreateCollection(this DocumentClient client, string databaseName, string collectionName, int collectionThroughput = -1, string partitionKey = null, bool cleanupOnStart = false)
        {
            
            DocumentCollection dataCollection = GetCollectionIfExists(client, databaseName, collectionName);
            
            if (cleanupOnStart || dataCollection == null)
            {
                Database database = GetDatabaseIfExists(client, databaseName);
               
                if (database != null)
                {
                    await client.DeleteDatabaseAsync(database.SelfLink);
                }

                database = await client.CreateDatabaseAsync(new Database { Id = databaseName });
                Console.WriteLine("Created database {0}", databaseName);

                DocumentCollection collection = new DocumentCollection() { Id = collectionName };
           
                if (!string.IsNullOrEmpty(partitionKey))
                    collection.PartitionKey.Paths.Add(partitionKey);

                dataCollection = await client.CreateDocumentCollectionAsync(
                        UriFactory.CreateDatabaseUri(databaseName),
                        collection,
                        new RequestOptions { OfferThroughput = collectionThroughput });

                Console.WriteLine("Created collection {0}", collectionName);
            }
            else
            {
                Console.WriteLine($"Using existing collection {databaseName}.{collectionName}");
                await VerifyCollectionThroughput(client, dataCollection, collectionThroughput);
            }
        }

        private static async Task VerifyCollectionThroughput(this DocumentClient client, DocumentCollection dataCollection, int collectionThroughput)
        {
            OfferV2 offer = (OfferV2)client.CreateOfferQuery().Where(o => o.ResourceLink == dataCollection.SelfLink).AsEnumerable().FirstOrDefault();
            if (collectionThroughput != offer.Content.OfferThroughput)
            {
                await client.ReplaceOfferAsync(new OfferV2(offer, collectionThroughput));
            }
        }

       
        public static  async Task DeleteDatabase(this DocumentClient client, string databaseName)
        {
            Console.WriteLine("Deleting Database {0}", databaseName);
            await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName));
        }

       
        public static Database GetDatabaseIfExists(this DocumentClient client, string databaseName)
        {
            return client.CreateDatabaseQuery().Where(d => d.Id == databaseName).AsEnumerable().FirstOrDefault();
        }

      
        public static DocumentCollection GetCollectionIfExists(this DocumentClient client, string databaseName, string collectionName)
        {
            if (client.GetDatabaseIfExists(databaseName) == null)
            {
                return null;
            }

            return client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(databaseName))
                .Where(c => c.Id == collectionName).AsEnumerable().FirstOrDefault();
        }


        //private static async Task Recover(this DocumentClient client, Uri collectionUri)
        //{

        //    string conflictsFeedContinuationToken = null;
        //    do
        //    {
        //        FeedResponse<Conflict> conflictsFeed = await client.ReadConflictFeedAsync(collectionUri,
        //        new FeedOptions { RequestContinuation = conflictsFeedContinuationToken });

        //        foreach (Conflict conflict in conflictsFeed)
        //        {
        //            Document doc = conflict.GetResource<Document>();
        //            Console.WriteLine("Conflict record ResourceId = {0} ResourceType= {1}", conflict.ResourceId, conflict.ResourceType);

        //            // Perform application specific logic to process the conflict record / resource
        //        }

        //        conflictsFeedContinuationToken = conflictsFeed.ResponseContinuation;
        //    } while (conflictsFeedContinuationToken != null);
        //}
    }
}
