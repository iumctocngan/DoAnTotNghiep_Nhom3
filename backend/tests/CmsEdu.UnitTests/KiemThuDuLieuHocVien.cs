using System.ComponentModel.DataAnnotations;
using CmsEdu.Application.Students;
namespace CmsEdu.UnitTests;
public class KiemThuDuLieuHocVien
{
    [Fact]
    public void TuChoiKhiThieuNgaySinh()
    {
        var yeuCau = new YeuCauTaoHocVien("HV001", "An", default, null, null);
        Assert.Throws<ValidationException>(() => Validator.ValidateObject(yeuCau, new ValidationContext(yeuCau), true));
    }
    [Fact]
    public void ChapNhanDoDaiToiDaVaTruongKhongBatBuoc()
    {
        var yeuCau = new YeuCauTaoHocVien(new string('A', 50), new string('B', 100), new(2019, 1, 1), null, new string('C', 500));
        Validator.ValidateObject(yeuCau, new ValidationContext(yeuCau), true);
    }
    [Fact]
    public void TuChoiGhiChuVuotDoDai()
    {
        var yeuCau = new YeuCauCapNhatHocVien("HV001", "An", new(2019, 1, 1), null, new string('C', 501));
        Assert.Throws<ValidationException>(() => Validator.ValidateObject(yeuCau, new ValidationContext(yeuCau), true));
    }
}
