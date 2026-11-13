using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using CmsEdu.Application.Assessments;

namespace CmsEdu.Api.Controllers;

internal static class DocTepClo
{
    private static readonly XNamespace BangTinh = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace QuanHe = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static IReadOnlyList<Criterion> Doc(Stream luong)
    {
        using var tep = new ZipArchive(luong, ZipArchiveMode.Read);
        var soLamViec = DocXml(tep, "xl/workbook.xml");
        var trang = soLamViec.Descendants(BangTinh + "sheet")
            .SingleOrDefault(x => (string?)x.Attribute("name") == "Danh sách CLO chi tiết");
        var maTrang = (string?)trang?.Attribute(QuanHe + "id");
        if (maTrang is null)
            throw new InvalidDataException("Không tìm thấy trang Danh sách CLO chi tiết của Module 2.");

        var lienKet = DocXml(tep, "xl/_rels/workbook.xml.rels").Root!.Elements()
            .Single(x => (string?)x.Attribute("Id") == maTrang);
        var dich = (string?)lienKet.Attribute("Target") ?? "";
        var duongDan = dich.StartsWith('/') ? dich.TrimStart('/') : "xl/" + dich;
        var chuoi = tep.GetEntry("xl/sharedStrings.xml") is null ? [] :
            DocXml(tep, "xl/sharedStrings.xml").Descendants(BangTinh + "si").Select(NoiVanBan).ToArray();

        var hang = DocXml(tep, duongDan).Descendants(BangTinh + "row")
            .Select(x => x.Elements(BangTinh + "c").ToDictionary(
                o => new string(((string?)o.Attribute("r") ?? "").TakeWhile(char.IsLetter).ToArray()),
                o => DocO(o, chuoi))).ToList();
        if (hang.Count == 0 || hang[0].GetValueOrDefault("B") != "Mã CLO" ||
            hang[0].GetValueOrDefault("C") != "Chuẩn đầu ra" ||
            hang[0].GetValueOrDefault("D") != "Tiêu chí đánh giá")
            throw new InvalidDataException("Các cột trong tệp không đúng mẫu CLO của Module 2.");

        var duLieu = hang.Skip(1).Where(x => x.Values.Any(v => v.Length > 0)).ToList();
        if (duLieu.Count is < 1 or > 50)
            throw new InvalidDataException("Tệp cần có từ 1 đến 50 tiêu chí.");

        var trongSo = 10000 / duLieu.Count;
        var phanDu = 10000 % duLieu.Count;
        return duLieu.Select((dong, i) =>
        {
            var ma = dong.GetValueOrDefault("B", "");
            var chuan = dong.GetValueOrDefault("C", "");
            var tieuChi = dong.GetValueOrDefault("D", "").TrimStart('-').Trim();
            var ten = tieuChi.Split([':', '\n'], 2)[0].Trim().TrimEnd('.');
            var moTa = string.Join("\n", new[] { chuan, tieuChi }.Distinct());
            if (ma.Length is < 1 or > 50 || chuan.Length == 0 || ten.Length is < 1 or > 200 || moTa.Length > 1000)
                throw new InvalidDataException($"Tiêu chí tại dòng {i + 2} bị thiếu dữ liệu hoặc vượt độ dài cho phép.");
            var cacMuc = "EFGHI".Select(cot => dong.GetValueOrDefault(cot.ToString(), "")).ToArray();
            if (cacMuc.Any(muc => muc.Length > 1000))
                throw new InvalidDataException($"Mô tả mức đánh giá tại dòng {i + 2} quá dài.");
            return new Criterion(ten, (trongSo + (i < phanDu ? 1 : 0)) / 100m, ma, moTa, cacMuc);
        }).ToArray();
    }

    private static XDocument DocXml(ZipArchive tep, string ten)
    {
        var muc = tep.GetEntry(ten);
        if (muc is null || muc.Length > 4 * 1024 * 1024)
            throw new InvalidDataException("Tệp Excel không hợp lệ hoặc quá lớn.");
        using var luong = muc.Open();
        using var doc = System.Xml.XmlReader.Create(luong, new System.Xml.XmlReaderSettings { MaxCharactersInDocument = 4 * 1024 * 1024 });
        return XDocument.Load(doc);
    }

    private static string NoiVanBan(XElement o) => string.Concat(o.Descendants(BangTinh + "t").Select(t => t.Value));

    private static string DocO(XElement o, string[] chuoi)
    {
        var giaTri = o.Element(BangTinh + "v")?.Value ?? "";
        return ((string?)o.Attribute("t") switch
        {
            "inlineStr" => NoiVanBan(o),
            "s" when int.TryParse(giaTri, NumberStyles.None, CultureInfo.InvariantCulture, out var chiSo)
                && chiSo < chuoi.Length => chuoi[chiSo],
            _ => giaTri
        }).Trim();
    }
}
