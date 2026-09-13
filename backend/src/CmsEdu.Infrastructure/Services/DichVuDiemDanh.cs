using CmsEdu.Application.Attendances;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ValidationException = CmsEdu.Application.Common.Exceptions.ValidationException;

namespace CmsEdu.Infrastructure.Services;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ điểm danh (Attendance) cho buổi học.
/// </summary>
public class DichVuDiemDanh(AppDbContext nguCanh, ICurrentUser nguoiDungHienTai) : IDichVuDiemDanh
{
    // [Lấy danh sách điểm danh] Lấy danh sách toàn bộ học viên hợp lệ tại ngày học kèm trạng thái điểm danh
    public async Task<PhanHoiDiemDanhBuoiHoc> LayDanhSachDiemDanhTheoBuoiHocAsync(
        int maBuoiHoc,
        CancellationToken maHuy = default)
    {
        var buoiHoc = await nguCanh.Sessions
            .AsNoTracking()
            .Include(item => item.Class)
            .SingleOrDefaultAsync(item => item.Id == maBuoiHoc, maHuy)
            ?? throw new NotFoundException("Không tìm thấy buổi học.");

        KiemTraQuyenQuanLyDiemDanh(buoiHoc);

        var danhSachGhiDanhHopLe = await LayDanhSachGhiDanhHopLeAsync(buoiHoc.ClassId, buoiHoc.SessionDate, maHuy);

        var danhSachDiemDanhHienCo = await nguCanh.Attendances
            .AsNoTracking()
            .Where(item => item.SessionId == buoiHoc.Id)
            .ToListAsync(maHuy);

        return TaoPhanHoiDiemDanh(buoiHoc, danhSachGhiDanhHopLe, danhSachDiemDanhHienCo);
    }

    // [Lưu điểm danh theo lô] Lưu điểm danh đồng thời cho toàn bộ học viên hợp lệ trong transaction
    public async Task<PhanHoiDiemDanhBuoiHoc> LuuDiemDanhTheoBuoiHocAsync(
        int maBuoiHoc,
        YeuCauLuuDiemDanhBuoiHoc yeuCau,
        CancellationToken maHuy = default)
    {
        if (yeuCau?.Items == null)
        {
            throw new ValidationException("Danh sách điểm danh không được để trống.");
        }

        var buoiHoc = await nguCanh.Sessions
            .Include(item => item.Class)
            .SingleOrDefaultAsync(item => item.Id == maBuoiHoc, maHuy)
            ?? throw new NotFoundException("Không tìm thấy buổi học.");

        KiemTraQuyenQuanLyDiemDanh(buoiHoc);

        if (buoiHoc.Status == SessionStatus.Cancelled)
        {
            throw new ConflictException("Không thể điểm danh cho buổi học đã bị hủy.");
        }

        if (buoiHoc.Status == SessionStatus.Completed)
        {
            throw new ConflictException("Buổi học đã hoàn thành, không thể chỉnh sửa điểm danh.");
        }

        KiemTraDanhSachYeuCauHopLe(yeuCau.Items);

        var danhSachGhiDanhHopLe = await LayDanhSachGhiDanhHopLeAsync(buoiHoc.ClassId, buoiHoc.SessionDate, maHuy);

        var tapHopHopLe = danhSachGhiDanhHopLe.Select(item => item.Id).ToHashSet();
        var tapHopYeuCau = yeuCau.Items.Select(item => item.EnrollmentId).ToHashSet();

        if (!tapHopHopLe.SetEquals(tapHopYeuCau))
        {
            var thieu = tapHopHopLe.Except(tapHopYeuCau).ToList();
            var thua = tapHopYeuCau.Except(tapHopHopLe).ToList();

            if (thieu.Count > 0)
            {
                throw new ConflictException(
                    $"Danh sách điểm danh chưa bao phủ đầy đủ học viên hợp lệ của buổi học. Thiếu ghi danh: {string.Join(", ", thieu)}.");
            }

            if (thua.Count > 0)
            {
                throw new ConflictException(
                    $"Danh sách điểm danh chứa ghi danh không hợp lệ hoặc không thuộc lớp tại ngày học: {string.Join(", ", thua)}.");
            }
        }

        var thoiDiemThucHien = DateTime.UtcNow;
        var nguoiThucHien = nguoiDungHienTai.UserId ?? "System";

        // Thực hiện lưu trong Transaction bắt buộc
        if (nguCanh.Database.IsRelational())
        {
            await using var tx = await nguCanh.Database.BeginTransactionAsync(maHuy);
            await CapNhatDuLieuDiemDanhAsync(buoiHoc.Id, yeuCau.Items, nguoiThucHien, thoiDiemThucHien, maHuy);
            await nguCanh.SaveChangesAsync(maHuy);
            await tx.CommitAsync(maHuy);
        }
        else
        {
            await CapNhatDuLieuDiemDanhAsync(buoiHoc.Id, yeuCau.Items, nguoiThucHien, thoiDiemThucHien, maHuy);
            await nguCanh.SaveChangesAsync(maHuy);
        }

        var danhSachDiemDanhMoiNhat = await nguCanh.Attendances
            .AsNoTracking()
            .Where(item => item.SessionId == buoiHoc.Id)
            .ToListAsync(maHuy);

        return TaoPhanHoiDiemDanh(buoiHoc, danhSachGhiDanhHopLe, danhSachDiemDanhMoiNhat);
    }

    // [Helper] Cập nhật hoặc thêm mới các bản ghi điểm danh
    private async Task CapNhatDuLieuDiemDanhAsync(
        int maBuoiHoc,
        IReadOnlyList<YeuCauLuuDiemDanhChiTiet> danhSachChiTiet,
        string nguoiThucHien,
        DateTime thoiDiemThucHien,
        CancellationToken maHuy)
    {
        var danhSachHienCo = await nguCanh.Attendances
            .Where(item => item.SessionId == maBuoiHoc)
            .ToListAsync(maHuy);

        var mapHienCo = danhSachHienCo.ToDictionary(item => item.EnrollmentId);

        foreach (var item in danhSachChiTiet)
        {
            var ghiChuChuanHoa = ChuanHoaGhiChu(item.Note);

            if (mapHienCo.TryGetValue(item.EnrollmentId, out var banGhiDiemDanh))
            {
                banGhiDiemDanh.Status = item.Status;
                banGhiDiemDanh.Note = ghiChuChuanHoa;
                banGhiDiemDanh.UpdatedBy = nguoiThucHien;
                banGhiDiemDanh.UpdatedAt = thoiDiemThucHien;
            }
            else
            {
                var banGhiMoi = new Attendance
                {
                    SessionId = maBuoiHoc,
                    EnrollmentId = item.EnrollmentId,
                    Status = item.Status,
                    Note = ghiChuChuanHoa,
                    MarkedBy = nguoiThucHien,
                    MarkedAt = thoiDiemThucHien
                };
                nguCanh.Attendances.Add(banGhiMoi);
            }
        }
    }

    // [Helper] Kiểm tra quyền hạn quản lý điểm danh của người dùng (Admin hoặc Giáo viên chính của lớp)
    private void KiemTraQuyenQuanLyDiemDanh(Session buoiHoc)
    {
        var coQuyen = nguoiDungHienTai.Role == UserRole.Admin ||
            (nguoiDungHienTai.Role == UserRole.Teacher && nguoiDungHienTai.UserId == buoiHoc.Class.MainTeacherUserId);

        if (!coQuyen)
        {
            throw new ForbiddenAccessException("Bạn không có quyền quản lý điểm danh cho buổi học này.");
        }
    }

    // [Helper] Lấy danh sách Enrollment hợp lệ của lớp tại ngày diễn ra buổi học
    private async Task<List<Enrollment>> LayDanhSachGhiDanhHopLeAsync(
        int maLop,
        DateOnly ngayBuoiHoc,
        CancellationToken maHuy)
    {
        return await nguCanh.Enrollments
            .AsNoTracking()
            .Include(item => item.Student)
            .Where(item =>
                item.ClassId == maLop &&
                item.Status == EnrollmentStatus.Active &&
                item.StartDate <= ngayBuoiHoc &&
                (item.EndDate == null || item.EndDate >= ngayBuoiHoc))
            .OrderBy(item => item.Student.FullName)
            .ThenBy(item => item.Student.StudentCode)
            .ToListAsync(maHuy);
    }

    // [Helper] Kiểm tra tính hợp lệ về định dạng và trùng lặp của dữ liệu gửi lên
    private static void KiemTraDanhSachYeuCauHopLe(IReadOnlyList<YeuCauLuuDiemDanhChiTiet> items)
    {
        var danhSachMa = items.Select(item => item.EnrollmentId).ToList();
        if (danhSachMa.Distinct().Count() != danhSachMa.Count)
        {
            throw new ValidationException("Danh sách điểm danh gửi lên chứa mã ghi danh bị trùng lặp.");
        }

        foreach (var item in items)
        {
            if (item.Note?.Length > 500)
            {
                throw new ValidationException(
                    $"Ghi chú của ghi danh {item.EnrollmentId} không được vượt quá 500 ký tự.");
            }

            if (!Enum.IsDefined(typeof(AttendanceStatus), item.Status))
            {
                throw new ValidationException(
                    $"Trạng thái điểm danh của ghi danh {item.EnrollmentId} không hợp lệ.");
            }
        }
    }

    // [Helper] Chuẩn hóa chuỗi ghi chú
    private static string? ChuanHoaGhiChu(string? ghiChu)
    {
        return string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
    }

    // [Helper] Tạo đối tượng DTO phản hồi tổng hợp
    private static PhanHoiDiemDanhBuoiHoc TaoPhanHoiDiemDanh(
        Session buoiHoc,
        IReadOnlyList<Enrollment> danhSachGhiDanhHopLe,
        IReadOnlyList<Attendance> danhSachDiemDanh)
    {
        var mapDiemDanh = danhSachDiemDanh.ToDictionary(item => item.EnrollmentId);

        var danhSachHocVien = danhSachGhiDanhHopLe.Select(ghiDanh =>
        {
            mapDiemDanh.TryGetValue(ghiDanh.Id, out var diemDanh);

            return new PhanHoiDiemDanhHocVien(
                ghiDanh.Id,
                ghiDanh.StudentId,
                ghiDanh.Student.StudentCode,
                ghiDanh.Student.FullName,
                diemDanh?.Id,
                diemDanh?.Status,
                diemDanh?.Note,
                diemDanh?.MarkedBy,
                diemDanh?.MarkedAt,
                diemDanh?.UpdatedBy,
                diemDanh?.UpdatedAt);
        }).ToList();

        var soLuongCoMat = danhSachHocVien.Count(item => item.Status == AttendanceStatus.Present);
        var soLuongVangMat = danhSachHocVien.Count(item => item.Status == AttendanceStatus.Absent);
        var soLuongChuaDiemDanh = danhSachHocVien.Count(item => !item.Status.HasValue);

        return new PhanHoiDiemDanhBuoiHoc(
            buoiHoc.Id,
            buoiHoc.ClassId,
            buoiHoc.Class.ClassCode,
            buoiHoc.Class.Name,
            buoiHoc.SessionDate,
            buoiHoc.StartTime,
            buoiHoc.EndTime,
            buoiHoc.Status,
            danhSachHocVien.Count,
            soLuongCoMat,
            soLuongVangMat,
            soLuongChuaDiemDanh,
            danhSachHocVien);
    }
}
