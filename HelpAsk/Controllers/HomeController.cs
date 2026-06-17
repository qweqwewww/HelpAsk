using HelpAsk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpAsk.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AutoAskContext _context;

    public HomeController(AutoAskContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var employee = await _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        ViewBag.Employee = employee;
        return View();
    }
}
