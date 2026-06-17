namespace HelpAsk.Models;

public class Employee
{
    public int Id { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? Phone { get; set; }
    public int PositionId { get; set; }
    public int DepartmentId { get; set; }
    public Position Position { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<Request> Requests { get; set; } = new List<Request>();
    public ICollection<Request> ExecutedRequests { get; set; } = new List<Request>();
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
}
