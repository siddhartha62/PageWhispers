using System.ComponentModel.DataAnnotations;

namespace PageWhispers.Model
{
    public class FulfillOrderView
    {
        [Required(ErrorMessage = "Claim Code is required.")]
        public string ClaimCode { get; set; }

        [Required(ErrorMessage = "User ID is required.")]
        public string UserId { get; set; }

        public bool IsConfirmationStep { get; set; }

        public OrderModel Orders { get; set; } // Should include Book and User navigation properties

    }
}
