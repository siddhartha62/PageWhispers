using System.ComponentModel.DataAnnotations;

namespace PageWhispers.Model
{
    public class UserCartItem
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int BookId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1; // Default to 1

        public UserAccount User { get; set; } = null!;

        public BookCatalogs Book { get; set; } = null!;
    }
}
