using ClosedXML.Excel;
using HelpAsk.Data;
using HelpAsk.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HelpAsk.Controllers;

[Authorize]
public class EmployeeController : Controller
{
    private readonly AutoAskContext _context;

    public EmployeeController(AutoAskContext context)
    {
        _context = context;
    }

    private async Task<bool> TrySaveChangesAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Невозможно удалить: запись используется в других данных";
            return false;
        }
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (User.Claims.FirstOrDefault(c => c.Type == "IsAdmin")?.Value == "True")
        {
            var controller = context.Controller as Controller;
            controller?.TempData?.Add("Error", "Администратор не может использовать панель сотрудника");
            context.Result = RedirectToAction("Index", "Admin");
        }
        base.OnActionExecuting(context);
    }

    private bool IsUserInItDepartment()
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var employee = _context.Employees.Include(e => e.Department).FirstOrDefault(e => e.Id == employeeId);
        return employee?.Department?.Title == "Отдел информационных технологий";
    }

    public async Task<IActionResult> Index()
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var employee = await _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        ViewBag.Employee = employee;
        ViewBag.IsItDepartment = IsUserInItDepartment();
        return View();
    }

    public async Task<IActionResult> MyRequests()
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var requests = await _context.Requests
            .Include(r => r.Service)
            .ThenInclude(s => s.Subcategory)
            .ThenInclude(sc => sc.Category)
            .Include(r => r.Status)
            .Include(r => r.Urgency)
            .Include(r => r.Rating)
            .Include(r => r.Employee)
            .Include(r => r.Executor)
            .Include(r => r.Equipment)
            .ThenInclude(e => e!.EquipmentType)
            .Include(r => r.Equipment)
            .ThenInclude(e => e!.EquipmentModel)
            .ThenInclude(em => em.Manufacturer)
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.CreationDate)
            .ToListAsync();

        ViewBag.Statuses = await _context.Statuses.ToListAsync();
        ViewBag.Ratings = new SelectList(await _context.Ratings.ToListAsync(), "Id", "Title");
        ViewBag.IsItDepartment = IsUserInItDepartment();
        ViewBag.PendingStatusId = (await _context.Statuses.FirstOrDefaultAsync(s => s.Title == "В ожидании"))?.Id;
        ViewBag.CompletedStatusId = (await _context.Statuses.FirstOrDefaultAsync(s => s.Title == "Выполнено"))?.Id;
        return View(requests);
    }

    public async Task<IActionResult> AllRequests(string search)
    {
        var query = _context.Requests
            .Include(r => r.Employee)
            .ThenInclude(e => e.Department)
            .Include(r => r.Service)
            .ThenInclude(s => s.Subcategory)
            .ThenInclude(sc => sc.Category)
            .Include(r => r.Status)
            .Include(r => r.Urgency)
            .Include(r => r.Rating)
            .Include(r => r.Executor)
            .Include(r => r.Equipment)
            .ThenInclude(e => e!.EquipmentType)
            .Include(r => r.Equipment)
            .ThenInclude(e => e!.EquipmentModel)
            .ThenInclude(em => em.Manufacturer)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(r =>
                r.Employee.LastName.Contains(search) ||
                r.Employee.FirstName.Contains(search) ||
                r.Subject.Contains(search) ||
                r.Service.Title.Contains(search));
        }

        ViewBag.CompletedStatusId = (await _context.Statuses.FirstOrDefaultAsync(s => s.Title == "Выполнено"))?.Id;
        ViewBag.PendingStatusId = (await _context.Statuses.FirstOrDefaultAsync(s => s.Title == "В ожидании"))?.Id;
        ViewBag.Employees = await _context.Employees.ToListAsync();
        ViewBag.Services = await _context.Services.ToListAsync();
        var adminEmpIds = await _context.Users.Where(u => u.IsAdmin).Select(u => u.EmployeeId).ToListAsync();
        var creatorIds = await _context.Requests.Select(r => r.EmployeeId).Distinct().ToListAsync();
        var executorIds = await _context.Requests.Where(r => r.ExecutedBy != null).Select(r => r.ExecutedBy!.Value).Distinct().ToListAsync();
        ViewBag.CreatorEmployees = await _context.Employees.Where(e => creatorIds.Contains(e.Id) && !adminEmpIds.Contains(e.Id)).ToListAsync();
        ViewBag.ExecutorEmployees = await _context.Employees.Where(e => executorIds.Contains(e.Id)).ToListAsync();
        return View(await query.OrderByDescending(r => r.CreationDate).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> GenerateReport(string filter, string? subject, int? creatorId, int? executorId, int? serviceId, DateTime? date, DateTime? execDate, DateTime? dateFrom, DateTime? dateTo, DateTime? execDateFrom, DateTime? execDateTo)
    {
        var query = _context.Requests
            .Include(r => r.Employee).ThenInclude(e => e.Department)
            .Include(r => r.Service).ThenInclude(s => s.Subcategory).ThenInclude(sc => sc.Category)
            .Include(r => r.Status)
            .Include(r => r.Urgency)
            .Include(r => r.Rating)
            .Include(r => r.Executor)
            .Include(r => r.Equipment).ThenInclude(e => e!.EquipmentType)
            .Include(r => r.Equipment).ThenInclude(e => e!.EquipmentModel).ThenInclude(em => em.Manufacturer)
            .AsQueryable();

        switch (filter)
        {
            case "bySubject" when !string.IsNullOrEmpty(subject):
                query = query.Where(r => r.Subject.Contains(subject));
                break;
            case "byCreator" when creatorId.HasValue:
                query = query.Where(r => r.EmployeeId == creatorId.Value);
                break;
            case "byExecutor" when executorId.HasValue:
                query = query.Where(r => r.ExecutedBy == executorId.Value);
                break;
            case "byService" when serviceId.HasValue:
                query = query.Where(r => r.ServiceId == serviceId.Value);
                break;
            case "byDate" when date.HasValue:
                var dayStart = date.Value.Date;
                var dayEnd = dayStart.AddDays(1);
                query = query.Where(r => r.CreationDate >= dayStart && r.CreationDate < dayEnd);
                break;
            case "byExecDate" when execDate.HasValue:
                var execDayStart = execDate.Value.Date;
                var execDayEnd = execDayStart.AddDays(1);
                query = query.Where(r => r.ExecutionDate >= execDayStart && r.ExecutionDate < execDayEnd);
                break;
            case "byDateRange" when dateFrom.HasValue && dateTo.HasValue:
                var rangeFrom = dateFrom.Value.Date;
                var rangeTo = dateTo.Value.Date.AddDays(1);
                query = query.Where(r => r.CreationDate >= rangeFrom && r.CreationDate < rangeTo);
                break;
            case "byExecDateRange" when execDateFrom.HasValue && execDateTo.HasValue:
                var execRangeFrom = execDateFrom.Value.Date;
                var execRangeTo = execDateTo.Value.Date.AddDays(1);
                query = query.Where(r => r.ExecutionDate >= execRangeFrom && r.ExecutionDate < execRangeTo);
                break;
            case "completed":
                query = query.Where(r => r.Status.Title == "Выполнено");
                break;
            case "pending":
                query = query.Where(r => r.Status.Title == "В ожидании");
                break;
        }

        var requests = await query.OrderByDescending(r => r.CreationDate).ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Обращения");

        ws.Cell(1, 1).Value = "ID";
        ws.Cell(1, 2).Value = "Тема";
        ws.Cell(1, 3).Value = "Описание";
        ws.Cell(1, 4).Value = "Создатель";
        ws.Cell(1, 5).Value = "Отдел";
        ws.Cell(1, 6).Value = "Кабинет";
        ws.Cell(1, 7).Value = "Услуга";
        ws.Cell(1, 8).Value = "Категория";
        ws.Cell(1, 9).Value = "Статус";
        ws.Cell(1, 10).Value = "Срочность";
        ws.Cell(1, 11).Value = "Исполнитель";
        ws.Cell(1, 12).Value = "Дата создания";
        ws.Cell(1, 13).Value = "Дата выполнения";
        ws.Cell(1, 14).Value = "Оценка";

        var headerRange = ws.Range(1, 1, 1, 14);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        int row = 2;
        foreach (var r in requests)
        {
            ws.Cell(row, 1).Value = r.Id;
            ws.Cell(row, 2).Value = r.Subject;
            ws.Cell(row, 3).Value = r.Description;
            ws.Cell(row, 4).Value = $"{r.Employee.LastName} {r.Employee.FirstName}";
            ws.Cell(row, 5).Value = r.Employee.Department?.Title ?? "";
            ws.Cell(row, 6).Value = r.Employee.Department?.Cabinet ?? "";
            ws.Cell(row, 7).Value = r.Service.Title;
            ws.Cell(row, 8).Value = r.Service.Subcategory?.Category?.Title ?? "";
            ws.Cell(row, 9).Value = r.Status.Title;
            ws.Cell(row, 10).Value = r.Urgency.Title;
            ws.Cell(row, 11).Value = r.Executor != null ? $"{r.Executor.LastName} {r.Executor.FirstName}" : "";
            ws.Cell(row, 12).Value = r.CreationDate.ToString("dd.MM.yyyy HH:mm");
            ws.Cell(row, 13).Value = r.ExecutionDate?.ToString("dd.MM.yyyy HH:mm") ?? "";
            ws.Cell(row, 14).Value = r.Rating?.Title ?? "";

            var dataRange = ws.Range(row, 1, row, 14);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);

        string filename = $"Отчёт_обращения_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteRequest(int id)
    {
        var request = await _context.Requests.FindAsync(id);
        if (request == null) return NotFound();

        var completedStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Title == "Выполнено");
        if (completedStatus == null) return NotFound();

        request.StatusId = completedStatus.Id;
        request.ExecutionDate = DateTime.Now;
        request.ExecutedBy = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

        await _context.SaveChangesAsync();

        var referer = Request.Headers["Referer"].ToString();
        return Redirect(referer);
    }

    public async Task<IActionResult> CreateRequest()
    {
        ViewBag.Categories = await _context.Categories.ToListAsync();
        ViewBag.Urgencies = await _context.UrgencyLevels.ToListAsync();
        var equipment = await _context.Equipment
            .Include(e => e.EquipmentType)
            .Include(e => e.EquipmentModel)
            .ThenInclude(em => em.Manufacturer)
            .ToListAsync();
        ViewBag.Equipment = equipment.Select(e => new SelectListItem
        {
            Value = e.Id.ToString(),
            Text = $"{e.EquipmentType.Title} {e.EquipmentModel.Title} ({e.EquipmentModel.Manufacturer.Title})"
        }).ToList();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetSubcategories(int categoryId)
    {
        var subcategories = await _context.Subcategories
            .Where(s => s.CategoryId == categoryId)
            .Select(s => new { s.Id, s.Title })
            .ToListAsync();
        return Json(subcategories);
    }

    [HttpGet]
    public async Task<IActionResult> GetServices(int subcategoryId)
    {
        var services = await _context.Services
            .Where(s => s.SubcategoryId == subcategoryId)
            .Select(s => new { s.Id, s.Title })
            .ToListAsync();
        return Json(services);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRequest(string subject, string description, int serviceId, int urgencyId, int? equipmentId)
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var pendingStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Title == "В ожидании");

        var request = new Request
        {
            Subject = subject,
            Description = description,
            ServiceId = serviceId,
            UrgencyId = urgencyId,
            EmployeeId = employeeId,
            StatusId = pendingStatus?.Id ?? 1,
            CreationDate = DateTime.Now,
            EquipmentId = equipmentId
        };

        _context.Requests.Add(request);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(MyRequests));
    }

    [HttpGet]
    public async Task<IActionResult> EditRequest(int id)
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var request = await _context.Requests
            .Include(r => r.Service)
            .ThenInclude(s => s.Subcategory)
            .Include(r => r.Status)
            .FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == employeeId);

        if (request == null) return NotFound();
        if (request.Status.Title != "В ожидании")
        {
            TempData["Error"] = "Нельзя редактировать принятое обращение";
            return RedirectToAction(nameof(MyRequests));
        }

        ViewBag.Categories = await _context.Categories.ToListAsync();
        ViewBag.Urgencies = await _context.UrgencyLevels.ToListAsync();
        ViewBag.SelectedCategoryId = request.Service.Subcategory?.CategoryId;
        ViewBag.SelectedSubcategoryId = request.Service.SubcategoryId;
        ViewBag.SelectedEquipmentId = request.EquipmentId;

        var equipment = await _context.Equipment
            .Include(e => e.EquipmentType)
            .Include(e => e.EquipmentModel)
            .ThenInclude(em => em.Manufacturer)
            .ToListAsync();
        ViewBag.Equipment = equipment.Select(e => new SelectListItem
        {
            Value = e.Id.ToString(),
            Text = $"{e.EquipmentType.Title} {e.EquipmentModel.Title} ({e.EquipmentModel.Manufacturer.Title})"
        }).ToList();

        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRequest(int id, string subject, string description, int serviceId, int urgencyId, int? equipmentId)
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var request = await _context.Requests
            .Include(r => r.Status)
            .FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == employeeId);

        if (request == null) return NotFound();
        if (request.Status.Title != "В ожидании")
        {
            TempData["Error"] = "Нельзя редактировать принятое обращение";
            return RedirectToAction(nameof(MyRequests));
        }

        request.Subject = subject;
        request.Description = description;
        request.ServiceId = serviceId;
        request.UrgencyId = urgencyId;
        request.EquipmentId = equipmentId;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(MyRequests));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRequest(int id)
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var request = await _context.Requests
            .Include(r => r.Status)
            .FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == employeeId);

        if (request != null && request.Status.Title == "В ожидании")
        {
            _context.Requests.Remove(request);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(MyRequests));
        }

        return RedirectToAction(nameof(MyRequests));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRating(int requestId, int ratingId)
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var request = await _context.Requests
            .Include(r => r.Status)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.EmployeeId == employeeId);

        if (request != null && request.Status.Title == "Выполнено")
        {
            request.RatingId = ratingId;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(MyRequests));
    }

    [HttpGet]
    public async Task<IActionResult> Chat(int id)
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var isIt = IsUserInItDepartment();

        var request = await _context.Requests
            .Include(r => r.Employee)
            .ThenInclude(e => e.Department)
            .Include(r => r.Status)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null) return NotFound();
        if (request.EmployeeId != employeeId && !isIt)
            return Forbid();

        var messages = await _context.Messages
            .Include(m => m.Sender)
            .Where(m => m.RequestId == id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        ViewBag.Request = request;
        ViewBag.IsItDepartment = isIt;
        return View(messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int requestId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return RedirectToAction(nameof(Chat), new { id = requestId });
        }

        var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
        var isIt = IsUserInItDepartment();

        var request = await _context.Requests
            .Include(r => r.Status)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        if (request == null) return NotFound();
        if (request.EmployeeId != employeeId && !isIt)
            return Forbid();
        if (request.Status.Title != "В ожидании")
        {
            TempData["Error"] = "Нельзя отправлять сообщения по закрытому обращению";
            return RedirectToAction(nameof(Chat), new { id = requestId });
        }

        var message = new Message
        {
            RequestId = requestId,
            SenderId = employeeId,
            Text = text,
            CreatedAt = DateTime.Now
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Chat), new { id = requestId });
    }
}
