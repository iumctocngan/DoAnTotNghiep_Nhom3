import React, { useEffect, useState } from 'react';
import { Modal } from '../../components/common/Modal';
import { curriculumApi } from '../../api/curriculumApi';
import type { ApiError } from '../../types/auth';
import type { LevelRequest, LevelResponse } from '../../types/curriculum';

interface LevelModalProps {
  isOpen: boolean;
  courseId: number;
  level: LevelResponse | null; // null => Create, object => Edit
  onClose: () => void;
  onSuccess: () => void;
}

export const LevelModal: React.FC<LevelModalProps> = ({
  isOpen,
  courseId,
  level,
  onClose,
  onSuccess,
}) => {
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [sortOrder, setSortOrder] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (level) {
      setCode(level.code);
      setName(level.name);
      setSortOrder(level.sortOrder);
    } else {
      setCode('');
      setName('');
      setSortOrder(1);
    }
    setError(null);
  }, [level, isOpen]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!code.trim() || !name.trim()) {
      setError('Mã cấp độ và Tên cấp độ là bắt buộc.');
      return;
    }

    setIsSubmitting(true);
    try {
      const payload: LevelRequest = {
        courseId,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        sortOrder: Number(sortOrder) || 1,
      };

      if (level) {
        await curriculumApi.updateLevel(level.id, payload);
      } else {
        await curriculumApi.createLevel(payload);
      }

      onSuccess();
      onClose();
    } catch (err: unknown) {
      const apiErr = err as ApiError;
      setError(apiErr.message || 'Lưu cấp độ thất bại.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal title={level ? `Sửa cấp độ (${level.code})` : 'Tạo cấp độ mới'} onClose={onClose} closeDisabled={isSubmitting}>

        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label htmlFor="levelCode">Mã cấp độ *</label>
            <input
              id="levelCode"
              type="text"
              placeholder="VD: LV-01"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="mb-3">
            <label htmlFor="levelName">Tên cấp độ *</label>
            <input
              id="levelName"
              type="text"
              placeholder="VD: Khởi động (Beginner)"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="mb-5">
            <label htmlFor="sortOrder">Thứ tự hiển thị (Sort Order) *</label>
            <input
              id="sortOrder"
              type="number"
              min={1}
              value={sortOrder}
              onChange={(e) => setSortOrder(Number(e.target.value))}
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
              {isSubmitting ? 'Đang lưu...' : level ? 'Cập nhật' : 'Tạo mới'}
            </button>
          </div>
        </form>
    </Modal>
  );
};
