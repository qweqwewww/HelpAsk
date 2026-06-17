using System.ComponentModel.DataAnnotations;

namespace HelpAsk.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Login { get; set; } = string.Empty;

    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public bool IsAdmin { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
}
