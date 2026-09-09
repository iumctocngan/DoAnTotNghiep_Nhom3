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

            var adminEmail = configuration["SeedAdmin:Email"];
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                logger.LogInformation("Default Admin seeding skipped because SeedAdmin:Email is not configured.");
                return;
            }

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser is not null)
            {
                logger.LogInformation("Default Admin user already exists: {Email}", adminEmail);
                return;
            }

            var adminUserToCreate = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                EmployeeCode = await GenerateEmployeeCodeAsync(userManager),
                FullName = GetRequiredSeedValue(configuration, "SeedAdmin:FullName"),
                EmploymentStatus = EmploymentStatus.Active
            };

            var createResult = await userManager.CreateAsync(
                adminUserToCreate,
                GetRequiredSeedValue(configuration, "SeedAdmin:Password"));
            EnsureSucceeded(createResult, "create the default Admin user");

            var addRoleResult = await userManager.AddToRoleAsync(adminUserToCreate, UserRole.Admin);
            EnsureSucceeded(addRoleResult, "assign the Admin role to the default Admin user");
            logger.LogInformation("Created default Admin user with email: {Email}", adminEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating or seeding the database.");
            throw;
        }
    }

    private static async Task<string> GenerateEmployeeCodeAsync(
        UserManager<ApplicationUser> userManager)
    {
        string employeeCode;
        do
        {
            employeeCode = $"NV-{Guid.NewGuid():N}"[..11].ToUpperInvariant();
        }
        while (await userManager.Users.AnyAsync(user => user.EmployeeCode == employeeCode));

        return employeeCode;
    }

    private static string GetRequiredSeedValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' is required to create the default Admin user.");
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
