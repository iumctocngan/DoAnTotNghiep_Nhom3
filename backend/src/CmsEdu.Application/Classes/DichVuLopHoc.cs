using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Classes;

public record YeuCauLopHoc(string MaLop, string TenLop, int CapDoId, string GiaoVienId,
    int SiSoToiDa, DateOnly NgayBatDau, DateOnly? NgayKetThuc, DayOfWeek Thu,
    TimeOnly GioBatDau, TimeOnly GioKetThuc, ClassStatus TrangThai);
public record ThongTinLopHoc(int Id, string MaLop, string TenLop, int CapDoId, string GiaoVienId,
    int SiSoToiDa, int SiSoHienTai, DateOnly NgayBatDau, DateOnly? NgayKetThuc,
    DayOfWeek Thu, TimeOnly GioBatDau, TimeOnly GioKetThuc, ClassStatus TrangThai);

public class DichVuLopHoc(IKhoDuLieuLopHoc kho, ICurrentUser nguoiDung)
{
    public async Task<List<ThongTinLopHoc>> DanhSach(CancellationToken maHuy)
    {
        KiemTraQuyen(UserRole.Admin, UserRole.Teacher, UserRole.Accountant, UserRole.CustomerCare);
        var danhSach = await kho.LayLopAsync(maHuy, nguoiDung.Role == UserRole.Teacher ? nguoiDung.UserId : null);
        var ketQua = new List<ThongTinLopHoc>(danhSach.Count);
        foreach (var lop in danhSach)
            ketQua.Add(ChuyenDoi(lop, await kho.DemSiSoAsync(lop.Id, maHuy)));
        return ketQua;
    }

    public async Task<ThongTinLopHoc> ChiTiet(int id, CancellationToken maHuy)
    {
        KiemTraQuyen(UserRole.Admin, UserRole.Teacher, UserRole.Accountant, UserRole.CustomerCare);
        var lop = await Tim(id, maHuy);
        if (nguoiDung.Role == UserRole.Teacher && lop.MainTeacherUserId != nguoiDung.UserId)
            throw new ForbiddenAccessException("Bạn không phụ trách lớp này.");
        return ChuyenDoi(lop, await kho.DemSiSoAsync(id, maHuy));
    }

    public async Task<ThongTinLopHoc> Tao(YeuCauLopHoc yeuCau, CancellationToken maHuy)
    {
        KiemTraQuyen(UserRole.Admin);
        await KiemTra(yeuCau, null, maHuy);
        var lop = new Class();
        Gan(lop, yeuCau);
        kho.Them(lop);
        await kho.LuuAsync(maHuy);
        return ChuyenDoi(lop, 0);
    }

    public Task<ThongTinLopHoc> CapNhat(int id, YeuCauLopHoc yeuCau, CancellationToken maHuy) =>
        kho.TrongGiaoDichAsync(async () =>
        {
            KiemTraQuyen(UserRole.Admin);
            var lop = await Tim(id, maHuy);
            await KiemTra(yeuCau, id, maHuy);
            var siSo = await kho.DemSiSoAsync(id, maHuy);
            if (yeuCau.SiSoToiDa < siSo)
                throw new ConflictException("Sĩ số tối đa không được nhỏ hơn số học viên đang ghi danh hoặc bảo lưu.");
            if (siSo > 0 && yeuCau.TrangThai is ClassStatus.Completed or ClassStatus.Cancelled)
                throw new ConflictException("Cần kết thúc các ghi danh trước khi đóng lớp.");
            Gan(lop, yeuCau);
            await kho.LuuAsync(maHuy);
            return ChuyenDoi(lop, siSo);
        }, maHuy);

    public async Task Xoa(int id, CancellationToken maHuy)
    {
        KiemTraQuyen(UserRole.Admin);
        var lop = await Tim(id, maHuy);
        if (await kho.CoDuLieuLienQuanAsync(id, maHuy))
            throw new ConflictException("Lớp đã có ghi danh hoặc buổi học; không thể xóa.");
        kho.Xoa(lop);
        await kho.LuuAsync(maHuy);
    }

    private void KiemTraQuyen(params string[] roles)
    {
        if (!nguoiDung.IsAuthenticated || string.IsNullOrWhiteSpace(nguoiDung.UserId) ||
            !roles.Contains(nguoiDung.Role))
            throw new ForbiddenAccessException("Bạn không có quyền thực hiện chức năng này.");
    }

    private async Task<Class> Tim(int id, CancellationToken maHuy) =>
        await kho.TimLopAsync(id, maHuy) ?? throw new NotFoundException("Không tìm thấy lớp học.");

    private async Task KiemTra(YeuCauLopHoc r, int? id, CancellationToken maHuy)
    {
        if (string.IsNullOrWhiteSpace(r.MaLop) || r.MaLop.Trim().Length > 50 ||
            string.IsNullOrWhiteSpace(r.TenLop) || r.TenLop.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(r.GiaoVienId) || r.SiSoToiDa < 1 ||
            r.GioBatDau >= r.GioKetThuc || r.NgayKetThuc < r.NgayBatDau ||
            !Enum.IsDefined(r.Thu) || !Enum.IsDefined(r.TrangThai))
            throw new ValidationException("Thông tin lớp học không hợp lệ.");
        if (await kho.TrungMaAsync(r.MaLop.Trim(), id, maHuy))
            throw new ConflictException("Mã lớp đã tồn tại.");
        if (!await kho.CapDoHoatDongAsync(r.CapDoId, maHuy))
            throw new ValidationException("Cấp độ không tồn tại hoặc đã ngừng sử dụng.");
        if (!await kho.GiaoVienHopLeAsync(r.GiaoVienId, maHuy))
            throw new ValidationException("Tài khoản giáo viên không hợp lệ hoặc đã nghỉ việc.");
        if (r.TrangThai is not (ClassStatus.Cancelled or ClassStatus.Completed) &&
            await kho.TrungLichAsync(r.GiaoVienId, id, r.Thu, r.GioBatDau, r.GioKetThuc,
                r.NgayBatDau, r.NgayKetThuc, maHuy))
            throw new ConflictException("Giáo viên đã có lớp trùng lịch.");
    }

    private static void Gan(Class x, YeuCauLopHoc r)
    {
        x.ClassCode = r.MaLop.Trim(); x.Name = r.TenLop.Trim(); x.LevelId = r.CapDoId;
        x.MainTeacherUserId = r.GiaoVienId; x.Capacity = r.SiSoToiDa;
        x.StartDate = r.NgayBatDau; x.EndDate = r.NgayKetThuc; x.DayOfWeek = r.Thu;
        x.StartTime = r.GioBatDau; x.EndTime = r.GioKetThuc; x.Status = r.TrangThai;
    }

    private static ThongTinLopHoc ChuyenDoi(Class x, int siSo) => new(x.Id, x.ClassCode, x.Name,
        x.LevelId, x.MainTeacherUserId, x.Capacity, siSo, x.StartDate, x.EndDate,
        x.DayOfWeek, x.StartTime, x.EndTime, x.Status);
}
