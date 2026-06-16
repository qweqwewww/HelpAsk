namespace HelpAsk.Models;

public class Subcategory
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public ICollection<Service> Services { get; set; } = new List<Service>();
}
