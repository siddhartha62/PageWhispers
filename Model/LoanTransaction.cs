namespace PageWhispers.Model
{
    public class BookLoan
    {
        public string UserId { get; set; }
        public UserAccount User { get; set; }
        public int BookId { get; set; }
        public BookCatalogs Book { get; set; }

        private DateTime _loanDate;
        public DateTime LoanDate
        {
            get => _loanDate;
            set => _loanDate = EnsureUtc(value);
        }

        private DateTime? _returnDate;
        public DateTime? ReturnDate
        {
            get => _returnDate;
            set => _returnDate = value.HasValue ? EnsureUtc(value.Value) : null;
        }

        private DateTime _dueDate;
        public DateTime DueDate
        {
            get => _dueDate;
            set => _dueDate = EnsureUtc(value);
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
