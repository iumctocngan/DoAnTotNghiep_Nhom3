import React, { useEffect, useState } from 'react';
import { Modal } from '../../components/common/Modal';
import { staffApi } from '../../api/staffApi';
import type { ApiError, UserRole } from '../../types/auth';
import type { CreateStaffRequest } from '../../types/staff';

interface CreateStaffModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const CreateStaffModal: React.FC<CreateStaffModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
}) => {
  const [employeeCode, setEmployeeCode] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [role, setRole] = useState<UserRole>('Teacher');
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      setEmployeeCode('');
      setFullName('');
      setEmail('');
      setPhoneNumber('');
      setRole('Teacher');
      setTemporaryPassword('');
      setError(null);
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!employeeCode.trim() || !fullName.trim() || !email.trim() || !temporaryPassword) {
      setError('Vui lòng điền đầy đủ các trường bắt buộc.');
      return;
    }

    if (temporaryPassword.length < 8) {
      setError('Mật khẩu tạm thời phải có tối thiểu 8 ký tự.');
      return;
    }

    if (!/[A-Z]/.test(temporaryPassword) || !/[a-z]/.test(temporaryPassword) || !/[0-9]/.test(temporaryPassword)) {
      setError('Mật khẩu tạm thời phải chứa ít nhất 1 chữ hoa, 1 chữ thường và 1 chữ số.');
      return;
    }

    setIsSubmitting(true);
    try {
      const payload: CreateStaffRequest = {
        employeeCode: employeeCode.trim().toUpperCase(),
        fullName: fullName.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim() || null,
        role,
        temporaryPassword,
      };

      await staffApi.createStaff(payload);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const apiErr = err as ApiError;
      setError(apiErr.message || 'Tạo nhân viên mới thất bại.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal title="Tạo tài khoản nhân viên mới" onClose={onClose} closeDisabled={isSubmitting} wide>

        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="modal-form-grid">
            <div>
              <label htmlFor="code">Mã nhân viên *</label>
              <input
                id="code"
                type="text"
                placeholder="VD: NV001"
                value={employeeCode}
                onChange={(e) => setEmployeeCode(e.target.value)}
                disabled={isSubmitting}
                required
              />
            </div>
            <div>
              <label htmlFor="role">Vai trò *</label>
              <select
                id="role"
                value={role}
                onChange={(e) => setRole(e.target.value as UserRole)}
                disabled={isSubmitting}
              >
                <option value="Teacher">Giáo viên</option>
                <option value="CustomerCare">Chăm sóc khách hàng</option>
                <option value="Accountant">Kế toán</option>
                <option value="Admin">Quản trị viên</option>
              </select>
            </div>
          </div>

          <div className="mb-3">
            <label htmlFor="name">Họ và tên *</label>
            <input
              id="name"
              type="text"
              placeholder="VD: Nguyễn Văn A"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="modal-form-grid">
            <div>
              <label htmlFor="email">Email đăng nhập *</label>
              <input
                id="email"
                type="email"
                placeholder="a.nguyen@cms.edu.vn"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                disabled={isSubmitting}
                required
              />
            </div>
            <div>
              <label htmlFor="phone">Số điện thoại</label>
              <input
                id="phone"
                type="text"
                placeholder="0912345678"
                value={phoneNumber}
                onChange={(e) => setPhoneNumber(e.target.value)}
                disabled={isSubmitting}
              />
            </div>
          </div>

          <div className="mb-5">
            <label htmlFor="tempPass">Mật khẩu tạm thời *</label>
            <input
              id="tempPass"
              type="password"
              autoComplete="new-password"
              placeholder="Tối thiểu 8 ký tự, gồm chữ hoa, thường và số"
              value={temporaryPassword}
              onChange={(e) => setTemporaryPassword(e.target.value)}
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
              {isSubmitting ? 'Đang tạo...' : 'Tạo tài khoản'}
            </button>
          </div>
        </form>
    </Modal>
  );
};
