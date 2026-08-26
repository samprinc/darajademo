using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DarajaDemo.Data;
using DarajaDemo.Data.Entities;
using DarajaDemo.Hubs;
using DarajaDemo.Models.Common;
using DarajaDemo.Models.Config;
using DarajaDemo.Models.DTOs;
using DarajaDemo.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DarajaDemo.Services.Implementations;

public class MpesaService : IMpesaService
{
    private readonly HttpClient _httpClient;
    private readonly DarajaSettings _settings;
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly IHubContext<PaymentHub> _hubContext;
    private readonly ILogger<MpesaService> _logger;
    private const string TOKEN_CACHE_KEY = "DarajaAccessToken";

    public MpesaService(
        IHttpClientFactory httpClientFactory,
        IOptions<DarajaSettings> settings,
        AppDbContext dbContext,
        IMemoryCache cache,
        IHubContext<PaymentHub> hubContext,
        ILogger<MpesaService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Daraja");
        _settings = settings.Value;
        _dbContext = dbContext;
        _cache = cache;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (_cache.TryGetValue(TOKEN_CACHE_KEY, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken;
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ConsumerKey}:{_settings.ConsumerSecret}"));
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.BaseUrl}/oauth/v1/generate?grant_type=client_credentials");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var token = document.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn = document.RootElement.GetProperty("expires_in").GetString();
        
        // Cache token slightly shorter than expiry (e.g. 3540 secs instead of 3600)
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(int.Parse(expiresIn!) - 60));
        
        _cache.Set(TOKEN_CACHE_KEY, token, cacheOptions);
        return token;
    }

    public async Task<ApiResponse<StkPushResponseDto>> InitiateStkPushAsync(StkPushRequestDto request)
    {
        var token = await GetAccessTokenAsync();
        var timestamp = GetEastAfricanTimestamp();

        // STK Push is authorized by the head-office shortcode; C2B uses StoreNumber.
        var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.HeadOffice}{_settings.Passkey}{timestamp}"));
        var formattedPhone = NormalizePhoneNumber(request.PhoneNumber);

        var payload = new
        {
            BusinessShortCode = _settings.HeadOffice,
            Password = password,
            Timestamp = timestamp,
            TransactionType = _settings.TransactionType,     // "CustomerBuyGoodsOnline"
            Amount = request.Amount,
            PartyA = formattedPhone,
            PartyB = _settings.TillNumber,
            PhoneNumber = formattedPhone,
            CallBackURL = _settings.StkCallbackUrl,
            AccountReference = request.AccountReference,
            TransactionDesc = request.TransactionDesc
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/mpesa/stkpush/v1/processrequest");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("STK Push Failed: {Content}", content);
            return new ApiResponse<StkPushResponseDto> { Success = false, Message = "Failed to initiate STK Push." };
        }

        var result = JsonSerializer.Deserialize<StkPushResponseDto>(content);

        // Persist PENDING transaction to Database
        var transaction = new MpesaTransaction
        {
            Source = TransactionSource.STK_PUSH,
            MerchantRequestId = result?.MerchantRequestId,
            CheckoutRequestId = result?.CheckoutRequestId,
            PhoneNumber = formattedPhone,
            Amount = request.Amount,
            BillRefNumber = request.AccountReference,
            Status = TransactionStatus.PENDING,
            RawPayloadJson = content
        };

        _dbContext.MpesaTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync();

        return new ApiResponse<StkPushResponseDto> { Success = true, Data = result };
    }

    public async Task ProcessStkCallbackAsync(StkCallbackPayload payload)
    {
        var callback = payload.Body.StkCallback;
        var transaction = await _dbContext.MpesaTransactions
            .FirstOrDefaultAsync(t => t.CheckoutRequestId == callback.CheckoutRequestId);

        if (transaction == null)
        {
            _logger.LogWarning("Transaction with CheckoutRequestId {Id} not found.", callback.CheckoutRequestId);
            return;
        }

        transaction.ResultCode = callback.ResultCode;
        transaction.ResultDesc = callback.ResultDesc;
        transaction.UpdatedAt = DateTime.UtcNow;
        transaction.RawPayloadJson = JsonSerializer.Serialize(payload);

        if (callback.ResultCode == 0 && callback.CallbackMetadata != null)
        {
            transaction.Status = TransactionStatus.COMPLETED;
            
            // Extract metadata fields Safely
            foreach (var item in callback.CallbackMetadata.Item)
            {
                if (item.Name == "MpesaReceiptNumber") transaction.TransId = item.Value?.ToString();
            }
        }
        else
        {
            transaction.Status = TransactionStatus.FAILED;
        }

        await _dbContext.SaveChangesAsync();

        // Broadcast to clients via SignalR
        await _hubContext.Clients.All.SendAsync("ReceivePaymentUpdate", transaction);
    }

    public async Task ProcessC2bConfirmationAsync(C2bWebhookPayloadDto payload)
{
    // Check if this transaction has already been recorded
    var existingTransaction = await _dbContext.MpesaTransactions
        .FirstOrDefaultAsync(t => t.TransId == payload.TransID);

    if (existingTransaction != null)
    {
        // Safaricom retry — update the existing transaction
        existingTransaction.Status = TransactionStatus.COMPLETED;
        existingTransaction.UpdatedAt = DateTime.UtcNow;
        existingTransaction.RawPayloadJson = JsonSerializer.Serialize(payload);

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "C2B Confirmation retry received and updated for TransID: {TransId}",
            payload.TransID);

        return;
    }

    // New C2B transaction
    var transaction = new MpesaTransaction
    {
        Id = Guid.NewGuid(),
        Source = TransactionSource.C2B_MANUAL,
        TransId = payload.TransID,
        PhoneNumber = payload.MSISDN,
        Amount = decimal.Parse(payload.TransAmount),
        BillRefNumber = payload.BillRefNumber,
        Status = TransactionStatus.COMPLETED,
        RawPayloadJson = JsonSerializer.Serialize(payload),
        CreatedAt = DateTime.UtcNow
    };

    _dbContext.MpesaTransactions.Add(transaction);
    await _dbContext.SaveChangesAsync();

    _logger.LogInformation(
        "New C2B Confirmation saved successfully for TransID: {TransId}",
        payload.TransID);
}

   public async Task RegisterC2bUrlsAsync()
{
    var token = await GetAccessTokenAsync();

    var payload = new
    {
        ShortCode = _settings.StoreNumber,
        ResponseType = "Completed",
        ConfirmationURL = _settings.C2bConfirmationUrl,
        ValidationURL = _settings.C2bValidationUrl
    };

    var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/mpesa/c2b/v2/registerurl");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    var response = await _httpClient.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        // Treat "already registered" as a successful state so it doesn't crash
        if (content.Contains("500.003.1001") || content.Contains("URLs are already registered"))
        {
            _logger.LogInformation("C2B URLs are already registered with Safaricom. Skipping overwrite.");
            return;
        }

        _logger.LogError("C2B URL Registration Failed: {Content}", content);
        throw new Exception($"C2B URL Registration failed: {content}");
    }

    _logger.LogInformation("C2B URLs registered successfully: {Content}", content);
}

    // ---------------- HELPER METHODS ----------------
    private static string GetEastAfricanTimestamp()
    {
        // Standard EAT offset is UTC+3. This safely bypasses OS-level TimeZone differences (Linux vs Windows)
        var eatTime = DateTime.UtcNow.AddHours(3);
        return eatTime.ToString("yyyyMMddHHmmss");
    }

    private static string NormalizePhoneNumber(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0") && digits.Length == 10) return "254" + digits[1..];
        if (digits.StartsWith("7") && digits.Length == 9) return "254" + digits;
        return digits;
    }
}