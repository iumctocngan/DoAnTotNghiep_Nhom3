import React, { useEffect, useState } from 'react';
import { Modal } from '../../components/common/Modal';
import { curriculumApi } from '../../api/curriculumApi';
import type { ApiError } from '../../types/auth';
import type { CourseRequest, CourseResponse } from '../../types/curriculum';

interface CourseModalProps {
  isOpen: boolean;
  course: CourseResponse | null; // null => Create mode, object => Edit mode
  onClose: () => void;
  onSuccess: () => void;
}

export const CourseModal: React.FC<CourseModalProps> = ({
  isOpen,
  course,
  onClose,
  onSuccess,
}) => {
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (course) {
      setCode(course.code);
      setName(course.name);
      setDescription(course.description || '');
    } else {
      setCode('');
      setName('');
      setDescription('');
    }
    setError(null);
  }, [course, isOpen]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!code.trim() || !name.trim()) {
      setError('Mã khóa học và Tên khóa học là bắt buộc.');
      return;
    }

    setIsSubmitting(true);
    try {
      const payload: CourseRequest = {
        code: code.trim().toUpperCase(),
        name: name.trim(),
        description: description.trim() || null,
      };

      if (course) {
        await curriculumApi.updateCourse(course.id, payload);
      } else {
        await curriculumApi.createCourse(payload);
      }

      onSuccess();
      onClose();
    } catch (err: unknown) {
      const apiErr = err as ApiError;
      setError(apiErr.message || 'Lưu khóa học thất bại.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal title={course ? `Sửa khóa học (${course.code})` : 'Tạo khóa học mới'} onClose={onClose} closeDisabled={isSubmitting}>

        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label htmlFor="courseCode">Mã khóa học *</label>
            <input
              id="courseCode"
              type="text"
              placeholder="VD: TOAN-TU-DUY"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="mb-3">
            <label htmlFor="courseName">Tên khóa học *</label>
            <input
              id="courseName"
              type="text"
              placeholder="VD: Toán tư duy CMS"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <div className="mb-5">
            <label htmlFor="courseDesc">Mô tả khóa học</label>
            <textarea
              id="courseDesc"
              rows={3}
              placeholder="Mô tả tóm tắt nội dung..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
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
              {isSubmitting ? 'Đang lưu...' : course ? 'Cập nhật' : 'Tạo mới'}
            </button>
          </div>
        </form>
    </Modal>
  );
};
