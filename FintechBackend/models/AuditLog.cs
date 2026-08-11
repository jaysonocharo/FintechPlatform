namespace FintechBackend.Models;

public class AuditLog
{
    public int Id { get; set; }
    
    // Who performed the action (null for anonymous/system events)
    public string? UserId { get; set; }
    
    // e.g., "USER_LOGIN_FAILED", "TRANSACTION_CREATED", "PASSWORD_CHANGED"
    public string Action { get; set; } = string.Empty;
    
    // Affected entity (e.g., "Transaction", "User")
    public string EntityName { get; set; } = string.Empty;
    
    // Primary key of affected entity if applicable
    public string? EntityId { get; set; }
    
    // Originating IP address and User Agent
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    
    // JSON representations for before/after state (if tracking entity edits)
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}