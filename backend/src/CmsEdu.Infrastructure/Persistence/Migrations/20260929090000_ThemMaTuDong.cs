using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmsEdu.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260929090000_ThemMaTuDong")]
public class ThemMaTuDong : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var (tienTo, bang, cot) in new[] {
            ("HS", "Students", "StudentCode"), ("LH", "Classes", "ClassCode"),
            ("GV", "AspNetUsers", "EmployeeCode"), ("CSKH", "AspNetUsers", "EmployeeCode"),
            ("KT", "AspNetUsers", "EmployeeCode"), ("QT", "AspNetUsers", "EmployeeCode"),
            ("KH", "Courses", "Code"), ("CD", "Levels", "Code"), ("BH", "Lessons", "Code") })
        {
            migrationBuilder.Sql($"""
                DECLARE @batDau bigint = (SELECT COALESCE(MAX(TRY_CONVERT(bigint,
                    SUBSTRING([{cot}], {tienTo.Length + 1}, 50))), 0) + 1
                    FROM [{bang}] WHERE [{cot}] LIKE '{tienTo}%');
                DECLARE @lenh nvarchar(max) = N'CREATE SEQUENCE [dbo].[MaTuDong_{tienTo}] AS bigint START WITH '
                    + CONVERT(nvarchar(20), @batDau) + N' INCREMENT BY 1 NO CYCLE';
                EXEC sp_executesql @lenh;
                """);
        }
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var tienTo in new[] { "HS", "LH", "GV", "CSKH", "KT", "QT", "KH", "CD", "BH" })
            migrationBuilder.Sql($"DROP SEQUENCE [dbo].[MaTuDong_{tienTo}]");
    }
}
