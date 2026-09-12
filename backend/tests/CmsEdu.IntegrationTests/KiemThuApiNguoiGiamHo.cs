using System.Net;
using System.Net.Http.Json;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Guardians;
using CmsEdu.Application.Students;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UngDungKiemThu = CmsEdu.IntegrationTests.KiemThuApiHocVien.UngDungKiemThu;

namespace CmsEdu.IntegrationTests;

public sealed class KiemThuApiNguoiGiamHo
{
    private static YeuCauNguoiGiamHo DuLieu(string ten = "Nguyễn Lan", bool hoatDong = true) => new(ten, "0912345678", "lan@example.com", hoatDong);

    private static async Task<PhanHoiNguoiGiamHo> TaoNguoiGiamHo(HttpClient khach, string ten = "Nguyễn Lan")
    {
        var phanHoi = await khach.PostAsJsonAsync("/api/guardians", DuLieu(ten));
        Assert.Equal(HttpStatusCode.Created, phanHoi.StatusCode);
        Assert.NotNull(phanHoi.Headers.Location);
        return (await phanHoi.Content.ReadFromJsonAsync<PhanHoiNguoiGiamHo>())!;
    }

    private static async Task<int> TaoHocVien(HttpClient khach)
    {
        var phanHoi = await khach.PostAsJsonAsync("/api/students", new YeuCauTaoHocVien("HV-GH", "Học viên", new(2019, 1, 1), null, null));
        Assert.Equal(HttpStatusCode.Created, phanHoi.StatusCode);
        return (await phanHoi.Content.ReadFromJsonAsync<PhanHoiHocVien>())!.Id;
    }

    [KiemThuSqlServer]
    public async Task CrudTraCuuPhanTrangVaNhatKy()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var khach = ungDung.TaoTrinhKhach();
        var nguoiGiamHo = await TaoNguoiGiamHo(khach, "  Nguyễn Lan  ");
        var duongDan = $"/api/guardians/{nguoiGiamHo.Id}";
        Assert.Equal("Nguyễn Lan", (await khach.GetFromJsonAsync<PhanHoiNguoiGiamHo>(duongDan))!.FullName);
        Assert.Single((await khach.GetFromJsonAsync<PagedResult<PhanHoiNguoiGiamHo>>("/api/guardians?search=0912345678&pageSize=1"))!.Items);
        Assert.Empty((await khach.GetFromJsonAsync<PagedResult<PhanHoiNguoiGiamHo>>("/api/guardians?page=2&pageSize=1"))!.Items);
        Assert.Equal(HttpStatusCode.OK, (await khach.PutAsJsonAsync(duongDan, DuLieu("Tên mới", false))).StatusCode);
        Assert.Empty((await khach.GetFromJsonAsync<PagedResult<PhanHoiNguoiGiamHo>>("/api/guardians?isActive=true"))!.Items);
        Assert.Single((await khach.GetFromJsonAsync<PagedResult<PhanHoiNguoiGiamHo>>("/api/guardians?isActive=false&search=lan%40example.com"))!.Items);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.DeleteAsync(duongDan)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await khach.GetAsync(duongDan)).StatusCode);
        using var phamVi = ungDung.Services.CreateScope();
        var hanhDong = await phamVi.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.OrderBy(nk => nk.Id).Select(nk => nk.Action).ToListAsync();
        Assert.Equal(new[] { "Guardian.Create", "Guardian.Update", "Guardian.Delete" }, hanhDong);
    }

    [KiemThuSqlServer]
    public async Task LienKetChuyenNguoiChinhVaBaoVeDuLieu()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var khach = ungDung.TaoTrinhKhach();
        var maHocVien = await TaoHocVien(khach);
        var me = await TaoNguoiGiamHo(khach);
        var bo = await TaoNguoiGiamHo(khach, "Nguyễn Nam");
        var duongDan = $"/api/students/{maHocVien}/guardians";
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(me.Id, "Mẹ"))).StatusCode);
        Assert.True(Assert.Single((await khach.GetFromJsonAsync<List<PhanHoiLienKetNguoiGiamHo>>(duongDan))!).IsPrimary);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(bo.Id, "Bố"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await khach.DeleteAsync($"{duongDan}/{me.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await khach.DeleteAsync($"/api/guardians/{me.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await khach.PutAsJsonAsync($"/api/guardians/{me.Id}", DuLieu(hoatDong: false))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PutAsync($"{duongDan}/{bo.Id}/primary", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PutAsync($"{duongDan}/{bo.Id}/primary", null)).StatusCode);
        var danhSach = (await khach.GetFromJsonAsync<List<PhanHoiLienKetNguoiGiamHo>>(duongDan))!;
        Assert.Equal(bo.Id, Assert.Single(danhSach, lk => lk.IsPrimary).GuardianId);
        // Gắn lại cập nhật quan hệ, không tạo trùng và không bỏ người chính.
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(bo.Id, "Cha"))).StatusCode);
        danhSach = (await khach.GetFromJsonAsync<List<PhanHoiLienKetNguoiGiamHo>>(duongDan))!;
        Assert.Equal(2, danhSach.Count);
        Assert.Equal("Cha", Assert.Single(danhSach, lk => lk.IsPrimary).Relationship);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PutAsJsonAsync($"{duongDan}/{bo.Id}", new YeuCauCapNhatLienKetNguoiGiamHo("Bố ruột"))).StatusCode);
        Assert.Equal("Bố ruột", Assert.Single((await khach.GetFromJsonAsync<List<PhanHoiLienKetNguoiGiamHo>>(duongDan))!, lk => lk.IsPrimary).Relationship);
        // Chọn qua POST cũng phải bỏ người chính cũ trước khi ghi người mới.
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(me.Id, "Mẹ", true))).StatusCode);
        Assert.Equal(me.Id, Assert.Single((await khach.GetFromJsonAsync<List<PhanHoiLienKetNguoiGiamHo>>(duongDan))!, lk => lk.IsPrimary).GuardianId);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.DeleteAsync($"{duongDan}/{bo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.DeleteAsync($"{duongDan}/{me.Id}")).StatusCode);
        Assert.Empty((await khach.GetFromJsonAsync<List<PhanHoiLienKetNguoiGiamHo>>(duongDan))!);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.DeleteAsync($"/api/guardians/{me.Id}")).StatusCode);
        using var phamVi = ungDung.Services.CreateScope();
        Assert.Equal(1, await phamVi.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.CountAsync(nk => nk.Action == "StudentGuardian.SetPrimary"));
    }

    [KiemThuSqlServer]
    public async Task PhanQuyenDuLieuVaHoSoLuuTru()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var khach = ungDung.TaoTrinhKhach();
        using var chuaDangNhap = ungDung.TaoTrinhKhach(null);
        using var giaoVien = ungDung.TaoTrinhKhach("Teacher");
        using var keToan = ungDung.TaoTrinhKhach("Accountant");
        using var chamSoc = ungDung.TaoTrinhKhach("CustomerCare");
        Assert.Equal(HttpStatusCode.Unauthorized, (await chuaDangNhap.GetAsync("/api/guardians")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await giaoVien.GetAsync("/api/guardians")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await keToan.PostAsJsonAsync("/api/guardians", DuLieu())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await khach.PostAsJsonAsync("/api/guardians", DuLieu() with { FullName = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await khach.PostAsJsonAsync("/api/guardians", DuLieu() with { Phone = "abc" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await khach.PostAsJsonAsync("/api/guardians", DuLieu() with { Email = "abc" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await khach.GetAsync("/api/guardians?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await khach.GetAsync("/api/guardians?page=2147483647&pageSize=100")).StatusCode);
        var maHocVien = await TaoHocVien(khach);
        var nguoiGiamHo = await TaoNguoiGiamHo(chamSoc);
        var duongDan = $"/api/students/{maHocVien}/guardians";
        Assert.Equal(HttpStatusCode.Forbidden, (await chamSoc.DeleteAsync($"/api/guardians/{nguoiGiamHo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await giaoVien.GetAsync(duongDan)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await keToan.GetAsync(duongDan)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await giaoVien.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(nguoiGiamHo.Id, "Mẹ"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(nguoiGiamHo.Id, " "))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await khach.PutAsync($"{duongDan}/{nguoiGiamHo.Id}/primary", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await khach.PutAsJsonAsync($"{duongDan}/{nguoiGiamHo.Id}", new YeuCauCapNhatLienKetNguoiGiamHo("Mẹ"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await khach.PutAsJsonAsync($"{duongDan}/{nguoiGiamHo.Id}", new YeuCauCapNhatLienKetNguoiGiamHo(" "))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(int.MaxValue, "Mẹ"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await khach.PutAsJsonAsync($"/api/guardians/{nguoiGiamHo.Id}", DuLieu(hoatDong: false))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(nguoiGiamHo.Id, "Mẹ"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await khach.PutAsJsonAsync($"/api/guardians/{nguoiGiamHo.Id}", DuLieu())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await chamSoc.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(nguoiGiamHo.Id, "Mẹ"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PostAsync($"/api/students/{maHocVien}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await khach.DeleteAsync($"{duongDan}/{nguoiGiamHo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await khach.PutAsync($"{duongDan}/{nguoiGiamHo.Id}/primary", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(nguoiGiamHo.Id, "Mẹ"))).StatusCode);
    }

    [KiemThuSqlServer]
    public async Task ChuyenNguoiChinhDongThoiKhongTaoHaiNguoiChinh()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var khach = ungDung.TaoTrinhKhach();
        var maHocVien = await TaoHocVien(khach);
        var me = await TaoNguoiGiamHo(khach);
        var bo = await TaoNguoiGiamHo(khach, "Nguyễn Nam");
        var duongDan = $"/api/students/{maHocVien}/guardians";
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(me.Id, "Mẹ"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await khach.PostAsJsonAsync(duongDan, new YeuCauLienKetNguoiGiamHo(bo.Id, "Bố"))).StatusCode);
        var ketQua = await Task.WhenAll(khach.PutAsync($"{duongDan}/{me.Id}/primary", null), khach.PutAsync($"{duongDan}/{bo.Id}/primary", null));
        Assert.All(ketQua, phanHoi => Assert.Contains(phanHoi.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }));
        Assert.Contains(ketQua, phanHoi => phanHoi.StatusCode == HttpStatusCode.NoContent);
        Assert.Single((await khach.GetFromJsonAsync<List<PhanHoiLienKetNguoiGiamHo>>(duongDan))!, lk => lk.IsPrimary);
    }
}
