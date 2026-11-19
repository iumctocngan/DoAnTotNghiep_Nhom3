using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Curriculum;
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

    public async Task DongBoLichTuanAsync(int? maLop, string? giaoVienId, DateOnly tuNgay, DateOnly denNgay,
        CancellationToken maHuy = default)
    {
        if (denNgay.DayNumber - tuNgay.DayNumber is < 0 or > 6)
            throw new ValidationException("Khoảng lịch phải từ 1 đến 7 ngày.");

        switch (nguoiDungHienTai.Role)
        {
            case UserRole.Admin: break;
            case UserRole.Teacher when !string.IsNullOrWhiteSpace(nguoiDungHienTai.UserId) &&
                (giaoVienId == null || giaoVienId == nguoiDungHienTai.UserId):
                giaoVienId = nguoiDungHienTai.UserId;
                break;
            default:
                throw new ForbiddenAccessException("Bạn không có quyền đồng bộ lịch này.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (denNgay < today) return;
        if (tuNgay < today) tuNgay = today;
        var days = Enumerable.Range(tuNgay.DayNumber, denNgay.DayNumber - tuNgay.DayNumber + 1)
            .Select(DateOnly.FromDayNumber).ToDictionary(x => x.DayOfWeek);
        var weekdays = days.Keys.ToArray();

        await using var transaction = await nguCanh.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, maHuy);
        var classes = await nguCanh.Classes.AsNoTracking()
            .Where(x => x.Status == ClassStatus.Active && (maLop == null || x.Id == maLop) &&
                (giaoVienId == null || x.MainTeacherUserId == giaoVienId) && weekdays.Contains(x.DayOfWeek) &&
                x.StartDate <= denNgay && (x.EndDate == null || x.EndDate >= tuNgay)).ToListAsync(maHuy);
        var classIds = classes.Select(x => x.Id).ToArray();
        var sessions = (await nguCanh.Sessions
            .Where(x => classIds.Contains(x.ClassId) && x.SessionDate >= tuNgay && x.SessionDate <= denNgay)
            .OrderBy(x => x.Id).ToListAsync(maHuy)).ToLookup(x => (x.ClassId, x.SessionDate));
        var sessionIds = sessions.SelectMany(x => x).Select(x => x.Id).ToArray();
        var attendanceIds = await nguCanh.Attendances.Where(x => sessionIds.Contains(x.SessionId))
            .Select(x => x.SessionId).Distinct().ToListAsync(maHuy);
        var assessmentIds = await nguCanh.Set<Assessment>().Where(x => x.SessionId.HasValue && sessionIds.Contains(x.SessionId.Value))
            .Select(x => x.SessionId!.Value).Distinct().ToListAsync(maHuy);
        var protectedSessions = attendanceIds.Concat(assessmentIds).ToHashSet();
        var levelIds = classes.Select(x => x.LevelId).Distinct().ToArray();
        var lessons = (await nguCanh.Lessons.AsNoTracking().Where(x => levelIds.Contains(x.LevelId) && x.IsActive)
            .ToListAsync(maHuy)).GroupBy(x => x.LevelId).SelectMany(x => LessonOrdering.ByNumber(x)).ToLookup(x => x.LevelId);
        var usedLessons = (await nguCanh.Sessions.AsNoTracking().Where(x => classIds.Contains(x.ClassId) &&
            x.LessonId != null && x.Status != SessionStatus.Cancelled)
            .Select(x => new { x.ClassId, x.LessonId }).Distinct().ToListAsync(maHuy))
            .ToHashSet();

        foreach (var lop in classes)
        {
            var date = days[lop.DayOfWeek];
            if (date < lop.StartDate || date > lop.EndDate) continue;
            var slot = sessions[(lop.Id, date)];
            var regular = slot.FirstOrDefault(x => x.IsRegular == true) ?? slot.FirstOrDefault(x => x.IsRegular == null);
            if (regular != null)
            {
                if (regular.Status == SessionStatus.Scheduled && !protectedSessions.Contains(regular.Id))
                {
                    regular.IsRegular = true;
                    regular.StartTime = lop.StartTime;
                    regular.EndTime = lop.EndTime;
                }
                continue;
            }

            if (slot.Any(x => x.Status != SessionStatus.Cancelled && x.StartTime < lop.EndTime && lop.StartTime < x.EndTime))
                continue;

            var curriculum = lessons[lop.LevelId].ToList();
            var startingIndex = lop.StartingLessonId is int startId ? curriculum.FindIndex(x => x.Id == startId) : 0;
            var lesson = curriculum.Skip(startingIndex < 0 ? curriculum.Count : startingIndex).FirstOrDefault(x => !usedLessons.Contains(new { ClassId = lop.Id, LessonId = (int?)x.Id }));
            nguCanh.Sessions.Add(new Session
            {
                ClassId = lop.Id, SessionDate = date, StartTime = lop.StartTime, EndTime = lop.EndTime,
                LessonId = lesson?.Id, IsRegular = true
            });
        }
        await nguCanh.SaveChangesAsync(maHuy);
        await transaction.CommitAsync(maHuy);
    }

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
            IsRegular = false,
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
        if (buoiHoc.IsRegular == true && (yeuCau.SessionDate != buoiHoc.SessionDate ||
            yeuCau.StartTime != buoiHoc.StartTime || yeuCau.EndTime != buoiHoc.EndTime))
            throw new ConflictException("Buổi học định kỳ lấy giờ từ lớp. Để đổi riêng buổi này, hãy hủy buổi và thêm buổi học bù.");

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
                (ghiDanh.Student.RemainingSessions > 0 || ghiDanh.Attendances.Any(a => a.SessionId == buoiHoc.Id)) &&
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
            buoiHoc.Note,
            buoiHoc.IsRegular == true);
    }
}
