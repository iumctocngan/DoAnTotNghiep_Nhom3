using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Sessions;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using ValidationException = CmsEdu.Application.Common.Exceptions.ValidationException;

namespace CmsEdu.UnitTests;

public class KiemThuDichVuBuoiHoc
{
    [Fact]
    public async Task TaoBuoiHocAsync_TuChoiBaiHocKhongCungCapDo()
    {
        await using var boGiaLap = await GiaLapBuoiHoc.TaoMoiAsync();

        var loi = await Assert.ThrowsAsync<ValidationException>(() => boGiaLap.DichVu.TaoBuoiHocAsync(
            new YeuCauTaoBuoiHoc(
                boGiaLap.LopHoc.Id,
                boGiaLap.BaiHocCapDoKhac.Id,
                boGiaLap.NgayBuoiHoc,
                new TimeOnly(9, 0),
                new TimeOnly(10, 0),
                null)));

        Assert.Contains("cùng cấp độ", loi.Message);
    }

    [Fact]
    public async Task TaoBuoiHocAsync_TuChoiKhiTrungLichGiaoVien()
    {
        await using var boGiaLap = await GiaLapBuoiHoc.TaoMoiAsync();
        boGiaLap.NguCanh.Sessions.Add(new Session
        {
            ClassId = boGiaLap.LopHoc.Id,
            SessionDate = boGiaLap.NgayBuoiHoc,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0)
        });
        await boGiaLap.NguCanh.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => boGiaLap.DichVu.TaoBuoiHocAsync(
            new YeuCauTaoBuoiHoc(
                boGiaLap.LopHoc.Id,
                null,
                boGiaLap.NgayBuoiHoc,
                new TimeOnly(9, 30),
                new TimeOnly(10, 30),
                null)));
    }

    [Fact]
    public async Task HuyBuoiHocAsync_ChuyenTrangThaiSangCancelledThanhCong()
    {
        await using var boGiaLap = await GiaLapBuoiHoc.TaoMoiAsync();
        var buoiHoc = await boGiaLap.TaoBuoiHocMauAsync();

        var ketQua = await boGiaLap.DichVu.HuyBuoiHocAsync(buoiHoc.Id);

        Assert.Equal(SessionStatus.Cancelled, ketQua.Status);
    }

    [Fact]
    public async Task HoanTatBuoiHocAsync_TuChoiKhiChuaDiemDanhDayDu()
    {
        await using var boGiaLap = await GiaLapBuoiHoc.TaoMoiAsync();
        var buoiHoc = await boGiaLap.TaoBuoiHocMauAsync();
        boGiaLap.NguCanh.Enrollments.Add(new Enrollment
        {
            StudentId = 1,
            ClassId = boGiaLap.LopHoc.Id,
            StartDate = boGiaLap.NgayBuoiHoc.AddDays(-1),
            Status = EnrollmentStatus.Active
        });
        await boGiaLap.NguCanh.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => boGiaLap.DichVu.HoanTatBuoiHocAsync(buoiHoc.Id));
    }

    [Fact]
    public async Task HoanTatBuoiHocAsync_HoanTatKhiTatCaGhiDanhHopLeDaDiemDanh()
    {
        await using var boGiaLap = await GiaLapBuoiHoc.TaoMoiAsync();
        var buoiHoc = await boGiaLap.TaoBuoiHocMauAsync();
        var ghiDanh = new Enrollment
        {
            StudentId = 1,
            ClassId = boGiaLap.LopHoc.Id,
            StartDate = boGiaLap.NgayBuoiHoc.AddDays(-1),
            Status = EnrollmentStatus.Active
        };
        boGiaLap.NguCanh.Enrollments.Add(ghiDanh);
        await boGiaLap.NguCanh.SaveChangesAsync();
        boGiaLap.NguCanh.Attendances.Add(new Attendance
        {
            SessionId = buoiHoc.Id,
            EnrollmentId = ghiDanh.Id,
            Status = AttendanceStatus.Present,
            MarkedBy = "admin",
            MarkedAt = DateTime.UtcNow
        });
        await boGiaLap.NguCanh.SaveChangesAsync();

        var ketQua = await boGiaLap.DichVu.HoanTatBuoiHocAsync(buoiHoc.Id);

        Assert.Equal(SessionStatus.Completed, ketQua.Status);
    }

    private sealed class GiaLapBuoiHoc : IAsyncDisposable
    {
        private GiaLapBuoiHoc(AppDbContext nguCanh, Class thucTheLop, Lesson baiHocCapDoKhac)
        {
            NguCanh = nguCanh;
            LopHoc = thucTheLop;
            BaiHocCapDoKhac = baiHocCapDoKhac;
            DichVu = new DichVuBuoiHoc(nguCanh, new NguoiDungKiemThu(UserRole.Admin, "admin"));
        }

        public AppDbContext NguCanh { get; }
        public Class LopHoc { get; }
        public Lesson BaiHocCapDoKhac { get; }
        public DichVuBuoiHoc DichVu { get; }
        public DateOnly NgayBuoiHoc { get; } = new(2026, 9, 10);

        public static async Task<GiaLapBuoiHoc> TaoMoiAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var nguCanh = new AppDbContext(options);
            var capDo = new Level { Code = "L1", Name = "Level 1", SortOrder = 1 };
            var capDoKhac = new Level { Code = "L2", Name = "Level 2", SortOrder = 1 };
            nguCanh.Levels.AddRange(capDo, capDoKhac);
            await nguCanh.SaveChangesAsync();

            var thucTheLop = new Class
            {
                ClassCode = "CLS-001",
                Name = "Class 1",
                LevelId = capDo.Id,
                MainTeacherUserId = "teacher-1",
                Capacity = 10,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31),
                DayOfWeek = DayOfWeek.Thursday,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0),
                Status = ClassStatus.Active
            };
            var baiHocCapDoKhac = new Lesson
            {
                LevelId = capDoKhac.Id,
                Code = "L2-01",
                Name = "Other level lesson",
                SortOrder = 1
            };
            nguCanh.Classes.Add(thucTheLop);
            nguCanh.Lessons.Add(baiHocCapDoKhac);
            await nguCanh.SaveChangesAsync();

            return new GiaLapBuoiHoc(nguCanh, thucTheLop, baiHocCapDoKhac);
        }

        public Task<PhanHoiBuoiHoc> TaoBuoiHocMauAsync()
        {
            return DichVu.TaoBuoiHocAsync(new YeuCauTaoBuoiHoc(
                LopHoc.Id,
                null,
                NgayBuoiHoc,
                new TimeOnly(9, 0),
                new TimeOnly(10, 0),
                null));
        }

        public ValueTask DisposeAsync() => NguCanh.DisposeAsync();
    }

    private sealed class NguoiDungKiemThu(string vaiTro, string maNguoiDung) : ICurrentUser
    {
        public string? UserId => maNguoiDung;
        public string? EmployeeCode => null;
        public string? Email => null;
        public string? Role => vaiTro;
        public bool IsAuthenticated => true;
    }
}
