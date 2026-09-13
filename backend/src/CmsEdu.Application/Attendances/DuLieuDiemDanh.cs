using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Attendances;

/// <summary>
/// DTO ghi nhận điểm danh cho từng học viên (ghi danh) trong buổi học.
/// </summary>
public sealed record YeuCauLuuDiemDanhChiTiet(
    int EnrollmentId,
    AttendanceStatus Status,
    string? Note);

/// <summary>
/// DTO yêu cầu lưu điểm danh theo lô cho toàn bộ học viên hợp lệ của buổi học.
/// </summary>
public sealed record YeuCauLuuDiemDanhBuoiHoc(
    IReadOnlyList<YeuCauLuuDiemDanhChiTiet> Items);

/// <summary>
/// DTO thông tin điểm danh chi tiết của từng học viên trong buổi học.
/// </summary>
public sealed record PhanHoiDiemDanhHocVien(
    int EnrollmentId,
    int StudentId,
    string StudentCode,
    string StudentFullName,
    int? AttendanceId,
    AttendanceStatus? Status,
    string? Note,
    string? MarkedBy,
    DateTime? MarkedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt);

/// <summary>
/// DTO phản hồi tổng hợp thông tin điểm danh của một buổi học.
/// </summary>
public sealed record PhanHoiDiemDanhBuoiHoc(
    int SessionId,
    int ClassId,
    string ClassCode,
    string ClassName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    SessionStatus SessionStatus,
    int TongSoHocVien,
    int SoLuongCoMat,
    int SoLuongVangMat,
    int SoLuongChuaDiemDanh,
    IReadOnlyList<PhanHoiDiemDanhHocVien> DanhSachHocVien);
