using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace TechBrew.UserSwitcher.Api
{
    [ApiController]
    [Route("Plugin/UserSwitcher")]
    public class UserSwitcherController : ControllerBase
    {
        private readonly ILogger<UserSwitcherController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public UserSwitcherController(
            ILogger<UserSwitcherController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public class UserListItem
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsAdministrator { get; set; }
            public bool IsDisabled { get; set; }
        }

        public class ImpersonateRequest
        {
            public Guid UserId { get; set; }
        }

        public class ImpersonateResponse
        {
            public string ImpersonationUrl { get; set; } = string.Empty;
        }

        public class AuthorizeCodeRequest
        {
            public string Code { get; set; } = string.Empty;
            public Guid UserId { get; set; }
        }

        public class OkResponse
        {
            public bool Ok { get; set; }
        }

        private HttpClient CreateServerHttpClient()
        {
            var client = _httpClientFactory.CreateClient();

            var scheme = Request.Scheme;
            var host = Request.Host.Value;
            var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
            client.BaseAddress = new Uri($"{scheme}://{host}{pathBase}/");

            // Forward auth headers so server requests use the current admin's context
            if (Request.Headers.TryGetValue("Authorization", out var auth))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.ToString());
            }
            if (Request.Headers.TryGetValue("X-Emby-Authorization", out var embyAuth))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Emby-Authorization", embyAuth.ToString());
            }

            return client;
        }

        private async Task EnsureAdminAsync()
        {
            using var client = CreateServerHttpClient();
            using var response = await client.GetAsync("Users/Me");
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            var policy = root.TryGetProperty("Policy", out var p) ? p : default;
            var isAdmin = policy.ValueKind != JsonValueKind.Undefined && policy.TryGetProperty("IsAdministrator", out var ia) && ia.GetBoolean();
            if (!isAdmin)
            {
                _logger.LogWarning("Non-admin attempted to access UserSwitcher endpoints");
                Response.StatusCode = 403;
                throw new InvalidOperationException("Admin required");
            }
        }

        [HttpGet("Users")]
        public async Task<ActionResult<IReadOnlyList<UserListItem>>> GetUsers([FromQuery] string? search)
        {
            await EnsureAdminAsync();

            using var client = CreateServerHttpClient();
            // Request all users; server will filter by search client-side for now
            using var resp = await client.GetAsync("Users");
            resp.EnsureSuccessStatusCode();
            var arr = await resp.Content.ReadFromJsonAsync<JsonElement>();

            var list = new List<UserListItem>();
            var query = (search ?? string.Empty).Trim();
            if (arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var id = el.TryGetProperty("Id", out var idEl) ? idEl.GetGuid() : Guid.Empty;
                    var name = el.TryGetProperty("Name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
                    var policy = el.TryGetProperty("Policy", out var polEl) ? polEl : default;
                    var isAdmin = policy.ValueKind != JsonValueKind.Undefined && policy.TryGetProperty("IsAdministrator", out var ia) && ia.GetBoolean();
                    var isDisabled = policy.ValueKind != JsonValueKind.Undefined && policy.TryGetProperty("IsDisabled", out var idis) && idis.GetBoolean();

                    if (!string.IsNullOrEmpty(query) && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    list.Add(new UserListItem
                    {
                        Id = id,
                        Name = name,
                        IsAdministrator = isAdmin,
                        IsDisabled = isDisabled
                    });
                }
            }

            return Ok(list.OrderBy(u => u.Name).ToList());
        }

        [HttpPost("AuthorizeCode")]
        public async Task<ActionResult<OkResponse>> AuthorizeCode([FromBody] AuthorizeCodeRequest request)
        {
            await EnsureAdminAsync();

            var code = (request.Code ?? string.Empty).Trim();
            if (code.Length != 6)
            {
                return BadRequest(new { error = "Code must be 6 characters" });
            }

            try
            {
                using var client = CreateServerHttpClient();
                var payload = new
                {
                    Code = code,
                    UserId = request.UserId
                };
                using var resp = await client.PostAsJsonAsync("QuickConnect/Authorize", payload);
                resp.EnsureSuccessStatusCode();

                _logger.LogInformation("Quick Connect code authorized by admin for user {UserId}", request.UserId);
                return Ok(new OkResponse { Ok = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to authorize Quick Connect code for user {UserId}", request.UserId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("Impersonate")]
        public async Task<ActionResult<ImpersonateResponse>> Impersonate([FromBody] ImpersonateRequest request)
        {
            await EnsureAdminAsync();

            try
            {
                using var client = CreateServerHttpClient();

                // 1) Initiate Quick Connect as a pseudo device
                var initPayload = new
                {
                    AppName = "UserSwitcher",
                    AppVersion = "0.1.0",
                    DeviceName = "AdminConsole",
                    DeviceId = Guid.NewGuid().ToString("N")
                };

                using var initResp = await client.PostAsJsonAsync("QuickConnect/Initiate", initPayload);
                initResp.EnsureSuccessStatusCode();
                var initObj = await initResp.Content.ReadFromJsonAsync<JsonElement>();
                var secret = initObj.TryGetProperty("Secret", out var s) ? s.GetString() : null;
                var code = initObj.TryGetProperty("Code", out var c) ? c.GetString() : null;

                if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(code))
                {
                    throw new InvalidOperationException("Quick Connect initiation did not return Secret/Code");
                }

                // 2) Authorize the generated code for the target user
                var authorizePayload = new { Code = code, UserId = request.UserId };
                using var authResp = await client.PostAsJsonAsync("QuickConnect/Authorize", authorizePayload);
                authResp.EnsureSuccessStatusCode();

                // 3) Authenticate with the secret to obtain an access token for that user
                var authenticatePayload = new { Secret = secret };
                using var tokenResp = await client.PostAsJsonAsync("Users/AuthenticateWithQuickConnect", authenticatePayload);
                tokenResp.EnsureSuccessStatusCode();
                var tokenObj = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();
                var accessToken = tokenObj.TryGetProperty("AccessToken", out var t) ? t.GetString() : null;

                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException("Quick Connect authentication did not return AccessToken");
                }

                // Construct a URL that opens the web UI with the acquired token.
                var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
                var impersonationUrl = $"{baseUrl}/web/index.html?api_key={Uri.EscapeDataString(accessToken)}&imp=1";

                _logger.LogInformation("Impersonation URL generated for user {UserId}", request.UserId);
                return Ok(new ImpersonateResponse { ImpersonationUrl = impersonationUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to impersonate as user {UserId}", request.UserId);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
