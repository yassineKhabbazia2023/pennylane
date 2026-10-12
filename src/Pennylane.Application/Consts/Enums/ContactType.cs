using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pennylane.Application.Consts.Enums
{
    public enum ContactType
    {
        [Description("Collaborator")]
        collaborator,

        [Description("Customer")]
        customer
    }
}
