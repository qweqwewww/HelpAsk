using ClosedXML.Excel;
using HelpAsk.Data;
using HelpAsk.Models;
using HelpAsk.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HelpAsk.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly AutoAskContext _context;

    public AdminController(AutoAskContext context)
    {
        _context = context;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (User.Claims.FirstOrDefault(c => c.Type == "IsAdmin")?.Value != "True")
        {
            var controller = context.Controller as Controller;
            controller?.TempData?.Add("Error", "У вас нет прав для доступа к панели администратора");
            context.Result = RedirectToAction("Index", "Employee");
        }
        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index(string search)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var users = _context.Users
            .Include(u => u.Employee)
            .ThenInclude(e => e.Department)
            .Include(u => u.Employee)
            .ThenInclude(e => e.Position)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            users = users.Where(u =>
                u.Employee.LastName.Contains(search) ||
                u.Employee.FirstName.Contains(search) ||
                (u.Employee.MiddleName != null && u.Employee.MiddleName.Contains(search)) ||
                u.Login.Contains(search));
        }

        ViewBag.CurrentUserId = currentUserId;
        return View(await users.ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> CreateEmployee()
    {
        await PopulateSelectLists();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(string lastName, string firstName, string? middleName, string? phone, int positionId, int departmentId, string login, string password, bool isAdmin)
    {
        if (string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName))
        {
            ModelState.AddModelError("", "Фамилия и имя обязательны");
            await PopulateSelectLists();
            return View();
        }

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError("", "Логин и пароль обязательны");
            await PopulateSelectLists();
            return View();
        }

        if (await _context.Users.AnyAsync(u => u.Login == login))
        {
            ModelState.AddModelError("", "Пользователь с таким логином уже существует");
            await PopulateSelectLists();
            return View();
        }

        var employee = new Employee
        {
            LastName = lastName,
            FirstName = firstName,
            MiddleName = middleName,
            Phone = phone,
            PositionId = positionId,
            DepartmentId = departmentId
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var user = new User
        {
            Login = login,
            PasswordHash = PasswordHelper.HashPassword(password),
            IsAdmin = isAdmin,
            EmployeeId = employee.Id
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (id == currentUserId)
        {
            TempData["Error"] = "Нельзя удалить самого себя";
            return RedirectToAction(nameof(Index));
        }

        var user = await _context.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.EmployeeId == id);
        if (user != null)
        {
            _context.Users.Remove(user);
        }

        var employee = await _context.Employees.FindAsync(id);
        if (employee != null)
        {
            _context.Employees.Remove(employee);
        }

        if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Index));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(int id)
    {
        var user = await _context.Users
            .Include(u => u.Employee)
            .ThenInclude(e => e.Department)
            .Include(u => u.Employee)
            .ThenInclude(e => e.Position)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        await PopulateSelectLists();
        return View(user); 
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(int id, string lastName, string firstName, string? middleName, string? phone, int positionId, int departmentId, string login, string? password, bool isAdmin)
    {
        var user = await _context.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName))
        {
            ModelState.AddModelError("", "Фамилия и имя обязательны");
            await PopulateSelectLists();
            return View(user);
        }

        if (string.IsNullOrEmpty(login))
        {
            ModelState.AddModelError("", "Логин обязателен");
            await PopulateSelectLists();
            return View(user);
        }

        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Login == login && u.Id != id);
        if (existingUser != null)
        {
            ModelState.AddModelError("", "Пользователь с таким логином уже существует");
            await PopulateSelectLists();
            return View(user);
        }

        user.Employee.LastName = lastName;
        user.Employee.FirstName = firstName;
        user.Employee.MiddleName = middleName;
        user.Employee.Phone = phone;
        user.Employee.PositionId = positionId;
        user.Employee.DepartmentId = departmentId;
        user.Login = login;
        user.IsAdmin = isAdmin;

        if (!string.IsNullOrEmpty(password))
        {
            user.PasswordHash = PasswordHelper.HashPassword(password);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSelectLists()
    {
        ViewBag.Positions = new SelectList(await _context.Positions.ToListAsync(), "Id", "Title");
        ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Title");
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

    #region Positions
    public async Task<IActionResult> Positions(string search)
    {
        var query = _context.Positions.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePosition(string title)
    {
        _context.Positions.Add(new Position { Title = title });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Positions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPosition(int id, string title)
    {
        var position = await _context.Positions.FindAsync(id);
        if (position != null)
        {
            position.Title = title;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Positions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePosition(int id)
    {
        var position = await _context.Positions.FindAsync(id);
        if (position != null)
        {
            _context.Positions.Remove(position);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Positions));
        }
        return RedirectToAction(nameof(Positions));
    }
    #endregion

    #region Departments
    public async Task<IActionResult> Departments(string search)
    {
        var query = _context.Departments.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(d => d.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDepartment(string title, string? cabinet)
    {
        _context.Departments.Add(new Department { Title = title, Cabinet = cabinet });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Departments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDepartment(int id, string title, string? cabinet)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department != null)
        {
            department.Title = title;
            department.Cabinet = cabinet;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Departments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department != null)
        {
            _context.Departments.Remove(department);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Departments));
        }
        return RedirectToAction(nameof(Departments));
    }
    #endregion

    #region Statuses
    public async Task<IActionResult> Statuses(string search)
    {
        var query = _context.Statuses.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(s => s.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStatus(string title)
    {
        _context.Statuses.Add(new Status { Title = title });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Statuses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStatus(int id, string title)
    {
        var status = await _context.Statuses.FindAsync(id);
        if (status != null)
        {
            status.Title = title;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Statuses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStatus(int id)
    {
        var status = await _context.Statuses.FindAsync(id);
        if (status != null)
        {
            _context.Statuses.Remove(status);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Statuses));
        }
        return RedirectToAction(nameof(Statuses));
    }
    #endregion

    #region UrgencyLevels
    public async Task<IActionResult> UrgencyLevels(string search)
    {
        var query = _context.UrgencyLevels.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUrgencyLevel(string title)
    {
        _context.UrgencyLevels.Add(new UrgencyLevel { Title = title });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(UrgencyLevels));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUrgencyLevel(int id, string title)
    {
        var level = await _context.UrgencyLevels.FindAsync(id);
        if (level != null)
        {
            level.Title = title;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(UrgencyLevels));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUrgencyLevel(int id)
    {
        var level = await _context.UrgencyLevels.FindAsync(id);
        if (level != null)
        {
            _context.UrgencyLevels.Remove(level);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(UrgencyLevels));
        }
        return RedirectToAction(nameof(UrgencyLevels));
    }
    #endregion

    #region Ratings
    public async Task<IActionResult> Ratings(string search)
    {
        var query = _context.Ratings.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(r => r.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRating(string title)
    {
        _context.Ratings.Add(new Rating { Title = title });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Ratings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRating(int id, string title)
    {
        var rating = await _context.Ratings.FindAsync(id);
        if (rating != null)
        {
            rating.Title = title;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Ratings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRating(int id)
    {
        var rating = await _context.Ratings.FindAsync(id);
        if (rating != null)
        {
            _context.Ratings.Remove(rating);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Ratings));
        }
        return RedirectToAction(nameof(Ratings));
    }
    #endregion

    #region Categories
    public async Task<IActionResult> Categories(string search)
    {
        var query = _context.Categories.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string title)
    {
        _context.Categories.Add(new Category { Title = title });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, string title)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category != null)
        {
            category.Title = title;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category != null)
        {
            _context.Categories.Remove(category);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Categories));
        }
        return RedirectToAction(nameof(Categories));
    }
    #endregion

    #region Subcategories
    public async Task<IActionResult> Subcategories(string search)
    {
        ViewBag.Categories = await _context.Categories.ToListAsync();
        var query = _context.Subcategories.Include(s => s.Category).AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(s => s.Title.Contains(search) || s.Category.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubcategory(string title, int categoryId)
    {
        var subcategory = new Subcategory { Title = title, CategoryId = categoryId };
        _context.Subcategories.Add(subcategory);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Subcategories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubcategory(int id, string title, int categoryId)
    {
        var subcategory = await _context.Subcategories.FindAsync(id);
        if (subcategory != null)
        {
            subcategory.Title = title;
            subcategory.CategoryId = categoryId;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Subcategories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubcategory(int id)
    {
        var subcategory = await _context.Subcategories.FindAsync(id);
        if (subcategory != null)
        {
            _context.Subcategories.Remove(subcategory);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Subcategories));
        }
        return RedirectToAction(nameof(Subcategories));
    }
    #endregion

    #region EquipmentTypes
    public async Task<IActionResult> EquipmentTypes(string search)
    {
        var query = _context.EquipmentTypes.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(et => et.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEquipmentType(string title)
    {
        _context.EquipmentTypes.Add(new EquipmentType { Title = title });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(EquipmentTypes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEquipmentType(int id, string title)
    {
        var type = await _context.EquipmentTypes.FindAsync(id);
        if (type != null)
        {
            type.Title = title;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(EquipmentTypes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEquipmentType(int id)
    {
        var type = await _context.EquipmentTypes.FindAsync(id);
        if (type != null)
        {
            _context.EquipmentTypes.Remove(type);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(EquipmentTypes));
        }
        return RedirectToAction(nameof(EquipmentTypes));
    }
    #endregion

    #region Countries
    public async Task<IActionResult> Countries(string search)
    {
        var query = _context.Countries.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCountry(string title)
    {
        _context.Countries.Add(new Country { Title = title });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Countries));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCountry(int id, string title)
    {
        var country = await _context.Countries.FindAsync(id);
        if (country != null)
        {
            country.Title = title;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Countries));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCountry(int id)
    {
        var country = await _context.Countries.FindAsync(id);
        if (country != null)
        {
            _context.Countries.Remove(country);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Countries));
        }
        return RedirectToAction(nameof(Countries));
    }
    #endregion

    #region Manufacturers
    public async Task<IActionResult> Manufacturers(string search)
    {
        ViewBag.Countries = await _context.Countries.ToListAsync();
        var query = _context.Manufacturers.Include(m => m.Country).AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(m => m.Title.Contains(search) || m.Country.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateManufacturer(string title, int countryId)
    {
        var manufacturer = new Manufacturer { Title = title, CountryId = countryId };
        _context.Manufacturers.Add(manufacturer);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Manufacturers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditManufacturer(int id, string title, int countryId)
    {
        var manufacturer = await _context.Manufacturers.FindAsync(id);
        if (manufacturer != null)
        {
            manufacturer.Title = title;
            manufacturer.CountryId = countryId;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Manufacturers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteManufacturer(int id)
    {
        var manufacturer = await _context.Manufacturers.FindAsync(id);
        if (manufacturer != null)
        {
            _context.Manufacturers.Remove(manufacturer);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Manufacturers));
        }
        return RedirectToAction(nameof(Manufacturers));
    }
    #endregion

    #region EquipmentModels
    public async Task<IActionResult> EquipmentModels(string search)
    {
        ViewBag.Manufacturers = await _context.Manufacturers.ToListAsync();
        var query = _context.EquipmentModels.Include(em => em.Manufacturer).AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(em => em.Title.Contains(search) || em.Manufacturer.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEquipmentModel(string title, int manufacturerId)
    {
        _context.EquipmentModels.Add(new EquipmentModel { Title = title, ManufacturerId = manufacturerId });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(EquipmentModels));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEquipmentModel(int id, string title, int manufacturerId)
    {
        var model = await _context.EquipmentModels.FindAsync(id);
        if (model != null)
        {
            model.Title = title;
            model.ManufacturerId = manufacturerId;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(EquipmentModels));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEquipmentModel(int id)
    {
        var model = await _context.EquipmentModels.FindAsync(id);
        if (model != null)
        {
            _context.EquipmentModels.Remove(model);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(EquipmentModels));
        }
        return RedirectToAction(nameof(EquipmentModels));
    }
    #endregion

    #region Equipment
    public async Task<IActionResult> Equipment(string search)
    {
        ViewBag.EquipmentTypes = await _context.EquipmentTypes.ToListAsync();
        ViewBag.EquipmentModels = await _context.EquipmentModels.Include(em => em.Manufacturer).ToListAsync();
        var query = _context.Equipment
            .Include(e => e.EquipmentType)
            .Include(e => e.EquipmentModel)
            .ThenInclude(em => em.Manufacturer)
            .AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(e => e.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEquipment(string title, int equipmentTypeId, int equipmentModelId)
    {
        _context.Equipment.Add(new Equipment { Title = title, EquipmentTypeId = equipmentTypeId, EquipmentModelId = equipmentModelId });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Equipment));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEquipment(int id, string title, int equipmentTypeId, int equipmentModelId)
    {
        var equipment = await _context.Equipment.FindAsync(id);
        if (equipment != null)
        {
            equipment.Title = title;
            equipment.EquipmentTypeId = equipmentTypeId;
            equipment.EquipmentModelId = equipmentModelId;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Equipment));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEquipment(int id)
    {
        var equipment = await _context.Equipment.FindAsync(id);
        if (equipment != null)
        {
            _context.Equipment.Remove(equipment);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Equipment));
        }
        return RedirectToAction(nameof(Equipment));
    }
    #endregion

    #region Services
    public async Task<IActionResult> Services(string search)
    {
        ViewBag.Subcategories = await _context.Subcategories.Include(sc => sc.Category).ToListAsync();
        var query = _context.Services
            .Include(s => s.Subcategory)
            .ThenInclude(sc => sc.Category)
            .AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(s => s.Title.Contains(search));
        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateService(string title, int subcategoryId)
    {
        _context.Services.Add(new Service { Title = title, SubcategoryId = subcategoryId });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Services));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditService(int id, string title, int subcategoryId)
    {
        var service = await _context.Services.FindAsync(id);
        if (service != null)
        {
            service.Title = title;
            service.SubcategoryId = subcategoryId;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Services));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteService(int id)
    {
        var service = await _context.Services.FindAsync(id);
        if (service != null)
        {
            _context.Services.Remove(service);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Services));
        }
        return RedirectToAction(nameof(Services));
    }
    #endregion

    #region Requests
    public async Task<IActionResult> Requests(string search)
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

        ViewBag.Statuses = await _context.Statuses.ToListAsync();
        ViewBag.Employees = await _context.Employees.ToListAsync();
        ViewBag.Services = await _context.Services.ToListAsync();
        var adminEmpIds = await _context.Users.Where(u => u.IsAdmin).Select(u => u.EmployeeId).ToListAsync();
        var creatorIds = await _context.Requests.Select(r => r.EmployeeId).Distinct().ToListAsync();
        var executorIds = await _context.Requests.Where(r => r.ExecutedBy != null).Select(r => r.ExecutedBy!.Value).Distinct().ToListAsync();
        ViewBag.CreatorEmployees = await _context.Employees.Where(e => creatorIds.Contains(e.Id) && !adminEmpIds.Contains(e.Id)).ToListAsync();
        ViewBag.ExecutorEmployees = await _context.Employees.Where(e => executorIds.Contains(e.Id)).ToListAsync();
        return View(await query.ToListAsync());
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

    [HttpGet]
    public async Task<IActionResult> EditRequest(int id)
    {
        var request = await _context.Requests
            .Include(r => r.Employee)
            .Include(r => r.Service)
            .ThenInclude(s => s.Subcategory)
            .ThenInclude(sc => sc.Category)
            .Include(r => r.Status)
            .Include(r => r.Urgency)
            .Include(r => r.Equipment)
            .ThenInclude(e => e!.EquipmentType)
            .Include(r => r.Equipment)
            .ThenInclude(e => e!.EquipmentModel)
            .ThenInclude(em => em.Manufacturer)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();

        ViewBag.Statuses = new SelectList(await _context.Statuses.ToListAsync(), "Id", "Title", request.StatusId);
        ViewBag.Urgencies = new SelectList(await _context.UrgencyLevels.ToListAsync(), "Id", "Title", request.UrgencyId);
        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRequest(int id, string subject, string description, int statusId, int urgencyId)
    {
        var request = await _context.Requests.FindAsync(id);
        if (request == null) return NotFound();

        request.Subject = subject;
        request.Description = description;
        request.StatusId = statusId;
        request.UrgencyId = urgencyId;

        var completedStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Title == "Выполнено");
        if (completedStatus != null && statusId == completedStatus.Id && request.ExecutionDate == null)
        {
            request.ExecutionDate = DateTime.Now;
            var currentUserId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
            request.ExecutedBy = currentUserId;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Requests));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRequest(int id)
    {
        var request = await _context.Requests.FindAsync(id);
        if (request != null)
        {
            _context.Requests.Remove(request);
            if (!await TrySaveChangesAsync()) return RedirectToAction(nameof(Requests));
        }
        return RedirectToAction(nameof(Requests));
    }
    #endregion
}
