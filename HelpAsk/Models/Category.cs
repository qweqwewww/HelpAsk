namespace HelpAsk.Models;

public class Category
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<Subcategory> Subcategories { get; set; } = new List<Subcategory>();
}
