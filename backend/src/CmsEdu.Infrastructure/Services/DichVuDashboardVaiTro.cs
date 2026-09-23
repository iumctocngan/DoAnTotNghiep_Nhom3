using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Dashboard;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Services;

public class DichVuDashboardVaiTro(AppDbContext nguCanh, ICurrentUser nguoiDungHienTai)
    : IDichVuDashboardVaiTro
{
    public async Task<PhanHoiDashboardAdmin> LayDashboardAdminAsync(
        CancellationToken cancellationToken = default)
    {
        KiemTraQuyen(UserRole.Admin);

        return new PhanHoiDashboardAdmin(
            await nguCanh.Users.CountAsync(
                user => user.EmploymentStatus == EmploymentStatus.Active, cancellationToken),
            await nguCanh.Students.CountAsync(
                hocVien => !hocVien.IsArchived, cancellationToken),
            await nguCanh.Classes.CountAsync(
                lop => lop.Status == ClassStatus.Active, cancellationToken),
            await nguCanh.Enrollments.CountAsync(
                ghiDanh => ghiDanh.Status == EnrollmentStatus.Active, cancellationToken));
    }

    public async Task<PhanHoiDashboardTeacher> LayDashboardTeacherAsync(
        CancellationToken cancellationToken = default)
    {
        KiemTraQuyen(UserRole.Teacher);
        var teacherId = nguoiDungHienTai.UserId!;
        var homNay = DateOnly.FromDateTime(DateTime.Today);

        var soLop = await nguCanh.Classes.CountAsync(
            lop => lop.MainTeacherUserId == teacherId && lop.Status == ClassStatus.Active,
            cancellationToken);
        var soHocVien = await nguCanh.Enrollments
            .Where(ghiDanh =>
                ghiDanh.Class.MainTeacherUserId == teacherId &&
                ghiDanh.Class.Status == ClassStatus.Active &&
                ghiDanh.Status == EnrollmentStatus.Active)
            .Select(ghiDanh => ghiDanh.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);
        var soBuoiHocHomNay = await nguCanh.Sessions.CountAsync(
            buoiHoc =>
                buoiHoc.Class.MainTeacherUserId == teacherId &&
                buoiHoc.SessionDate == homNay &&
                buoiHoc.Status != SessionStatus.Cancelled,
            cancellationToken);
        var buoiHocSapToi = await nguCanh.Sessions
            .AsNoTracking()
            .Where(buoiHoc =>
                buoiHoc.Class.MainTeacherUserId == teacherId &&
                buoiHoc.SessionDate >= homNay &&
                buoiHoc.Status == SessionStatus.Scheduled)
            .OrderBy(buoiHoc => buoiHoc.SessionDate)
            .ThenBy(buoiHoc => buoiHoc.StartTime)
            .Take(5)
            .Select(buoiHoc => new PhanHoiBuoiHocSapToi(
                buoiHoc.Id,
                buoiHoc.ClassId,
                buoiHoc.Class.ClassCode,
                buoiHoc.Class.Name,
                buoiHoc.SessionDate,
                buoiHoc.StartTime,
                buoiHoc.EndTime))
            .ToListAsync(cancellationToken);

        return new PhanHoiDashboardTeacher(
            soLop, soHocVien, soBuoiHocHomNay, buoiHocSapToi);
    }

    public async Task<PhanHoiDashboardCustomerCare> LayDashboardCustomerCareAsync(
        CancellationToken cancellationToken = default)
    {
        KiemTraQuyen(UserRole.CustomerCare);

        return new PhanHoiDashboardCustomerCare(
            await nguCanh.Students.CountAsync(
                hocVien => !hocVien.IsArchived, cancellationToken),
            await nguCanh.Enrollments.CountAsync(
                ghiDanh => ghiDanh.Status == EnrollmentStatus.Active, cancellationToken),
            await nguCanh.Enrollments.CountAsync(
                ghiDanh => ghiDanh.Status == EnrollmentStatus.Paused, cancellationToken),
            await nguCanh.Students.CountAsync(
                hocVien => !hocVien.IsArchived && !hocVien.StudentGuardians.Any(),
                cancellationToken));
    }

    public async Task<PhanHoiDashboardTaiChinh> LayDashboardKeToanAsync(
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken = default)
    {
        KiemTraQuyen(UserRole.Admin, UserRole.Accountant);

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ValidationException("fromDate không được sau toDate.");

        var paymentQuery = nguCanh.Payments.AsNoTracking().AsQueryable();
        if (fromDate.HasValue)
            paymentQuery = paymentQuery.Where(payment =>
                payment.PaidAt >= fromDate.Value.UtcDateTime);
        if (toDate.HasValue)
            paymentQuery = paymentQuery.Where(payment =>
                payment.PaidAt <= toDate.Value.UtcDateTime);

        var revenue = await paymentQuery
            .Where(payment => payment.Status == PaymentStatus.Confirmed)
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

    private void KiemTraQuyen(params string[] roles)
    {
        if (!nguoiDungHienTai.IsAuthenticated)
            throw new UnauthorizedAccessException();
        if (!roles.Contains(nguoiDungHienTai.Role) ||
            string.IsNullOrWhiteSpace(nguoiDungHienTai.UserId))
            throw new ForbiddenAccessException("Bạn không có quyền xem dashboard này.");
    }
}
