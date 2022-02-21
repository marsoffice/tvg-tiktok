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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Tvg.TikTok
{
    public class TikTokAccountApi
    {
        private readonly IMapper _mapper;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public TikTokAccountApi(IMapper mapper, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _mapper = mapper;
            _httpClient = httpClientFactory.CreateClient();
            _config = config;
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

                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://open-api.tiktok.com/oauth/access_token/?client_key={_config["ttclientkey"]}&client_secret={_config["ttclientsecret"]}&code={payload.AuthCode}&grant_type=authorization_code");
                var getAuthTokenResponse = await _httpClient.SendAsync(request);
                getAuthTokenResponse.EnsureSuccessStatusCode();
                var jsonResponse = await getAuthTokenResponse.Content.ReadAsStringAsync();
                var objResponse = JsonConvert.DeserializeObject<TikTokAuthResponse>(jsonResponse, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                }).Data;

                entity.LastRefreshDate = DateTimeOffset.UtcNow;
                entity.AccountId = objResponse.open_id;
                entity.RowKey = entity.AccountId;
                entity.AccessToken = objResponse.access_token;
                entity.AccessTokenExpAt = DateTimeOffset.UtcNow.AddSeconds(objResponse.expires_in);
                entity.RefreshToken = objResponse.refresh_token;
                entity.RefreshTokenExpAt = DateTimeOffset.UtcNow.AddSeconds(objResponse.refresh_expires_in);

                var userInfoRequest = new HttpRequestMessage(HttpMethod.Post, $"https://open-api.tiktok.com/user/info/")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(
                    new TikTokUserInfoRequest
                    {
                        access_token = entity.AccessToken,
                        open_id = entity.AccountId,
                        fields = new[] { "open_id", "avatar_url", "display_name", "union_id" }
                    }, new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    }
                ))
                };
                var userInfoResponse = await _httpClient.SendAsync(userInfoRequest);
                if (userInfoResponse.IsSuccessStatusCode)
                {
                    var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
                    var userResponse = JsonConvert.DeserializeObject<TikTokUserResponse>(userInfoJson, new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        NullValueHandling = NullValueHandling.Ignore
                    });

                    entity.Name = userResponse?.Data?.User?.display_name;
                    entity.AvatarUrl = userResponse?.Data?.User?.avatar_url;
                    entity.UnionId = userResponse?.Data?.User?.union_id;
                }
                else
                {
                    entity.Name = "unknown";
                }
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
                while (hasData)
                {
                    var tableResponse = await tikTokAccountsTable.ExecuteQuerySegmentedAsync(query, tct);
                    accounts.AddRange(
                        _mapper.Map<IEnumerable<TikTokAccount>>(tableResponse)
                    );
                    tct = tableResponse.ContinuationToken;
                    if (tct == null)
                    {
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
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "api/tiktok/deleteAccount/{accountId}")] HttpRequest req,
            [Table("TikTokAccounts", Connection = "localsaconnectionstring")] CloudTable tikTokAccountsTable,
            ILogger log
            )
        {
            try
            {
                var principal = MarsOfficePrincipal.Parse(req);
                var userId = principal.FindFirst("id").Value;
                var op = TableOperation.Delete(new TikTokAccountEntity
                {
                    PartitionKey = userId,
                    RowKey = req.RouteValues["accountId"].ToString(),
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