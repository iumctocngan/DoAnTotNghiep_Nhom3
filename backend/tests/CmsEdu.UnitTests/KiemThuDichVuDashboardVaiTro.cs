using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Identity;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.UnitTests;

public class KiemThuDichVuDashboardVaiTro
{
    [Fact]
    public async Task LayDashboardAdminAsync_TinhDungSoLieuHoatDong()
    {
        await using var fixture = await Fixture.TaoMoiAsync(UserRole.Admin, "admin");

        var result = await fixture.Service.LayDashboardAdminAsync();

        Assert.Equal(1, result.SoNhanVienDangHoatDong);
        Assert.Equal(1, result.SoHocVienDangHoatDong);
        Assert.Equal(1, result.SoLopDangHoatDong);
        Assert.Equal(1, result.SoGhiDanhDangHoc);
    }

    [Fact]
    public async Task LayDashboardTeacherAsync_ChiLayLopPhuTrach()
    {
        await using var fixture = await Fixture.TaoMoiAsync(UserRole.Teacher, "teacher-01");

        var result = await fixture.Service.LayDashboardTeacherAsync();

        Assert.Equal(1, result.SoLopDangPhuTrach);
        Assert.Equal(1, result.SoHocVienDangHoc);
        Assert.Equal(1, result.SoBuoiHocHomNay);
        Assert.Single(result.BuoiHocSapToi);
    }

    [Fact]
    public async Task LayDashboardCustomerCareAsync_TinhDungSoLieuChamSoc()
    {
        await using var fixture = await Fixture.TaoMoiAsync(UserRole.CustomerCare);

        var result = await fixture.Service.LayDashboardCustomerCareAsync();

        Assert.Equal(1, result.SoHocVienDangHoatDong);
        Assert.Equal(1, result.SoGhiDanhDangHoc);
        Assert.Equal(0, result.SoGhiDanhBaoLuu);
        Assert.Equal(1, result.SoHocVienChuaCoNguoiGiamHo);
    }

    [Fact]
    public async Task LayDashboardKeToanAsync_TinhRevenueVaDebt_ChiTinhPaymentConfirmed()
    {
        await using var fixture = await Fixture.TaoMoiAsync();

        var result = await fixture.Service.LayDashboardKeToanAsync(null, null);

        Assert.Equal(2_000_000m, result.Revenue);
        Assert.Equal(4_000_000m, result.Debt);
        Assert.Equal(2, result.Transactions.Count);
        Assert.Equal("REC-202609-0001", result.Transactions[0].ReceiptNumber);
        Assert.Equal("Cancelled", result.Transactions[1].Status);
        Assert.Single(result.AuditLogs);
        Assert.Equal("CANCEL_PAYMENT", result.AuditLogs[0].Action);
    }

    [Fact]
    public async Task LayDashboardKeToanAsync_LocRevenueTheoPaidAt()
    {
        await using var fixture = await Fixture.TaoMoiAsync();

        var result = await fixture.Service.LayDashboardKeToanAsync(
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero));

        Assert.Equal(2_000_000m, result.Revenue);
        Assert.Equal(4_000_000m, result.Debt);
        Assert.Single(result.Transactions);
    }

    [Fact]
    public async Task LayDashboardKeToanAsync_TuChoiTeacher()
    {
        await using var fixture = await Fixture.TaoMoiAsync(UserRole.Teacher);

        await Assert.ThrowsAsync<CmsEdu.Application.Common.Exceptions.ForbiddenAccessException>(() =>
            fixture.Service.LayDashboardKeToanAsync(null, null));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(AppDbContext context, DichVuDashboardVaiTro service)
        {
            Context = context;
            Service = service;
        }

        public AppDbContext Context { get; }
        public DichVuDashboardVaiTro Service { get; }

        public static async Task<Fixture> TaoMoiAsync(
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
                PaidAt = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc),
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

            context.Users.Add(new ApplicationUser
            {
                Id = "admin",
                UserName = "admin@cms.edu.vn",
                Email = "admin@cms.edu.vn",
                EmployeeCode = "ADMIN001",
                FullName = "Admin",
                EmploymentStatus = EmploymentStatus.Active
            });
            context.Invoices.Add(invoice);
            context.Payments.AddRange(confirmedPayment, cancelledPayment);
            context.Sessions.Add(new Session
            {
                Class = enrollment.Class,
                SessionDate = DateOnly.FromDateTime(DateTime.Today),
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
            return new Fixture(context, new DichVuDashboardVaiTro(
                context,
                new CurrentUser(role, userId ?? role.ToLowerInvariant())));
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class CurrentUser(string role, string userId) : ICurrentUser
    {
        public string? UserId => userId;
        public string? Role => role;
        public bool IsAuthenticated => true;
    }
}
