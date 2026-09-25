import React, { useEffect, useState } from 'react';
import { Modal } from '../../components/common/Modal';
import { staffApi } from '../../api/staffApi';
import type { ApiError } from '../../types/auth';
import type { StaffResponse } from '../../types/staff';
import { useToast } from '../../components/common/ToastProvider';

interface ResetStaffPasswordModalProps {
  staff: StaffResponse | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const ResetStaffPasswordModal: React.FC<ResetStaffPasswordModalProps> = ({
  staff,
  isOpen,
  onClose,
  onSuccess,
}) => {
  const showToast = useToast();
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    setNewPassword('');
    setConfirmPassword('');
    setError(null);
  }, [staff, isOpen]);

  if (!isOpen || !staff) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!newPassword || !confirmPassword) {
      setError('Vui lòng điền đầy đủ cả 2 trường mật khẩu.');
      return;
    }

    if (newPassword.length < 8) {
      setError('Mật khẩu mới phải có tối thiểu 8 ký tự.');
      return;
    }

    if (!/[A-Z]/.test(newPassword) || !/[a-z]/.test(newPassword) || !/[0-9]/.test(newPassword)) {
      setError('Mật khẩu mới phải chứa ít nhất 1 chữ hoa, 1 chữ thường và 1 chữ số.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setError('Xác nhận mật khẩu mới không khớp.');
      return;
    }

    setIsSubmitting(true);
    try {
      await staffApi.resetPassword(staff.id, { newPassword });
      showToast(`Đã đặt lại mật khẩu thành công cho ${staff.fullName} (${staff.employeeCode}).`);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const apiErr = err as ApiError;
      setError(apiErr.message || 'Đặt lại mật khẩu thất bại.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal title={`Đặt lại mật khẩu (${staff.employeeCode})`} onClose={onClose} closeDisabled={isSubmitting}>

        {error && <div className="alert alert-danger">{error}</div>}

        <p className="text-xs text-slate-500 mb-4">
          Hệ thống sẽ cập nhật mật khẩu mới và hủy toàn bộ phiên đăng nhập hiện tại của nhân viên <strong>{staff.fullName}</strong>.
        </p>

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label htmlFor="staffNewPass">Mật khẩu mới *</label>
            <input
              id="staffNewPass"
              type="password"
              placeholder="Tối thiểu 8 ký tự, gồm chữ hoa, thường và số"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="mb-5">
            <label htmlFor="staffConfirmPass">Xác nhận mật khẩu mới *</label>
            <input
              id="staffConfirmPass"
              type="password"
              placeholder="Nhập lại mật khẩu mới"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="modal-footer">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={onClose}
              disabled={isSubmitting}
            >
              Hủy
            </button>
            <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
              {isSubmitting ? 'Đang cập nhật...' : 'Xác nhận đặt lại'}
            </button>
          </div>
        </form>
    </Modal>
  );
};
