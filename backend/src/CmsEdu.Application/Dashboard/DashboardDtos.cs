namespace CmsEdu.Application.Dashboard;

public record StatusCountResponse(string Status, int Count);
public record MonthlyCountResponse(int Year, int Month, int Count);

public record AdminDashboardResponse(
    int ActiveStaffCount,
    int UnarchivedStudentCount,
    int ActiveClassCount,
    int ActiveEnrollmentCount,
    IReadOnlyList<StatusCountResponse> ClassesByStatus,
    IReadOnlyList<MonthlyCountResponse> EnrollmentStartsByMonth);

public record UpcomingSessionResponse(
    int Id,
    int ClassId,
    string ClassCode,
    string ClassName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime);

public record TeacherDashboardResponse(
    int AssignedClassCount,
    int ActiveStudentCount,
    int TodaySessionCount,
    int PendingSessionCount,
    IReadOnlyList<UpcomingSessionResponse> UpcomingSessions);

public record StudentSummaryResponse(int Id, string StudentCode, string FullName);

public record CustomerCareDashboardResponse(
    int UnarchivedStudentCount,
    int ActiveEnrollmentCount,
    int PausedEnrollmentCount,
    int StudentsWithoutGuardianCount,
    IReadOnlyList<StatusCountResponse> EnrollmentsByStatus,
    IReadOnlyList<StudentSummaryResponse> StudentsWithoutGuardian);

public record AccountingTransactionResponse(
    int PaymentId,
    string PaymentNumber,
    string ReceiptNumber,
    int InvoiceId,
    string InvoiceNumber,
    int StudentId,
    string StudentName,
    decimal Amount,
    DateTimeOffset PaidAt,
    string Method,
    string Status,
    string? Note);

public record AccountingAuditLogResponse(
    int Id,
    string? UserId,
    string Action,
    string EntityType,
    string EntityId,
    string Description,
    DateTimeOffset OccurredAt);

public record MonthlyRevenueResponse(int Year, int Month, decimal Amount);

public record AccountingDashboardResponse(
    decimal Revenue,
    decimal CurrentDebt,
    decimal OverdueDebt,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<MonthlyRevenueResponse> RevenueByMonth,
    IReadOnlyList<AccountingTransactionResponse> Transactions,
    IReadOnlyList<AccountingAuditLogResponse> AuditLogs);
