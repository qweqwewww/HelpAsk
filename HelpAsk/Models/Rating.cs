namespace HelpAsk.Models;

public class Rating
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<Request> Requests { get; set; } = new List<Request>();
}
