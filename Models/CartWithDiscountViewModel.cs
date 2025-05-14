namespace PageWhisphers.Models
{
    public class CartWithDiscountViewModel
    {
        public Cart CartItem { get; set; }
        public bool OnSaleFlag { get; set; }
        public bool IsDiscountActive { get; set; }
        public decimal DiscountedPrice { get; set; }
    }
}