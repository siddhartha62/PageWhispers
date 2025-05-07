using System.ComponentModel.DataAnnotations;

namespace PageWhispers.Model
{
    public class BookView
    {

        public int Id { get; set; }

        [Required(ErrorMessage = "The Title field is required.")]
        [StringLength(100, ErrorMessage = "The Title must be at most 100 characters long.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "The Author field is required.")]
        [StringLength(100, ErrorMessage = "The Author must be at most 100 characters long.")]
        public string Author { get; set; }

        [StringLength(500, ErrorMessage = "The Description must be at most 500 characters long.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "The Price field is required.")]
        [Range(0.01, 10000, ErrorMessage = "The Price must be between 0.01 and 10,000.")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "The Quantity field is required.")]
        [Range(0, 10000, ErrorMessage = "The Quantity must be between 0 and 10,000.")]
        public int Quantity { get; set; }

        public string CoverImageUrl { get; set; }

        public IFormFile CoverImage { get; set; }
    }
}
