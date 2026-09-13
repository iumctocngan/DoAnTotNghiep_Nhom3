using CmsEdu.Application.Attendances;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using ValidationException = CmsEdu.Application.Common.Exceptions.ValidationException;

namespace CmsEdu.UnitTests;

public class KiemThuDichVuDiemDanh
{
    [Fact]
    public async Task LayDanhSachDiemDanhTheoBuoiHocAsync_TraVeDanhSachHocVienHopLeChuaDiemDanh()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync();

        var ketQua = await boGiaLap.DichVu.LayDanhSachDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id);

        Assert.Equal(boGiaLap.BuoiHoc.Id, ketQua.SessionId);
        Assert.Equal(2, ketQua.TongSoHocVien);
        Assert.Equal(2, ketQua.SoLuongChuaDiemDanh);
        Assert.Equal(0, ketQua.SoLuongCoMat);
        Assert.Equal(0, ketQua.SoLuongVangMat);
        Assert.All(ketQua.DanhSachHocVien, item => Assert.Null(item.Status));
    }

    [Fact]
    public async Task LuuDiemDanhTheoBuoiHocAsync_LuuThanhCongTatCaHocVienHopLe()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync();

        var yeuCau = new YeuCauLuuDiemDanhBuoiHoc([
            new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Present, null),
            new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh2.Id, AttendanceStatus.Absent, "Bị ốm có phép")
        ]);

        var ketQua = await boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, yeuCau);

        Assert.Equal(2, ketQua.TongSoHocVien);
        Assert.Equal(1, ketQua.SoLuongCoMat);
        Assert.Equal(1, ketQua.SoLuongVangMat);
        Assert.Equal(0, ketQua.SoLuongChuaDiemDanh);

        var diemDanhDb = await boGiaLap.NguCanh.Attendances
            .Where(item => item.SessionId == boGiaLap.BuoiHoc.Id)
            .ToListAsync();

        Assert.Equal(2, diemDanhDb.Count);
        var hs1 = diemDanhDb.Single(item => item.EnrollmentId == boGiaLap.GhiDanh1.Id);
        Assert.Equal(AttendanceStatus.Present, hs1.Status);
        Assert.Equal("admin", hs1.MarkedBy);

        var hs2 = diemDanhDb.Single(item => item.EnrollmentId == boGiaLap.GhiDanh2.Id);
        Assert.Equal(AttendanceStatus.Absent, hs2.Status);
        Assert.Equal("Bị ốm có phép", hs2.Note);
    }

    [Fact]
    public async Task LuuDiemDanhTheoBuoiHocAsync_CapNhatThanhCongDiemDanhDaTonTai()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync();

        // Lần 1: Lưu vắng mặt
        await boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, new YeuCauLuuDiemDanhBuoiHoc([
            new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Absent, "Vắng chưa rõ lý do"),
            new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh2.Id, AttendanceStatus.Present, null)
        ]));

        // Lần 2: Sửa lại thành có mặt
        var ketQuaCapNhat = await boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, new YeuCauLuuDiemDanhBuoiHoc([
            new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Present, "Đến muộn 15p"),
            new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh2.Id, AttendanceStatus.Present, null)
        ]));

        Assert.Equal(2, ketQuaCapNhat.SoLuongCoMat);
        Assert.Equal(0, ketQuaCapNhat.SoLuongVangMat);

        var hs1 = await boGiaLap.NguCanh.Attendances
            .SingleAsync(item => item.SessionId == boGiaLap.BuoiHoc.Id && item.EnrollmentId == boGiaLap.GhiDanh1.Id);

        Assert.Equal(AttendanceStatus.Present, hs1.Status);
        Assert.Equal("Đến muộn 15p", hs1.Note);
        Assert.Equal("admin", hs1.UpdatedBy);
        Assert.NotNull(hs1.UpdatedAt);
    }

    [Fact]
    public async Task LuuDiemDanhTheoBuoiHocAsync_ChoPhepGiaoVienChinhPhuTrachLop()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync("teacher-1", UserRole.Teacher);

        var ketQua = await boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, new YeuCauLuuDiemDanhBuoiHoc([
            new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Present, null),
            new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh2.Id, AttendanceStatus.Present, null)
        ]));

        Assert.Equal(2, ketQua.SoLuongCoMat);
    }

    [Fact]
    public async Task LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiGiaoVienKhongPhuTrachLop()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync("teacher-khac", UserRole.Teacher);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, new YeuCauLuuDiemDanhBuoiHoc([
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Present, null),
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh2.Id, AttendanceStatus.Present, null)
            ])));
    }

    [Fact]
    public async Task LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiBuoiHocDaHoanThanh()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync();
        boGiaLap.BuoiHoc.Status = SessionStatus.Completed;
        await boGiaLap.NguCanh.SaveChangesAsync();

        var loi = await Assert.ThrowsAsync<ConflictException>(() =>
            boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, new YeuCauLuuDiemDanhBuoiHoc([
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Present, null),
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh2.Id, AttendanceStatus.Present, null)
            ])));

        Assert.Contains("hoàn thành", loi.Message);
    }

    [Fact]
    public async Task LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiBuoiHocBiHuy()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync();
        boGiaLap.BuoiHoc.Status = SessionStatus.Cancelled;
        await boGiaLap.NguCanh.SaveChangesAsync();

        var loi = await Assert.ThrowsAsync<ConflictException>(() =>
            boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, new YeuCauLuuDiemDanhBuoiHoc([
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Present, null),
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh2.Id, AttendanceStatus.Present, null)
            ])));

        Assert.Contains("hủy", loi.Message);
    }

    [Fact]
    public async Task LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiThieuHocVienHopLe()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync();

        // Chỉ gửi 1 trong 2 học viên hợp lệ
        var loi = await Assert.ThrowsAsync<ConflictException>(() =>
            boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, new YeuCauLuuDiemDanhBuoiHoc([
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Present, null)
            ])));

        Assert.Contains("chưa bao phủ đầy đủ", loi.Message);
    }

    [Fact]
    public async Task LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiChuaEnrollmentKhongHopLeHoacTrangThaiKhacActive()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync();

        // Tạo thêm 1 ghi danh trạng thái Paused
        var ghiDanhTamDung = new Enrollment
        {
            StudentId = 99,
            ClassId = boGiaLap.LopHoc.Id,
            StartDate = boGiaLap.BuoiHoc.SessionDate.AddDays(-5),
            Status = EnrollmentStatus.Paused
        };
        boGiaLap.NguCanh.Enrollments.Add(ghiDanhTamDung);
        await boGiaLap.NguCanh.SaveChangesAsync();

        var loi = await Assert.ThrowsAsync<ConflictException>(() =>
            boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, new YeuCauLuuDiemDanhBuoiHoc([
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Present, null),
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh2.Id, AttendanceStatus.Present, null),
                new YeuCauLuuDiemDanhChiTiet(ghiDanhTamDung.Id, AttendanceStatus.Present, null)
            ])));

        Assert.Contains("không hợp lệ", loi.Message);
    }

    [Fact]
    public async Task LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiTrungLapEnrollmentTrongRequest()
    {
        await using var boGiaLap = await GiaLapDiemDanh.TaoMoiAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            boGiaLap.DichVu.LuuDiemDanhTheoBuoiHocAsync(boGiaLap.BuoiHoc.Id, new YeuCauLuuDiemDanhBuoiHoc([
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Present, null),
                new YeuCauLuuDiemDanhChiTiet(boGiaLap.GhiDanh1.Id, AttendanceStatus.Absent, null)
            ])));
    }

    private sealed class GiaLapDiemDanh : IAsyncDisposable
    {
        private GiaLapDiemDanh(
            AppDbContext nguCanh,
            Class lopHoc,
            Session buoiHoc,
            Enrollment ghiDanh1,
            Enrollment ghiDanh2,
            DichVuDiemDanh dichVu)
        {
            NguCanh = nguCanh;
            LopHoc = lopHoc;
            BuoiHoc = buoiHoc;
            GhiDanh1 = ghiDanh1;
            GhiDanh2 = ghiDanh2;
            DichVu = dichVu;
        }

        public AppDbContext NguCanh { get; }
        public Class LopHoc { get; }
        public Session BuoiHoc { get; }
        public Enrollment GhiDanh1 { get; }
        public Enrollment GhiDanh2 { get; }
        public DichVuDiemDanh DichVu { get; }

        public static async Task<GiaLapDiemDanh> TaoMoiAsync(string maNguoiDung = "admin", string vaiTro = UserRole.Admin)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var nguCanh = new AppDbContext(options);

            var lopHoc = new Class
            {
                ClassCode = "CLS-ATTENDANCE",
                Name = "Lớp Toán Nâng Cao",
                LevelId = 1,
                MainTeacherUserId = "teacher-1",
                Capacity = 10,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31),
                Status = ClassStatus.Active
            };
            nguCanh.Classes.Add(lopHoc);
            await nguCanh.SaveChangesAsync();

            var buoiHoc = new Session
            {
                ClassId = lopHoc.Id,
                SessionDate = new DateOnly(2026, 9, 15),
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(19, 30),
                Status = SessionStatus.Scheduled,
                Class = lopHoc
            };
            nguCanh.Sessions.Add(buoiHoc);

            var hocVien1 = new Student { StudentCode = "HV-001", FullName = "Nguyễn Văn A" };
            var hocVien2 = new Student { StudentCode = "HV-002", FullName = "Trần Thị B" };
            nguCanh.Students.AddRange(hocVien1, hocVien2);
            await nguCanh.SaveChangesAsync();

            var ghiDanh1 = new Enrollment
            {
                StudentId = hocVien1.Id,
                ClassId = lopHoc.Id,
                StartDate = new DateOnly(2026, 9, 1),
                Status = EnrollmentStatus.Active,
                Student = hocVien1,
                Class = lopHoc
            };

            var ghiDanh2 = new Enrollment
            {
                StudentId = hocVien2.Id,
                ClassId = lopHoc.Id,
                StartDate = new DateOnly(2026, 9, 5),
                Status = EnrollmentStatus.Active,
                Student = hocVien2,
                Class = lopHoc
            };

            nguCanh.Enrollments.AddRange(ghiDanh1, ghiDanh2);
            await nguCanh.SaveChangesAsync();

            var dichVu = new DichVuDiemDanh(nguCanh, new NguoiDungKiemThu(vaiTro, maNguoiDung));

            return new GiaLapDiemDanh(nguCanh, lopHoc, buoiHoc, ghiDanh1, ghiDanh2, dichVu);
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
