// <copyright file="ContactFunctionsHelper.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Consts;
using Pennylane.WebApi.Models;

namespace Pennylane.Application.Helpers
{
    /// <summary>
    /// Helper class for processing contact functions.
    /// </summary>
    public static class ContactFunctionsHelper
    {
        /// <summary>
        /// Extracts accounting contacts from a list of contact functions.
        /// </summary>
        /// <param name="contactFunctions">The list of contact functions.</param>
        /// <returns>An AccountingContacts object containing the IDs of accounting-related contacts.</returns>
        public static AccountingContacts GetAccountingContacts(IReadOnlyList<ContactFunctions> contactFunctions)
        {
            if (contactFunctions == null || contactFunctions.Count == 0)
            {
                return new AccountingContacts();
            }

            int accountingManagerId = 0;
            int accountingSupervisorId = 0;
            int accountantId = 0;

            // Single pass through contactFunctions
            foreach (var cf in contactFunctions)
            {
                if (cf.FunctionId == PennylaneFunctions.AccountingManagerFunctionId && accountingManagerId == 0)
                {
                    accountingManagerId = cf.ContactId;
                }
                else if (cf.FunctionId == PennylaneFunctions.AccountingSupervisorFunctionId && accountingSupervisorId == 0)
                {
                    accountingSupervisorId = cf.ContactId;
                }
                else if (cf.FunctionId == PennylaneFunctions.AccountantFunctionId && accountantId == 0)
                {
                    accountantId = cf.ContactId;
                }
            }

            return new AccountingContacts
            {
                AccountingManagerId = accountingManagerId,
                AccountingSupervisorId = accountingSupervisorId,
                AccountantId = accountantId
            };
        }
    }

    /// <summary>
    /// Represents the IDs of accounting-related contacts.
    /// </summary>
    public class AccountingContacts
    {
        /// <summary>
        /// Gets or sets the accounting manager contact ID.
        /// </summary>
        public int AccountingManagerId { get; set; }

        /// <summary>
        /// Gets or sets the accounting supervisor contact ID.
        /// </summary>
        public int AccountingSupervisorId { get; set; }

        /// <summary>
        /// Gets or sets the accountant contact ID.
        /// </summary>
        public int AccountantId { get; set; }
    }
}
