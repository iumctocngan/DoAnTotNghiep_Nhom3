using System.Data;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Payments;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ValidationException = CmsEdu.Application.Common.Exceptions.ValidationException;

namespace CmsEdu.Infrastructure.Services;

/// <summary>Triển khai nghiệp vụ ghi nhận và hủy khoản thanh toán.</summary>
public sealed class DichVuPayment(AppDbContext nguCanh, ICurrentUser nguoiDungHienTai) : IDichVuPayment
{
    private const int KichThuocTrangToiDa = 100;

    public async Task<PagedResult<PhanHoiPayment>> LayDanhSachPaymentAsync(
        int? invoiceId,
        int? studentId,
        PaymentStatus? status,
        PaymentMethod? method,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        KiemTraPhanTrang(page, pageSize);
        KiemTraQuyen();

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ValidationException("fromDate không được sau toDate.");

        var query = TaoTruyVanPayment();
        if (invoiceId.HasValue)
            query = query.Where(tt => tt.InvoiceId == invoiceId.Value);
        if (studentId.HasValue)
            query = query.Where(tt => tt.Invoice.Enrollment.StudentId == studentId.Value);
        if (status.HasValue)
            query = query.Where(tt => tt.Status == status.Value);
        if (method.HasValue)
            query = query.Where(tt => tt.Method == method.Value);
        if (fromDate.HasValue)
            query = query.Where(tt => tt.PaidAt >= fromDate.Value.UtcDateTime);
        if (toDate.HasValue)
            query = query.Where(tt => tt.PaidAt <= toDate.Value.UtcDateTime);

        var totalItems = await query.CountAsync(cancellationToken);
        var payments = await query
            .OrderByDescending(tt => tt.PaidAt)
            .ThenByDescending(tt => tt.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PhanHoiPayment>(
            payments.Select(ChuyenThanhPhanHoi).ToList(), page, pageSize, totalItems);
    }

    public async Task<PhanHoiPayment> LayChiTietPaymentAsync(
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        KiemTraQuyen();
        var payment = await TaoTruyVanPayment()
            .SingleOrDefaultAsync(tt => tt.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy khoản thanh toán.");
        return ChuyenThanhPhanHoi(payment);
    }

    public async Task<PhanHoiPayment> TaoPaymentAsync(
        int invoiceId,
        YeuCauTaoPayment request,
        CancellationToken cancellationToken = default)
    {
        KiemTraQuyen();
        KiemTraYeuCauTao(request);

        if (nguCanh.Database.IsRelational())
        {
            await using var transaction = await nguCanh.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            try
            {
                await KhoaInvoiceAsync(invoiceId, cancellationToken);
                await KhoaSinhSoChungTuAsync(request.PaidAt, cancellationToken);
                var payment = await TaoPaymentTrongGiaoDichAsync(invoiceId, request, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ChuyenThanhPhanHoi(payment);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        var paymentInMemory = await TaoPaymentTrongGiaoDichAsync(invoiceId, request, cancellationToken);
        return ChuyenThanhPhanHoi(paymentInMemory);
    }

    public async Task<PhanHoiPayment> HuyPaymentAsync(
        int paymentId,
        YeuCauHuyPayment request,
        CancellationToken cancellationToken = default)
    {
        KiemTraQuyen();
        var lyDo = KiemTraLyDoHuy(request);

        if (nguCanh.Database.IsRelational())
        {
            await using var transaction = await nguCanh.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var payment = await LayPaymentTheoDoiAsync(paymentId, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy khoản thanh toán.");
                await KhoaInvoiceAsync(payment.InvoiceId, cancellationToken);
                payment = await LayPaymentTheoDoiAsync(paymentId, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy khoản thanh toán.");
                var result = await HuyPaymentTrongGiaoDichAsync(payment, lyDo, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ChuyenThanhPhanHoi(result);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        var paymentInMemory = await LayPaymentTheoDoiAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy khoản thanh toán.");
        var cancelledPayment = await HuyPaymentTrongGiaoDichAsync(paymentInMemory, lyDo, cancellationToken);
        return ChuyenThanhPhanHoi(cancelledPayment);
    }

    private async Task<Payment> TaoPaymentTrongGiaoDichAsync(
        int invoiceId,
        YeuCauTaoPayment request,
        CancellationToken cancellationToken)
    {
        var invoice = await LayInvoiceTheoDoiAsync(invoiceId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hóa đơn.");

        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new ConflictException("Không thể ghi nhận payment cho hóa đơn đã hủy.");

        var confirmedAmount = invoice.Payments
            .Where(tt => tt.Status == PaymentStatus.Confirmed)
            .Sum(tt => tt.Amount);
        var remainingAmount = invoice.AmountDue - confirmedAmount;
        if (remainingAmount <= 0)
            throw new ConflictException("Hóa đơn đã được thanh toán đủ.");
        if (request.Amount > remainingAmount)
            throw new ConflictException("Số tiền payment vượt quá số tiền còn phải thu của hóa đơn.");

        var payment = new Payment
        {
            PaymentNumber = await TaoMaPaymentAsync(request.PaidAt, cancellationToken),
            ReceiptNumber = await TaoSoPhieuThuAsync(request.PaidAt, cancellationToken),
            InvoiceId = invoiceId,
            Invoice = invoice,
            Amount = request.Amount,
            PaidAt = request.PaidAt.UtcDateTime,
            Method = request.Method,
            Status = PaymentStatus.Confirmed,
            Note = ChuanHoaGhiChu(request.Note),
            CreatedBy = nguoiDungHienTai.UserId ?? string.Empty
        };

        nguCanh.Payments.Add(payment);
        CapNhatTrangThaiHoaDon(invoice, confirmedAmount + payment.Amount);
        await nguCanh.SaveChangesAsync(cancellationToken);

        GhiAuditLog(
            "CREATE_PAYMENT",
            payment.Id.ToString(),
            $"Tạo payment {payment.PaymentNumber}, phiếu thu {payment.ReceiptNumber} cho hóa đơn {invoice.InvoiceNumber}, số tiền {payment.Amount:N0} VNĐ.");
        await nguCanh.SaveChangesAsync(cancellationToken);
        return payment;
    }

    private async Task<Payment> HuyPaymentTrongGiaoDichAsync(
        Payment payment,
        string lyDo,
        CancellationToken cancellationToken)
    {
        if (payment.Status == PaymentStatus.Cancelled)
            throw new ConflictException("Payment này đã bị hủy trước đó.");

        payment.Status = PaymentStatus.Cancelled;
        payment.CancelledBy = nguoiDungHienTai.UserId;
        payment.CancelledAt = DateTime.UtcNow;
        payment.CancelReason = lyDo;

        var confirmedAmount = payment.Invoice.Payments
            .Where(tt => tt.Status == PaymentStatus.Confirmed)
            .Sum(tt => tt.Amount);
        CapNhatTrangThaiHoaDon(payment.Invoice, confirmedAmount);

        GhiAuditLog("CANCEL_PAYMENT", payment.Id.ToString(),
            $"Hủy payment {payment.PaymentNumber}. Lý do: {lyDo}");
        await nguCanh.SaveChangesAsync(cancellationToken);
        return payment;
    }

    private IQueryable<Payment> TaoTruyVanPayment()
        => nguCanh.Payments
            .AsNoTracking()
            .Include(tt => tt.Invoice)
                .ThenInclude(hd => hd.Enrollment)
                    .ThenInclude(gd => gd.Student);

    private async Task<Invoice?> LayInvoiceTheoDoiAsync(int invoiceId, CancellationToken cancellationToken)
        => await nguCanh.Invoices
            .Include(hd => hd.Enrollment)
                .ThenInclude(gd => gd.Student)
            .Include(hd => hd.Payments)
            .SingleOrDefaultAsync(hd => hd.Id == invoiceId, cancellationToken);

    private async Task<Payment?> LayPaymentTheoDoiAsync(int paymentId, CancellationToken cancellationToken)
        => await nguCanh.Payments
            .Include(tt => tt.Invoice)
                .ThenInclude(hd => hd.Enrollment)
                    .ThenInclude(gd => gd.Student)
            .Include(tt => tt.Invoice)
                .ThenInclude(hd => hd.Payments)
            .SingleOrDefaultAsync(tt => tt.Id == paymentId, cancellationToken);

    private async Task<string> TaoMaPaymentAsync(DateTimeOffset paidAt, CancellationToken cancellationToken)
    {
        var prefix = $"PAY-{paidAt:yyyyMM}-";
        var count = await nguCanh.Payments.CountAsync(
            tt => tt.PaymentNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}{count + 1:D4}";
    }

    private async Task<string> TaoSoPhieuThuAsync(DateTimeOffset paidAt, CancellationToken cancellationToken)
    {
        var prefix = $"REC-{paidAt:yyyyMM}-";
        var count = await nguCanh.Payments.CountAsync(
            tt => tt.ReceiptNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}{count + 1:D4}";
    }

    private async Task KhoaInvoiceAsync(int invoiceId, CancellationToken cancellationToken)
    {
        if (!nguCanh.Database.IsSqlServer())
            return;

        var resource = $"CmsEdu:Payment:Invoice:{invoiceId}";
        await nguCanh.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = {resource},
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 10000;
            IF @result < 0
                THROW 50002, 'Không thể khóa nghiệp vụ payment của hóa đơn.', 1;
            """, cancellationToken);
    }

    private async Task KhoaSinhSoChungTuAsync(
        DateTimeOffset paidAt,
        CancellationToken cancellationToken)
    {
        if (!nguCanh.Database.IsSqlServer())
            return;

        var resource = $"CmsEdu:Payment:Number:{paidAt:yyyyMM}";
        await nguCanh.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = {resource},
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 10000;
            IF @result < 0
                THROW 50003, 'Không thể khóa sinh số payment và phiếu thu.', 1;
            """, cancellationToken);
    }

    private void GhiAuditLog(string action, string entityId, string description)
        => nguCanh.AuditLogs.Add(new AuditLog
        {
            UserId = nguoiDungHienTai.UserId,
            Action = action,
            EntityType = "Payment",
            EntityId = entityId,
            Description = description,
            OccurredAt = DateTime.UtcNow
        });

    private static void CapNhatTrangThaiHoaDon(Invoice invoice, decimal confirmedAmount)
    {
        if (invoice.Status == InvoiceStatus.Cancelled)
            return;

        invoice.Status = confirmedAmount switch
        {
            <= 0 => InvoiceStatus.Issued,
            var amount when amount < invoice.AmountDue => InvoiceStatus.Partial,
            _ => InvoiceStatus.Paid
        };
    }

    private void KiemTraQuyen()
    {
        if (nguoiDungHienTai.Role != UserRole.Admin &&
            nguoiDungHienTai.Role != UserRole.Accountant)
            throw new ForbiddenAccessException("Bạn không có quyền thao tác payment.");
    }

    private static void KiemTraYeuCauTao(YeuCauTaoPayment request)
    {
        if (request.Amount <= 0)
            throw new ValidationException("Số tiền payment phải lớn hơn 0.");
        if (request.Note?.Length > 500)
            throw new ValidationException("Ghi chú payment không được vượt quá 500 ký tự.");
    }

    private static string KiemTraLyDoHuy(YeuCauHuyPayment request)
    {
        if (string.IsNullOrWhiteSpace(request.LyDoHuy))
            throw new ValidationException("Lý do hủy payment không được để trống.");
        var lyDo = request.LyDoHuy.Trim();
        if (lyDo.Length > 500)
            throw new ValidationException("Lý do hủy payment không được vượt quá 500 ký tự.");
        return lyDo;
    }

    private static string? ChuanHoaGhiChu(string? note)
        => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static void KiemTraPhanTrang(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > KichThuocTrangToiDa)
            throw new ValidationException("Trang phải từ 1 trở lên và kích thước trang phải từ 1 đến 100.");
    }

    private static PhanHoiPayment ChuyenThanhPhanHoi(Payment payment)
        => new(
            payment.Id,
            payment.PaymentNumber,
            payment.ReceiptNumber,
            payment.InvoiceId,
            payment.Invoice.InvoiceNumber,
            payment.Invoice.Enrollment.StudentId,
            payment.Invoice.Enrollment.Student.FullName,
            payment.Amount,
            new DateTimeOffset(DateTime.SpecifyKind(payment.PaidAt, DateTimeKind.Utc)),
            payment.Method,
            payment.Status,
            payment.Note,
            payment.CreatedBy,
            payment.CancelledBy,
            payment.CancelledAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(payment.CancelledAt.Value, DateTimeKind.Utc))
                : null,
            payment.CancelReason);
}
