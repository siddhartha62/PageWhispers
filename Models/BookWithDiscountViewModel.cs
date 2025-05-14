namespace PageWhisphers.Models
{
    public class BookWithDiscountViewModel
    {
        public Book Book { get; set; }
        public bool OnSaleFlag { get; set; }
        public bool IsDiscountActive { get; set; }
        public decimal DiscountedPrice { get; set; }
        public List<Review> Reviews { get; set; }
    }
}