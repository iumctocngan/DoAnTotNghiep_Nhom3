using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Curriculum;
using CmsEdu.Application.Classes;
using CmsEdu.Application.Enrollments;
using CmsEdu.Application.Remarks;
using CmsEdu.Application.Teachers;
using CmsEdu.Infrastructure.Identity;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Persistence.Repositories;
using CmsEdu.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CmsEdu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            }));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(options => options.SigningKey.Length >= 32, "Jwt:SigningKey must contain at least 32 characters.")
            .Validate(options => options.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be greater than zero.")
            .Validate(options => options.RefreshTokenDays > 0, "Jwt:RefreshTokenDays must be greater than zero.");

        services.AddScoped<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IKhoDuLieuNguoiGiamHo, Persistence.Repositories.KhoDuLieuNguoiGiamHo>();
        services.AddScoped<CmsEdu.Application.Guardians.DichVuNguoiGiamHo>();
        services.AddScoped<IDichVuBuoiHoc, DichVuBuoiHoc>();
        services.AddScoped<IDichVuDiemDanh, DichVuDiemDanh>();
        services.AddScoped<IDichVuHoaDon, DichVuHoaDon>();
        services.AddScoped<IDichVuPayment, DichVuPayment>();
        services.AddScoped<IDichVuDashboardVaiTro, DichVuDashboardVaiTro>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<TeacherService>();
        services.AddScoped<ICurriculumRepository, CurriculumRepository>();
        services.AddScoped<CurriculumService>();
        services.AddScoped<KhoDuLieuQuanLyLop>();
        services.AddScoped<IKhoDuLieuLopHoc>(provider => provider.GetRequiredService<KhoDuLieuQuanLyLop>());
        services.AddScoped<IKhoDuLieuGhiDanh>(provider => provider.GetRequiredService<KhoDuLieuQuanLyLop>());
        services.AddScoped<IKhoDuLieuNhanXet>(provider => provider.GetRequiredService<KhoDuLieuQuanLyLop>());
        services.AddScoped<DichVuLopHoc>();
        services.AddScoped<DichVuGhiDanh>();
        services.AddScoped<DichVuNhanXetHocVien>();

        return services;
    }
}
