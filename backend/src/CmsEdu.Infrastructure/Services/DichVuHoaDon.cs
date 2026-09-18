using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Invoices;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ValidationException = CmsEdu.Application.Common.Exceptions.ValidationException;

namespace CmsEdu.Infrastructure.Services;

/// <summary>
/// Cài đặt dịch vụ xử lý nghiệp vụ hóa đơn học phí (Invoice).
/// </summary>
public class DichVuHoaDon(AppDbContext nguCanh, ICurrentUser nguoiDungHienTai) : IDichVuHoaDon
{
    private const int KichThuocTrangToiDa = 100;

    // [Danh sách hóa đơn] Lọc theo enrollment, học sinh, trạng thái và phân trang
    public async Task<PagedResult<PhanHoiHoaDon>> LayDanhSachHoaDonAsync(
        int? enrollmentId,
        int? studentId,
        InvoiceStatus? trangThai,
        int trang,
        int kichThuocTrang,
        CancellationToken maHuy = default)
    {
        KiemTraPhanTrang(trang, kichThuocTrang);
        KiemTraQuyenKeToan();

        var truyVan = nguCanh.Invoices
            .AsNoTracking()
            .Include(hd => hd.Enrollment)
                .ThenInclude(gd => gd.Student)
            .Include(hd => hd.Payments)
            .AsQueryable();

        if (enrollmentId.HasValue)
            truyVan = truyVan.Where(hd => hd.EnrollmentId == enrollmentId.Value);

        if (studentId.HasValue)
            truyVan = truyVan.Where(hd => hd.Enrollment.StudentId == studentId.Value);

        if (trangThai.HasValue)
            truyVan = truyVan.Where(hd => hd.Status == trangThai.Value);

        var tongSo = await truyVan.CountAsync(maHuy);
        var danhSach = await truyVan
            .OrderByDescending(hd => hd.PeriodStart)
            .ThenByDescending(hd => hd.Id)
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .ToListAsync(maHuy);

        return new PagedResult<PhanHoiHoaDon>(
            danhSach.Select(ChuyenThanhPhanHoi).ToList(),
            trang, kichThuocTrang, tongSo);
    }

    // [Chi tiết hóa đơn] Lấy thông tin đầy đủ kèm tổng thanh toán đã xác nhận
    public async Task<PhanHoiHoaDon> LayChiTietHoaDonAsync(
        int maHoaDon,
        CancellationToken maHuy = default)
    {
        KiemTraQuyenKeToan();

        var hoaDon = await LayHoaDonAsync(maHoaDon, coTracking: false, maHuy)
            ?? throw new NotFoundException("Không tìm thấy hóa đơn.");

        return ChuyenThanhPhanHoi(hoaDon);
    }

    // [Tạo hóa đơn] Kiểm tra đầy đủ ràng buộc nghiệp vụ trước khi tạo
    public async Task<PhanHoiHoaDon> TaoHoaDonAsync(
        YeuCauTaoHoaDon yeuCau,
        CancellationToken maHuy = default)
    {
        KiemTraQuyenKeToan();

        // --- Kiểm tra enrollment tồn tại và đang Active ---
        var ghiDanh = await nguCanh.Enrollments
            .Include(gd => gd.Student)
            .SingleOrDefaultAsync(gd => gd.Id == yeuCau.EnrollmentId, maHuy)
            ?? throw new NotFoundException("Không tìm thấy ghi danh.");

        if (ghiDanh.Status != EnrollmentStatus.Active)
            throw new ConflictException(
                "Chỉ có thể lập hóa đơn cho ghi danh đang hoạt động (Active).");

        // --- Kiểm tra AmountDue > 0 ---
        if (yeuCau.AmountDue <= 0)
            throw new ValidationException("Số tiền phải thu phải lớn hơn 0.");

        // --- Tính PeriodEnd = PeriodStart + 6 tháng - 1 ngày ---
        var ngayKetThucKy = yeuCau.PeriodStart.AddMonths(6).AddDays(-1);

        // --- Kiểm tra DueDate <= PeriodEnd ---
        if (yeuCau.DueDate > ngayKetThucKy)
            throw new ValidationException(
                $"Ngày đến hạn không được sau ngày kết thúc kỳ học phí ({ngayKetThucKy:dd/MM/yyyy}).");

        // --- Kiểm tra kỳ không chồng lấn với các hóa đơn chưa hủy của enrollment ---
        var biTrungKy = await nguCanh.Invoices
            .AnyAsync(hd =>
                hd.EnrollmentId == yeuCau.EnrollmentId &&
                hd.Status != InvoiceStatus.Cancelled &&
                hd.PeriodStart <= ngayKetThucKy &&
                hd.PeriodEnd >= yeuCau.PeriodStart,
                maHuy);

        if (biTrungKy)
            throw new ConflictException(
                "Kỳ học phí bị chồng lấn với hóa đơn đã tồn tại của ghi danh này.");

        // --- Tạo hóa đơn & Ghi AuditLog trong Transaction ---
        var soHoaDon = await TaoSoHoaDonAsync(yeuCau.PeriodStart, maHuy);
        var hoaDon = new Invoice
        {
            InvoiceNumber = soHoaDon,
            EnrollmentId  = yeuCau.EnrollmentId,
            PeriodStart   = yeuCau.PeriodStart,
            PeriodEnd     = ngayKetThucKy,
            AmountDue     = yeuCau.AmountDue,
            DueDate       = yeuCau.DueDate,
            Status        = InvoiceStatus.Issued,
            Note          = ChuanHoaGhiChu(yeuCau.Note),
            CreatedBy     = nguoiDungHienTai.UserId ?? string.Empty,
            CreatedAt     = DateTime.UtcNow
        };

        if (nguCanh.Database.IsRelational())
        {
            await using var tx = await nguCanh.Database.BeginTransactionAsync(maHuy);
            try
            {
                nguCanh.Invoices.Add(hoaDon);
                await nguCanh.SaveChangesAsync(maHuy);

                GhiAuditLog("CREATE_INVOICE",
                    hoaDon.Id.ToString(),
                    $"Tạo hóa đơn {soHoaDon} cho ghi danh {yeuCau.EnrollmentId}, kỳ {yeuCau.PeriodStart:yyyy-MM-dd} đến {ngayKetThucKy:yyyy-MM-dd}, số tiền {yeuCau.AmountDue:N0} VNĐ.");
                await nguCanh.SaveChangesAsync(maHuy);

                await tx.CommitAsync(maHuy);
            }
            catch
            {
                await tx.RollbackAsync(maHuy);
                throw;
            }
        }
        else
        {
            nguCanh.Invoices.Add(hoaDon);
            await nguCanh.SaveChangesAsync(maHuy);

            GhiAuditLog("CREATE_INVOICE",
                hoaDon.Id.ToString(),
                $"Tạo hóa đơn {soHoaDon} cho ghi danh {yeuCau.EnrollmentId}, kỳ {yeuCau.PeriodStart:yyyy-MM-dd} đến {ngayKetThucKy:yyyy-MM-dd}, số tiền {yeuCau.AmountDue:N0} VNĐ.");
            await nguCanh.SaveChangesAsync(maHuy);
        }

        hoaDon.Enrollment = ghiDanh;
        return ChuyenThanhPhanHoi(hoaDon);
    }

    // [Hủy hóa đơn] Không xóa vật lý — bắt buộc có lý do và ghi audit log trong Transaction
    public async Task<PhanHoiHoaDon> HuyHoaDonAsync(
        int maHoaDon,
        YeuCauHuyHoaDon yeuCau,
        CancellationToken maHuy = default)
    {
        KiemTraQuyenKeToan();

        if (string.IsNullOrWhiteSpace(yeuCau.LyDoHuy))
            throw new ValidationException("Lý do hủy không được để trống.");

        if (yeuCau.LyDoHuy.Trim().Length > 500)
            throw new ValidationException("Lý do hủy không được vượt quá 500 ký tự.");

        var hoaDon = await LayHoaDonAsync(maHoaDon, coTracking: true, maHuy)
            ?? throw new NotFoundException("Không tìm thấy hóa đơn.");

        if (hoaDon.Status == InvoiceStatus.Cancelled)
            throw new ConflictException("Hóa đơn này đã bị hủy trước đó.");

        if (hoaDon.Status == InvoiceStatus.Paid)
            throw new ConflictException("Không thể hủy hóa đơn đã thanh toán đủ.");

        if (hoaDon.Payments.Any(p => p.Status == PaymentStatus.Confirmed))
            throw new ConflictException("Không thể hủy hóa đơn đã phát sinh thanh toán được xác nhận.");

        // --- Cập nhật trạng thái hủy ---
        hoaDon.Status       = InvoiceStatus.Cancelled;
        hoaDon.CancelledBy  = nguoiDungHienTai.UserId;
        hoaDon.CancelledAt  = DateTime.UtcNow;
        hoaDon.CancelReason = yeuCau.LyDoHuy.Trim();

        if (nguCanh.Database.IsRelational())
        {
            await using var tx = await nguCanh.Database.BeginTransactionAsync(maHuy);
            try
            {
                GhiAuditLog("CANCEL_INVOICE",
                    hoaDon.Id.ToString(),
                    $"Hủy hóa đơn {hoaDon.InvoiceNumber}. Lý do: {yeuCau.LyDoHuy.Trim()}");

                await nguCanh.SaveChangesAsync(maHuy);
                await tx.CommitAsync(maHuy);
            }
            catch
            {
                await tx.RollbackAsync(maHuy);
                throw;
            }
        }
        else
        {
            GhiAuditLog("CANCEL_INVOICE",
                hoaDon.Id.ToString(),
                $"Hủy hóa đơn {hoaDon.InvoiceNumber}. Lý do: {yeuCau.LyDoHuy.Trim()}");

            await nguCanh.SaveChangesAsync(maHuy);
        }

        return ChuyenThanhPhanHoi(hoaDon);
    }

    // [Danh sách theo học sinh] Lấy toàn bộ hóa đơn của một học sinh, mới nhất trước
    public async Task<PagedResult<PhanHoiHoaDon>> LayDanhSachHoaDonTheoHocVienAsync(
        int studentId,
        int trang,
        int kichThuocTrang,
        CancellationToken maHuy = default)
    {
        KiemTraPhanTrang(trang, kichThuocTrang);
        KiemTraQuyenKeToan();

        _ = await nguCanh.Students
            .SingleOrDefaultAsync(hv => hv.Id == studentId, maHuy)
            ?? throw new NotFoundException("Không tìm thấy học sinh.");

        var truyVan = nguCanh.Invoices
            .AsNoTracking()
            .Include(hd => hd.Enrollment)
                .ThenInclude(gd => gd.Student)
            .Include(hd => hd.Payments)
            .Where(hd => hd.Enrollment.StudentId == studentId);

        var tongSo = await truyVan.CountAsync(maHuy);
        var danhSach = await truyVan
            .OrderByDescending(hd => hd.PeriodStart)
            .ThenByDescending(hd => hd.Id)
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .ToListAsync(maHuy);

        return new PagedResult<PhanHoiHoaDon>(
            danhSach.Select(ChuyenThanhPhanHoi).ToList(),
            trang, kichThuocTrang, tongSo);
    }

    // [Công nợ học sinh] Debt = Σ AmountDue (chưa hủy) - Σ Amount (Confirmed)
    public async Task<PhanHoiCongNo> TinhCongNoHocVienAsync(
        int studentId,
        CancellationToken maHuy = default)
    {
        KiemTraQuyenKeToan();

        var hocVien = await nguCanh.Students
            .SingleOrDefaultAsync(hv => hv.Id == studentId, maHuy)
            ?? throw new NotFoundException("Không tìm thấy học sinh.");

        var danhSachHoaDon = await nguCanh.Invoices
            .AsNoTracking()
            .Include(hd => hd.Enrollment)
                .ThenInclude(gd => gd.Student)
            .Include(hd => hd.Payments)
            .Where(hd => hd.Enrollment.StudentId == studentId)
            .OrderByDescending(hd => hd.PeriodStart)
            .ToListAsync(maHuy);

        var tongPhaiTra = danhSachHoaDon
            .Where(hd => hd.Status != InvoiceStatus.Cancelled)
            .Sum(hd => hd.AmountDue);

        var tongDaXacNhan = danhSachHoaDon
            .SelectMany(hd => hd.Payments)
            .Where(tt => tt.Status == PaymentStatus.Confirmed)
            .Sum(tt => tt.Amount);

        var conNo = tongPhaiTra - tongDaXacNhan;

        var coQuaHan = danhSachHoaDon.Any(hd =>
            hd.Status != InvoiceStatus.Cancelled &&
            hd.Status != InvoiceStatus.Paid &&
            hd.DueDate < DateOnly.FromDateTime(DateTime.UtcNow));

        return new PhanHoiCongNo(
            studentId,
            hocVien.FullName,
            hocVien.StudentCode,
            tongPhaiTra,
            tongDaXacNhan,
            conNo,
            coQuaHan,
            danhSachHoaDon.Select(ChuyenThanhPhanHoi).ToList());
    }

    // [Dashboard] Danh sách học sinh còn công nợ chưa thanh toán
    public async Task<PagedResult<PhanHoiHocVienConNo>> LayDanhSachHocVienConNoAsync(
        int trang,
        int kichThuocTrang,
        CancellationToken maHuy = default)
    {
        KiemTraPhanTrang(trang, kichThuocTrang);
        KiemTraQuyenKeToan();

        var ngayHienTai = DateOnly.FromDateTime(DateTime.UtcNow);

        // Nhóm hóa đơn theo học sinh, tính tổng phải thu và tổng đã xác nhận
        var danhSach = await nguCanh.Invoices
            .AsNoTracking()
            .Include(hd => hd.Enrollment)
                .ThenInclude(gd => gd.Student)
            .Include(hd => hd.Payments)
            .Where(hd => hd.Status != InvoiceStatus.Cancelled)
            .GroupBy(hd => hd.Enrollment.Student)
            .Select(nhom => new
            {
                HocVien      = nhom.Key,
                TongPhaiTra  = nhom.Sum(hd => hd.AmountDue),
                TongXacNhan  = nhom.SelectMany(hd => hd.Payments)
                                   .Where(tt => tt.Status == PaymentStatus.Confirmed)
                                   .Sum(tt => tt.Amount),
                CoQuaHan     = nhom.Any(hd =>
                                   hd.Status != InvoiceStatus.Paid &&
                                   hd.DueDate < ngayHienTai),
                SoChuaTra    = nhom.Count(hd =>
                                   hd.Status != InvoiceStatus.Paid &&
                                   hd.Status != InvoiceStatus.Cancelled)
            })
            .ToListAsync(maHuy);

        // Chỉ lấy những học sinh còn thực sự nợ (ConNo > 0)
        var conNoList = danhSach
            .Where(x => x.TongPhaiTra - x.TongXacNhan > 0)
            .OrderByDescending(x => x.TongPhaiTra - x.TongXacNhan)
            .ToList();

        var tongSo = conNoList.Count;

        var trang_data = conNoList
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .Select(x => new PhanHoiHocVienConNo(
                x.HocVien.Id,
                x.HocVien.FullName,
                x.HocVien.StudentCode,
                x.TongPhaiTra - x.TongXacNhan,
                x.CoQuaHan,
                x.SoChuaTra))
            .ToList();

        return new PagedResult<PhanHoiHocVienConNo>(trang_data, trang, kichThuocTrang, tongSo);
    }

    // -------------------------------------------------------------------------
    // HELPER: Lấy hóa đơn kèm Include cần thiết
    // -------------------------------------------------------------------------
    private async Task<Invoice?> LayHoaDonAsync(int maHoaDon, bool coTracking, CancellationToken maHuy)
    {
        var truyVan = nguCanh.Invoices
            .Include(hd => hd.Enrollment)
                .ThenInclude(gd => gd.Student)
            .Include(hd => hd.Payments)
            .AsQueryable();

        if (!coTracking)
            truyVan = truyVan.AsNoTracking();

        return await truyVan
            .SingleOrDefaultAsync(hd => hd.Id == maHoaDon, maHuy);
    }

    // -------------------------------------------------------------------------
    // HELPER: Tự động sinh số hóa đơn dạng INV-YYYYMM-XXXX
    // -------------------------------------------------------------------------
    private async Task<string> TaoSoHoaDonAsync(DateOnly ngayBatDauKy, CancellationToken maHuy)
    {
        var tienTo = $"INV-{ngayBatDauKy:yyyyMM}-";

        // Đếm số hóa đơn đã tồn tại cùng tháng để tạo số thứ tự
        var soHienCo = await nguCanh.Invoices
            .CountAsync(hd => hd.InvoiceNumber.StartsWith(tienTo), maHuy);

        var soThuTu = (soHienCo + 1).ToString("D4");
        return $"{tienTo}{soThuTu}";
    }

    // -------------------------------------------------------------------------
    // HELPER: Ghi audit log cho các thao tác quan trọng
    // -------------------------------------------------------------------------
    private void GhiAuditLog(string hanhDong, string entityId, string moTa)
    {
        nguCanh.AuditLogs.Add(new AuditLog
        {
            UserId      = nguoiDungHienTai.UserId,
            Action      = hanhDong,
            EntityType  = "Invoice",
            EntityId    = entityId,
            Description = moTa,
            OccurredAt  = DateTime.UtcNow
        });
    }

    // -------------------------------------------------------------------------
    // HELPER: Kiểm tra quyền — chỉ Admin và Accountant được thao tác hóa đơn
    // -------------------------------------------------------------------------
    private void KiemTraQuyenKeToan()
    {
        if (nguoiDungHienTai.Role != UserRole.Admin &&
            nguoiDungHienTai.Role != UserRole.Accountant)
        {
            throw new ForbiddenAccessException("Bạn không có quyền thao tác hóa đơn.");
        }
    }

    // -------------------------------------------------------------------------
    // HELPER: Kiểm tra tham số phân trang
    // -------------------------------------------------------------------------
    private static void KiemTraPhanTrang(int trang, int kichThuocTrang)
    {
        if (trang < 1 || kichThuocTrang < 1 || kichThuocTrang > KichThuocTrangToiDa)
            throw new ValidationException("Trang phải từ 1 trở lên và kích thước trang phải từ 1 đến 100.");
    }

    // -------------------------------------------------------------------------
    // HELPER: Chuẩn hóa chuỗi ghi chú
    // -------------------------------------------------------------------------
    private static string? ChuanHoaGhiChu(string? ghiChu)
        => string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();

    // -------------------------------------------------------------------------
    // HELPER: Chuyển đổi Invoice Entity → PhanHoiHoaDon DTO
    // -------------------------------------------------------------------------
    private static PhanHoiHoaDon ChuyenThanhPhanHoi(Invoice hoaDon)
    {
        var tongDaXacNhan = hoaDon.Payments
            .Where(tt => tt.Status == PaymentStatus.Confirmed)
            .Sum(tt => tt.Amount);

        var conNo = hoaDon.Status == InvoiceStatus.Cancelled
            ? 0m
            : hoaDon.AmountDue - tongDaXacNhan;

        return new PhanHoiHoaDon(
            hoaDon.Id,
            hoaDon.InvoiceNumber,
            hoaDon.EnrollmentId,
            hoaDon.Enrollment.StudentId,
            hoaDon.Enrollment.Student.FullName,
            hoaDon.Enrollment.Student.StudentCode,
            hoaDon.PeriodStart,
            hoaDon.PeriodEnd,
            hoaDon.AmountDue,
            hoaDon.DueDate,
            hoaDon.Status,
            tongDaXacNhan,
            conNo,
            hoaDon.Note,
            hoaDon.CreatedBy,
            hoaDon.CreatedAt,
            hoaDon.CancelledBy,
            hoaDon.CancelledAt,
            hoaDon.CancelReason);
    }
}
