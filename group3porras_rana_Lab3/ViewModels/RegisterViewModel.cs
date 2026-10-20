using System.ComponentModel.DataAnnotations;

namespace group3porras_rana_Lab3.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(50)]
    public string Username { get; set; } = "";

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = "";

    [Required, StringLength(50), Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, StringLength(50), Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";

    [Required]
    public string Role { get; set; } = "Student";   // "Student" or "Instructor"
}