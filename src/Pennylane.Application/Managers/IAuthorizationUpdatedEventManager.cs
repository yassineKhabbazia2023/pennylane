using Application.Models.Contacts;
using Pennylane.Application.Models.Pagers;

namespace Pennylane.Application.Managers
{
    public interface IAuthorizationUpdatedEventManager
    {
        Task AssignUserAccessAsync(string accountNumber, Contact contact, string roleWhenCreatedContact, string? pennylaneUserId = null);
        Task<(bool hasExistingUser, string? pennylaneUserId)> CheckIfUserExistsAsync(string contactEmail, string? accountNumber = null);
        Task<string> CreatePennylaneUserAsync(Contact contact, string authenticationMethod);
        Task<(string? UserId, bool AlreadyExists)> TryCreatePennylaneUserAsync(Contact contact, string authenticationMethod);
        Task<string> GetAccountNumberByAccountIdAsync(int accountId);
        Task<Contact> GetContactByIdAsync(int? contactId);
        Task RevokeAccessUserCompanyAsync(string companyId, string userId);
    }
}