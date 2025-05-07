namespace PageWhispers.Model
{
    public class TimedAnnouncement
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }

        private DateTime _createdAt;
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => _createdAt = EnsureUtc(value);
        }

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set => _startDate = EnsureUtc(value);
        }

        private DateTime _expiresAt;
        public DateTime ExpiresAt
        {
            get => _expiresAt;
            set => _expiresAt = EnsureUtc(value);
        }

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
