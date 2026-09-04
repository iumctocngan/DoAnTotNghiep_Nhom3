using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CmsEdu.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));

        try
        {
            if (context.Database.IsSqlServer())
            {
                await context.Database.MigrateAsync();
            }

            foreach (var role in UserRole.AllRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                    EnsureSucceeded(roleResult, $"create role '{role}'");
                    logger.LogInformation("Created default role: {Role}", role);
                }
            }

            if (!configuration.GetValue<bool>("SeedAdmin:Enabled"))
            {
                logger.LogInformation("Default Admin seeding is disabled.");
                return;
            }

            var adminEmail = GetRequiredSeedValue(configuration, "SeedAdmin:Email");
            var adminPassword = GetRequiredSeedValue(configuration, "SeedAdmin:Password");
            var adminEmployeeCode = GetRequiredSeedValue(configuration, "SeedAdmin:EmployeeCode");
            var adminFullName = GetRequiredSeedValue(configuration, "SeedAdmin:FullName");

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    EmployeeCode = adminEmployeeCode,
                    FullName = adminFullName,
                    EmploymentStatus = EmploymentStatus.Active
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                EnsureSucceeded(createResult, "create the default Admin user");
                logger.LogInformation("Created default Admin user with email: {Email}", adminEmail);
            }

            if (adminUser.EmploymentStatus != EmploymentStatus.Active)
            {
                adminUser.EmploymentStatus = EmploymentStatus.Active;
                var updateResult = await userManager.UpdateAsync(adminUser);
                EnsureSucceeded(updateResult, "activate the default Admin user");
            }

            if (!await userManager.IsInRoleAsync(adminUser, UserRole.Admin))
            {
                var addRoleResult = await userManager.AddToRoleAsync(adminUser, UserRole.Admin);
                EnsureSucceeded(addRoleResult, "assign the Admin role to the default Admin user");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating or seeding the database.");
            throw;
        }
    }

    private static string GetRequiredSeedValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' is required when default Admin seeding is enabled.");
        }

        return value;
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(", ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Failed to {operation}: {errors}");
    }
}
