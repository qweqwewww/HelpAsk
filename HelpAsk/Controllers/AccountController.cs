using HelpAsk.Data;
using HelpAsk.Utils;
using HelpAsk.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HelpAsk.Controllers;

public class AccountController : Controller
{
    private readonly AutoAskContext _context;

    public AccountController(AutoAskContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Route("/")]
    public IActionResult Index()
    {
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var isAdmin = User.Claims.FirstOrDefault(c => c.Type == "IsAdmin")?.Value == "True";
            return isAdmin ? RedirectToAction("Index", "Admin") : RedirectToAction("Index", "Employee");
        }
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users
            .Include(u => u.Employee)
            .ThenInclude(e => e.Department)
            .Include(u => u.Employee)
            .ThenInclude(e => e.Position)
            .FirstOrDefaultAsync(u => u.Login == model.Login);

        if (user == null || !PasswordHelper.VerifyPassword(model.Password, user.PasswordHash))
        {
            model.ErrorMessage = "Неверный логин или пароль";
            return View(model);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, $"{user.Employee.LastName} {user.Employee.FirstName}"),
            new Claim("IsAdmin", user.IsAdmin.ToString()),
            new Claim("EmployeeId", user.EmployeeId.ToString()),
            new Claim("DepartmentId", user.Employee.DepartmentId.ToString()),
            new Claim("DepartmentTitle", user.Employee.Department.Title)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return user.IsAdmin ? RedirectToAction("Index", "Admin") : RedirectToAction("Index", "Employee");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
