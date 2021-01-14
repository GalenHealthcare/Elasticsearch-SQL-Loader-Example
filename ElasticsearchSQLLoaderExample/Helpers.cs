using System;
using Nest;

namespace ElasticsearchSQLLoaderExample
{
    class Helpers
    {
        public static ElasticClient CreateElasticClient(string host)
        {
            Uri node = new Uri(host);

            ConnectionSettings settings = new ConnectionSettings(node);

            return new ElasticClient(settings);
        }
    }
}
