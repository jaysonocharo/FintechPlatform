namespace FintechBackend.DTOs 
{
    public class UpdateTransactionDto
    {
        public string AccountHolder { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
    }
}