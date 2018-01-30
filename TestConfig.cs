using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosDbBenchmark
{
    public class TestConfig
    {
        public string ResourceGroup;
        public string CosmosDbName;
        public string[] Regions;
        public string DatabaseName;
        public string CollectionName;
        public int CollectionThroughput;
        public string PartitionKey;
        public bool CleanupDatabaseOnStart;
        public bool CleanupDatabaseOnFinish;
        public bool CleanupAcountOnFinish;
    }
}
