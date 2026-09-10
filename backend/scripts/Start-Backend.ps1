param(
    [Alias("ConnectionString")]
    [string]$ChuoiKetNoi = 'Server=(localdb)\MSSQLLocalDB;Database=CmsEduDb;Integrated Security=true;TrustServerCertificate=true',
    [Alias("Urls")]
    [string]$DiaChi = 'https://localhost:7145;http://localhost:5145'
)
$ErrorActionPreference = 'Stop'
$ketNoiTruocDo = $env:ConnectionStrings__DefaultConnection
$moiTruongTruocDo = $env:ASPNETCORE_ENVIRONMENT
$khoaKyTruocDo = $env:Jwt__SigningKey
try {
    $env:ConnectionStrings__DefaultConnection = $ChuoiKetNoi
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    if ([string]::IsNullOrWhiteSpace($env:Jwt__SigningKey)) {
        $mangByteKhoa = New-Object byte[] 48
        $boSinhNgauNhien = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        $boSinhNgauNhien.GetBytes($mangByteKhoa)
        $boSinhNgauNhien.Dispose()
        $env:Jwt__SigningKey = [Convert]::ToBase64String($mangByteKhoa)
        Write-Host 'Đang dùng khóa JWT tạm cho môi trường phát triển. Token cũ sẽ không còn hợp lệ khi khởi động lại máy chủ.'
    }
    $duAnApi = Join-Path $PSScriptRoot '../src/CmsEdu.Api/CmsEdu.Api.csproj'
    & dotnet run --project $duAnApi --no-launch-profile --urls $DiaChi
    if ($LASTEXITCODE -ne 0) { throw "Máy chủ kết thúc với mã $LASTEXITCODE." }
}
finally {
    $env:ConnectionStrings__DefaultConnection = $ketNoiTruocDo
    $env:ASPNETCORE_ENVIRONMENT = $moiTruongTruocDo
    $env:Jwt__SigningKey = $khoaKyTruocDo
}
