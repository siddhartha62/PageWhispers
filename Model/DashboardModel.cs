namespace PageWhispers.Model
{
    public class HomeViewModel
    {

        public List<DiscountCatalogEntry> Books { get; set; }
        public UserAccount CurrentUser { get; set; }
        public List<TimedAnnouncement> TimedAnnouncements { get; set; }
    }
}
