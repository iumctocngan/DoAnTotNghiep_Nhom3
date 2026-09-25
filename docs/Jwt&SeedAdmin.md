# Chạy backend trên máy local

Mỗi thành viên cấu hình User Secrets trên máy của mình. Không lưu secret thật vào Git.

ví dụ: 
```powershell
dotnet user-secrets set "Jwt:SigningKey" "key_sieu_cap_bi_mat_1234567890ab" --project backend/src/CmsEdu.Api
dotnet user-secrets set "SeedAdmin:Email" "admin@cms.edu.vn" --project backend/src/CmsEdu.Api
dotnet user-secrets set "SeedAdmin:Password" "Bietthenaodc123" --project backend/src/CmsEdu.Api
dotnet user-secrets set "SeedAdmin:FullName" "NGUYEN VAN MINH" --project backend/src/CmsEdu.Api
dotnet user-secrets set "SeedAdmin:EmployeeCode" "STAFF001" --project backend/src/CmsEdu.Api
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
