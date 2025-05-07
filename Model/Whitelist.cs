using System.ComponentModel.DataAnnotations;

namespace PageWhispers.Model
{
    public class Whitelist
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public UserAccount User { get; set; } = null!;

        [Required]
        public int BookId { get; set; }
        public BookCatalogs Book { get; set; } = null!;
    }
}
