using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LicenseSystem.Admin.Pages;

public class LoginModel : PageModel
{
    private const string DefaultAdminEmail = "admin@licensesystem.local";
    private const string DefaultAdminPassword = "Admin_Secure_Pass_2026!";

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; } = true;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please provide both an administrator email and password.";
            return Page();
        }

        // Validate administrator credentials
        bool isValid = string.Equals(Email.Trim(), DefaultAdminEmail, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(Password, DefaultAdminPassword, StringComparison.Ordinal);

        if (!isValid)
        {
            ErrorMessage = "Invalid administrator credentials. Access denied.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, Email.Trim()),
            new(ClaimTypes.Email, Email.Trim()),
            new(ClaimTypes.Role, "Administrator"),
            new("AuthenticatedAtUtc", DateTime.UtcNow.ToString("o"))
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = RememberMe,
            ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return LocalRedirect(returnUrl ?? "/");
    }
}
