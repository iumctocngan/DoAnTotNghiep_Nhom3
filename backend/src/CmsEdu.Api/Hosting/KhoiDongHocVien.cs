using CmsEdu.Infrastructure;
using Microsoft.AspNetCore.Hosting;
[assembly: HostingStartup(typeof(CmsEdu.Api.Hosting.KhoiDongHocVien))]
namespace CmsEdu.Api.Hosting;

// ASP.NET Core tự quét assembly khởi chạy để đăng ký dịch vụ, không cần sửa Program.cs.
public sealed class KhoiDongHocVien : IHostingStartup
{
    public void Configure(IWebHostBuilder boDung) => boDung.ConfigureServices(danhSachDichVu => danhSachDichVu.ThemDichVuHocVien());
}
