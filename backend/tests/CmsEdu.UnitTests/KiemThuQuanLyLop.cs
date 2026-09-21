using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Enrollments;
using CmsEdu.Application.Remarks;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;

namespace CmsEdu.UnitTests;

public class KiemThuQuanLyLop
{
    [Fact]
    public async Task GhiDanh_TuChoiKhiLopDaDuSiSo()
    {
        var kho = new KhoGhiDanhGia { SiSo = 2, Lop = new Class
            { Id = 3, Capacity = 2, Status = ClassStatus.Active,
              StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)) } };
        var dichVu = new DichVuGhiDanh(kho, new NguoiDungGia(UserRole.Admin));

        await Assert.ThrowsAsync<ConflictException>(() => dichVu.Tao(
            new YeuCauGhiDanh(1, 3, DateOnly.FromDateTime(DateTime.Today)), default));
        Assert.Null(kho.DaThem);
    }

    [Fact]
    public async Task NhanXet_TuChoiBuoiHocKhacLop()
    {
        var kho = new KhoNhanXetGia { GhiDanh = new Enrollment
            { Id = 1, ClassId = 3, Class = new Class { MainTeacherUserId = "gv-1" },
              Status = EnrollmentStatus.Active, StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)) },
            BuoiHoc = new Session { Id = 9, ClassId = 4 } };
        var dichVu = new DichVuNhanXetHocVien(kho, new NguoiDungGia());

        await Assert.ThrowsAsync<ValidationException>(() => dichVu.Tao(1,
            new YeuCauTaoNhanXet(9, "Nhận xét"), default));
        Assert.Null(kho.DaThem);
    }

    [Fact]
    public async Task NhanXet_GiaoVienKhongDuocSuaCuaNguoiKhac()
    {
        var kho = new KhoNhanXetGia { GhiDanh = new Enrollment
            { Id = 1, Class = new Class { MainTeacherUserId = "gv-1" } },
            NhanXet = new StudentRemark { Id = 7, EnrollmentId = 1, CreatedBy = "gv-2" } };
        var dichVu = new DichVuNhanXetHocVien(kho, new NguoiDungGia());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => dichVu.Sua(7,
            new YeuCauSuaNhanXet("Nội dung mới"), default));
        Assert.Equal(string.Empty, kho.NhanXet.Content);
    }

    private sealed class NguoiDungGia(string role = UserRole.Teacher) : ICurrentUser
    {
        public string? UserId => "gv-1";
        public string? Role => role;
        public bool IsAuthenticated => true;
    }

    private sealed class KhoGhiDanhGia : IKhoDuLieuGhiDanh
    {
        public Class Lop { get; set; } = new();
        public int SiSo { get; set; }
        public Enrollment? DaThem { get; private set; }
        public Task<List<Enrollment>> LayDanhSachAsync(int? lopId, int? hocVienId, CancellationToken ct) => Task.FromResult(new List<Enrollment>());
        public Task<Enrollment?> TimAsync(int id, CancellationToken ct) => Task.FromResult<Enrollment?>(null);
        public Task<Student?> TimHocVienAsync(int id, CancellationToken ct) => Task.FromResult<Student?>(new Student { Id = id });
        public Task<Class?> TimLopAsync(int id, CancellationToken ct) => Task.FromResult<Class?>(Lop);
        public Task<bool> DaGhiDanhAsync(int id, CancellationToken ct) => Task.FromResult(false);
        public Task<int> DemSiSoAsync(int id, CancellationToken ct) => Task.FromResult(SiSo);
        public void Them(Enrollment x) => DaThem = x;
        public Task LuuAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<T> TrongGiaoDichAsync<T>(Func<Task<T>> congViec, CancellationToken ct) => congViec();
    }

    private sealed class KhoNhanXetGia : IKhoDuLieuNhanXet
    {
        public Enrollment GhiDanh { get; set; } = new();
        public Session BuoiHoc { get; set; } = new();
        public StudentRemark NhanXet { get; set; } = new();
        public StudentRemark? DaThem { get; private set; }
        public Task<PagedResult<StudentRemark>> LayDanhSachAsync(int id, int trang, int kichThuoc,
            CancellationToken ct) => Task.FromResult(new PagedResult<StudentRemark>([], trang, kichThuoc, 0));
        public Task<StudentRemark?> TimAsync(int id, CancellationToken ct) => Task.FromResult<StudentRemark?>(NhanXet);
        public Task<Enrollment?> TimGhiDanhAsync(int id, CancellationToken ct) => Task.FromResult<Enrollment?>(GhiDanh);
        public Task<Session?> TimBuoiHocAsync(int id, CancellationToken ct) => Task.FromResult<Session?>(BuoiHoc);
        public void Them(StudentRemark x) => DaThem = x;
        public Task LuuAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
