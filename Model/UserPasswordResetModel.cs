using System.ComponentModel.DataAnnotations;

namespace PageWhispers.Model
{
    public class UserPasswordResetModel
    {

        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords are different.")]
        public string? ConfirmPassword { get; set; }

        public string? Token { get; set; }
    }
}
