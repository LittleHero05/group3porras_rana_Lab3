using System.Security.Claims;
using group3porras_rana_Lab3.Models;
using group3porras_rana_Lab3.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace group3porras_rana_Lab3.Controllers;

public class AccountController : Controller
{
    private readonly LearningPortalContext _context;
    private readonly PasswordHasher<User> _hasher = new();
    public AccountController(LearningPortalContext context) => _context = context;

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel { Role = "Student" });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // Do not trust a role posted by a browser; instructor accounts are pre-provisioned.
        model.Role = "Student";
        ModelState.Remove(nameof(model.Role));
        if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            ModelState.AddModelError(nameof(model.Username), "That username is already taken.");
        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            ModelState.AddModelError(nameof(model.Email), "That email is already registered.");
        if (!ModelState.IsValid) return View(model);

        var user = new User
        {
            Username = model.Username.Trim(), Email = model.Email.Trim(),
            FirstName = model.FirstName.Trim(), LastName = model.LastName.Trim(),
            Role = "Student", CreatedDate = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, model.Password);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        TempData["Message"] = "Account created. Please log in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
        if (user == null ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password)
               == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
