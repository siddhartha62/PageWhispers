namespace PageWhispers.Model
{
    public class UserView
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string ProfileImageUrl { get; set; }
        public List<string> Roles { get; set; }
    }
}
