using System.ComponentModel;

namespace Pennylane.Application.StaticValues.Enum;

public enum PennylaneRoles
{
    None = 0,

    [Description("accountant")]
    Accountant = 1,

    [Description("executive")]
    Executive = 2,

    [Description("employee")]
    Employee = 3,

    [Description("invoicing_manager")]
    InvoicingManager = 4,

    [Description("sales_manager")]
    SalesManager = 5,

    [Description("purchasing_manager")]
    PurchasingManager = 6,

    [Description("internal_accountant")]
    InternalAccountant = 7
}