using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Dashboard;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Services;

/// <summary>Triển khai số liệu tổng hợp cho dashboard kế toán.</summary>
public sealed class DichVuDashboardTaiChinh(AppDbContext nguCanh, ICurrentUser nguoiDungHienTai) : IDichVuDashboardTaiChinh
{
    public async Task<PhanHoiDashboardTaiChinh> LayDashboardKeToanAsync(
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken = default)
    {
        KiemTraQuyen();

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ValidationException("fromDate không được sau toDate.");

        var paymentQuery = nguCanh.Payments
            .AsNoTracking()
            .AsQueryable();

        if (fromDate.HasValue)
            paymentQuery = paymentQuery.Where(payment => payment.PaidAt >= fromDate.Value.UtcDateTime);
        if (toDate.HasValue)
            paymentQuery = paymentQuery.Where(payment => payment.PaidAt <= toDate.Value.UtcDateTime);

        var confirmedPaymentQuery = paymentQuery
            .Where(payment => payment.Status == PaymentStatus.Confirmed);

        var revenue = await confirmedPaymentQuery
            .Select(payment => (decimal?)payment.Amount)
            .SumAsync(cancellationToken) ?? 0m;

        var totalInvoice = await nguCanh.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Status != InvoiceStatus.Cancelled)
            .Select(invoice => (decimal?)invoice.AmountDue)
            .SumAsync(cancellationToken) ?? 0m;

        var totalConfirmedPayment = await nguCanh.Payments
            .AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Confirmed)
            .Select(payment => (decimal?)payment.Amount)
            .SumAsync(cancellationToken) ?? 0m;

        var paymentEntities = await paymentQuery
            .Include(payment => payment.Invoice)
                .ThenInclude(invoice => invoice.Enrollment)
                    .ThenInclude(enrollment => enrollment.Student)
            .OrderByDescending(payment => payment.PaidAt)
            .ThenByDescending(payment => payment.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        var transactions = paymentEntities.Select(payment => new PhanHoiGiaoDichTaiChinh(
                payment.Id,
                payment.PaymentNumber,
                payment.ReceiptNumber,
                payment.InvoiceId,
                payment.Invoice.InvoiceNumber,
                payment.Invoice.Enrollment.StudentId,
                payment.Invoice.Enrollment.Student.FullName,
                payment.Amount,
                new DateTimeOffset(DateTime.SpecifyKind(payment.PaidAt, DateTimeKind.Utc)),
                payment.Method.ToString(),
                payment.Status.ToString(),
                payment.Note))
            .ToList();

        var auditLogs = await nguCanh.AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityType == "Invoice" || log.EntityType == "Payment")
            .Where(log => !fromDate.HasValue || log.OccurredAt >= fromDate.Value.UtcDateTime)
            .Where(log => !toDate.HasValue || log.OccurredAt <= toDate.Value.UtcDateTime)
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id)
            .Take(50)
            .Select(log => new PhanHoiAuditTaiChinh(
                log.Id,
                log.UserId,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.Description,
                new DateTimeOffset(DateTime.SpecifyKind(log.OccurredAt, DateTimeKind.Utc))))
            .ToListAsync(cancellationToken);

        return new PhanHoiDashboardTaiChinh(
            revenue,
            totalInvoice - totalConfirmedPayment,
            fromDate,
            toDate,
            transactions,
            auditLogs);
    }

    private void KiemTraQuyen()
    {
        if (nguoiDungHienTai.Role != UserRole.Admin &&
            nguoiDungHienTai.Role != UserRole.Accountant)
            throw new ForbiddenAccessException("Bạn không có quyền xem dashboard kế toán.");
    }
}
