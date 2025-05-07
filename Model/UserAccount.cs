using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PageWhispers.Model
{
    public class UserAccount : IdentityUser
    {
        public string? ProfileImageUrl { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; } // Added for completeness, as seen in the query

        [NotMapped] // Ignore this property in the database
        public List<string> Roles { get; set; } = new List<string>();

     }
}
