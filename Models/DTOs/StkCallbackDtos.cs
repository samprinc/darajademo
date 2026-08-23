using System.Text.Json.Serialization;

namespace DarajaDemo.Models.DTOs;

public class StkCallbackPayload
{
    [JsonPropertyName("Body")]
    public StkCallbackBody Body { get; set; } = new();
}

public class StkCallbackBody
{
    [JsonPropertyName("stkCallback")]
    public StkCallback StkCallback { get; set; } = new();
}

public class StkCallback
{
    [JsonPropertyName("MerchantRequestID")]
    public string MerchantRequestId { get; set; } = string.Empty;

    [JsonPropertyName("CheckoutRequestID")]
    public string CheckoutRequestId { get; set; } = string.Empty;

    [JsonPropertyName("ResultCode")]
    public int ResultCode { get; set; }

    [JsonPropertyName("ResultDesc")]
    public string ResultDesc { get; set; } = string.Empty;

    [JsonPropertyName("CallbackMetadata")]
    public CallbackMetadata? CallbackMetadata { get; set; }
}

public class CallbackMetadata
{
    [JsonPropertyName("Item")]
    public List<CallbackItem> Item { get; set; } = new();
}

public class CallbackItem
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Value")]
    public object? Value { get; set; }
}