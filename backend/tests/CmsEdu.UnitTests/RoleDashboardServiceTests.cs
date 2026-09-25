using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Identity;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.UnitTests;

public class RoleDashboardServiceTests
{
    [Fact]
    public async Task GetAdminDashboardAsync_ReturnsCountsAndClassChart()
    {
        await using var fixture = await Fixture.CreateAsync(UserRole.Admin, "admin");

        var result = await fixture.Service.GetAdminDashboardAsync();

        Assert.Equal(1, result.ActiveStaffCount);
        Assert.Equal(1, result.UnarchivedStudentCount);
        Assert.Equal(1, result.ActiveClassCount);
        Assert.Equal(1, result.ActiveEnrollmentCount);
        Assert.Equal(6, result.EnrollmentStartsByMonth.Count);
        Assert.Contains(result.ClassesByStatus,
            item => item.Status == nameof(ClassStatus.Active) && item.Count == 1);
    }

    [Fact]
    public async Task GetTeacherDashboardAsync_ReturnsOnlyOwnedActiveClasses()
    {
        await using var fixture = await Fixture.CreateAsync(UserRole.Teacher, "teacher-01");

        var result = await fixture.Service.GetTeacherDashboardAsync();

        Assert.Equal(1, result.AssignedClassCount);
        Assert.Equal(1, result.ActiveStudentCount);
        Assert.Equal(1, result.TodaySessionCount);
        Assert.Equal(0, result.PendingSessionCount);
        Assert.Single(result.UpcomingSessions);
        Assert.Equal("CLS-001", result.UpcomingSessions[0].ClassCode);
    }

    [Fact]
    public async Task GetCustomerCareDashboardAsync_ReturnsCountsAndEnrollmentChart()
    {
        await using var fixture = await Fixture.CreateAsync(UserRole.CustomerCare);

        var result = await fixture.Service.GetCustomerCareDashboardAsync();

        Assert.Equal(1, result.UnarchivedStudentCount);
        Assert.Equal(1, result.ActiveEnrollmentCount);
        Assert.Equal(0, result.PausedEnrollmentCount);
        Assert.Equal(1, result.StudentsWithoutGuardianCount);
        Assert.Single(result.StudentsWithoutGuardian);
        Assert.Contains(result.EnrollmentsByStatus,
            item => item.Status == nameof(EnrollmentStatus.Active) && item.Count == 1);
    }

    [Fact]
    public async Task GetAccountingDashboardAsync_ExcludesDraftInvoicesAndCancelledPayments()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.GetAccountingDashboardAsync(null, null);

        Assert.Equal(2_000_000m, result.Revenue);
        Assert.Equal(4_000_000m, result.CurrentDebt);
        Assert.Single(result.RevenueByMonth);
        Assert.Equal(2, result.Transactions.Count);
        Assert.Equal("REC-202609-0001", result.Transactions[0].ReceiptNumber);
        Assert.Equal("Cancelled", result.Transactions[1].Status);
        Assert.Single(result.AuditLogs);
    }

    [Fact]
    public async Task GetAccountingDashboardAsync_ReturnsOverdueDebt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invoice = await fixture.Context.Invoices
            .SingleAsync(item => item.InvoiceNumber == "INV-202609-0001");
        invoice.DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.GetAccountingDashboardAsync(null, null);

        Assert.Equal(4_000_000m, result.OverdueDebt);
    }

    [Fact]
    public async Task GetAccountingDashboardAsync_IncludesTheWholeToDate()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.GetAccountingDashboardAsync(
            new DateOnly(2026, 9, 10),
            new DateOnly(2026, 9, 10));

        Assert.Equal(2_000_000m, result.Revenue);
        Assert.Equal(4_000_000m, result.CurrentDebt);
        Assert.Single(result.Transactions);
    }

    [Fact]
    public async Task GetAccountingDashboardAsync_RejectsAnInvalidDateRange()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.GetAccountingDashboardAsync(
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 9, 1)));
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Teacher)]
    public async Task GetAccountingDashboardAsync_RejectsOtherRoles(string role)
    {
        await using var fixture = await Fixture.CreateAsync(role);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            fixture.Service.GetAccountingDashboardAsync(null, null));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(AppDbContext context, RoleDashboardService service)
        {
            Context = context;
            Service = service;
        }

        public AppDbContext Context { get; }
        public RoleDashboardService Service { get; }

        public static async Task<Fixture> CreateAsync(
            string role = UserRole.Accountant,
            string? userId = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new AppDbContext(options);
            var enrollment = new Enrollment
            {
                Student = new Student
                {
                    StudentCode = "HV-001",
                    FullName = "Nguyễn Văn A"
                },
                Class = new Class
                {
                    ClassCode = "CLS-001",
                    Name = "Lớp Toán",
                    MainTeacherUserId = "teacher-01",
                    Capacity = 15,
                    StartDate = new DateOnly(2026, 9, 1),
                    Status = ClassStatus.Active
                },
                StartDate = new DateOnly(2026, 9, 1),
                Status = EnrollmentStatus.Active
            };
            var invoice = new Invoice
            {
                InvoiceNumber = "INV-202609-0001",
                Enrollment = enrollment,
                PeriodStart = new DateOnly(2026, 9, 1),
                PeriodEnd = new DateOnly(2027, 2, 28),
                AmountDue = 6_000_000m,
                DueDate = new DateOnly(2027, 2, 28),
                Status = InvoiceStatus.Partial,
                CreatedBy = "admin",
                CreatedAt = DateTime.UtcNow
            };
            var confirmedPayment = new Payment
            {
                PaymentNumber = "PAY-202609-0001",
                ReceiptNumber = "REC-202609-0001",
                Invoice = invoice,
                Amount = 2_000_000m,
                PaidAt = new DateTime(2026, 9, 10, 16, 30, 0, DateTimeKind.Utc),
                Status = PaymentStatus.Confirmed,
                CreatedBy = "accountant"
            };
            var cancelledPayment = new Payment
            {
                PaymentNumber = "PAY-202608-0001",
                ReceiptNumber = "REC-202608-0001",
                Invoice = invoice,
                Amount = 1_000_000m,
                PaidAt = new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc),
                Status = PaymentStatus.Cancelled,
                CreatedBy = "accountant",
                CancelledBy = "accountant",
                CancelledAt = DateTime.UtcNow,
                CancelReason = "Ghi nhận nhầm"
            };
            var vietnamToday = DateOnly.FromDateTime(
                DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);

            context.Users.Add(new ApplicationUser
            {
                Id = "admin",
                UserName = "admin@cms.edu.vn",
                Email = "admin@cms.edu.vn",
                EmployeeCode = "ADMIN001",
                FullName = "Admin",
                EmploymentStatus = EmploymentStatus.Active
            });
            context.Invoices.AddRange(invoice, new Invoice
            {
                InvoiceNumber = "INV-DRAFT",
                Enrollment = enrollment,
                PeriodStart = new DateOnly(2027, 3, 1),
                PeriodEnd = new DateOnly(2027, 8, 31),
                AmountDue = 9_000_000m,
                DueDate = new DateOnly(2027, 8, 31),
                Status = InvoiceStatus.Draft,
                CreatedBy = "admin",
                CreatedAt = DateTime.UtcNow
            });
            context.Payments.AddRange(confirmedPayment, cancelledPayment);
            context.Sessions.AddRange(
                new Session
                {
                    Class = enrollment.Class,
                    SessionDate = vietnamToday,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(10, 0),
                    Status = SessionStatus.Completed
                },
                new Session
                {
                    Class = enrollment.Class,
                    SessionDate = vietnamToday.AddDays(1),
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(10, 0),
                    Status = SessionStatus.Scheduled
                });
            context.AuditLogs.Add(new AuditLog
            {
                UserId = "accountant",
                Action = "CANCEL_PAYMENT",
                EntityType = "Payment",
                EntityId = "2",
                Description = "Hủy payment",
                OccurredAt = new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc)
            });
            await context.SaveChangesAsync();

            return new Fixture(context, new RoleDashboardService(
                context,
                new TestCurrentUser(role, userId ?? role.ToLowerInvariant())));
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class TestCurrentUser(string role, string userId) : ICurrentUser
    {
        public string? UserId => userId;
        public string? Role => role;
        public bool IsAuthenticated => true;
    }
}
