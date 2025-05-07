namespace PageWhispers.Model
{
    public class OrderModel
    {
        public string UserId { get; set; }
        public UserAccount User { get; set; }
        public int BookId { get; set; }
        public BookCatalogs Book { get; set; }

        private DateTime _orderDate;
        public DateTime OrderDate
        {
            get => _orderDate;
            set => _orderDate = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        public decimal TotalPrice { get; set; }
        public int Quantity { get; set; }
        public bool IsCancelled { get; set; }

        private DateTime? _cancelledAt;
        public DateTime? CancelledAt
        {
            get => _cancelledAt;
            set => _cancelledAt = value.HasValue ? (value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime()) : null;
        }

        public string ClaimCode { get; set; }

        public bool IsFulfilled { get; set; }

        private DateTime? _fulfilledAt;
        public DateTime? FulfilledAt
        {
            get => _fulfilledAt;
            set => _fulfilledAt = value.HasValue ? (value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime()) : null;
        }

        public string Status { get; set; } // New property to track order status (e.g., "Placed", "Received", "Cancelled")

        public bool IsCancellable
        {
            get
            {
                if (IsCancelled || IsFulfilled || Status == "Received") return false; // Cannot cancel if cancelled, fulfilled, or received
                var cancellationWindow = TimeSpan.FromHours(24); // 24-hour cancellation window
                return (DateTime.UtcNow - OrderDate) <= cancellationWindow;
            }
        }
    }
}
