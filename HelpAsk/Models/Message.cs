namespace HelpAsk.Models;

public class Message
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int SenderId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Request Request { get; set; } = null!;
    public Employee Sender { get; set; } = null!;
}