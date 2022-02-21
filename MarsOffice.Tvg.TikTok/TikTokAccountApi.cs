using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
using MarsOffice.Microfunction;
using MarsOffice.Tvg.TikTok.Abstractions;
using MarsOffice.Tvg.TikTok.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Tvg.TikTok
{
    public class TikTokAccountApi
    {
        private readonly IMapper _mapper;
        private readonly HttpClient _httpClient;

        public TikTokAccountApi(IMapper mapper, IHttpClientFactory httpClientFactory)
        {
            _mapper = mapper;
            _httpClient = httpClientFactory.CreateClient();
        }

        [FunctionName("AddAccount")]
        public async Task<IActionResult> AddAccount(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/tiktok/addAccount")] HttpRequest req,
            [Table("TikTokAccounts", Connection = "localsaconnectionstring")] CloudTable tikTokAccountsTable,
            ILogger log
            )
        {
            try
            {
                var principal = MarsOfficePrincipal.Parse(req);
                var userId = principal.FindFirst("id").Value;
                var json = string.Empty;
                using (var streamReader = new StreamReader(req.Body))
                {
                    json = await streamReader.ReadToEndAsync();
                }
                var payload = JsonConvert.DeserializeObject<TikTokAccount>(json, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                payload.UserId = userId;

                var entity = _mapper.Map<TikTokAccountEntity>(payload);
                entity.ETag = "*";
                entity.PartitionKey = entity.UserId;
                entity.RowKey = entity.AuthCode;

                // TODO

                var op = TableOperation.InsertOrMerge(entity);
                await tikTokAccountsTable.ExecuteAsync(op);
                return new OkObjectResult(payload);
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("GetAccounts")]
        public async Task<IActionResult> GetAccounts(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/tiktok/getAccounts")] HttpRequest req,
            [Table("TikTokAccounts", Connection = "localsaconnectionstring")] CloudTable tikTokAccountsTable,
            ILogger log
            )
        {
            try
            {
                var principal = MarsOfficePrincipal.Parse(req);
                var userId = principal.FindFirst("id").Value;
                var accounts = new List<TikTokAccount>();
                var query = new TableQuery<TikTokAccountEntity>()
                    .Where(
                        TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, userId)
                    ).OrderBy("Timestamp");

                var hasData = true;
                TableContinuationToken tct = null;
                while (hasData) {
                    var tableResponse = await tikTokAccountsTable.ExecuteQuerySegmentedAsync(query, tct);
                    accounts.AddRange(
                        _mapper.Map<IEnumerable<TikTokAccount>>(tableResponse)
                    );
                    tct = tableResponse.ContinuationToken;
                    if (tct == null) {
                        hasData = false;
                    }
                }
                return new OkObjectResult(accounts);
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("DeleteAccount")]
        public async Task<IActionResult> DeleteAccount(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "api/tiktok/deleteAccount/{authCode}")] HttpRequest req,
            [Table("TikTokAccounts", Connection = "localsaconnectionstring")] CloudTable tikTokAccountsTable,
            ILogger log
            )
        {
            try
            {
                var principal = MarsOfficePrincipal.Parse(req);
                var userId = principal.FindFirst("id").Value;
                var op = TableOperation.Delete(new TikTokAccountEntity {
                    PartitionKey = userId,
                    RowKey = req.RouteValues["authCode"].ToString(),
                    ETag = "*"
                });
                await tikTokAccountsTable.ExecuteAsync(op);
                return new OkResult();
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }
    }
}