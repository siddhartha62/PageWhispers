namespace PageWhisphers.Models
{
    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string? Genre { get; set; } // Added for genre filtering
        public string Description { get; set; }
        private DateTime _addedDate;
        public DateTime AddedDate
        {
            get => _addedDate;
            set => _addedDate = EnsureUtc(value);
        }
        public DateTime PublicationDate { get; set; } // Added for publication date sorting
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string CoverImageUrl { get; set; }
        public bool IsAvailable => Quantity > 0;
        public string? ISBN { get; set; } // Added for ISBN search
        public string? Language { get; set; } // Added for language filtering
        public string? Format { get; set; } // Made nullable for format filtering (e.g., paperback, hardcover)
        public string? Publisher { get; set; } // Added for publisher filtering
        public bool IsPhysicalLibraryAccess { get; set; } // Added for physical library access filtering
        public bool IsBestseller { get; set; } // Added for bestseller category
        public bool IsAwardWinner { get; set; } // Added for award winner category
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        private DateTime EnsureUtc(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            }
            return dateTime.Kind == DateTimeKind.Local ? dateTime.ToUniversalTime() : dateTime;
        }
    }
}