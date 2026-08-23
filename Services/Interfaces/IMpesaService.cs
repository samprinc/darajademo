using DarajaDemo.Models.Common;
using DarajaDemo.Models.DTOs;

namespace DarajaDemo.Services.Interfaces;

public interface IMpesaService
{
    Task<string> GetAccessTokenAsync();
    Task<ApiResponse<StkPushResponseDto>> InitiateStkPushAsync(StkPushRequestDto request);
    Task ProcessStkCallbackAsync(StkCallbackPayload payload);
    Task RegisterC2bUrlsAsync();
    Task ProcessC2bConfirmationAsync(C2bWebhookPayloadDto payload);
}