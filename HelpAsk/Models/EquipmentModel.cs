namespace HelpAsk.Models;

public class EquipmentModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ManufacturerId { get; set; }
    public Manufacturer Manufacturer { get; set; } = null!;
    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
