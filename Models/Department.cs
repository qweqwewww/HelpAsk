namespace HelpAsk.Models;

public class Department
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Cabinet { get; set; }
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
