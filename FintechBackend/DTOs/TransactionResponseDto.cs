namespace FintechBackend.DTOs;

public class TransactionResponseDto
{
    public int Id { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid UserId { get; set; }
}