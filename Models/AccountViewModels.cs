using System.ComponentModel.DataAnnotations;

namespace GtrackWeb.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Type UserName")]
    [Display(Name = "User Name")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Type Userpassword")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? Error { get; set; }
}

public sealed class ChangePasswordViewModel
{
    [Required]
    public string UserName { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string OldPassword { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class SelectCompanyViewModel
{
    public List<LookupItem> Companies { get; set; } = new();

    [Required(ErrorMessage = "Select a company")]
    public int CompId { get; set; }

    [Required]
    public string Year { get; set; } = DateTime.Now.Year.ToString();
}
