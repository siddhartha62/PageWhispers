namespace PageWhispers.Model
{
    public class DiscountCatalogEntry
    {
        public BookCatalogs Book { get; set; }
        public bool OnSaleFlag { get; set; }
        public bool IsDiscountActive { get; set; }
        public decimal DiscountedPrice { get; set; }
        public List<BookFeedbackRecordModel> Reviews { get; set; }
    }
}
