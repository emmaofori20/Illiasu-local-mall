using System.ComponentModel.DataAnnotations;

namespace ILLiasu_Local_Mall.ViewModels.Account;

public sealed class LoginViewModel
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password), StringLength(100)] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
