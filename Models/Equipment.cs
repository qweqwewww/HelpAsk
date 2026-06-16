namespace HelpAsk.Models;

public class Equipment
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int EquipmentTypeId { get; set; }
    public int EquipmentModelId { get; set; }
    public EquipmentType EquipmentType { get; set; } = null!;
    public EquipmentModel EquipmentModel { get; set; } = null!;
    public ICollection<Request> Requests { get; set; } = new List<Request>();
}
