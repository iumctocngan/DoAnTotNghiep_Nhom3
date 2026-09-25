import type { UserRole } from '../types/auth';

export function getRoleLabel(role?: UserRole): string {
  switch (role) {
    case 'Admin': return 'Quản trị viên';
    case 'Teacher': return 'Giáo viên';
    case 'CustomerCare': return 'Chăm sóc khách hàng';
    case 'Accountant': return 'Kế toán';
    default: return 'Nhân viên';
  }
}
