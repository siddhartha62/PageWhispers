namespace PageWhisphers.Models
{
    public class HomeViewModel
    {
        public List<BookWithDiscountViewModel> Books { get; set; }
        public ApplicationUser CurrentUser { get; set; }
        public List<TimedAnnouncement> TimedAnnouncements { get; set; }
    }
}