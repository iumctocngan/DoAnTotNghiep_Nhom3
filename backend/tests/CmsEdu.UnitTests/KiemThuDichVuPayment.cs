using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Payments;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.UnitTests;

public class KiemThuDichVuPayment
{
    [Fact]
    public async Task TaoPaymentAsync_ThanhToanMotPhan_CapNhatInvoicePartial()
    {
        await using var fixture = await Fixture.TaoMoiAsync();

        var result = await fixture.Service.TaoPaymentAsync(
            fixture.Invoice.Id,
            new YeuCauTaoPayment(
                2_000_000m,
                new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero),
                PaymentMethod.BankTransfer,
                "Chuyển khoản"));

        Assert.Equal(PaymentStatus.Confirmed, result.Status);
        Assert.StartsWith("PAY-202609-", result.PaymentNumber);
        Assert.StartsWith("REC-202609-", result.ReceiptNumber);
        Assert.Equal(InvoiceStatus.Partial, fixture.Invoice.Status);
        Assert.Single(await fixture.Context.Payments.ToListAsync());
        Assert.Contains(
            await fixture.Context.AuditLogs.ToListAsync(),
            log => log.Action == "CREATE_PAYMENT" && log.EntityType == "Payment");
    }

    [Fact]
    public async Task TaoPaymentAsync_TaoHaiPayment_SinhReceiptNumberKhacNhau()
    {
        await using var fixture = await Fixture.TaoMoiAsync();

        var firstPayment = await fixture.Service.TaoPaymentAsync(
            fixture.Invoice.Id,
            new YeuCauTaoPayment(
                2_000_000m,
                new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero),
                PaymentMethod.Cash,
                null));
        var secondPayment = await fixture.Service.TaoPaymentAsync(
            fixture.Invoice.Id,
            new YeuCauTaoPayment(
                1_000_000m,
                new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero),
                PaymentMethod.Cash,
                null));

        Assert.NotEqual(firstPayment.ReceiptNumber, secondPayment.ReceiptNumber);
        Assert.All(new[] { firstPayment, secondPayment }, payment =>
            Assert.StartsWith("REC-202609-", payment.ReceiptNumber));
    }

    [Fact]
    public async Task TaoPaymentAsync_ThanhToanDu_CapNhatInvoicePaid()
    {
        await using var fixture = await Fixture.TaoMoiAsync();

        await fixture.Service.TaoPaymentAsync(
            fixture.Invoice.Id,
            new YeuCauTaoPayment(
                6_000_000m,
                DateTimeOffset.UtcNow,
                PaymentMethod.Cash,
                null));

        Assert.Equal(InvoiceStatus.Paid, fixture.Invoice.Status);
    }

    [Fact]
    public async Task TaoPaymentAsync_KhongChoVuotAmountDue()
    {
        await using var fixture = await Fixture.TaoMoiAsync();

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.TaoPaymentAsync(
                fixture.Invoice.Id,
                new YeuCauTaoPayment(
                    6_000_001m,
                    DateTimeOffset.UtcNow,
                    PaymentMethod.Cash,
                    null)));

        Assert.Contains("vượt quá", exception.Message);
    }

    [Fact]
    public async Task HuyPaymentAsync_KhongXoaPaymentVaCapNhatLaiInvoice()
    {
        await using var fixture = await Fixture.TaoMoiAsync();
        var payment = await fixture.Service.TaoPaymentAsync(
            fixture.Invoice.Id,
            new YeuCauTaoPayment(
                2_000_000m,
                DateTimeOffset.UtcNow,
                PaymentMethod.Cash,
                null));

        var result = await fixture.Service.HuyPaymentAsync(
            payment.Id,
            new YeuCauHuyPayment("Ghi nhận nhầm số tiền"));

        Assert.Equal(PaymentStatus.Cancelled, result.Status);
        Assert.Equal(InvoiceStatus.Issued, fixture.Invoice.Status);
        Assert.Single(await fixture.Context.Payments.ToListAsync());
        Assert.Equal("Ghi nhận nhầm số tiền", result.CancelReason);
        Assert.Contains(
            await fixture.Context.AuditLogs.ToListAsync(),
            log => log.Action == "CANCEL_PAYMENT" && log.EntityId == payment.Id.ToString());
    }

    [Fact]
    public async Task KiemTraQuyen_TuChoiTeacher()
    {
        await using var fixture = await Fixture.TaoMoiAsync(UserRole.Teacher);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            fixture.Service.TaoPaymentAsync(
                fixture.Invoice.Id,
                new YeuCauTaoPayment(
                    1_000_000m,
                    DateTimeOffset.UtcNow,
                    PaymentMethod.Cash,
                    null)));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(AppDbContext context, Invoice invoice, DichVuPayment service)
        {
            Context = context;
            Invoice = invoice;
            Service = service;
        }

        public AppDbContext Context { get; }
        public Invoice Invoice { get; }
        public DichVuPayment Service { get; }

        public static async Task<Fixture> TaoMoiAsync(string role = UserRole.Admin)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new AppDbContext(options);
            var student = new Student
            {
                StudentCode = "HV-001",
                FullName = "Nguyễn Văn A"
            };
            var classEntity = new Class
            {
                ClassCode = "CLS-001",
                Name = "Lớp Toán",
                MainTeacherUserId = "teacher-01",
                Capacity = 15,
                StartDate = new DateOnly(2026, 9, 1),
                Status = ClassStatus.Active
            };
            var enrollment = new Enrollment
            {
                Student = student,
                Class = classEntity,
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
                Status = InvoiceStatus.Issued,
                CreatedBy = "admin",
                CreatedAt = DateTime.UtcNow
            };

            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            return new Fixture(context, invoice, new DichVuPayment(context, new CurrentUser(role)));
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class CurrentUser(string role) : ICurrentUser
    {
        public string? UserId => "admin";
        public string? Role => role;
        public bool IsAuthenticated => true;
    }
}
