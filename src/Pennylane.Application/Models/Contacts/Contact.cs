using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Models.Contacts
{
    public class Contact
    {
        public required int ContactId { get; set; }
        public Guid? ContactGlobalUniqueId { get; set; }
        public required string Email { get; set; }
        public string? Type { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
