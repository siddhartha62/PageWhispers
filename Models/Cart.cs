using System.ComponentModel.DataAnnotations;

namespace PageWhisphers.Models
{
    public class Cart
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int BookId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1; // Default to 1

        public ApplicationUser User { get; set; } = null!;

        public Book Book { get; set; } = null!;
    }
}