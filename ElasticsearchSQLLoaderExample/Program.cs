using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Nest;
using Microsoft.Extensions.Configuration;

namespace ElasticsearchSQLLoaderExample
{
    class Program
    {
        static async Task Main()
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();

            string IndexName = config["IndexName"];
            string SqlDbConnectionString = config["SqlDbConnectionString"];
            string ElasticsearchHostUrl = config["ElasticsearchHostUrl"];
            int BatchIndexMaxSize = int.Parse(config["BatchIndexMaxSize"]);

            IEnumerable <HospitalReadmission> results = await GetReadmissionMetrics(SqlDbConnectionString);

            // Connect to Elasticsearch
            ElasticClient elasticClient = Helpers.CreateElasticClient(ElasticsearchHostUrl);

            /**
              * Initialize temp list for ready to index
              * documents and counter to support batch indexing
              */
            List<HospitalReadmission> documentBatch = new List<HospitalReadmission>();
            int documentCount = 0;

            // Treat the SQL results as a list and batch index into Elasticsearch
            foreach (HospitalReadmission document in results.AsList())
            {
                documentCount += 1;
                documentBatch.Add(document);

                if (documentCount >= BatchIndexMaxSize)
                {
                    // Index documents currently in temp list
                    await IndexBatch(elasticClient, IndexName, documentBatch);

                    // Reset document counter
                    documentCount = 0;
                    // Empty temp list so documents aren't indexed multiple times
                    documentBatch.Clear();
                }
            }

            // Index any remaining documents
            await IndexBatch(elasticClient, IndexName, documentBatch);
        }

        private static Task<IEnumerable<HospitalReadmission>> GetReadmissionMetrics(string connectionString)
        {
            // Connect to source SQL database
            using IDbConnection db = new SqlConnection(connectionString);

            var query = @"
                SELECT
                    [FacilityName],
                    [FacilityId],
                    [State],
                    [MeasureName],
                    [NumberOfDischarges],
                    [Footnote],
                    [ExcessReadmissionRatio],
                    [PredictedReadmissionRate],
                    [ExpectedReadmissionRate],
                    [NumberOfReadmissions],
                    [StartDate],
                    [EndDate]
                FROM [HRRP].[dbo].[ReadmissionMetrics]
            ";

            // Query metrics and serialize to HospitalReadmission objects
            return db.QueryAsync<HospitalReadmission>(query);
        }

        private static Task<BulkResponse> IndexBatch(ElasticClient client, string indexName, IEnumerable<dynamic> documents)
        {
            return client.BulkAsync(b => b
                .Index(indexName)
                .IndexMany(documents)
            );
        }
    }
}
