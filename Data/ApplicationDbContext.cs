using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PageWhispers.Model;


namespace PageWhispers.Data
{
    public class ApplicationDbContext : IdentityDbContext<UserAccount>
    {

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }
        public DbSet<BookCatalogs> Books { get; set; }
        public DbSet<UserCartItem> Carts { get; set; }
        public DbSet<Whitelist> Whitelists { get; set; }
        public DbSet<OrderModel> Orders { get; set; }
        public DbSet<BookFeedbackRecordModel> Reviews { get; set; }
        public DbSet<DiscountPeriod> TimedDiscounts { get; set; }
        public DbSet<BookLoan> BookLoans { get; set; }
        public DbSet<TimedAnnouncement> TimedAnnouncements { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Cart with composite key
            builder.Entity<UserCartItem>()
                .HasKey(c => new { c.UserId, c.BookId });

            // Configure Whitelist with composite key
            builder.Entity<Whitelist>()
                .HasKey(w => new { w.UserId, w.BookId });

            // Configure Order with composite key
            builder.Entity<OrderModel>()
                .HasKey(o => new { o.UserId, o.BookId, o.OrderDate });

            // Configure Review with single Id as primary key
            builder.Entity<BookFeedbackRecordModel>()
                .HasKey(r => r.Id);

            // Configure self-referencing relationship for Review
            builder.Entity<BookFeedbackRecordModel>()
                .HasOne(r => r.ParentReview)
                .WithMany(r => r.Replies)
                .HasForeignKey(r => r.ParentReviewId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Book-Review relationship
            builder.Entity<BookFeedbackRecordModel>()
                .HasOne(r => r.Book)
                .WithMany(b => b.Reviews)
                .HasForeignKey(r => r.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Review-User relationship
            builder.Entity<BookFeedbackRecordModel>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TimedDiscount
            builder.Entity<DiscountPeriod>()
                .HasOne(td => td.Book)
                .WithMany()
                .HasForeignKey(td => td.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure BookLoan with composite key
            builder.Entity<BookLoan>()
                .HasKey(bl => new { bl.UserId, bl.BookId, bl.LoanDate });

            // Configure BookLoan relationships
            builder.Entity<BookLoan>()
                .HasOne(bl => bl.User)
                .WithMany()
                .HasForeignKey(bl => bl.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BookLoan>()
                .HasOne(bl => bl.Book)
                .WithMany()
                .HasForeignKey(bl => bl.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TimedAnnouncement
            builder.Entity<TimedAnnouncement>()
                .HasKey(ta => ta.Id);

            // Ignore the Roles property on ApplicationUser
            builder.Entity<UserAccount>()
                .Ignore(u => u.Roles);
        }

    }
}

