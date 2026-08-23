
namespace DarajaDemo.Models.DTOs;

public class C2bRegisterUrlDto
{
    public string ShortCode { get; set; } = string.Empty;
    public string ResponseType { get; set; } = "Completed";
    public string ConfirmationURL { get; set; } = string.Empty;
    public string ValidationURL { get; set; } = string.Empty;
}