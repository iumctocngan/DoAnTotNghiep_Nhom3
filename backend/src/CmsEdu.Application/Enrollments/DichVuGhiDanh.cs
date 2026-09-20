using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Enrollments;

public record YeuCauGhiDanh(int HocVienId, int LopId, DateOnly NgayBatDau);
public record YeuCauBaoLuu(string LyDo, DateOnly NgayDuKienTroLai);
public record KetThucYeuCauGhiDanh(string LyDo, DateOnly NgayKetThuc);
public record ThongTinGhiDanh(int Id, int HocVienId, int LopId, DateOnly NgayBatDau,
    DateOnly? NgayKetThuc, EnrollmentStatus TrangThai, string? LyDoBaoLuu,
    DateOnly? NgayDuKienTroLai, string? LyDoKetThuc);

public class DichVuGhiDanh(IKhoDuLieuGhiDanh kho)
{
    public async Task<List<ThongTinGhiDanh>> DanhSach(int? lopId, int? hocVienId,
        CancellationToken maHuy) =>
        (await kho.LayDanhSachAsync(lopId, hocVienId, maHuy)).Select(ChuyenDoi).ToList();

    public async Task<ThongTinGhiDanh> ChiTiet(int id, CancellationToken maHuy) =>
        ChuyenDoi(await Tim(id, maHuy));

    public Task<ThongTinGhiDanh> Tao(YeuCauGhiDanh yeuCau, CancellationToken maHuy) =>
        kho.TrongGiaoDichAsync(async () =>
        {
            await KiemTraCho(yeuCau.HocVienId, yeuCau.LopId, yeuCau.NgayBatDau, maHuy);
            var ghiDanh = new Enrollment
            {
                StudentId = yeuCau.HocVienId, ClassId = yeuCau.LopId,
                StartDate = yeuCau.NgayBatDau, Status = EnrollmentStatus.Active
            };
            kho.Them(ghiDanh);
            await kho.LuuAsync(maHuy);
            return ChuyenDoi(ghiDanh);
        }, maHuy);

    public async Task<ThongTinGhiDanh> BaoLuu(int id, YeuCauBaoLuu yeuCau, CancellationToken maHuy)
    {
        var x = await Tim(id, maHuy);
        if (x.Status != EnrollmentStatus.Active)
            throw new ConflictException("Chỉ ghi danh đang học mới được bảo lưu.");
        if (string.IsNullOrWhiteSpace(yeuCau.LyDo) || yeuCau.LyDo.Trim().Length > 500 ||
            yeuCau.NgayDuKienTroLai <= DateOnly.FromDateTime(DateTime.Today))
            throw new ValidationException("Lý do hoặc ngày dự kiến trở lại không hợp lệ.");
        x.Status = EnrollmentStatus.Paused;
        x.PauseReason = yeuCau.LyDo.Trim();
        x.ExpectedReturnDate = yeuCau.NgayDuKienTroLai;
        await kho.LuuAsync(maHuy);
        return ChuyenDoi(x);
    }

    public async Task<ThongTinGhiDanh> TroLai(int id, CancellationToken maHuy)
    {
        var x = await Tim(id, maHuy);
        if (x.Status != EnrollmentStatus.Paused)
            throw new ConflictException("Chỉ ghi danh bảo lưu mới được trở lại học.");
        if (x.Class.Status != ClassStatus.Active || x.Class.EndDate < DateOnly.FromDateTime(DateTime.Today))
            throw new ConflictException("Lớp không còn hoạt động.");
        x.Status = EnrollmentStatus.Active;
        x.PauseReason = null; x.ExpectedReturnDate = null;
        await kho.LuuAsync(maHuy);
        return ChuyenDoi(x);
    }

    public Task<ThongTinGhiDanh> HoanThanh(int id, KetThucYeuCauGhiDanh yeuCau,
        CancellationToken maHuy) => KetThuc(id, yeuCau, EnrollmentStatus.Completed, maHuy);

    public Task<ThongTinGhiDanh> NghiHoc(int id, KetThucYeuCauGhiDanh yeuCau,
        CancellationToken maHuy) => KetThuc(id, yeuCau, EnrollmentStatus.Withdrawn, maHuy);

    private async Task<ThongTinGhiDanh> KetThuc(int id, KetThucYeuCauGhiDanh yeuCau,
        EnrollmentStatus trangThai, CancellationToken maHuy)
    {
        var x = await Tim(id, maHuy);
        if (x.Status is not (EnrollmentStatus.Active or EnrollmentStatus.Paused))
            throw new ConflictException("Ghi danh đã kết thúc.");
        if (string.IsNullOrWhiteSpace(yeuCau.LyDo) || yeuCau.LyDo.Trim().Length > 500 ||
            yeuCau.NgayKetThuc < x.StartDate)
            throw new ValidationException("Lý do hoặc ngày kết thúc không hợp lệ.");
        x.Status = trangThai; x.EndDate = yeuCau.NgayKetThuc;
        x.EndReason = yeuCau.LyDo.Trim(); x.PauseReason = null; x.ExpectedReturnDate = null;
        await kho.LuuAsync(maHuy);
        return ChuyenDoi(x);
    }

    private async Task KiemTraCho(int hocVienId, int lopId, DateOnly ngay, CancellationToken maHuy)
    {
        var hocVien = await kho.TimHocVienAsync(hocVienId, maHuy)
            ?? throw new NotFoundException("Không tìm thấy học viên.");
        var lop = await kho.TimLopAsync(lopId, maHuy)
            ?? throw new NotFoundException("Không tìm thấy lớp học.");
        if (hocVien.IsArchived) throw new ConflictException("Học viên đã được lưu trữ.");
        if (lop.Status != ClassStatus.Active || ngay < lop.StartDate ||
            lop.EndDate < ngay || ngay > DateOnly.FromDateTime(DateTime.Today))
            throw new ValidationException("Ngày ghi danh phải nằm trong thời gian lớp đang hoạt động và không ở tương lai.");
        if (await kho.DaGhiDanhAsync(hocVienId, maHuy))
            throw new ConflictException("Học viên đã có ghi danh đang học hoặc bảo lưu.");
        if (await kho.DemSiSoAsync(lopId, maHuy) >= lop.Capacity)
            throw new ConflictException("Lớp đã đủ sĩ số.");
    }

    private async Task<Enrollment> Tim(int id, CancellationToken maHuy) =>
        await kho.TimAsync(id, maHuy) ?? throw new NotFoundException("Không tìm thấy ghi danh.");

    private static ThongTinGhiDanh ChuyenDoi(Enrollment x) => new(x.Id, x.StudentId, x.ClassId,
        x.StartDate, x.EndDate, x.Status, x.PauseReason, x.ExpectedReturnDate, x.EndReason);
}
