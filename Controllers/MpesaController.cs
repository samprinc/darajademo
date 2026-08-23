using DarajaDemo.Models.DTOs;
using DarajaDemo.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DarajaDemo.Controllers;

[ApiController]
[Route("api/payments")]
public class MpesaController : ControllerBase
{
    private readonly IMpesaService _mpesaService;

    public MpesaController(IMpesaService mpesaService)
    {
        _mpesaService = mpesaService;
    }

    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        var token = await _mpesaService.GetAccessTokenAsync();
        return Ok(new { access_token = token });
    }

    [HttpPost("stkpush")]
    public async Task<IActionResult> InitiateStkPush([FromBody] StkPushRequestDto request)
    {
        var result = await _mpesaService.InitiateStkPushAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPost("c2b/register")]
    public async Task<IActionResult> RegisterC2bUrls()
    {
        await _mpesaService.RegisterC2bUrlsAsync();
        return Ok(new { message = "C2B Webhook URLs registered successfully with Safaricom." });
    }

    [HttpPost("callback")]
    public async Task<IActionResult> StkCallback([FromBody] StkCallbackPayload payload)
    {
        await _mpesaService.ProcessStkCallbackAsync(payload);
        // Safaricom expects a success response so it stops retrying the webhook
        return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
    }

    [HttpPost("c2b/validation")]
    public IActionResult C2bValidation([FromBody] C2bWebhookPayloadDto payload)
    {
        // Automatically accept the payment. Add specific validation logic here if required.
        return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
    }

    [HttpPost("c2b/confirmation")]
    public async Task<IActionResult> C2bConfirmation([FromBody] C2bWebhookPayloadDto payload)
    {
        await _mpesaService.ProcessC2bConfirmationAsync(payload);
        return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
    }
}