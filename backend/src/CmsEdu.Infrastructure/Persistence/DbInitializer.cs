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

            await EnsureRolesAsync(roleManager, logger);

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

            var adminEmployeeCode = GetRequiredSeedValue(configuration, "SeedAdmin:EmployeeCode")
                .Trim()
                .ToUpperInvariant();
            if (adminEmployeeCode.Length > 50) {
                throw new InvalidOperationException("SeedAdmin:EmployeeCode must not exceed 50 characters.");
            }

            var adminUserToCreate = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                EmployeeCode = adminEmployeeCode,
                FullName = GetRequiredSeedValue(configuration, "SeedAdmin:FullName"),
                EmploymentStatus = EmploymentStatus.Active
            };

            await using var transaction = await context.Database.BeginTransactionAsync();
            var createResult = await userManager.CreateAsync(
                adminUserToCreate,
                GetRequiredSeedValue(configuration, "SeedAdmin:Password"));
            EnsureSucceeded(createResult, "create the default Admin user");

            var addRoleResult = await userManager.AddToRoleAsync(adminUserToCreate, UserRole.Admin);
            EnsureSucceeded(addRoleResult, "assign the Admin role to the default Admin user");
            await transaction.CommitAsync();
            logger.LogInformation("Created default Admin user with email: {Email}", adminEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating or seeding the database.");
            throw;
        }
    }

    private static async Task EnsureRolesAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger)
    {
        foreach (var role in UserRole.AllRoles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)), $"create role '{role}'");
            logger.LogInformation("Created default role: {Role}", role);
        }
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
