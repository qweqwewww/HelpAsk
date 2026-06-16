using System.ComponentModel.DataAnnotations;

namespace HelpAsk.Models;

public class Request
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public DateTime CreationDate { get; set; } = DateTime.Now;
    public DateTime? ExecutionDate { get; set; }
    public int? ExecutedBy { get; set; }
    public int StatusId { get; set; }
    public int UrgencyId { get; set; }
    public int ServiceId { get; set; }
    public int? EquipmentId { get; set; }
    public int? RatingId { get; set; }
    public int EmployeeId { get; set; }

    public Employee? Executor { get; set; }
    public Status Status { get; set; } = null!;
    public UrgencyLevel Urgency { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Equipment? Equipment { get; set; }
    public Rating? Rating { get; set; }
    public Employee Employee { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
