using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CosmosDbBenchmark
{
    public class FailoverTestClient : IDisposable
    {
        private DocumentClient _client;
        private string _databaseName;
        private string _collectionName;
        private Uri _databaseUri;
        private Uri _collectionUri;
        public DocumentClient DocumentClient
        {
            get
            {
                return _client;
            }
        }

        private string findRegionFromEndpoint(Uri endpoint)
        {
            var i = _client.ServiceEndpoint.Host.LastIndexOf(".documents.azure.com");
            var accountName = _client.ServiceEndpoint.Host.Remove(i);
            i = endpoint.Host.LastIndexOf(".documents.azure.com");
            var region = endpoint.Host.Remove(i);
            return region.Remove(0, accountName.Length + 1);
        }


        public string ReadRegion
        {
            get
            {
                return findRegionFromEndpoint(_client.ReadEndpoint);
            }
        }

        public string WriteRegion
        {
            get
            {
                return findRegionFromEndpoint(_client.WriteEndpoint);
            }
        }


        private FailoverTestClient(Uri cosmosDbEndpoint, string authorizationKey, IEnumerable<string> regions = null)
        {
            _client = DocumentDbUtility.GetDocumentDbClient(cosmosDbEndpoint, authorizationKey, regions);
        }

        public static async Task<FailoverTestClient> GetFailoverTestClient(Uri cosmosDbEndpoint, string authorizationKey, string databaseName, string collectionName,  int collectionThroughput, IEnumerable<string> regions = null, bool cleanupOnStart = false)
        {
            var client = new FailoverTestClient(cosmosDbEndpoint, authorizationKey, regions);
            client._databaseName = databaseName;
            client._collectionName = collectionName;
           
            await client.DocumentClient.GetOrCreateCollection(client._databaseName, client._collectionName, collectionThroughput, "/partitionKey", cleanupOnStart);
            client._databaseUri = UriFactory.CreateDatabaseUri(client._databaseName);
            client._collectionUri = UriFactory.CreateDocumentCollectionUri(client._databaseName, client._collectionName);

            return client;
        }

           
        public async Task<Tuple<TimeSpan,Item>> GetItem(string airport, string terminal, string gate, string deviceId)
        {
            var id = $"{airport}{terminal}{gate}{deviceId}";
            var partitionKey = $"{airport}{terminal}";
            
            var requestOptions = new RequestOptions()
            {
                PartitionKey = new PartitionKey(partitionKey)
                 
            };
            var documentUri = UriFactory.CreateDocumentUri(_databaseName, _collectionName, id);
            var documentResponse = await _client.ReadDocumentAsync<Item>(documentUri, requestOptions);
            return new Tuple<TimeSpan, Item>(documentResponse.RequestLatency, documentResponse.Document);
        }


        public async Task<Tuple<TimeSpan,Item>> UpsertItem(string airport, string terminal, string gate, string deviceId)
        {
            var item = new Item()
            {
                id = $"{ airport}{terminal}{gate}{deviceId}",
                partitionKey = $"{airport}{terminal}",
                airport = airport,
                terminal = terminal,
                gate = gate,
                deviceId = deviceId,
                fromEndpoint = _client.WriteEndpoint.ToString(),
                text = Guid.NewGuid().ToString()
            };
            

            var resourceResponse = await _client.UpsertDocumentAsync(_collectionUri, item);

            return new Tuple<TimeSpan,Item>(resourceResponse.RequestLatency, item);
          
        }

        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected  virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }
            }
        }
    }
}
