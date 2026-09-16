# Chạy backend trên máy local

Mỗi thành viên cấu hình User Secrets trên máy của mình. Không lưu secret thật vào Git.

```powershell
dotnet user-secrets set "Jwt:SigningKey" "<development-key-at-least-32-characters>" --project backend/src/CmsEdu.Api
dotnet user-secrets set "SeedAdmin:Email" "admin@cms.edu.vn" --project backend/src/CmsEdu.Api
dotnet user-secrets set "SeedAdmin:Password" "<development-admin-password>" --project backend/src/CmsEdu.Api
dotnet user-secrets set "SeedAdmin:FullName" "ADMIN" --project backend/src/CmsEdu.Api
dotnet user-secrets set "SeedAdmin:EmployeeCode" "ADMIN001" --project backend/src/CmsEdu.Api
```

Nếu cấu hình SQL Server trên máy khác với `appsettings.json`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-local-connection-string>" --project backend/src/CmsEdu.Api
```

Kiểm tra và chạy:

```powershell
dotnet user-secrets list --project backend/src/CmsEdu.Api
dotnet run --project backend/src/CmsEdu.Api
```
