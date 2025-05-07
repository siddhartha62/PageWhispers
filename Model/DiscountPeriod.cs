namespace PageWhispers.Model
{
    public class DiscountPeriod
    {

        public int Id { get; set; }
        public int BookId { get; set; }
        public BookCatalogs Book { get; set; }
        public decimal DiscountPercentage { get; set; }

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set => _startDate = EnsureUtc(value);
        }

        private DateTime _expiresAt;
        public DateTime ExpiresAt // Renamed from EndDate
        {
            get => _expiresAt;
            set => _expiresAt = EnsureUtc(value);
        }

        public bool OnSaleFlag { get; set; }

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

