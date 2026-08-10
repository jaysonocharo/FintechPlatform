using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using FintechBackend.Models;
using FintechBackend.Constants;


namespace FintechBackend.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context, IConfiguration configuration)
        {
            // 1. Ensure the database is created and apply any pending migrations
            context.Database.Migrate();

            // 2. Check if an Admin already exists
            if (context.Users.Any(u => u.Role == Roles.Admin))
            {
                return; // Database has already been seeded with an admin
            }

            // 3. Fetch credentials from User Secrets / appsettings
            var adminEmail = configuration["AdminConfig:DefaultEmail"];
            var adminPassword = configuration["AdminConfig:DefaultPassword"];

            // Fail-safe: Prevent startup if the password isn't configured
            if (string.IsNullOrEmpty(adminPassword) || string.IsNullOrEmpty(adminEmail))
            {
                throw new InvalidOperationException("Default admin credentials are not configured.");
            }

            // 4. Create the Super Admin user
            var adminUser = new User
            {
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = Roles.Admin,
                // Add any other required fields for your User model (e.g., CreatedAt = DateTime.UtcNow)
            };

            // 5. Save to the database
            context.Users.Add(adminUser);
            context.SaveChanges();
        }
    }
}