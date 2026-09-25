import React, { useEffect, useState } from 'react';
import { Modal } from '../../components/common/Modal';
import { staffApi } from '../../api/staffApi';
import { useAuth } from '../../context/AuthContext';
import type { ApiError } from '../../types/auth';
import type { StaffResponse, UpdateStaffRequest } from '../../types/staff';

interface EditStaffModalProps {
  staff: StaffResponse | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const EditStaffModal: React.FC<EditStaffModalProps> = ({
  staff,
  isOpen,
  onClose,
  onSuccess,
}) => {
  const { user, logout, refreshSession } = useAuth();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (staff) {
      setFullName(staff.fullName || '');
      setEmail(staff.email || '');
      setPhoneNumber(staff.phoneNumber || '');
      setError(null);
    }
  }, [staff]);

  if (!isOpen || !staff) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!fullName.trim() || !email.trim()) {
      setError('Họ và tên và Email không được để trống.');
      return;
    }

    setIsSubmitting(true);
    try {
      const payload: UpdateStaffRequest = {
        fullName: fullName.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim() || null,
      };

      await staffApi.updateStaff(staff.id, payload);
      onSuccess();
      onClose();
      if (staff.id === user?.userId) {
        if (staff.email.toLowerCase() !== payload.email.toLowerCase()) {
          await logout().catch(() => {});
        } else {
          await refreshSession();
        }
      }
    } catch (err: unknown) {
      const apiErr = err as ApiError;
      setError(apiErr.message || 'Cập nhật thông tin thất bại.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal title={`Chỉnh sửa nhân viên (${staff.employeeCode})`} onClose={onClose} closeDisabled={isSubmitting}>

        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label htmlFor="editName">Họ và tên *</label>
            <input
              id="editName"
              type="text"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="mb-3">
            <label htmlFor="editEmail">Email *</label>
            <input
              id="editEmail"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={isSubmitting}
              required
            />
            <small className="text-slate-400 text-xs">
              Lưu ý: Nếu đổi email, các phiên đăng nhập cũ của nhân viên này sẽ bị thu hồi.
            </small>
          </div>

          <div className="mb-5">
            <label htmlFor="editPhone">Số điện thoại</label>
            <input
              id="editPhone"
              type="text"
              value={phoneNumber}
              onChange={(e) => setPhoneNumber(e.target.value)}
              disabled={isSubmitting}
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
              {isSubmitting ? 'Đang lưu...' : 'Lưu thay đổi'}
            </button>
          </div>
        </form>
    </Modal>
  );
};
