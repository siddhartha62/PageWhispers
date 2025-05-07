using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using PageWhispers.Model;
using PageWhispers.Data;

namespace PageWhispers.Data
{
    public static class BookSeeder
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Seed Roles
            string[] roleNames = { "Admin", "Staff", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Seed Admin User (admin@bookhive.com)
            var adminEmail = "ana@gmail.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new UserAccount
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User"
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
                else
                {
                    throw new Exception($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            // Seed New Admin User (newadmin@example.com)
            var newAdminEmail = "newadmin@example.com";
            var newAdminUser = await userManager.FindByEmailAsync(newAdminEmail);
            if (newAdminUser == null)
            {
                newAdminUser = new UserAccount
                {
                    UserName = newAdminEmail,
                    Email = newAdminEmail,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(newAdminUser, "NewAdminPassword123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdminUser, "Admin");
                }
                else
                {
                    throw new Exception($"Failed to create new admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            // Seed Books
            if (!context.Books.Any())
            {
                context.Books.AddRange(
                    new BookCatalogs
                    {
                        Title = "Look Closer",
                        Author = "Rachel Amphlett",
                        BookDescription = "A gripping psychological thriller that keeps you questioning everything.",
                        CoverImageUrl = "",
                        AddedDate = DateTime.UtcNow.AddDays(-7)
                    },
                    new BookCatalogs
                    {
                        Title = "Little Voices",
                        Author = "Vanessa Lillie",
                        BookDescription = "A suspenseful story of a mother unraveling dark secrets after a tragic loss.",
                        CoverImageUrl = "",
                        AddedDate = DateTime.UtcNow.AddDays(-6)
                    },
                    new BookCatalogs
                    {
                        Title = "The Hate U Give",
                        Author = "Angie Thomas",
                        BookDescription = "A powerful novel about race, justice, and activism through the eyes of a young girl.",
                        CoverImageUrl = "",
                        AddedDate = DateTime.UtcNow.AddDays(-3)
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}