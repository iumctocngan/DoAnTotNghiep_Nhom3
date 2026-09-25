import React, { useEffect, useState } from 'react';
import { Modal } from '../../components/common/Modal';
import { curriculumApi } from '../../api/curriculumApi';
import type { ApiError } from '../../types/auth';
import type { LessonRequest, LessonResponse } from '../../types/curriculum';

interface LessonModalProps {
  isOpen: boolean;
  levelId: number;
  lesson: LessonResponse | null; // null => Create, object => Edit
  onClose: () => void;
  onSuccess: () => void;
}

export const LessonModal: React.FC<LessonModalProps> = ({
  isOpen,
  levelId,
  lesson,
  onClose,
  onSuccess,
}) => {
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [objective, setObjective] = useState('');
  const [sortOrder, setSortOrder] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (lesson) {
      setCode(lesson.code);
      setName(lesson.name);
      setObjective(lesson.objective || '');
      setSortOrder(lesson.sortOrder);
    } else {
      setCode('');
      setName('');
      setObjective('');
      setSortOrder(1);
    }
    setError(null);
  }, [lesson, isOpen]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!code.trim() || !name.trim()) {
      setError('Mã bài học và Tên bài học là bắt buộc.');
      return;
    }

    setIsSubmitting(true);
    try {
      const payload: LessonRequest = {
        levelId,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        objective: objective.trim() || null,
        sortOrder: Number(sortOrder) || 1,
      };

      if (lesson) {
        await curriculumApi.updateLesson(lesson.id, payload);
      } else {
        await curriculumApi.createLesson(payload);
      }

      onSuccess();
      onClose();
    } catch (err: unknown) {
      const apiErr = err as ApiError;
      setError(apiErr.message || 'Lưu bài học thất bại.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal title={lesson ? `Sửa bài học (${lesson.code})` : 'Tạo bài học mới'} onClose={onClose} closeDisabled={isSubmitting}>

        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label htmlFor="lessonCode">Mã bài học *</label>
            <input
              id="lessonCode"
              type="text"
              placeholder="VD: LES-01"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="mb-3">
            <label htmlFor="lessonName">Tên bài học *</label>
            <input
              id="lessonName"
              type="text"
              placeholder="VD: Nhận biết hình học phẳng"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="mb-3">
            <label htmlFor="lessonObjective">Mục tiêu bài học</label>
            <textarea
              id="lessonObjective"
              rows={2}
              placeholder="Mục tiêu kiến thức, kỹ năng đạt được..."
              value={objective}
              onChange={(e) => setObjective(e.target.value)}
              disabled={isSubmitting}
            />
          </div>

          <div className="mb-5">
            <label htmlFor="lessonSortOrder">Thứ tự bài học *</label>
            <input
              id="lessonSortOrder"
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
              {isSubmitting ? 'Đang lưu...' : lesson ? 'Cập nhật' : 'Tạo mới'}
            </button>
          </div>
        </form>
    </Modal>
  );
};
