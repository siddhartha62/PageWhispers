using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PageWhisphers.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? ProfileImageUrl { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; } // Added for completeness, as seen in the query

        [NotMapped] // Ignore this property in the database
        public List<string> Roles { get; set; } = new List<string>();
    }
}