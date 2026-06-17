using HelpAsk.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpAsk.Data;

public class AutoAskContext : DbContext
{
    public AutoAskContext(DbContextOptions<AutoAskContext> options) : base(options) { }

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Status> Statuses => Set<Status>();
    public DbSet<UrgencyLevel> UrgencyLevels => Set<UrgencyLevel>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<EquipmentModel> EquipmentModels => Set<EquipmentModel>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Subcategory>()
            .HasOne(s => s.Category)
            .WithMany(c => c.Subcategories)
            .HasForeignKey(s => s.CategoryId);

        modelBuilder.Entity<Manufacturer>()
            .HasOne(m => m.Country)
            .WithMany(c => c.Manufacturers)
            .HasForeignKey(m => m.CountryId);

        modelBuilder.Entity<EquipmentModel>()
            .HasOne(em => em.Manufacturer)
            .WithMany(m => m.EquipmentModels)
            .HasForeignKey(em => em.ManufacturerId);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Position)
            .WithMany(p => p.Employees)
            .HasForeignKey(e => e.PositionId);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Employee)
            .WithOne(e => e.User)
            .HasForeignKey<User>(u => u.EmployeeId);

        modelBuilder.Entity<Equipment>()
            .HasOne(eq => eq.EquipmentType)
            .WithMany(et => et.Equipment)
            .HasForeignKey(eq => eq.EquipmentTypeId);

        modelBuilder.Entity<Equipment>()
            .HasOne(eq => eq.EquipmentModel)
            .WithMany(em => em.Equipment)
            .HasForeignKey(eq => eq.EquipmentModelId);

        modelBuilder.Entity<Service>()
            .HasOne(s => s.Subcategory)
            .WithMany(sc => sc.Services)
            .HasForeignKey(s => s.SubcategoryId);

        modelBuilder.Entity<Request>()
            .HasOne(r => r.Employee)
            .WithMany(e => e.Requests)
            .HasForeignKey(r => r.EmployeeId);

        modelBuilder.Entity<Request>()
            .HasOne(r => r.Executor)
            .WithMany(e => e.ExecutedRequests)
            .HasForeignKey(r => r.ExecutedBy);

        modelBuilder.Entity<Request>()
            .HasOne(r => r.Status)
            .WithMany(s => s.Requests)
            .HasForeignKey(r => r.StatusId);

        modelBuilder.Entity<Request>()
            .HasOne(r => r.Urgency)
            .WithMany(u => u.Requests)
            .HasForeignKey(r => r.UrgencyId);

        modelBuilder.Entity<Request>()
            .HasOne(r => r.Service)
            .WithMany(s => s.Requests)
            .HasForeignKey(r => r.ServiceId);

        modelBuilder.Entity<Request>()
            .HasOne(r => r.Equipment)
            .WithMany(e => e.Requests)
            .HasForeignKey(r => r.EquipmentId);

        modelBuilder.Entity<Request>()
            .HasOne(r => r.Rating)
            .WithMany(rt => rt.Requests)
            .HasForeignKey(r => r.RatingId);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Request)
            .WithMany(r => r.Messages)
            .HasForeignKey(m => m.RequestId);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany(e => e.SentMessages)
            .HasForeignKey(m => m.SenderId);
    }
}
