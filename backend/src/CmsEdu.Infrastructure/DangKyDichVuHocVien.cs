using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Students;
using CmsEdu.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace CmsEdu.Infrastructure;
public static class DangKyDichVuHocVien
{
    public static IServiceCollection ThemDichVuHocVien(this IServiceCollection danhSachDichVu)
    {
        danhSachDichVu.TryAddScoped<IKhoDuLieuHocVien, KhoDuLieuHocVien>();
        danhSachDichVu.TryAddScoped<IDichVuHocVien, DichVuHocVien>();
        return danhSachDichVu;
    }
}
