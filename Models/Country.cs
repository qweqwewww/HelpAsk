namespace HelpAsk.Models;

public class Country
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<Manufacturer> Manufacturers { get; set; } = new List<Manufacturer>();
}
