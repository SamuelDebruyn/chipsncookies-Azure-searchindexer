using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Search;
using System.Configuration;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using System;
using System.Diagnostics;

namespace ChipsnCookies.SearchIndexer
{
    class Program
    {
        static Stopwatch _stopWatch;
        static Stopwatch StopWatch => _stopWatch ?? (_stopWatch = Stopwatch.StartNew());

        static void Main()
        {
            try
            {
                var searchServiceName = ConfigurationManager.AppSettings["SearchServiceName"];
                var adminApiKey = ConfigurationManager.AppSettings["SearchServiceAdminApiKey"];
                var indexName = ConfigurationManager.AppSettings["SearchServiceIndexName"];
                var indexFileName = ConfigurationManager.AppSettings["IndexFile"];

                Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Reading {indexFileName}...");
                string json = null;
                json = System.IO.File.ReadAllText(indexFileName);
                Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Read {json.Length} characters");

                Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Deserialing JSON...");
                var list = JsonConvert.DeserializeObject<List<Document>>(json)?.Where(d => !string.IsNullOrEmpty(d.Uid) && !string.IsNullOrEmpty(d.Content) && !string.IsNullOrEmpty(d.Rendered) && !string.IsNullOrEmpty(d.Title));

                if (list == null || list.Any())
                {
                    Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] No documents found");
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Found {list.Count()} documents");

                var batch = IndexBatch.Upload(list);

                Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Authenticating to search service {searchServiceName}...");
                using (var searchServiceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(adminApiKey)))
                {
                    Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Checking if index {indexName} exists...");
                    if (!searchServiceClient.Indexes.Exists(indexName))
                    {
                        Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Index {indexName} did not exist. Building index...");
                        var scoringProfile = BuildScoringProfile<Document>(indexName + "-scoring");
                        var index = new Index
                        {
                            Name = indexName,
                            Fields = FieldBuilder.BuildForType<Document>(),
                            ScoringProfiles = new ScoringProfile[] { scoringProfile },
                            DefaultScoringProfile = scoringProfile.Name,
                            CorsOptions = new CorsOptions(new string[] { "*" }){ MaxAgeInSeconds = 3600 }
                        };
                        Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Index built, creating on Azure...");
                        searchServiceClient.Indexes.Create(index);
                    }
                    else
                    {
                        Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Index {indexName} existed");
                    }
                }

                Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Authenticating with the index client to {indexName} on {searchServiceName}...");
                using (var indexClient = new SearchIndexClient(searchServiceName, indexName, new SearchCredentials(adminApiKey)))
                {
                    Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Sending batch of actions...");
                    var result = indexClient.Documents.Index(batch);

                    Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Batch completed succesfully");
                    Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Success: " + result.Results.Count(r => r.Succeeded));

                    var failed = result.Results.Where(r => !r.Succeeded).ToList();
                    foreach (var failure in failed)
                    {
                        Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Failure for key {failure.Key} (status {failure.StatusCode}): {failure.ErrorMessage}");
                        Environment.Exit(1);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] {e.GetType().Name} occured: {e.Message}");
                if(e.InnerException != null)
                {
                    Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] {e.InnerException.GetType().Name} occured: {e.InnerException.Message}");
                }

                Environment.Exit(1);
                return;
            }
        }

        static ScoringProfile BuildScoringProfile<T>(string name)
        {
            Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Building scoring profile with name {name}...");
            var textWeights = typeof(T).GetProperties().Where(p => p.CustomAttributes.Any(ca => ca.AttributeType == typeof(ScoringAttribute))).ToDictionary(p => p.Name.ToLower(), p => ((ScoringAttribute)p.GetCustomAttributes(true).First(mi => mi.GetType() == typeof(ScoringAttribute))).Weight);
            Console.WriteLine($"[{StopWatch.ElapsedMilliseconds}] Found {textWeights.Count} text weights: {textWeights}");

            var scoringProfile = new ScoringProfile
            {
                FunctionAggregation = ScoringFunctionAggregation.Sum,
                Name = name,
                TextWeights = new TextWeights(textWeights)
            };
            return scoringProfile;
        }
    }
}
