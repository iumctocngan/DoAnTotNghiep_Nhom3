using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Identity;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Api.Services;

public static class KhoiPhucMatKhauAdmin
{
    public static async Task ThucHienAsync(IServiceProvider dichVu, bool laMoiTruongPhatTrien)
    {
        if (!laMoiTruongPhatTrien || Console.IsInputRedirected)
            throw new InvalidOperationException("Chỉ khôi phục trong Development bằng terminal tương tác.");

        using var phamVi = dichVu.CreateScope();
        var quanLy = phamVi.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var coSoDuLieu = phamVi.ServiceProvider.GetRequiredService<AppDbContext>();
        Console.Write("Email admin cần khôi phục: ");
        var email = Console.ReadLine()?.Trim();
        var taiKhoan = string.IsNullOrWhiteSpace(email) ? null : await quanLy.FindByEmailAsync(email);
        if (taiKhoan is null || !await quanLy.IsInRoleAsync(taiKhoan, UserRole.Admin))
            throw new InvalidOperationException("Không tìm thấy tài khoản admin với email này.");
        if (taiKhoan.EmploymentStatus != EmploymentStatus.Active)
            throw new InvalidOperationException("Tài khoản đã ngừng hoạt động; không thể khôi phục bằng lệnh này.");

        Console.Write("Mật khẩu mới (không hiển thị khi gõ): ");
        var matKhau = DocMatKhau();
        Console.Write("Nhập lại mật khẩu mới: ");
        if (matKhau != DocMatKhau())
            throw new InvalidOperationException("Hai mật khẩu không khớp. Chưa thay đổi dữ liệu.");

        await using var giaoDich = await coSoDuLieu.Database.BeginTransactionAsync();
        var maKhoiPhuc = await quanLy.GeneratePasswordResetTokenAsync(taiKhoan);
        KiemTra(await quanLy.ResetPasswordAsync(taiKhoan, maKhoiPhuc, matKhau));
        KiemTra(await quanLy.SetLockoutEndDateAsync(taiKhoan, null));
        KiemTra(await quanLy.ResetAccessFailedCountAsync(taiKhoan));
        await coSoDuLieu.RefreshTokens.Where(phien => phien.UserId == taiKhoan.Id && phien.RevokedAt == null)
            .ExecuteUpdateAsync(capNhat => capNhat
                .SetProperty(phien => phien.RevokedAt, DateTime.UtcNow)
                .SetProperty(phien => phien.RevokeReason, "Local admin password recovery."));
        await giaoDich.CommitAsync();
        Console.WriteLine("Đã đặt lại mật khẩu admin. Hãy chạy backend bình thường rồi đăng nhập bằng mật khẩu mới.");
    }

    private static string DocMatKhau()
    {
        var ketQua = new System.Text.StringBuilder();
        while (true)
        {
            var phim = Console.ReadKey(intercept: true);
            if (phim.Key == ConsoleKey.Enter) { Console.WriteLine(); return ketQua.ToString(); }
            if (phim.Key == ConsoleKey.Escape) throw new OperationCanceledException("Đã hủy khôi phục.");
            if (phim.Key == ConsoleKey.Backspace) { if (ketQua.Length > 0) ketQua.Length--; }
            else if (!char.IsControl(phim.KeyChar)) ketQua.Append(phim.KeyChar);
        }
    }

    private static void KiemTra(IdentityResult ketQua)
    {
        if (!ketQua.Succeeded)
            throw new InvalidOperationException(string.Join("; ", ketQua.Errors.Select(loi => loi.Description)));
    }
}
