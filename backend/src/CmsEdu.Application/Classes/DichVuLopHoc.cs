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

    public Task<ThongTinLopHoc> Tao(YeuCauLopHoc yeuCau, CancellationToken maHuy) =>
        kho.TrongGiaoDichAsync(async () =>
        {
            KiemTraQuyen(UserRole.Admin);
            await KiemTra(yeuCau, null, maHuy);
            var lop = new Class();
            Gan(lop, yeuCau);
            if (lop.Status == ClassStatus.Active)
                await XepBuoiHangTuan(lop, maHuy);
            kho.Them(lop);
            await kho.LuuAsync(maHuy);
            return ChuyenDoi(lop, 0);
        }, maHuy);

    public Task<ThongTinLopHoc> CapNhat(int id, YeuCauLopHoc yeuCau, CancellationToken maHuy) =>
        kho.TrongGiaoDichAsync(async () =>
        {
            KiemTraQuyen(UserRole.Admin);
            var lop = await Tim(id, maHuy);
            yeuCau = yeuCau with { MaLop = lop.ClassCode };
            await KiemTra(yeuCau, id, maHuy);
            var siSo = await kho.DemSiSoAsync(id, maHuy);
            if (yeuCau.SiSoToiDa < siSo)
                throw new ConflictException("Sĩ số tối đa không được nhỏ hơn số học viên đang ghi danh hoặc bảo lưu.");
            if (siSo > 0 && yeuCau.TrangThai is ClassStatus.Completed or ClassStatus.Cancelled)
                throw new ConflictException("Cần kết thúc các ghi danh trước khi đóng lớp.");
            var moLop = lop.Status != ClassStatus.Active && yeuCau.TrangThai == ClassStatus.Active;
            var doiGioDinhKy = lop.Status == ClassStatus.Active && yeuCau.TrangThai == ClassStatus.Active &&
                lop.DayOfWeek == yeuCau.Thu && (lop.StartTime != yeuCau.GioBatDau || lop.EndTime != yeuCau.GioKetThuc);
            Gan(lop, yeuCau);
            if (moLop)
                await XepBuoiHangTuan(lop, maHuy);
            else if (doiGioDinhKy)
                await kho.DongBoGioBuoiDinhKyAsync(lop, maHuy);
            await kho.LuuAsync(maHuy);
            return ChuyenDoi(lop, siSo);
        }, maHuy);

    private async Task XepBuoiHangTuan(Class lop, CancellationToken maHuy)
    {
        var baiHoc = await kho.LayBaiHocHoatDongAsync(lop.LevelId, maHuy);
        if (lop.EndDate == null && baiHoc.Count == 0)
            throw new ValidationException("Nhập ngày kết thúc hoặc bổ sung bài học cho cấp độ để xếp lịch hằng tuần.");

        var lechNgay = ((int)lop.DayOfWeek - (int)lop.StartDate.DayOfWeek + 7) % 7;
        var ngayDau = lop.StartDate.DayNumber + lechNgay;
        var ngayCuoi = lop.EndDate?.DayNumber ?? ngayDau + 7L * (baiHoc.Count - 1);
        if (ngayDau > ngayCuoi || ngayCuoi > DateOnly.MaxValue.DayNumber)
            throw new ValidationException("Khoảng ngày của lớp không có đủ ngày học theo lịch đã chọn.");

        var trungKhung = (await kho.LayBuoiCungKhungGioAsync(lop,
            DateOnly.FromDayNumber(ngayDau), DateOnly.FromDayNumber((int)ngayCuoi), maHuy))
            .ToLookup(x => x.SessionDate);

        for (var ngay = ngayDau; ngay <= ngayCuoi; ngay += 7)
        {
            var ngayHoc = DateOnly.FromDayNumber(ngay);
            if (trungKhung[ngayHoc].Any(x => x.ClassId != lop.Id))
                throw new ConflictException($"Giáo viên đã có buổi học trùng giờ ngày {ngayHoc:dd/MM/yyyy}.");
            if (trungKhung[ngayHoc].Any(x => x.ClassId == lop.Id))
                continue;

            var thuTu = (ngay - ngayDau) / 7;
            lop.Sessions.Add(new Session
            {
                IsRegular = true,
                LessonId = thuTu < baiHoc.Count ? baiHoc[thuTu] : null,
                SessionDate = ngayHoc, StartTime = lop.StartTime, EndTime = lop.EndTime
            });
        }
    }

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
