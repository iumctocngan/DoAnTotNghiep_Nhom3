# Hướng dẫn khởi chạy CMS EDU

- [Module 1 — Đánh giá và nhận xét](module1/README.md) được nạp vào backend chung.


1. Chạy Backend :
   ```powershell
   dotnet run --project backend/src/CmsEdu.Api
   ```

2. Chạy Frontend (React + Tailwind CSS):
   ```powershell
   cd frontend; npm install; npm run dev
   ```

3. Đọc Jwt&SeedAdmin.md để tạo jwt, tài khoản admin.
