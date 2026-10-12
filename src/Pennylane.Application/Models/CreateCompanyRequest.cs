using System.ComponentModel.DataAnnotations;

namespace Pennylane.WebApi.Models
{
    /// <summary>
    /// Request model for creating a company in Pennylane.
    /// </summary>
    public class CreateCompanyRequest : IValidatableObject
    {
        /// <summary>
        /// Gets or sets the account ID.
        /// </summary>
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "AccountId must be a positive integer.")]
        public int AccountId { get; set; }

        /// <summary>
        /// Gets or sets the list of contact IDs to add to the company.
        /// Each contact will be retrieved from the database, created as a user in Pennylane if needed,
        /// and granted access to the company with a role based on their type (Collaborator or Customer).
        /// </summary>
        [Required]
        [MaxLength(100, ErrorMessage = "Cannot add more than 100 contacts at once.")]
        public IReadOnlyList<int> Contacts { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of contact functions (collaborators with specific roles).
        /// These contacts will be created in Pennylane and their IDs will be used as foreign keys
        /// for accounting manager, accounting supervisor, and accountant fields.
        /// </summary>
        public IReadOnlyList<ContactFunctions>? ContactFunctions { get; set; }

        /// <summary>
        /// Gets or sets the hub name to resolve the firm ID in Pennylane.
        /// </summary>
        public string? HubName { get; set; }

        /// <summary>
        /// Company registration number (SIREN).
        /// Maps to: <c>reg_no</c>.
        /// </summary>
        public  string RegistrationNumber { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the company is not yet registered.
        /// Maps to: <c>not_yet_registered</c>.
        /// Typically <c>false</c>.
        /// </summary>
        public  bool NotYetRegistered { get; set; }

        /// <summary>
        /// Indicates the accounting type of the company.
        /// Maps to: <c>accounting_type</c>.
        /// </summary>
        public string? AccountingType { get; set; }

        /// <summary>
        /// Indicates the country code.
        /// Maps to: <c>country_alpha2</c>.
        /// </summary>
        public string? CountryCode { get; set; }

        /// <summary>
        /// Gets or sets the requested plan code from the Offer system.
        /// Used to compare with the actual SaaS plan assigned by Pennylane.
        /// </summary>
        public string? RequestedPlanCode { get; set; }

        /// <summary>
        /// Gets or sets the Pennylane user ID of the accounting manager.
        /// This will be populated after creating collaborators from ContactFunctions.
        /// Maps to: <c>accounting_manager_id</c>.
        /// </summary>
        public string? PennylaneAccountingManagerId { get; set; }

        /// <summary>
        /// Gets or sets the Pennylane user ID of the accounting supervisor.
        /// This will be populated after creating collaborators from ContactFunctions.
        /// Maps to: <c>accounting_supervisor_id</c>.
        /// </summary>
        public string? PennylaneAccountingSupervisorId { get; set; }

        /// <summary>
        /// Gets or sets the Pennylane user ID of the accountant.
        /// This will be populated after creating collaborators from ContactFunctions.
        /// Maps to: <c>accountant_id</c>.
        /// </summary>
        public string? PennylaneAccountantId { get; set; }

        /// <summary>
        /// The number of user you want to define in the current selected plan.
        /// </summary>
        public string? NumberOfUsers { get; set; }

        /// <summary>
        /// Validates the CreateCompanyRequest object.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Validate each contact ID is positive
            if (Contacts != null)
            {
                for (int i = 0; i < Contacts.Count; i++)
                {
                    if (Contacts[i] < 1 || Contacts[i] > int.MaxValue)
                    {
                        yield return new ValidationResult(
                            $"Contact ID at index {i} must be a positive integer.",
                            new[] { $"Contacts[{i}]" });
                    }
                }
            }
        }
    }
}
