using System.ComponentModel.DataAnnotations;

namespace ILLiasu_Local_Mall.ViewModels.Account;

public sealed class RegisterViewModel
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 12)] public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
