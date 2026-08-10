namespace FintechBackend.Models;

public class Transaction
{
    public int Id {get; set; } 
    // Inside Transaction.cs
    public string IdempotencyKey { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public long Amount { get; set;}
    public string TransactionType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set;} = DateTime.UtcNow;

    // Foreign Key to User
    public Guid UserId { get; set; }
    public User? User { get; set; } 
}

/*
get: Lets other parts of your code read the value stored in the property.
set: Lets other parts of your code change or assign a new value to the property.
*/