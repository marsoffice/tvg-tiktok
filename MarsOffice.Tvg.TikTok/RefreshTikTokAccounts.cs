using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using MarsOffice.Tvg.TikTok.Entities;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Tvg.TikTok
{
    public class RefreshTikTokAccounts
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public RefreshTikTokAccounts(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClient = httpClientFactory.CreateClient();
        }

        [FunctionName("RefreshTikTokAccounts")]
        public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo myTimer,
        [Table("TikTokAccounts", Connection = "localsaconnectionstring")] CloudTable tikTokAccountsTable,
        ILogger log)
        {
            var now = DateTimeOffset.UtcNow;
            log.LogInformation($"Refresh TikTok Accounts function executed at: {DateTime.Now}");

            var query = new TableQuery<TikTokAccountEntity>().Where(
                TableQuery.GenerateFilterCondition("RefreshToken", QueryComparisons.NotEqual, null)
            );

            var allAccounts = new List<TikTokAccountEntity>();

            var hasData = true;
            TableContinuationToken tct = null;
            while (hasData)
            {
                var response = await tikTokAccountsTable.ExecuteQuerySegmentedAsync(query, tct);
                allAccounts.AddRange(response);
                tct = response.ContinuationToken;
                if (tct == null)
                {
                    hasData = false;
                }
            }

            foreach (var account in allAccounts)
            {
                try
                {
                    if (account.AccessToken != null && account.AccessTokenExpAt.HasValue && account.AccessTokenExpAt.Value > now
                        && (account.AccessTokenExpAt.Value - now) >= TimeSpan.FromMinutes(75))
                    {
                        continue;
                    }

                    if (account.RefreshTokenExpAt.HasValue && now > account.RefreshTokenExpAt.Value)
                    {
                        throw new Exception("Account " + account.AccountId + " is unrecoverable.");
                    }

                    var refreshRequest = new HttpRequestMessage(HttpMethod.Post,
                        $"https://open-api.tiktok.com/oauth/refresh_token/?client_key={_config["ttclientkey"]}&grant_type=refresh_token&refresh_token={account.RefreshToken}");

                    var refreshResponse = await _httpClient.SendAsync(refreshRequest);
                    refreshResponse.EnsureSuccessStatusCode();
                    var json = await refreshResponse.Content.ReadAsStringAsync();
                    var response = JsonConvert.DeserializeObject<TikTokRefreshResponse>(json, new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        NullValueHandling = NullValueHandling.Ignore
                    });
                    account.ETag = "*";
                    account.AccessToken = response.Data.access_token;
                    account.AccessTokenExpAt = DateTimeOffset.UtcNow.AddSeconds(response.Data.expires_in);
                    account.RefreshToken = response.Data.refresh_token;
                    account.RefreshTokenExpAt = DateTimeOffset.UtcNow.AddSeconds(response.Data.refresh_expires_in);
                    account.LastRefreshDate = now;
                    var updateOp = TableOperation.Merge(
                        account
                    );
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Refresh failed for account " + account.AccountId);
                }
            }
        }
    }
}
