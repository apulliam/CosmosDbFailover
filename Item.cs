using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosDbBenchmark
{
    public class Item
    {
        public string id { get; set; }
        public string partitionKey { get; set; }
        public string text { get; set; }
        public string airport { get; set; }
        public string terminal { get; set; }
        public string gate { get; set; }
        public string deviceId { get; set; }
        public string fromEndpoint { get; set; }

    }
}
