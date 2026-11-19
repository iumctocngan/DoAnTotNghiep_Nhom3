using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Identity;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Api.Services;

internal static class TaoTaiKhoanAdmin
{
    public static async Task ThucHienAsync(IServiceProvider dichVu, bool laMoiTruongPhatTrien)
    {
        if (!laMoiTruongPhatTrien || Console.IsInputRedirected)
            throw new InvalidOperationException("Chỉ tạo Admin bằng terminal tương tác trong môi trường Development.");

        using var phamVi = dichVu.CreateScope();
        var quanLy = phamVi.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var vaiTro = phamVi.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var coSoDuLieu = phamVi.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await vaiTro.RoleExistsAsync(UserRole.Admin))
            throw new InvalidOperationException("Chưa có vai trò Admin. Hãy khởi tạo cơ sở dữ liệu trước.");

        Console.Write("Email đăng nhập: ");
        var email = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new InvalidOperationException("Email đăng nhập không hợp lệ.");
        if (await quanLy.FindByEmailAsync(email) != null || await quanLy.FindByNameAsync(email) != null)
            throw new InvalidOperationException("Email này đã được sử dụng.");

        Console.Write("Họ và tên: ");
        var hoTen = Console.ReadLine()?.Trim();
        Console.Write("Mã nhân viên: ");
        var ma = Console.ReadLine()?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(hoTen) || hoTen.Length > 100 || string.IsNullOrWhiteSpace(ma) || ma.Length > 50)
            throw new InvalidOperationException("Nhập họ tên tối đa 100 ký tự và mã nhân viên tối đa 50 ký tự.");
        if (await quanLy.Users.AnyAsync(x => x.EmployeeCode == ma))
            throw new InvalidOperationException("Mã nhân viên đã được sử dụng.");

        Console.Write("Mật khẩu (không hiển thị khi gõ): ");
        var matKhau = KhoiPhucMatKhauAdmin.DocMatKhau();
        Console.Write("Nhập lại mật khẩu: ");
        if (matKhau != KhoiPhucMatKhauAdmin.DocMatKhau())
            throw new InvalidOperationException("Hai mật khẩu không khớp.");

        var taiKhoan = new ApplicationUser
        {
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            FullName = hoTen,
            EmployeeCode = ma,
            EmploymentStatus = EmploymentStatus.Active
        };
        await using var giaoDich = await coSoDuLieu.Database.BeginTransactionAsync();
        KhoiPhucMatKhauAdmin.KiemTra(await quanLy.CreateAsync(taiKhoan, matKhau));
        KhoiPhucMatKhauAdmin.KiemTra(await quanLy.AddToRoleAsync(taiKhoan, UserRole.Admin));
        await giaoDich.CommitAsync();
        Console.WriteLine($"Đã tạo tài khoản Admin: {email}");
    }
}
