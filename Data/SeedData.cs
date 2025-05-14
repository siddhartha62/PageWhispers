using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using PageWhisphers.Models;

namespace PageWhisphers.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
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

            // Seed Admin User 
            var adminEmail = "admin@gmail.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
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
            var newAdminEmail = "newadmin@gmail.com";
            var newAdminUser = await userManager.FindByEmailAsync(newAdminEmail);
            if (newAdminUser == null)
            {
                newAdminUser = new ApplicationUser
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
            context.Books.AddRange(
            new Book
            {
                Title = "To Kill a Hamlock",
                Author = "Harper Lee",
                Description = "A gripping tale of racial injustice and the loss of innocence in a small Southern town.",
                CoverImageUrl = "",
                AddedDate = DateTime.UtcNow.AddDays(-10),
                Publisher = "J. B. Lippincott & Co." // Provide a non-null Publisher value
            }
    
        );
            await context.SaveChangesAsync();
        }
    }
}