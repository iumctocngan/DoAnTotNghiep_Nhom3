using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Remarks;

public record YeuCauTaoNhanXet(int? MaBuoiHoc, string NoiDung);
public record YeuCauSuaNhanXet(string NoiDung);
public record ThongTinNhanXet(int Id, int MaGhiDanh, int? MaBuoiHoc, string NoiDung,
    string NguoiTao, DateTime NgayTao, string? NguoiSua, DateTime? NgaySua);

public class DichVuNhanXetHocVien(IKhoDuLieuNhanXet kho, ICurrentUser nguoiDung)
{
    public async Task<PagedResult<ThongTinNhanXet>> LayDanhSach(int maGhiDanh, int trang,
        int kichThuocTrang, CancellationToken maHuy)
    {
        if (trang < 1 || kichThuocTrang is < 1 or > 100 || (long)(trang - 1) * kichThuocTrang > int.MaxValue)
            throw new ValidationException("Trang phải từ 1 và kích thước trang phải từ 1 đến 100.");
        await KiemTraPhamViGhiDanh(maGhiDanh, maHuy);
        var ketQua = await kho.LayDanhSachAsync(maGhiDanh, trang, kichThuocTrang, maHuy);
        return new PagedResult<ThongTinNhanXet>(ketQua.Items.Select(ChuyenDoi).ToList(),
            ketQua.Page, ketQua.PageSize, ketQua.TotalItems);
    }

    public async Task<ThongTinNhanXet> LayChiTiet(int id, CancellationToken maHuy)
    {
        var nhanXet = await Tim(id, maHuy);
        await KiemTraPhamViGhiDanh(nhanXet.EnrollmentId, maHuy);
        return ChuyenDoi(nhanXet);
    }

    public async Task<ThongTinNhanXet> Tao(int maGhiDanh, YeuCauTaoNhanXet yeuCau,
        CancellationToken maHuy)
    {
        KiemTraNoiDung(yeuCau.NoiDung);
        var ghiDanh = await KiemTraPhamViGhiDanh(maGhiDanh, maHuy);
        if (ghiDanh.Status is EnrollmentStatus.Completed or EnrollmentStatus.Withdrawn)
            throw new ConflictException("Không thể thêm nhận xét cho ghi danh đã kết thúc.");
        if (yeuCau.MaBuoiHoc.HasValue)
        {
            var buoiHoc = await kho.TimBuoiHocAsync(yeuCau.MaBuoiHoc.Value, maHuy)
                ?? throw new NotFoundException("Không tìm thấy buổi học.");
            if (buoiHoc.ClassId != ghiDanh.ClassId || buoiHoc.Status == SessionStatus.Cancelled ||
                buoiHoc.SessionDate < ghiDanh.StartDate ||
                (ghiDanh.EndDate.HasValue && buoiHoc.SessionDate > ghiDanh.EndDate))
                throw new ValidationException("Buổi học không phù hợp với lớp hoặc thời gian ghi danh.");
        }
        var nhanXet = new StudentRemark
        {
            EnrollmentId = maGhiDanh, SessionId = yeuCau.MaBuoiHoc,
            Content = yeuCau.NoiDung.Trim(), CreatedBy = nguoiDung.UserId!,
            CreatedAt = DateTime.UtcNow
        };
        kho.Them(nhanXet);
        await kho.LuuAsync(maHuy);
        return ChuyenDoi(nhanXet);
    }

    public async Task<ThongTinNhanXet> Sua(int id, YeuCauSuaNhanXet yeuCau,
        CancellationToken maHuy)
    {
        KiemTraNoiDung(yeuCau.NoiDung);
        var nhanXet = await Tim(id, maHuy);
        await KiemTraPhamViGhiDanh(nhanXet.EnrollmentId, maHuy);
        if (nguoiDung.Role == UserRole.Teacher && nhanXet.CreatedBy != nguoiDung.UserId)
            throw new ForbiddenAccessException("Giáo viên chỉ được sửa nhận xét mình tạo trong lớp đang phụ trách.");
        nhanXet.Content = yeuCau.NoiDung.Trim();
        nhanXet.UpdatedBy = nguoiDung.UserId;
        nhanXet.UpdatedAt = DateTime.UtcNow;
        await kho.LuuAsync(maHuy);
        return ChuyenDoi(nhanXet);
    }

    private async Task<StudentRemark> Tim(int id, CancellationToken maHuy) =>
        await kho.TimAsync(id, maHuy) ?? throw new NotFoundException("Không tìm thấy nhận xét.");

    private async Task<Enrollment> KiemTraPhamViGhiDanh(int id, CancellationToken maHuy)
    {
        var ghiDanh = await kho.TimGhiDanhAsync(id, maHuy)
            ?? throw new NotFoundException("Không tìm thấy ghi danh.");
        if (nguoiDung.Role == UserRole.Teacher && ghiDanh.Class.MainTeacherUserId != nguoiDung.UserId)
            throw new ForbiddenAccessException("Giáo viên không phụ trách lớp của ghi danh này.");
        return ghiDanh;
    }

    private static void KiemTraNoiDung(string? noiDung)
    {
        if (string.IsNullOrWhiteSpace(noiDung) || noiDung.Trim().Length > 1000)
            throw new ValidationException("Nội dung nhận xét phải có từ 1 đến 1000 ký tự.");
    }

    private static ThongTinNhanXet ChuyenDoi(StudentRemark x) => new(x.Id, x.EnrollmentId,
        x.SessionId, x.Content, x.CreatedBy, x.CreatedAt, x.UpdatedBy, x.UpdatedAt);
}
