using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Dashboard;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Services;

public class RoleDashboardService(AppDbContext dbContext, ICurrentUser currentUser)
    : IRoleDashboardService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<AdminDashboardResponse> GetAdminDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureRole(UserRole.Admin);
        var vietnamToday = DateOnly.FromDateTime(
            DateTimeOffset.UtcNow.ToOffset(VietnamOffset).DateTime);
        var currentMonth = new DateOnly(vietnamToday.Year, vietnamToday.Month, 1);
        var firstMonth = currentMonth.AddMonths(-5);

        var classCounts = await dbContext.Classes
            .AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderBy(item => item.Status)
            .ToListAsync(cancellationToken);
        var classCountMap = classCounts.ToDictionary(item => item.Status, item => item.Count);
        var classesByStatus = Enum.GetValues<ClassStatus>()
            .Select(status => new StatusCountResponse(
                status.ToString(), classCountMap.GetValueOrDefault(status)))
            .ToList();
        var enrollmentCounts = await dbContext.Enrollments
            .AsNoTracking()
            .Where(item => item.StartDate >= firstMonth && item.StartDate < currentMonth.AddMonths(1))
            .GroupBy(item => new { item.StartDate.Year, item.StartDate.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
        var enrollmentCountMap = enrollmentCounts.ToDictionary(
            item => (item.Year, item.Month), item => item.Count);
        var enrollmentStartsByMonth = Enumerable.Range(0, 6)
            .Select(index => firstMonth.AddMonths(index))
            .Select(month => new MonthlyCountResponse(
                month.Year,
                month.Month,
                enrollmentCountMap.GetValueOrDefault((month.Year, month.Month))))
            .ToList();

        return new AdminDashboardResponse(
            await dbContext.Users.CountAsync(
                user => user.EmploymentStatus == EmploymentStatus.Active, cancellationToken),
            await dbContext.Students.CountAsync(
                student => !student.IsArchived, cancellationToken),
            classCountMap.GetValueOrDefault(ClassStatus.Active),
            await dbContext.Enrollments.CountAsync(
                enrollment => enrollment.Status == EnrollmentStatus.Active, cancellationToken),
            classesByStatus,
            enrollmentStartsByMonth);
    }

    public async Task<TeacherDashboardResponse> GetTeacherDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureRole(UserRole.Teacher);
        var teacherId = currentUser.UserId!;
        var vietnamNow = DateTimeOffset.UtcNow.ToOffset(VietnamOffset);
        var today = DateOnly.FromDateTime(vietnamNow.DateTime);
        var currentTime = TimeOnly.FromDateTime(vietnamNow.DateTime);

        var assignedClassCount = await dbContext.Classes.CountAsync(
            item => item.MainTeacherUserId == teacherId && item.Status == ClassStatus.Active,
            cancellationToken);
        var activeStudentCount = await dbContext.Enrollments
            .Where(item =>
                item.Class.MainTeacherUserId == teacherId &&
                item.Class.Status == ClassStatus.Active &&
                item.Status == EnrollmentStatus.Active)
            .Select(item => item.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);
        var todaySessionCount = await dbContext.Sessions.CountAsync(
            item =>
                item.Class.MainTeacherUserId == teacherId &&
                item.Class.Status == ClassStatus.Active &&
                item.SessionDate == today &&
                item.Status != SessionStatus.Cancelled,
            cancellationToken);
        var pendingSessionCount = await dbContext.Sessions.CountAsync(
            item =>
                item.Class.MainTeacherUserId == teacherId &&
                item.Class.Status == ClassStatus.Active &&
                item.Status == SessionStatus.Scheduled &&
                (item.SessionDate < today ||
                 item.SessionDate == today && item.EndTime <= currentTime),
            cancellationToken);
        var upcomingSessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(item =>
                item.Class.MainTeacherUserId == teacherId &&
                item.Class.Status == ClassStatus.Active &&
                item.Status == SessionStatus.Scheduled &&
                (item.SessionDate > today ||
                 item.SessionDate == today && item.StartTime >= currentTime))
            .OrderBy(item => item.SessionDate)
            .ThenBy(item => item.StartTime)
            .Take(5)
            .Select(item => new UpcomingSessionResponse(
                item.Id,
                item.ClassId,
                item.Class.ClassCode,
                item.Class.Name,
                item.SessionDate,
                item.StartTime,
                item.EndTime))
            .ToListAsync(cancellationToken);

        return new TeacherDashboardResponse(
            assignedClassCount,
            activeStudentCount,
            todaySessionCount,
            pendingSessionCount,
            upcomingSessions);
    }

    public async Task<CustomerCareDashboardResponse> GetCustomerCareDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureRole(UserRole.CustomerCare);

        var enrollmentCounts = await dbContext.Enrollments
            .AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderBy(item => item.Status)
            .ToListAsync(cancellationToken);
        var enrollmentCountMap = enrollmentCounts.ToDictionary(item => item.Status, item => item.Count);
        var enrollmentsByStatus = Enum.GetValues<EnrollmentStatus>()
            .Select(status => new StatusCountResponse(
                status.ToString(), enrollmentCountMap.GetValueOrDefault(status)))
            .ToList();
        var studentsWithoutGuardian = await dbContext.Students
            .AsNoTracking()
            .Where(student => !student.IsArchived && !student.StudentGuardians.Any())
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.Id)
            .Take(5)
            .Select(student => new StudentSummaryResponse(
                student.Id,
                student.StudentCode,
                student.FullName))
            .ToListAsync(cancellationToken);

        return new CustomerCareDashboardResponse(
            await dbContext.Students.CountAsync(
                student => !student.IsArchived, cancellationToken),
            enrollmentCountMap.GetValueOrDefault(EnrollmentStatus.Active),
            enrollmentCountMap.GetValueOrDefault(EnrollmentStatus.Paused),
            await dbContext.Students.CountAsync(
                student => !student.IsArchived && !student.StudentGuardians.Any(),
                cancellationToken),
            enrollmentsByStatus,
            studentsWithoutGuardian);
    }

    public async Task<AccountingDashboardResponse> GetAccountingDashboardAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        EnsureRole(UserRole.Accountant);

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ValidationException("fromDate không được sau toDate.");

        var fromUtc = fromDate.HasValue ? StartOfDayUtc(fromDate.Value) : (DateTime?)null;
        var toUtc = toDate.HasValue ? StartOfDayUtc(toDate.Value.AddDays(1)) : (DateTime?)null;
        var paymentQuery = dbContext.Payments.AsNoTracking().AsQueryable();
        if (fromUtc.HasValue)
            paymentQuery = paymentQuery.Where(item => item.PaidAt >= fromUtc.Value);
        if (toUtc.HasValue)
            paymentQuery = paymentQuery.Where(item => item.PaidAt < toUtc.Value);

        var confirmedPayments = paymentQuery
            .Where(item => item.Status == PaymentStatus.Confirmed);
        var revenue = await confirmedPayments
            .Select(item => (decimal?)item.Amount)
            .SumAsync(cancellationToken) ?? 0m;
        var monthlyRevenue = await confirmedPayments
            .GroupBy(item => new
            {
                Year = item.PaidAt.AddHours(7).Year,
                Month = item.PaidAt.AddHours(7).Month
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Amount = group.Sum(item => item.Amount)
            })
            .OrderBy(item => item.Year)
            .ThenBy(item => item.Month)
            .ToListAsync(cancellationToken);
        var revenueByMonth = monthlyRevenue
            .Select(item => new MonthlyRevenueResponse(item.Year, item.Month, item.Amount))
            .ToList();

        var collectibleInvoices = dbContext.Invoices
            .AsNoTracking()
            .Where(item =>
                item.Status != InvoiceStatus.Draft &&
                item.Status != InvoiceStatus.Cancelled);
        var totalInvoice = await collectibleInvoices
            .Select(item => (decimal?)item.AmountDue)
            .SumAsync(cancellationToken) ?? 0m;
        var totalConfirmedPayment = await dbContext.Payments
            .AsNoTracking()
            .Where(item =>
                item.Status == PaymentStatus.Confirmed &&
                item.Invoice.Status != InvoiceStatus.Draft &&
                item.Invoice.Status != InvoiceStatus.Cancelled)
            .Select(item => (decimal?)item.Amount)
            .SumAsync(cancellationToken) ?? 0m;
        var currentDebt = Math.Max(0m, totalInvoice - totalConfirmedPayment);
        var vietnamToday = DateOnly.FromDateTime(
            DateTimeOffset.UtcNow.ToOffset(VietnamOffset).DateTime);
        var overdueBalances = await collectibleInvoices
            .Where(item => item.DueDate < vietnamToday)
            .Select(item => new
            {
                item.AmountDue,
                Paid = item.Payments
                    .Where(payment => payment.Status == PaymentStatus.Confirmed)
                    .Select(payment => (decimal?)payment.Amount)
                    .Sum() ?? 0m
            })
            .ToListAsync(cancellationToken);
        var overdueDebt = overdueBalances.Sum(item => Math.Max(0m, item.AmountDue - item.Paid));

        var paymentEntities = await paymentQuery
            .Include(item => item.Invoice)
                .ThenInclude(invoice => invoice.Enrollment)
                    .ThenInclude(enrollment => enrollment.Student)
            .OrderByDescending(item => item.PaidAt)
            .ThenByDescending(item => item.Id)
            .Take(50)
            .ToListAsync(cancellationToken);
        var transactions = paymentEntities.Select(item => new AccountingTransactionResponse(
                item.Id,
                item.PaymentNumber,
                item.ReceiptNumber,
                item.InvoiceId,
                item.Invoice.InvoiceNumber,
                item.Invoice.Enrollment.StudentId,
                item.Invoice.Enrollment.Student.FullName,
                item.Amount,
                new DateTimeOffset(DateTime.SpecifyKind(item.PaidAt, DateTimeKind.Utc)),
                item.Method.ToString(),
                item.Status.ToString(),
                item.Note))
            .ToList();
        var auditLogs = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(item => item.EntityType == "Invoice" || item.EntityType == "Payment")
            .Where(item => !fromUtc.HasValue || item.OccurredAt >= fromUtc.Value)
            .Where(item => !toUtc.HasValue || item.OccurredAt < toUtc.Value)
            .OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.Id)
            .Take(50)
            .Select(item => new AccountingAuditLogResponse(
                item.Id,
                item.UserId,
                item.Action,
                item.EntityType,
                item.EntityId,
                item.Description,
                new DateTimeOffset(DateTime.SpecifyKind(item.OccurredAt, DateTimeKind.Utc))))
            .ToListAsync(cancellationToken);

        return new AccountingDashboardResponse(
            revenue,
            currentDebt,
            overdueDebt,
            fromDate,
            toDate,
            revenueByMonth,
            transactions,
            auditLogs);
    }

    private static DateTime StartOfDayUtc(DateOnly date)
    {
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime;
    }

    private void EnsureRole(params string[] roles)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();
        if (!roles.Contains(currentUser.Role) || string.IsNullOrWhiteSpace(currentUser.UserId))
            throw new ForbiddenAccessException("Bạn không có quyền xem dashboard này.");
    }
}
