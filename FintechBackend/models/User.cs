namespace FintechBackend.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid(); // Using GUID instead of int for security
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "User"; // e.g., "User", "Admin"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property: One user has many transactions
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}