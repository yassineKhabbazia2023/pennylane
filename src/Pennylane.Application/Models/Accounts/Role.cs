namespace Application.Models.Accounts
{
    public class Role
    {
        public required string Code { get; set; }
        public string? Label { get; set; }
        public string? Description { get; set; }
    }
}