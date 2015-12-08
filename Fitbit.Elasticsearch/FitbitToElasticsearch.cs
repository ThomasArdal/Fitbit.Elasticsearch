using System;
using System.Diagnostics;
using System.Linq;
using Fitbit.Api;
using Fitbit.Models;
using Nest;

namespace Fitbit.Elasticsearch
{
    public class FitbitToElasticsearch
    {
        private readonly string _consumerKey;
        private readonly string _consumerSecret;

        public FitbitToElasticsearch(string consumerKey, string consumerSecret)
        {
            _consumerKey = consumerKey;
            _consumerSecret = consumerSecret;
        }

        public void Dump()
        {
            var authCredential = Authenticate();
            var fitbitClient = new FitbitClient(_consumerKey, _consumerSecret, authCredential.AuthToken, authCredential.AuthTokenSecret);

            const string indexName = "personalstats";
            var elasticClient = new ElasticClient(new ConnectionSettings(new Uri("http://localhost:9200")).SetDefaultIndex(indexName));
            if (!elasticClient.IndexExists(indexName).Exists)
            {
                elasticClient.CreateIndex(x => x.Index(indexName).NumberOfReplicas(0).NumberOfShards(3));
                elasticClient.Map<Weight>(x => x.MapFromAttributes());
            }

            var result = elasticClient.Search<Weight>(s => s
                .Size(1)
                .Sort(sort => sort.OnField(f => f.Timestamp).Descending()));

            var startDate = result.Documents.Any() ? result.Documents.First().Timestamp : DateTime.Today.AddDays(-180);
            var endDate = startDate.AddDays(30);

            while (endDate <= DateTime.Today)
            {
                var weight = fitbitClient.GetWeight(startDate, endDate);
                foreach (var w in weight.Weights)
                {
                    elasticClient.Index(new Weight {Timestamp = w.DateTime, Value = w.Weight});
                }
                startDate = endDate;
                endDate = endDate.AddDays(30);
            }
        }

        AuthCredential Authenticate()
        {
            var requestTokenUrl = "http://api.fitbit.com/oauth/request_token";
            var accessTokenUrl = "http://api.fitbit.com/oauth/access_token";
            var authorizeUrl = "http://www.fitbit.com/oauth/authorize";

            var a = new Authenticator(_consumerKey, _consumerSecret, requestTokenUrl, accessTokenUrl, authorizeUrl);

            RequestToken token = a.GetRequestToken();

            var url = a.GenerateAuthUrlFromRequestToken(token, false);

            Process.Start(url);

            Console.WriteLine("Enter the verification code from the website");
            var pin = Console.ReadLine();

            var credentials = a.GetAuthCredentialFromPin(pin, token);
            return credentials;
        }

        [ElasticType(Name = "weight")]
        public class Weight
        {
            [ElasticProperty(Name = "@timestamp")]
            public DateTime Timestamp { get; set; }

            public double Value { get; set; }
        }
    }
}
