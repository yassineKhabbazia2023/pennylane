namespace Application.Models.Accounts
{
    public class Account
    {
        public Guid? AccountGlobalUniqueId { get; set; }
        public required int AccountId { get; set; }
        public required string AccountNumber { get; set; }
    }
}
