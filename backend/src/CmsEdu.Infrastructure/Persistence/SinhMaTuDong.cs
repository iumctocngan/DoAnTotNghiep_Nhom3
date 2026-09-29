using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CmsEdu.Infrastructure.Persistence;

public static class SinhMaTuDong
{
    public static string TienToNhanVien(string vaiTro) => vaiTro switch
    {
        "Teacher" => "GV", "CustomerCare" => "CSKH", "Accountant" => "KT", "Admin" => "QT",
        _ => throw new CmsEdu.Application.Common.Exceptions.ValidationException("Vai trò không hợp lệ.")
    };

    public static async Task<string> TaoAsync(AppDbContext duLieu, string tienTo, CancellationToken maHuy)
    {
        if (!new[] { "HS", "LH", "GV", "CSKH", "KT", "QT", "KH", "CD", "BH" }.Contains(tienTo))
            throw new ArgumentException("Tiền tố mã không hợp lệ.");
        await duLieu.Database.OpenConnectionAsync(maHuy);
        try
        {
            await using var lenh = duLieu.Database.GetDbConnection().CreateCommand();
            lenh.Transaction = duLieu.Database.CurrentTransaction?.GetDbTransaction();
            // Tên sequence chỉ lấy từ danh sách cố định ở trên, không nhận SQL từ người dùng.
            lenh.CommandText = $"SELECT NEXT VALUE FOR [dbo].[MaTuDong_{tienTo}]";
            var so = Convert.ToInt64(await lenh.ExecuteScalarAsync(maHuy));
            return tienTo + so.ToString(tienTo is "HS" or "LH" ? "D4" : "D3");
        }
        finally { await duLieu.Database.CloseConnectionAsync(); }
    }
}
