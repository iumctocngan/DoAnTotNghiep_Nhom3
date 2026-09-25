import React, { useEffect, useState } from 'react';
import { Modal } from '../../components/common/Modal';
import { staffApi } from '../../api/staffApi';
import type { ApiError, UserRole } from '../../types/auth';
import type { StaffResponse } from '../../types/staff';
import { getRoleLabel } from '../../utils/role';

interface ChangeRoleModalProps {
  staff: StaffResponse | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const ChangeRoleModal: React.FC<ChangeRoleModalProps> = ({
  staff,
  isOpen,
  onClose,
  onSuccess,
}) => {
  const [role, setRole] = useState<UserRole>('Teacher');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (staff) {
      setRole(staff.role);
      setError(null);
    }
  }, [staff]);

  if (!isOpen || !staff) return null;

  const isStaffActive = staff.status === 1;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (role === staff.role) {
      setError('Vai trò mới trùng với vai trò hiện tại.');
      return;
    }

    if (isStaffActive) {
      setError('Quy tắc nghiệp vụ: Phải ngừng hoạt động (vô hiệu hóa) tài khoản trước khi đổi vai trò.');
      return;
    }

    setIsSubmitting(true);
    try {
      await staffApi.changeRole(staff.id, { role });
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const apiErr = err as ApiError;
      setError(apiErr.message || 'Đổi vai trò thất bại.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal title={`Đổi vai trò (${staff.employeeCode})`} onClose={onClose} closeDisabled={isSubmitting}>

        {isStaffActive && (
          <div className="alert alert-danger text-xs">
            <strong>Cảnh báo nghiệp vụ:</strong> Tài khoản này hiện <strong>đang hoạt động</strong>. Hệ thống yêu cầu phải chuyển tài khoản sang trạng thái <em>ngừng hoạt động</em> trước khi thay đổi vai trò.
          </div>
        )}

        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="mb-4">
            <label>Nhân viên:</label>
            <div className="font-semibold text-sm mb-1">
              {staff.fullName} ({staff.email})
            </div>
            <div className="text-xs text-slate-500">
              Vai trò hiện tại: <strong>{getRoleLabel(staff.role)}</strong>
            </div>
          </div>

          <div className="mb-5">
            <label htmlFor="newRole">Chọn vai trò mới *</label>
            <select
              id="newRole"
              value={role}
              onChange={(e) => setRole(e.target.value as UserRole)}
              disabled={isSubmitting || isStaffActive}
            >
              <option value="Teacher">Giáo viên</option>
              <option value="CustomerCare">Chăm sóc khách hàng</option>
              <option value="Accountant">Kế toán</option>
              <option value="Admin">Quản trị viên</option>
            </select>
          </div>

          <div className="modal-footer">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={onClose}
              disabled={isSubmitting}
            >
              Đóng
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isSubmitting || isStaffActive}
            >
              {isSubmitting ? 'Đang cập nhật...' : 'Xác nhận đổi vai trò'}
            </button>
          </div>
        </form>
    </Modal>
  );
};
