namespace HelpAsk.Models;

public class Position
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
