// <copyright file="Errors.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Consts
{
    public static class Errors
    {
        public static readonly string AccountNotFoundCode = "PNL001";
        public static readonly string AccountNotFoundMessage = "Account with ID {0} was not found.";
        public static readonly string AccountNumberNotFoundMessage = "Account with number {0} was not found.";

        public static readonly string ContactNotFoundCode = "PNL002";
        public static readonly string ContactNotFoundMessage = "Contact with ID {0} was not found.";

        public static readonly string RoleNotFoundCode = "PNL003";
        public static readonly string RoleNotFoundMessage = "Role not found for the Contact with ID {0} on the account with ID {1}.";

        public static readonly string PennylaneApiErrorCode = "PNL004";
        public static readonly string PennylaneApiErrorMessage = "An error occurred while communicating with the Pennylane API: {0}";

        public static readonly string SignatoryAccessNotFoundCode = "PNL005";
        public static readonly string SignatoryAccessNotFoundMessage = "SignatoryAccess with accountNumber {0} was not found.";

        public static readonly string TermsNotFoundCode = "PNL006";
        public static readonly string TermsNotFoundMessage = "No Terms with name '{0}' is configured.";

        public static readonly string UserNotSignatoryCode = "PNL007";
        public static readonly string UserNotSignatoryMessage = "Only signatory can validate Terms.";

        public static readonly string CGUFileNotFoundCode = "PNL008";
        public static readonly string CGUFileNotFoundMessage = "CGU file named '{0}' doesn't exist.";

        public static readonly string NullFileNameCode = "PNL009";
        public static readonly string NullFileNameMessage = "Parameter {0} must be informed.";

        public static readonly string NotFoundFileNameCode = "PNL010";
        public static readonly string NotFoundFileNameMessage = "File named '{0}' doesn't exist.";

        public static readonly string InvalidTermsCode = "PNL011";
        public static readonly string InvalidTermsMessage = "No Terms correspond to the date {0:dd/MM/yyyy}";

        public static readonly string SignatoryAccessNotFoundWithEmailCode = "PNL012";
        public static readonly string SignatoryAccessNotFoundWithEmailMessage = "No signatoryAccess found for email '{0}'.";

        public static readonly string InconsistentWavesCode = "PNL013";
        public static readonly string InconsistentWavesMessage = "Vagues incohérentes détectées: {0}. Tous les dossiers d'un client doivent avoir la même classification de vague.";
    }
}
