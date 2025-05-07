namespace PageWhispers.Model
{
    public class BookCatalogs
    {
        public int Id { get; set; }
        public string? Title { get; set; }

        //For enabling catalog filtering 
        public string? Author { get; set; }

        public string? Format { get; set; }
        public string? Publisher { get; set; }
        public string? Genre { get; set; } 
        public string? BookDescription { get; set; }

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
        public string ISBN { get; set; } 

        public string Language { get; set; }
      
        public bool IsPhysicalLibraryAccess { get; set; }
        public bool IsBestseller { get; set; } 
        public bool IsAwardWinner { get; set; } 
        public ICollection<BookFeedbackRecordModel> Reviews { get; set; } = new List<BookFeedbackRecordModel>();

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
