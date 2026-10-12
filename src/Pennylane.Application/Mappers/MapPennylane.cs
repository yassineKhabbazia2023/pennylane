using Application.Models.Contacts;
using Pennylane.Application.Models;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;

namespace Pennylane.Application.Mappers;

public static class MapPennylane
{
    public static PennylaneContact ToPennylaneContact(this Contact contact, string authenticationMethod)
    {
        return new PennylaneContact
        {
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            Email = contact.Email,
            SkipWelcomeEmail = true,
            AuthenticationMethod = authenticationMethod
        };
    }

   public static PennylaneContact PennylaneUserToPennylaneContact(this PennylaneUsers user)
    {
        return new PennylaneContact
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
        };
    }

    public static PennylaneCompany ToPennylaneCompany(this CompanyEntity company)
    {
        return new PennylaneCompany
        {
            Id = company.Id,
            FirmId = company.FirmId,
            RegNo = company.RegNo!,
            Name = company.Name,
            SaasPlan = company.SaasPlan!,
            SupplierInvoiceTransmissionEmails = [company.SupplierInvoiceTransmissionEmail!],
            CustomerInvoiceTransmissionEmails = [company.CustomerInvoiceTransmissionEmail!],
        };
    }

    public static CompanyEntity ToCompanyEntity(this PennylaneCompany company)
    {
        return new CompanyEntity
        {
            Id = company.Id,
            FirmId = company.FirmId,
            RegNo = company.RegNo,
            Name = company.Name,
            SaasPlan = company.SaasPlan,
            SupplierInvoiceTransmissionEmail = company.SupplierInvoiceTransmissionEmails?.FirstOrDefault(),
            CustomerInvoiceTransmissionEmail = company.CustomerInvoiceTransmissionEmails?.FirstOrDefault(),
            LastSyncDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}