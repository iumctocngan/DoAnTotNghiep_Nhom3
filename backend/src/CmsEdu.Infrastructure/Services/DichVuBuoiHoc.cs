using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Sessions;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ValidationException = CmsEdu.Application.Common.Exceptions.ValidationException;

namespace CmsEdu.Infrastructure.Services;

/// <summary>
/// Cài đặt dịch vụ xử lý nghiệp vụ buổi học (Session).
/// </summary>
public class DichVuBuoiHoc(AppDbContext nguCanh, ICurrentUser nguoiDungHienTai) : IDichVuBuoiHoc
{
    private const int KichThuocTrangToiDa = 100;

    // [Danh sách buổi học] Lọc theo lớp, khoảng ngày, phân trang và áp dụng phạm vi phân quyền
    public async Task<PagedResult<PhanHoiBuoiHoc>> LayDanhSachBuoiHocAsync(
        int? maLop,
        DateOnly? tuNgay,
        DateOnly? denNgay,
        int trang,
        int kichThuocTrang,
        CancellationToken maHuy = default)
    {
        KiemTraPhanTrang(trang, kichThuocTrang);
        KiemTraKhoangNgay(tuNgay, denNgay);

        var truyVan = ApDungPhamViXem(nguCanh.Sessions
            .AsNoTracking()
            .Include(buoiHoc => buoiHoc.Class)
            .Include(buoiHoc => buoiHoc.Lesson)
            .AsQueryable());

        if (maLop.HasValue)
        {
            truyVan = truyVan.Where(buoiHoc => buoiHoc.ClassId == maLop.Value);
        }

        if (tuNgay.HasValue)
        {
            truyVan = truyVan.Where(buoiHoc => buoiHoc.SessionDate >= tuNgay.Value);
        }

        if (denNgay.HasValue)
        {
            truyVan = truyVan.Where(buoiHoc => buoiHoc.SessionDate <= denNgay.Value);
        }

        var tongSo = await truyVan.CountAsync(maHuy);
        var danhSachThucThe = await truyVan
            .OrderBy(buoiHoc => buoiHoc.SessionDate)
            .ThenBy(buoiHoc => buoiHoc.StartTime)
            .ThenBy(buoiHoc => buoiHoc.Id)
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .ToListAsync(maHuy);

        var danhSachPhanHoi = danhSachThucThe.Select(ChuyenThanhPhanHoi).ToList();

        return new PagedResult<PhanHoiBuoiHoc>(danhSachPhanHoi, trang, kichThuocTrang, tongSo);
    }

    // [Chi tiết buổi học] Lấy thông tin buổi học theo mã định danh
    public async Task<PhanHoiBuoiHoc> LayChiTietBuoiHocTheoIdAsync(
        int maBuoiHoc,
        CancellationToken maHuy = default)
    {
        var buoiHoc = await ApDungPhamViXem(nguCanh.Sessions
            .AsNoTracking()
            .Include(item => item.Class)
            .Include(item => item.Lesson))
            .SingleOrDefaultAsync(item => item.Id == maBuoiHoc, maHuy)
            ?? throw new NotFoundException("Không tìm thấy buổi học.");

        return ChuyenThanhPhanHoi(buoiHoc);
    }

    // [Tạo buổi học] Thêm buổi học mới (kiểm tra lớp hoạt động, lịch GV, bài học hợp lệ)
    public async Task<PhanHoiBuoiHoc> TaoBuoiHocAsync(
        YeuCauTaoBuoiHoc yeuCau,
        CancellationToken maHuy = default)
    {
        KiemTraYeuCau(yeuCau.StartTime, yeuCau.EndTime, yeuCau.Note);

        var lopHoc = await nguCanh.Classes
            .SingleOrDefaultAsync(item => item.Id == yeuCau.ClassId, maHuy)
            ?? throw new NotFoundException("Không tìm thấy lớp học.");

        if (lopHoc.Status != ClassStatus.Active)
        {
            throw new ConflictException("Chỉ có thể tạo buổi học cho lớp học đang hoạt động.");
        }

        KiemTraNgayBuoiHoc(lopHoc, yeuCau.SessionDate);
        await DamBaoGiaoVienRanhAsync(
            lopHoc.MainTeacherUserId,
            yeuCau.SessionDate,
            yeuCau.StartTime,
            yeuCau.EndTime,
            null,
            maHuy);

        var baiHoc = await LayBaiHocChoLopAsync(yeuCau.LessonId, lopHoc.LevelId, maHuy);
        var buoiHoc = new Session
        {
            ClassId = lopHoc.Id,
            LessonId = baiHoc?.Id,
            SessionDate = yeuCau.SessionDate,
            StartTime = yeuCau.StartTime,
            EndTime = yeuCau.EndTime,
            Note = ChuanHoaGhiChu(yeuCau.Note)
        };

        nguCanh.Sessions.Add(buoiHoc);
        await nguCanh.SaveChangesAsync(maHuy);

        buoiHoc.Class = lopHoc;
        buoiHoc.Lesson = baiHoc;
        return ChuyenThanhPhanHoi(buoiHoc);
    }

    // [Cập nhật buổi học] Cập nhật thông tin khi buổi học ở trạng thái Scheduled
    public async Task<PhanHoiBuoiHoc> CapNhatBuoiHocAsync(
        int maBuoiHoc,
        YeuCauCapNhatBuoiHoc yeuCau,
        CancellationToken maHuy = default)
    {
        KiemTraYeuCau(yeuCau.StartTime, yeuCau.EndTime, yeuCau.Note);

        var buoiHoc = await nguCanh.Sessions
            .Include(item => item.Class)
            .Include(item => item.Lesson)
            .SingleOrDefaultAsync(item => item.Id == maBuoiHoc, maHuy)
            ?? throw new NotFoundException("Không tìm thấy buổi học.");

        if (buoiHoc.Status != SessionStatus.Scheduled)
        {
            throw new ConflictException("Chỉ có thể cập nhật buổi học đang ở trạng thái lên lịch.");
        }

        KiemTraNgayBuoiHoc(buoiHoc.Class, yeuCau.SessionDate);
        await DamBaoGiaoVienRanhAsync(
            buoiHoc.Class.MainTeacherUserId,
            yeuCau.SessionDate,
            yeuCau.StartTime,
            yeuCau.EndTime,
            buoiHoc.Id,
            maHuy);

        var baiHoc = await LayBaiHocChoLopAsync(yeuCau.LessonId, buoiHoc.Class.LevelId, maHuy);
        buoiHoc.LessonId = baiHoc?.Id;
        buoiHoc.Lesson = baiHoc;
        buoiHoc.SessionDate = yeuCau.SessionDate;
        buoiHoc.StartTime = yeuCau.StartTime;
        buoiHoc.EndTime = yeuCau.EndTime;
        buoiHoc.Note = ChuanHoaGhiChu(yeuCau.Note);

        await nguCanh.SaveChangesAsync(maHuy);
        return ChuyenThanhPhanHoi(buoiHoc);
    }

    // [Hủy buổi học] Chuyển trạng thái buổi học sang Cancelled
    public async Task<PhanHoiBuoiHoc> HuyBuoiHocAsync(
        int maBuoiHoc,
        CancellationToken maHuy = default)
    {
        var buoiHoc = await LayBuoiHocCoTrackingAsync(maBuoiHoc, maHuy);

        if (buoiHoc.Status != SessionStatus.Scheduled)
        {
            throw new ConflictException("Chỉ có thể hủy buổi học đang ở trạng thái lên lịch.");
        }

        buoiHoc.Status = SessionStatus.Cancelled;
        await nguCanh.SaveChangesAsync(maHuy);
        return ChuyenThanhPhanHoi(buoiHoc);
    }

    // [Hoàn thành buổi học] Chốt hoàn thành khi đã điểm danh đầy đủ học viên
    public async Task<PhanHoiBuoiHoc> HoanTatBuoiHocAsync(
        int maBuoiHoc,
        CancellationToken maHuy = default)
    {
        var buoiHoc = await LayBuoiHocCoTrackingAsync(maBuoiHoc, maHuy);

        if (buoiHoc.Status != SessionStatus.Scheduled)
        {
            throw new ConflictException("Chỉ có thể hoàn thành buổi học đang ở trạng thái lên lịch.");
        }

        KiemTraQuyenHoanTat(buoiHoc);

        var danhSachGhiDanhHopLe = await nguCanh.Enrollments
            .Where(ghiDanh =>
                ghiDanh.ClassId == buoiHoc.ClassId &&
                ghiDanh.Status == EnrollmentStatus.Active &&
                ghiDanh.StartDate <= buoiHoc.SessionDate &&
                (ghiDanh.EndDate == null || ghiDanh.EndDate >= buoiHoc.SessionDate))
            .Select(ghiDanh => ghiDanh.Id)
            .ToListAsync(maHuy);

        var danhSachDaDiemDanh = await nguCanh.Attendances
            .Where(diemDanh => diemDanh.SessionId == buoiHoc.Id)
            .Select(diemDanh => diemDanh.EnrollmentId)
            .ToListAsync(maHuy);

        var danhSachChuaDiemDanh = danhSachGhiDanhHopLe.Except(danhSachDaDiemDanh).ToList();
        if (danhSachChuaDiemDanh.Count > 0)
        {
            throw new ConflictException(
                $"Chưa hoàn tất điểm danh cho các mã ghi danh: {string.Join(", ", danhSachChuaDiemDanh)}.");
        }

        buoiHoc.Status = SessionStatus.Completed;
        await nguCanh.SaveChangesAsync(maHuy);
        return ChuyenThanhPhanHoi(buoiHoc);
    }

    // [Helper] Kiểm tra và lấy bài học hợp lệ theo cấp độ của lớp
    private async Task<Lesson?> LayBaiHocChoLopAsync(
        int? maBaiHoc,
        int maCapDoLop,
        CancellationToken maHuy)
    {
        if (!maBaiHoc.HasValue)
        {
            return null;
        }

        var baiHoc = await nguCanh.Lessons
            .SingleOrDefaultAsync(item => item.Id == maBaiHoc.Value, maHuy)
            ?? throw new NotFoundException("Không tìm thấy bài học.");

        if (baiHoc.LevelId != maCapDoLop)
        {
            throw new ValidationException("Bài học phải thuộc cùng cấp độ với lớp học của buổi học.");
        }

        return baiHoc;
    }

    // [Helper] Lấy entity buổi học có tracking để cập nhật
    private async Task<Session> LayBuoiHocCoTrackingAsync(int maBuoiHoc, CancellationToken maHuy)
    {
        return await nguCanh.Sessions
            .Include(item => item.Class)
            .Include(item => item.Lesson)
            .SingleOrDefaultAsync(item => item.Id == maBuoiHoc, maHuy)
            ?? throw new NotFoundException("Không tìm thấy buổi học.");
    }

    // [Helper] Kiểm tra xung đột lịch dạy của giáo viên phụ trách
    private async Task DamBaoGiaoVienRanhAsync(
        string maNguoiDungGiaoVien,
        DateOnly ngayBuoiHoc,
        TimeOnly gioBatDau,
        TimeOnly gioKetThuc,
        int? maBuoiHocLoaiTru,
        CancellationToken maHuy)
    {
        var biTrungLich = await nguCanh.Sessions
            .Where(buoiHoc =>
                buoiHoc.Class.MainTeacherUserId == maNguoiDungGiaoVien &&
                buoiHoc.SessionDate == ngayBuoiHoc &&
                buoiHoc.Status != SessionStatus.Cancelled &&
                (!maBuoiHocLoaiTru.HasValue || buoiHoc.Id != maBuoiHocLoaiTru.Value) &&
                buoiHoc.StartTime < gioKetThuc &&
                gioBatDau < buoiHoc.EndTime)
            .AnyAsync(maHuy);

        if (biTrungLich)
        {
            throw new ConflictException("Giáo viên phụ trách lớp đã có buổi học bị trùng thời gian.");
        }
    }

    // [Helper] Kiểm tra ngày học nằm trong thời gian hoạt động của lớp
    private static void KiemTraNgayBuoiHoc(Class thucTheLop, DateOnly ngayBuoiHoc)
    {
        if (ngayBuoiHoc < thucTheLop.StartDate ||
            (thucTheLop.EndDate.HasValue && ngayBuoiHoc > thucTheLop.EndDate.Value))
        {
            throw new ValidationException("Ngày học phải nằm trong khoảng thời gian diễn ra lớp học.");
        }
    }

    // [Helper] Kiểm tra quyền hoàn thành buổi học (Admin hoặc GV phụ trách)
    private void KiemTraQuyenHoanTat(Session buoiHoc)
    {
        var coQuyen = nguoiDungHienTai.Role == UserRole.Admin ||
            (nguoiDungHienTai.Role == UserRole.Teacher && nguoiDungHienTai.UserId == buoiHoc.Class.MainTeacherUserId);

        if (!coQuyen)
        {
            throw new ForbiddenAccessException("Bạn không có quyền hoàn thành buổi học này.");
        }
    }

    // [Helper] Phân quyền phạm vi dữ liệu đọc theo vai trò người dùng
    private IQueryable<Session> ApDungPhamViXem(IQueryable<Session> truyVan)
    {
        return nguoiDungHienTai.Role switch
        {
            UserRole.Admin => truyVan,
            UserRole.Teacher when !string.IsNullOrWhiteSpace(nguoiDungHienTai.UserId) =>
                truyVan.Where(buoiHoc => buoiHoc.Class.MainTeacherUserId == nguoiDungHienTai.UserId),
            UserRole.CustomerCare => truyVan,
            _ => throw new ForbiddenAccessException("Bạn không có quyền xem danh sách buổi học.")
        };
    }

    // [Helper] Kiểm tra tính hợp lệ của thời gian học và ghi chú
    private static void KiemTraYeuCau(TimeOnly gioBatDau, TimeOnly gioKetThuc, string? ghiChu)
    {
        if (gioBatDau >= gioKetThuc)
        {
            throw new ValidationException("Thời gian bắt đầu phải trước thời gian kết thúc.");
        }

        if (ghiChu?.Length > 500)
        {
            throw new ValidationException("Ghi chú không được vượt quá 500 ký tự.");
        }
    }

    // [Helper] Kiểm tra tham số phân trang
    private static void KiemTraPhanTrang(int trang, int kichThuocTrang)
    {
        if (trang < 1 || kichThuocTrang < 1 || kichThuocTrang > KichThuocTrangToiDa)
        {
            throw new ValidationException("Trang phải từ 1 trở lên và kích thước trang phải từ 1 đến 100.");
        }
    }

    // [Helper] Kiểm tra khoảng ngày tìm kiếm hợp lệ
    private static void KiemTraKhoangNgay(DateOnly? tuNgay, DateOnly? denNgay)
    {
        if (tuNgay.HasValue && denNgay.HasValue && tuNgay > denNgay)
        {
            throw new ValidationException("Từ ngày không được lớn hơn đến ngày.");
        }
    }

    // [Helper] Chuẩn hóa chuỗi ghi chú
    private static string? ChuanHoaGhiChu(string? ghiChu)
    {
        return string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
    }

    // [Helper] Chuyển đổi Session Entity sang PhanHoiBuoiHoc DTO
    private static PhanHoiBuoiHoc ChuyenThanhPhanHoi(Session buoiHoc)
    {
        return new PhanHoiBuoiHoc(
            buoiHoc.Id,
            buoiHoc.ClassId,
            buoiHoc.Class.ClassCode,
            buoiHoc.Class.Name,
            buoiHoc.LessonId,
            buoiHoc.Lesson?.Code,
            buoiHoc.Lesson?.Name,
            buoiHoc.SessionDate,
            buoiHoc.StartTime,
            buoiHoc.EndTime,
            buoiHoc.Status,
            buoiHoc.Note);
    }
}
