namespace PageWhispers.Model
{
    public class CartWithDiscount
    {
        public UserCartItem CartItem { get; set; }
        public bool OnSaleFlag { get; set; }
        public bool IsDiscountActive { get; set; }
        public decimal DiscountedPrice { get; set; }
    }
}
