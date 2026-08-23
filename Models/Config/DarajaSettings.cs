namespace DarajaDemo.Models.Config;

public class DarajaSettings
{
    public string Environment { get; set; } = "Production";
    public string ConsumerKey { get; set; } = string.Empty;
    public string ConsumerSecret { get; set; } = string.Empty;
    
    // Split into Store and Till
    public string StoreNumber { get; set; } = string.Empty;
    public string TillNumber { get; set; } = string.Empty;
    
    public string Passkey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;

    // The single root URL (Ngrok or Render domain)
    public string ServerBaseUrl { get; set; } = string.Empty;

    // Helper getters that dynamically build the exact endpoints
    public string StkCallbackUrl => $"{ServerBaseUrl.TrimEnd('/')}/api/payments/callback";
    public string C2bConfirmationUrl => $"{ServerBaseUrl.TrimEnd('/')}/api/payments/c2b/confirmation";
    public string C2bValidationUrl => $"{ServerBaseUrl.TrimEnd('/')}/api/payments/c2b/validation";
}