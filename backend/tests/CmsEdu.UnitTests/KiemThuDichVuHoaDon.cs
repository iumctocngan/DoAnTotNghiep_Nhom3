using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Invoices;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using ValidationException = CmsEdu.Application.Common.Exceptions.ValidationException;

namespace CmsEdu.UnitTests;

public class KiemThuDichVuHoaDon
{
    [Fact]
    public async Task TaoHoaDonAsync_ThanhCong_LuuDuLieuKy6ThangVaAuditLogHopLe()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        var ngayBatDau = new DateOnly(2026, 9, 1);
        var ngayDenHan = new DateOnly(2027, 2, 28);
        var yeuCau = new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            ngayBatDau,
            6_000_000m,
            ngayDenHan,
            "Thu học phí kỳ 1");

        var ketQua = await boGiaLap.DichVu.TaoHoaDonAsync(yeuCau);

        Assert.NotNull(ketQua);
        Assert.True(ketQua.Id > 0);
        Assert.StartsWith("INV-202609-", ketQua.SoHoaDon);
        Assert.Equal(boGiaLap.GhiDanhActive.Id, ketQua.EnrollmentId);
        Assert.Equal(boGiaLap.HocVien.Id, ketQua.StudentId);
        Assert.Equal(ngayBatDau, ketQua.NgayBatDauKy);
        Assert.Equal(new DateOnly(2027, 2, 28), ketQua.NgayKetThucKy); // 6 tháng - 1 ngày
        Assert.Equal(6_000_000m, ketQua.SoTienPhaiTra);
        Assert.Equal(InvoiceStatus.Issued, ketQua.TrangThai);
        Assert.Equal(6_000_000m, ketQua.ConNo);
        Assert.Equal(0m, ketQua.TongDaXacNhan);

        // Kiểm tra AuditLog được ghi đúng EntityId với Id thật của hóa đơn
        var audit = await boGiaLap.NguCanh.AuditLogs
            .SingleOrDefaultAsync(x => x.Action == "CREATE_INVOICE" && x.EntityId == ketQua.Id.ToString());
        Assert.NotNull(audit);
        Assert.NotEqual("0", audit.EntityId);
        Assert.Equal("Invoice", audit.EntityType);
        Assert.Contains(ketQua.SoHoaDon, audit.Description);
    }

    [Fact]
    public async Task TaoHoaDonAsync_ThatBai_KhiEnrollmentKhongActive()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        var yeuCau = new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhPaused.Id,
            new DateOnly(2026, 9, 1),
            5_000_000m,
            new DateOnly(2027, 2, 20),
            null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            boGiaLap.DichVu.TaoHoaDonAsync(yeuCau));
        Assert.Contains("Active", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100_000)]
    public async Task TaoHoaDonAsync_ThatBai_KhiAmountDueNhoHonHoacBangKhong(decimal amountDue)
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        var yeuCau = new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            new DateOnly(2026, 9, 1),
            amountDue,
            new DateOnly(2027, 2, 20),
            null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            boGiaLap.DichVu.TaoHoaDonAsync(yeuCau));
        Assert.Contains("lớn hơn 0", ex.Message);
    }

    [Fact]
    public async Task TaoHoaDonAsync_ThatBai_KhiDueDateSauPeriodEnd()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        // PeriodEnd sẽ là 2027-02-28, nếu DueDate là 2027-03-01 -> Lỗi
        var yeuCau = new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            new DateOnly(2026, 9, 1),
            6_000_000m,
            new DateOnly(2027, 3, 1),
            null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            boGiaLap.DichVu.TaoHoaDonAsync(yeuCau));
        Assert.Contains("Ngày đến hạn không được sau", ex.Message);
    }

    [Fact]
    public async Task TaoHoaDonAsync_ThatBai_KhiTrungKyHocPhiChuaHuy()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        var yeuCau1 = new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            new DateOnly(2026, 9, 1),
            6_000_000m,
            new DateOnly(2027, 2, 28),
            null);
        await boGiaLap.DichVu.TaoHoaDonAsync(yeuCau1);

        // Kỳ thứ 2 chồng lấn vào kỳ 1 (bắt đầu 2026-11-01)
        var yeuCau2 = new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            new DateOnly(2026, 11, 1),
            6_000_000m,
            new DateOnly(2027, 4, 30),
            null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            boGiaLap.DichVu.TaoHoaDonAsync(yeuCau2));
        Assert.Contains("chồng lấn", ex.Message);
    }

    [Fact]
    public async Task HuyHoaDonAsync_ThanhCong_CapNhatTrangThaiVaGhiAuditLog()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        var hoaDon = await boGiaLap.DichVu.TaoHoaDonAsync(new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            new DateOnly(2026, 9, 1),
            6_000_000m,
            new DateOnly(2027, 2, 28),
            null));

        var lyDo = "Hủy theo yêu cầu của ban quản lý";
        var ketQua = await boGiaLap.DichVu.HuyHoaDonAsync(hoaDon.Id, new YeuCauHuyHoaDon(lyDo));

        Assert.Equal(InvoiceStatus.Cancelled, ketQua.TrangThai);
        Assert.Equal(lyDo, ketQua.LyDoHuy);
        Assert.Equal("admin", ketQua.NguoiHuy);
        Assert.NotNull(ketQua.NgayHuy);
        Assert.Equal(0m, ketQua.ConNo);

        // Kiểm tra trong CSDL
        var hoaDonDb = await boGiaLap.NguCanh.Invoices.FindAsync(hoaDon.Id);
        Assert.NotNull(hoaDonDb);
        Assert.Equal(InvoiceStatus.Cancelled, hoaDonDb.Status);
        Assert.Equal(lyDo, hoaDonDb.CancelReason);

        // Kiểm tra AuditLog
        var audit = await boGiaLap.NguCanh.AuditLogs
            .SingleOrDefaultAsync(x => x.Action == "CANCEL_INVOICE" && x.EntityId == hoaDon.Id.ToString());
        Assert.NotNull(audit);
        Assert.Contains(lyDo, audit.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HuyHoaDonAsync_ThatBai_KhiLyDoRong(string lyDo)
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        var hoaDon = await boGiaLap.DichVu.TaoHoaDonAsync(new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            new DateOnly(2026, 9, 1),
            6_000_000m,
            new DateOnly(2027, 2, 28),
            null));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            boGiaLap.DichVu.HuyHoaDonAsync(hoaDon.Id, new YeuCauHuyHoaDon(lyDo)));
        Assert.Contains("Lý do hủy không được để trống", ex.Message);
    }

    [Fact]
    public async Task HuyHoaDonAsync_ThatBai_KhiHoaDonDaHuyTruocDo()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        var hoaDon = await boGiaLap.DichVu.TaoHoaDonAsync(new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            new DateOnly(2026, 9, 1),
            6_000_000m,
            new DateOnly(2027, 2, 28),
            null));

        await boGiaLap.DichVu.HuyHoaDonAsync(hoaDon.Id, new YeuCauHuyHoaDon("Hủy lần 1"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            boGiaLap.DichVu.HuyHoaDonAsync(hoaDon.Id, new YeuCauHuyHoaDon("Hủy lần 2")));
        Assert.Contains("đã bị hủy trước đó", ex.Message);
    }

    [Fact]
    public async Task HuyHoaDonAsync_ThatBai_KhiHoaDonDaPaid()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        var hoaDon = new Invoice
        {
            InvoiceNumber = "INV-PAID-001",
            EnrollmentId = boGiaLap.GhiDanhActive.Id,
            PeriodStart = new DateOnly(2026, 9, 1),
            PeriodEnd = new DateOnly(2027, 2, 28),
            AmountDue = 6_000_000m,
            DueDate = new DateOnly(2027, 2, 28),
            Status = InvoiceStatus.Paid,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        boGiaLap.NguCanh.Invoices.Add(hoaDon);
        await boGiaLap.NguCanh.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            boGiaLap.DichVu.HuyHoaDonAsync(hoaDon.Id, new YeuCauHuyHoaDon("Lý do hủy")));
        Assert.Contains("đã thanh toán đủ", ex.Message);
    }

    [Fact]
    public async Task HuyHoaDonAsync_ThatBai_KhiHoaDonDaCoThanhToanConfirmed()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();
        var hoaDon = new Invoice
        {
            InvoiceNumber = "INV-PARTIAL-001",
            EnrollmentId = boGiaLap.GhiDanhActive.Id,
            PeriodStart = new DateOnly(2026, 9, 1),
            PeriodEnd = new DateOnly(2027, 2, 28),
            AmountDue = 6_000_000m,
            DueDate = new DateOnly(2027, 2, 28),
            Status = InvoiceStatus.Partial,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        boGiaLap.NguCanh.Invoices.Add(hoaDon);
        await boGiaLap.NguCanh.SaveChangesAsync();

        var payment = new Payment
        {
            InvoiceId = hoaDon.Id,
            PaymentNumber = "PAY-001",
            ReceiptNumber = "REC-001",
            Amount = 2_000_000m,
            Status = PaymentStatus.Confirmed,
            Method = PaymentMethod.Cash,
            PaidAt = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        boGiaLap.NguCanh.Payments.Add(payment);
        await boGiaLap.NguCanh.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            boGiaLap.DichVu.HuyHoaDonAsync(hoaDon.Id, new YeuCauHuyHoaDon("Lý do hủy")));
        Assert.Contains("đã phát sinh thanh toán được xác nhận", ex.Message);
    }

    [Fact]
    public async Task TinhCongNoHocVienAsync_TinhDungCongNoTheoTongPhaiThuVaTongDaXacNhan()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();

        // Invoice 1: 6,000,000 -> Có 1 payment Confirmed 2,000,000 và 1 Pending 1,000,000
        var hd1 = new Invoice
        {
            InvoiceNumber = "INV-DEBT-001",
            EnrollmentId = boGiaLap.GhiDanhActive.Id,
            PeriodStart = new DateOnly(2026, 1, 1),
            PeriodEnd = new DateOnly(2026, 6, 30),
            AmountDue = 6_000_000m,
            DueDate = new DateOnly(2026, 6, 30),
            Status = InvoiceStatus.Partial,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        // Invoice 2: 5,000,000 -> Chưa thanh toán
        var hd2 = new Invoice
        {
            InvoiceNumber = "INV-DEBT-002",
            EnrollmentId = boGiaLap.GhiDanhActive.Id,
            PeriodStart = new DateOnly(2026, 7, 1),
            PeriodEnd = new DateOnly(2026, 12, 31),
            AmountDue = 5_000_000m,
            DueDate = new DateOnly(2026, 12, 31),
            Status = InvoiceStatus.Issued,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        // Invoice 3: 4,000,000 -> Đã hủy (không được tính vào công nợ)
        var hd3 = new Invoice
        {
            InvoiceNumber = "INV-DEBT-003",
            EnrollmentId = boGiaLap.GhiDanhActive.Id,
            PeriodStart = new DateOnly(2027, 1, 1),
            PeriodEnd = new DateOnly(2027, 6, 30),
            AmountDue = 4_000_000m,
            DueDate = new DateOnly(2027, 6, 30),
            Status = InvoiceStatus.Cancelled,
            CancelReason = "Hủy kỳ học",
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        boGiaLap.NguCanh.Invoices.AddRange(hd1, hd2, hd3);
        await boGiaLap.NguCanh.SaveChangesAsync();

        var payConfirmed = new Payment
        {
            InvoiceId = hd1.Id,
            PaymentNumber = "PAY-C",
            ReceiptNumber = "REC-C",
            Amount = 2_000_000m,
            Status = PaymentStatus.Confirmed,
            Method = PaymentMethod.BankTransfer,
            PaidAt = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        var payCancelled = new Payment
        {
            InvoiceId = hd1.Id,
            PaymentNumber = "PAY-CANC",
            ReceiptNumber = "REC-CANC",
            Amount = 1_000_000m,
            Status = PaymentStatus.Cancelled, // Đã hủy không được tính vào số đã thu
            Method = PaymentMethod.BankTransfer,
            PaidAt = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        boGiaLap.NguCanh.Payments.AddRange(payConfirmed, payCancelled);
        await boGiaLap.NguCanh.SaveChangesAsync();

        var ketQua = await boGiaLap.DichVu.TinhCongNoHocVienAsync(boGiaLap.HocVien.Id);

        // TongPhaiTra = 6tr + 5tr = 11tr (hd3 bị hủy bỏ qua)
        Assert.Equal(11_000_000m, ketQua.TongPhaiTra);
        // TongDaXacNhan = 2tr (payCancelled không tính)
        Assert.Equal(2_000_000m, ketQua.TongDaXacNhan);
        // ConNo = 11tr - 2tr = 9tr
        Assert.Equal(9_000_000m, ketQua.ConNo);
        Assert.Equal(3, ketQua.DanhSachHoaDon.Count);
    }

    [Fact]
    public async Task LayDanhSachHocVienConNoAsync_TraVeHocVienConNoGiamDan()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync();

        // Thêm học viên thứ 2
        var hocVien2 = new Student { StudentCode = "HV-002", FullName = "Học Viên 2" };
        boGiaLap.NguCanh.Students.Add(hocVien2);
        await boGiaLap.NguCanh.SaveChangesAsync();

        var ghiDanhHv2 = new Enrollment
        {
            StudentId = hocVien2.Id,
            ClassId = boGiaLap.LopHoc.Id,
            StartDate = new DateOnly(2026, 9, 1),
            Status = EnrollmentStatus.Active
        };
        boGiaLap.NguCanh.Enrollments.Add(ghiDanhHv2);
        await boGiaLap.NguCanh.SaveChangesAsync();

        // Học viên 1: Hóa đơn 10,000,000, trả 2,000,000 -> Còn nợ 8,000,000
        var hdHv1 = new Invoice
        {
            InvoiceNumber = "INV-HV1",
            EnrollmentId = boGiaLap.GhiDanhActive.Id,
            PeriodStart = new DateOnly(2026, 1, 1),
            PeriodEnd = new DateOnly(2026, 6, 30),
            AmountDue = 10_000_000m,
            DueDate = new DateOnly(2026, 6, 30),
            Status = InvoiceStatus.Partial,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        boGiaLap.NguCanh.Invoices.Add(hdHv1);
        await boGiaLap.NguCanh.SaveChangesAsync();

        boGiaLap.NguCanh.Payments.Add(new Payment
        {
            InvoiceId = hdHv1.Id,
            PaymentNumber = "PAY-HV1",
            ReceiptNumber = "REC-HV1",
            Amount = 2_000_000m,
            Status = PaymentStatus.Confirmed,
            CreatedBy = "admin"
        });

        // Học viên 2: Hóa đơn 5,000,000, chưa trả -> Còn nợ 5,000,000
        var hdHv2 = new Invoice
        {
            InvoiceNumber = "INV-HV2",
            EnrollmentId = ghiDanhHv2.Id,
            PeriodStart = new DateOnly(2026, 1, 1),
            PeriodEnd = new DateOnly(2026, 6, 30),
            AmountDue = 5_000_000m,
            DueDate = new DateOnly(2026, 6, 30),
            Status = InvoiceStatus.Issued,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        boGiaLap.NguCanh.Invoices.Add(hdHv2);
        await boGiaLap.NguCanh.SaveChangesAsync();

        var ketQua = await boGiaLap.DichVu.LayDanhSachHocVienConNoAsync(1, 20);

        Assert.Equal(2, ketQua.TotalItems);
        // HV1 (nợ 8tr) phải đứng trước HV2 (nợ 5tr)
        Assert.Equal(boGiaLap.HocVien.Id, ketQua.Items[0].StudentId);
        Assert.Equal(8_000_000m, ketQua.Items[0].ConNo);

        Assert.Equal(hocVien2.Id, ketQua.Items[1].StudentId);
        Assert.Equal(5_000_000m, ketQua.Items[1].ConNo);
    }

    [Fact]
    public async Task KiemTraQuyen_ChanCacVaiTroKhongPhaiAdminHoacKeToan()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync(vaiTro: UserRole.Teacher);

        var yeuCau = new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            new DateOnly(2026, 9, 1),
            6_000_000m,
            new DateOnly(2027, 2, 28),
            null);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            boGiaLap.DichVu.TaoHoaDonAsync(yeuCau));
        Assert.Contains("Bạn không có quyền thao tác hóa đơn", ex.Message);
    }

    [Fact]
    public async Task KiemTraQuyen_KeToanDuocPhepThaoTacHoaDon()
    {
        await using var boGiaLap = await GiaLapHoaDon.TaoMoiAsync(vaiTro: UserRole.Accountant);

        var yeuCau = new YeuCauTaoHoaDon(
            boGiaLap.GhiDanhActive.Id,
            new DateOnly(2026, 9, 1),
            6_000_000m,
            new DateOnly(2027, 2, 28),
            null);

        var ketQua = await boGiaLap.DichVu.TaoHoaDonAsync(yeuCau);
        Assert.NotNull(ketQua);
        Assert.Equal(InvoiceStatus.Issued, ketQua.TrangThai);
    }

    // -------------------------------------------------------------------------
    // HỘP GIẢ LẬP DỮ LIỆU KIỂM THỬ INVOICE
    // -------------------------------------------------------------------------
    private sealed class GiaLapHoaDon
    {
        private GiaLapHoaDon(
            AppDbContext nguCanh,
            Student hocVien,
            Class lopHoc,
            Enrollment ghiDanhActive,
            Enrollment ghiDanhPaused,
            DichVuHoaDon dichVu)
        {
            NguCanh = nguCanh;
            HocVien = hocVien;
            LopHoc = lopHoc;
            GhiDanhActive = ghiDanhActive;
            GhiDanhPaused = ghiDanhPaused;
            DichVu = dichVu;
        }

        public AppDbContext NguCanh { get; }
        public Student HocVien { get; }
        public Class LopHoc { get; }
        public Enrollment GhiDanhActive { get; }
        public Enrollment GhiDanhPaused { get; }
        public DichVuHoaDon DichVu { get; }

        public static async Task<GiaLapHoaDon> TaoMoiAsync(string maNguoiDung = "admin", string vaiTro = UserRole.Admin)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var nguCanh = new AppDbContext(options);

            var hocVien = new Student
            {
                StudentCode = "HV-001",
                FullName = "Nguyễn Văn A"
            };
            nguCanh.Students.Add(hocVien);
            await nguCanh.SaveChangesAsync();

            var lopHoc = new Class
            {
                ClassCode = "CLS-MATH-01",
                Name = "Lớp Toán Cơ Bản",
                LevelId = 1,
                MainTeacherUserId = "teacher-01",
                Capacity = 15,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2027, 8, 31),
                Status = ClassStatus.Active
            };
            nguCanh.Classes.Add(lopHoc);
            await nguCanh.SaveChangesAsync();

            var ghiDanhActive = new Enrollment
            {
                StudentId = hocVien.Id,
                ClassId = lopHoc.Id,
                StartDate = new DateOnly(2026, 9, 1),
                Status = EnrollmentStatus.Active,
                Student = hocVien,
                Class = lopHoc
            };

            var ghiDanhPaused = new Enrollment
            {
                StudentId = hocVien.Id,
                ClassId = lopHoc.Id,
                StartDate = new DateOnly(2026, 9, 1),
                Status = EnrollmentStatus.Paused,
                Student = hocVien,
                Class = lopHoc
            };

            nguCanh.Enrollments.AddRange(ghiDanhActive, ghiDanhPaused);
            await nguCanh.SaveChangesAsync();

            var nguoiDung = new NguoiDungKiemThu(vaiTro, maNguoiDung);
            var dichVu = new DichVuHoaDon(nguCanh, nguoiDung);

            return new GiaLapHoaDon(nguCanh, hocVien, lopHoc, ghiDanhActive, ghiDanhPaused, dichVu);
        }

        public ValueTask DisposeAsync() => NguCanh.DisposeAsync();
    }

    private sealed class NguoiDungKiemThu(string vaiTro, string maNguoiDung) : ICurrentUser
    {
        public string? UserId => maNguoiDung;
        public string? Role => vaiTro;
        public bool IsAuthenticated => true;
    }
}
