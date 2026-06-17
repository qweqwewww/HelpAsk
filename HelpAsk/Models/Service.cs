namespace HelpAsk.Models;

public class Service
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SubcategoryId { get; set; }
    public Subcategory Subcategory { get; set; } = null!;
    public ICollection<Request> Requests { get; set; } = new List<Request>();
}
