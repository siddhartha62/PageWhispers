using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using PageWhisphers.Data;
using PageWhisphers.Models;
using System.Net.Mail;
using System.Net;
using PageWhisphers.Hubs;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL; 

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Configure PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add SignalR for real-time notifications
builder.Services.AddSignalR();

// Configure Identity with email confirmation
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure email sending service
builder.Services.AddTransient<IEmailSender>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var smtpServer = config["EmailSettings:SmtpServer"] ?? throw new InvalidOperationException("SmtpServer not configured.");
    var smtpPort = config.GetValue<int>("EmailSettings:SmtpPort");
    var smtpUsername = config["EmailSettings:SmtpUsername"] ?? throw new InvalidOperationException("SmtpUsername not configured.");
    var smtpPassword = config["EmailSettings:SmtpPassword"] ?? throw new InvalidOperationException("SmtpPassword not configured.");
    var senderEmail = config["EmailSettings:SenderEmail"] ?? throw new InvalidOperationException("SenderEmail not configured.");
    var senderName = config["EmailSettings:SenderName"] ?? throw new InvalidOperationException("SenderName not configured.");

    return new EmailSender(smtpServer, smtpPort, smtpUsername, smtpPassword, senderEmail, senderName);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs
app.MapHub<AnnouncementHub>("/announcementHub");
app.MapHub<OrderNotificationHub>("/orderNotificationHub");

// Map default controller route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed the database and roles
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SeedData.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
        throw;
    }
}

app.Run();

// Email sender implementation
public class EmailSender : IEmailSender
{
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _senderEmail;
    private readonly string _senderName;

    public EmailSender(string smtpServer, int smtpPort, string smtpUsername, string smtpPassword, string senderEmail, string senderName)
    {
        _smtpServer = smtpServer;
        _smtpPort = smtpPort;
        _smtpUsername = smtpUsername;
        _smtpPassword = smtpPassword;
        _senderEmail = senderEmail;
        _senderName = senderName;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var client = new SmtpClient(_smtpServer, _smtpPort)
        {
            Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_senderEmail, _senderName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        mailMessage.To.Add(email);

        return client.SendMailAsync(mailMessage);
    }
}

public interface IEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage);
}