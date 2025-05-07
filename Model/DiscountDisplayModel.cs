namespace PageWhispers.Model
{
    public class DiscountDisplayModel
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string BookTitle { get; set; }
        public decimal DiscountPercentage { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiresAt { get; set; } // Renamed from EndDate
        public bool OnSaleFlag { get; set; }
    }
}
