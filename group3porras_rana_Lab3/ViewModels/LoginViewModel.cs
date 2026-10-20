using System.ComponentModel.DataAnnotations;

namespace group3porras_rana_Lab3.ViewModels;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }   // sends the user back to the page they wanted
}