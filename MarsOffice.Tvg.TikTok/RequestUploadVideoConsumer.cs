using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MarsOffice.Tvg.TikTok.Abstractions;
using MarsOffice.Tvg.TikTok.Entities;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Tvg.TikTok
{
    public class RequestUploadVideoConsumer
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public RequestUploadVideoConsumer(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClient = httpClientFactory.CreateClient();
            _config = config;
        }



        [FunctionName("RequestUploadVideoConsumer")]
        public async Task Run(
            [QueueTrigger("request-upload-video", Connection = "localsaconnectionstring")] CloudQueueMessage message,
            [Table("TikTokAccounts", Connection = "localsaconnectionstring")] CloudTable tikTokAccountsTable,
            [Queue("video-upload-result", Connection = "localsaconnectionstring")] IAsyncCollector<VideoUploadResult> videoUploadResultQueue,
            ILogger log
            )
        {
            RequestUploadVideo request = null;
            try
            {
                request = JsonConvert.DeserializeObject<RequestUploadVideo>(message.AsString, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                });

                var orFilters = TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, request.OpenIds.First());
                for (var i = 1; i < request.OpenIds.Count(); i++)
                {
                    orFilters = TableQuery.CombineFilters(
                        orFilters,
                        TableOperators.Or,
                        TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, request.OpenIds.ElementAt(i))
                    );
                }

                var query = new TableQuery<TikTokAccountEntity>()
                    .Where(
                        TableQuery.CombineFilters(
                            TableQuery.GenerateFilterCondition("AccessToken", QueryComparisons.NotEqual, null),
                            TableOperators.And,
                            TableQuery.CombineFilters(
                                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, request.UserId),
                                TableOperators.And,
                                orFilters
                            )
                        )
                    );

                var allAccounts = new List<TikTokAccountEntity>();
                var hasData = true;
                TableContinuationToken tct = null;
                while (hasData)
                {
                    var accountEntities = await tikTokAccountsTable.ExecuteQuerySegmentedAsync(query, tct);
                    allAccounts.AddRange(accountEntities);
                    tct = accountEntities.ContinuationToken;
                    if (tct == null)
                    {
                        hasData = false;
                    }
                }

                if (!allAccounts.Any())
                {
                    throw new Exception("No accounts found");
                }

                var csa = Microsoft.Azure.Storage.CloudStorageAccount.Parse(_config["localsaconnectionstring"]);
                var blobClient = csa.CreateCloudBlobClient();
                var containerName = request.VideoPath.Split("/").First();
                var containerReference = blobClient.GetContainerReference(containerName);
                if (!await containerReference.ExistsAsync())
                {
                    throw new Exception("Video doesn't exist - no container");
                }
                var blobPath = string.Join("/", request.VideoPath.Split("/").Skip(1).ToList());
                var blobRef = containerReference.GetBlockBlobReference(blobPath);
                if (!await blobRef.ExistsAsync())
                {
                    throw new Exception("Video doesn't exist - no file");
                }

                foreach (var account in allAccounts)
                {
                    try
                    {
                        var stream = await blobRef.OpenReadAsync();
                        var sc = new StreamContent(
                            stream
                        );
                        var mfdc = new MultipartFormDataContent
                        {
                            {
                                sc,
                                "video"
                            }
                        };
                        var httpRequest = new HttpRequestMessage(HttpMethod.Post,
                            $"https://open-api.tiktok.com/share/video/upload/?open_id={account.AccountId}&access_token={account.AccessToken}")
                        {
                            Content = mfdc
                        };
                        var uploadResponse = await _httpClient.SendAsync(httpRequest);
                        uploadResponse.EnsureSuccessStatusCode();
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Video upload failed for account " + account.AccountId);
                        throw new Exception("STOP: TikTok upload failed for " + account.AccountId);
                    }
                }

                await videoUploadResultQueue.AddAsync(new VideoUploadResult
                {
                    Success = true,
                    JobId = request?.JobId,
                    UserId = request?.UserId,
                    VideoId = request?.VideoId,
                    UserEmail = request?.UserEmail
                });
                await videoUploadResultQueue.FlushAsync();
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                if (message.DequeueCount >= 5 || e.Message.Contains("STOP"))
                {
                    await videoUploadResultQueue.AddAsync(new VideoUploadResult
                    {
                        Error = "TikTokService: " + e.Message,
                        Success = false,
                        JobId = request?.JobId,
                        UserId = request?.UserId,
                        VideoId = request?.VideoId,
                        UserEmail = request?.UserEmail
                    });
                    await videoUploadResultQueue.FlushAsync();
                }
                else
                {
                    throw;
                }
            }
        }
    }
}