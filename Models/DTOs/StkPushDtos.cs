using System.Text.Json.Serialization;

namespace DarajaDemo.Models.DTOs;

public class StkPushRequestDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AccountReference { get; set; } = "Invoice001";
    public string TransactionDesc { get; set; } = "Payment";
}

public class StkPushResponseDto
{
    [JsonPropertyName("MerchantRequestID")]
    public string MerchantRequestId { get; set; } = string.Empty;
    
    [JsonPropertyName("CheckoutRequestID")]
    public string CheckoutRequestId { get; set; } = string.Empty;
    
    [JsonPropertyName("ResponseCode")]
    public string ResponseCode { get; set; } = string.Empty;
    
    [JsonPropertyName("ResponseDescription")]
    public string ResponseDescription { get; set; } = string.Empty;
    
    [JsonPropertyName("CustomerMessage")]
    public string CustomerMessage { get; set; } = string.Empty;
}