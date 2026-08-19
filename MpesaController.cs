using Microsoft.AspNetCore.Mvc;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace DarajaDemo.Controllers;

// =========================================================
// WEEK 11 DEMO BUILD — "Live AI Debugging" Session
// -----------------------------------------------------------
// This is the same Daraja integration from Level 3, with one
// extra production-style optimization: OAuth token caching.
//
// WHY CACHE THE TOKEN AT ALL?
// Safaricom's access token is valid for 3600 seconds (1 hour).
// A well-built API should NOT request a brand new token on every
// single payment request — that's an unnecessary network round
// trip and it adds latency for the customer standing there with
// their phone out. So we cache the token and only ask Safaricom
// for a new one when the old one is about to expire.
//
// Tonight we are going to run this code, hit a real error (or
// walk through real behavior), paste it into Claude/ChatGPT, and
// compare what a lazy prompt gets us versus a specific, contextual
// prompt. Follow along on your own machine — don't just watch.
// =========================================================

public class StkPushRequestDto
{
    public string PhoneNumber { get; set; } = string.Empty; // e.g. 0712345678 or 254712345678
    public int Amount { get; set; } = 1;
    public string AccountReference { get; set; } = "PataSpace";
    public string TransactionDesc { get; set; } = "Payment";
}

public class StkQueryRequestDto
{
    public string CheckoutRequestId { get; set; } = string.Empty;
}

// Holds what we get back from Safaricom's token endpoint, plus
// when it expires, so we can decide whether to reuse it or not.
internal class CachedToken
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class MpesaController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MpesaController> _logger;

    // -----------------------------------------------------
    // TOKEN CACHE
    // -----------------------------------------------------
    // Static so it survives across requests — ASP.NET Core creates
    // a new MpesaController instance per request, so an instance
    // field would reset every time. In a real production system
    // this would live in IMemoryCache or Redis instead of a bare
    // static field (so it survives app restarts and works across
    // multiple server instances), but the caching *logic* we're
    // demoing tonight is the same either way.
    private static CachedToken? _cachedToken;

    private string ConsumerKey => _config["Daraja:ConsumerKey"]!;
    private string ConsumerSecret => _config["Daraja:ConsumerSecret"]!;
    private string ShortCode => _config["Daraja:ShortCode"]!;
    private string Passkey => _config["Daraja:Passkey"]!;
    private string CallbackUrl => _config["Daraja:CallbackUrl"]!;
    private string TransactionType => _config["Daraja:TransactionType"]!;
    private string BaseUrl => _config["Daraja:BaseUrl"]!;

    public MpesaController(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<MpesaController> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // =========================================================
    // 1. GET TOKEN (OAuth) — now with caching
    // =========================================================
    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        try
        {
            // Reuse the cached token if we already have one — this is
            // what saves us the extra round-trip to Safaricom.
            if (_cachedToken != null)
            {
                _logger.LogInformation("Using cached Daraja token.");
                return Ok(new { access_token = _cachedToken.AccessToken, cached = true });
            }

            var client = _httpClientFactory.CreateClient("Daraja");

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{ConsumerKey}:{ConsumerSecret}")
            );

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{BaseUrl}/oauth/v1/generate?grant_type=client_credentials"
            );
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Daraja token request failed: {Content}", content);
                return StatusCode((int)response.StatusCode, content);
            }

            dynamic? tokenData = JsonConvert.DeserializeObject(content);
            string accessToken = tokenData!.access_token;
            int expiresIn = int.Parse((string)tokenData!.expires_in);

            _cachedToken = new CachedToken
            {
                AccessToken = accessToken,
                ExpiresAt = DateTime.Now.AddSeconds(expiresIn)
            };

            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while requesting Daraja OAuth token");
            return StatusCode(500, new { error = "Could not reach Safaricom. Check your internet connection or credentials." });
        }
    }

    // =========================================================
    // 2. INITIATE STK PUSH (The Pop-up on the User's Phone)
    // =========================================================
    [HttpPost("stkpush")]
    public async Task<IActionResult> InitiateStkPush(
        [FromBody] StkPushRequestDto body,
        [FromHeader(Name = "Authorization")] string accessToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.PhoneNumber))
                return BadRequest(new { error = "PhoneNumber is required." });

            var client = _httpClientFactory.CreateClient("Daraja");

            var tokenOnly = accessToken.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();

            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/mpesa/stkpush/v1/processrequest");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenOnly);

            // Safaricom requires the password timestamp to reflect
            // Nairobi local time (EAT), in the strict format
            // YYYYMMDDHHmmss. On our dev laptops (already set to EAT)
            // DateTime.Now gives us that for free.
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            string password = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{ShortCode}{Passkey}{timestamp}")
            );

            string formattedPhone = NormalizePhoneNumber(body.PhoneNumber);

            var payload = new
            {
                BusinessShortCode = ShortCode,
                Password = password,
                Timestamp = timestamp,
                TransactionType = TransactionType,
                Amount = body.Amount,
                PartyA = formattedPhone,
                PartyB = ShortCode,
                PhoneNumber = formattedPhone,
                CallBackURL = CallbackUrl,
                AccountReference = body.AccountReference,
                TransactionDesc = body.TransactionDesc
            };

            var jsonPayload = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json"
            );
            request.Content = jsonPayload;

            var response = await client.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("STK Push failed: {Result}", result);
                return StatusCode((int)response.StatusCode, result);
            }

            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while initiating STK Push");
            return StatusCode(500, new { error = "Something went wrong while starting the payment. Check the server logs." });
        }
    }

    // =========================================================
    // 3. QUERY STK PUSH STATUS
    // =========================================================
    [HttpPost("stkquery")]
    public async Task<IActionResult> QueryStkPushStatus(
        [FromBody] StkQueryRequestDto body,
        [FromHeader(Name = "Authorization")] string accessToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.CheckoutRequestId))
                return BadRequest(new { error = "CheckoutRequestId is required." });

            var client = _httpClientFactory.CreateClient("Daraja");
            var tokenOnly = accessToken.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();

            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/mpesa/stkpushquery/v1/query");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenOnly);

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string password = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{ShortCode}{Passkey}{timestamp}")
            );

            var payload = new
            {
                BusinessShortCode = ShortCode,
                Password = password,
                Timestamp = timestamp,
                CheckoutRequestID = body.CheckoutRequestId
            };

            request.Content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();

            return response.IsSuccessStatusCode
                ? Content(result, "application/json")
                : StatusCode((int)response.StatusCode, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while querying STK Push status");
            return StatusCode(500, new { error = "Could not check payment status. Try again shortly." });
        }
    }

    // =========================================================
    // 4. CALLBACK ENDPOINT (The Webhook)
    // =========================================================
    [HttpPost("callback")]
    public IActionResult MpesaCallback([FromBody] object callbackData)
    {
        _logger.LogInformation("MPESA CALLBACK RECEIVED: {Data}", callbackData?.ToString());
        return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
    }

    // =========================================================
    // HELPER: normalize phone numbers to Safaricom's 2547XXXXXXXX format
    // =========================================================
    private static string NormalizePhoneNumber(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("0") && digits.Length == 10)
            return "254" + digits[1..];

        if (digits.StartsWith("254") && digits.Length == 12)
            return digits;

        if (digits.StartsWith("7") && digits.Length == 9)
            return "254" + digits;

        return digits;
    }
}