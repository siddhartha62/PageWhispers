using System.ComponentModel.DataAnnotations;

namespace PageWhispers.Model
{
    public class UserAccountDeletionModel
    {
        //Declaring 
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "A message is required.")]
        public string Message { get; set; } = string.Empty;
    }
}
