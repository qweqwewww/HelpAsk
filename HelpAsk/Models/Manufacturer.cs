namespace HelpAsk.Models;

public class Manufacturer
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CountryId { get; set; }
    public Country Country { get; set; } = null!;
    public ICollection<EquipmentModel> EquipmentModels { get; set; } = new List<EquipmentModel>();
}
