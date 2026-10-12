using System;
using System.ComponentModel;
using Application.Models.Accounts;
using Pennylane.Application.Consts.Enums;
using Pennylane.Application.Extensions;

namespace Pennylane.Application.Consts
{
    public static class PennylaneRoles
    {
        public static readonly Role EmployeeRole = new Role { Code = "employee", Label = "Employé", Description = "Ce rôle permet de gérer et déclarer ses notes de frais, soumettre ses demandes d'achats et transmettre des factures fournisseurs." };
        public static readonly Role ExecutiveRole = new Role { Code = "executive", Label = "Dirigeant / Admin", Description = "Le dirigeant / admin a accès à toutes les fonctionnalités prévues par son plan tarifaire." };
        public static readonly Role InternalAccountant = new Role { Code = "internal_accountant", Label = "Comptable interne / Responsable finance", Description = "Ce rôle a accès à toutes les fonctionnalités prévues par son plan tarifaire et peut également gérer la comptabilité de l'entreprise. Idéal pour un DAF par exemple." };
        public static readonly Role InvoicingManager = new Role { Code = "invoicing_manager", Label = "Gestionnaire de facturation", Description = "Ce rôle permet de gérer la facturation client (éditer des devis et des factures) et fournisseur (importer, classer et payer les factures)." };
        public static readonly Role PurchasingManager = new Role { Code = "purchasing_manager", Label = "Gestionnaire de facturation fournisseur", Description = "Ce rôle permet d'importer, classer et payer les factures fournisseurs." };
        public static readonly Role SalesManager = new Role { Code = "sales_manager", Label = "Gestionnaire de facturation client", Description = "Ce rôle permet d'éditer des devis, des factures, et gérer le cycle de facturation client." };

        private static readonly HashSet<string> _validRoleCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            EmployeeRole.Code,
            ExecutiveRole.Code,
            InternalAccountant.Code,
            InvoicingManager.Code,
            PurchasingManager.Code,
            SalesManager.Code
        };

        public static IReadOnlyCollection<string> ValidRoleCodes => _validRoleCodes;

        public static bool IsValidRoleCode(string? roleCode)
        {
            return !string.IsNullOrWhiteSpace(roleCode) && _validRoleCodes.Contains(roleCode);
        }

        public static string GetValidRoleCodesAsString() => string.Join(", ", _validRoleCodes);

        private static readonly Dictionary<string, List<Role>> _roleSaasDictionary = new Dictionary<string, List<Role>>()
        {
            {SaasNames.v1_pro_internalized.GetDescription(), new List<Role>{PennylaneRoles.ExecutiveRole,PennylaneRoles.InternalAccountant,PennylaneRoles.InvoicingManager,PennylaneRoles.SalesManager,PennylaneRoles.PurchasingManager,PennylaneRoles.EmployeeRole}},
            {SaasNames.v1_basic.GetDescription(), new List<Role>{PennylaneRoles.ExecutiveRole,PennylaneRoles.InvoicingManager,PennylaneRoles.SalesManager,PennylaneRoles.PurchasingManager,PennylaneRoles.EmployeeRole}},
            {SaasNames.v1_saas_free.GetDescription(), new List<Role>{PennylaneRoles.ExecutiveRole, PennylaneRoles.InvoicingManager, PennylaneRoles.SalesManager,PennylaneRoles.PurchasingManager}},
            {SaasNames.v1_essential.GetDescription(), new List<Role>{PennylaneRoles.ExecutiveRole, PennylaneRoles.InvoicingManager, PennylaneRoles.SalesManager, PennylaneRoles.PurchasingManager,PennylaneRoles.EmployeeRole}},
            {SaasNames.v1_pro.GetDescription(), new List<Role>{PennylaneRoles.ExecutiveRole, PennylaneRoles.InvoicingManager,PennylaneRoles.SalesManager,PennylaneRoles.PurchasingManager,PennylaneRoles.EmployeeRole} },
            {SaasNames.v1_electronic_invoicing.GetDescription(), new List<Role>{PennylaneRoles.ExecutiveRole, PennylaneRoles.InvoicingManager, PennylaneRoles.SalesManager, PennylaneRoles.PurchasingManager, PennylaneRoles.EmployeeRole} }
        };

        public static Dictionary<string, List<Role>> RoleSaasDictionary { get { return _roleSaasDictionary; } }

    }


}
