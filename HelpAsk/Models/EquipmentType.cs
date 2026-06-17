namespace HelpAsk.Models;

public class EquipmentType
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
