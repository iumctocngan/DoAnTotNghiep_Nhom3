# Google Material Symbols

Official 24px outlined SVG paths from https://github.com/google/material-design-icons/tree/master/symbols/web.
License: Apache-2.0; see LICENSE.txt.

materialSymbols.ts stores original viewBox and path data without changes.
MaterialIcon renders inline SVG; no font, ligatures, or network requests required.

## Quy ước chung cho toàn bộ frontend

Mọi phân hệ dùng MaterialIcon từ src/components/common/MaterialIcon.tsx.
Không dùng emoji, tên icon dạng chữ, icon font hoặc SVG tự vẽ làm icon giao diện.
Khi thêm icon, lấy SVG Material Symbols Outlined 24px từ nguồn Google ở trên,
đưa nguyên viewBox và các path vào materialSymbols.ts, giữ giấy phép Apache-2.0.
Tên icon được TypeScript kiểm tra cho tất cả nơi sử dụng.
Icon trang trí được ẩn khỏi trình đọc màn hình; nút chỉ có icon cần aria-label.
Logo thương hiệu không thuộc bộ icon giao diện.
