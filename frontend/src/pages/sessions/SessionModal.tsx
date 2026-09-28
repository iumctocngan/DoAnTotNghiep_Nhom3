import React, { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { sessionApi } from '../../api/sessionApi';
import { classApi } from '../../api/classApi';
import { curriculumApi } from '../../api/curriculumApi';
import { Modal } from '../../components/common/Modal';
import { useToast } from '../../components/common/ToastProvider';
import type { ApiError } from '../../types/auth';
import type { CreateSessionRequest, SessionResponse } from '../../types/session';

interface SessionModalProps {
  isOpen: boolean;
  onClose: () => void;
  session?: SessionResponse | null;
  defaultClassId?: number;
}

export const SessionModal: React.FC<SessionModalProps> = ({
  isOpen,
  onClose,
  session,
  defaultClassId,
}) => {
  const queryClient = useQueryClient();
  const showToast = useToast();

  const [classId, setClassId] = useState<number | ''>(defaultClassId || '');
  const [lessonId, setLessonId] = useState<number | ''>('');
  const [sessionDate, setSessionDate] = useState('');
  const [startTime, setStartTime] = useState('08:00');
  const [endTime, setEndTime] = useState('09:30');
  const [note, setNote] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const isEditing = Boolean(session);

  // Danh sách lớp học để chọn
  const { data: classes } = useQuery({
    queryKey: ['classes'],
    queryFn: () => classApi.getClasses(),
    enabled: isOpen && !isEditing,
  });

  // Tìm lớp được chọn để lấy cấp độ
  const selectedClass = classes?.find((c) => c.id === Number(classId));

  // Danh sách bài học của cấp độ lớp
  const { data: lessons } = useQuery({
    queryKey: ['lessons', selectedClass?.capDoId],
    queryFn: () => curriculumApi.getLessons(selectedClass!.capDoId, { pageSize: 100 }),
    enabled: isOpen && !!selectedClass?.capDoId,
  });

  useEffect(() => {
    if (session) {
      setClassId(session.classId);
      setLessonId(session.lessonId ?? '');
      setSessionDate(session.sessionDate);
      setStartTime(session.startTime.substring(0, 5));
      setEndTime(session.endTime.substring(0, 5));
      setNote(session.note ?? '');
    } else {
      setClassId(defaultClassId || '');
      setLessonId('');
      setSessionDate('');
      setStartTime('08:00');
      setEndTime('09:30');
      setNote('');
    }
    setErrorMessage(null);
  }, [session, defaultClassId, isOpen]);

  const mutation = useMutation({
    mutationFn: (data: CreateSessionRequest) =>
      isEditing && session
        ? sessionApi.updateSession(session.id, {
            lessonId: data.lessonId,
            sessionDate: data.sessionDate,
            startTime: data.startTime,
            endTime: data.endTime,
            note: data.note,
          })
        : sessionApi.createSession(data),
    onSuccess: () => {
      showToast(
        isEditing ? 'Cập nhật buổi học thành công!' : 'Tạo buổi học thành công!',
        'success'
      );
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['teacher-schedule'] });
      onClose();
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setErrorMessage(apiErr.message || 'Lỗi khi lưu buổi học. Vui lòng kiểm tra lại.');
    },
  });

  if (!isOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!classId) {
      setErrorMessage('Vui lòng chọn lớp học.');
      return;
    }
    if (!sessionDate) {
      setErrorMessage('Vui lòng chọn ngày học.');
      return;
    }
    if (!startTime || !endTime) {
      setErrorMessage('Vui lòng nhập giờ bắt đầu và kết thúc.');
      return;
    }
    if (startTime >= endTime) {
      setErrorMessage('Giờ bắt đầu phải trước giờ kết thúc.');
      return;
    }

    setErrorMessage(null);
    mutation.mutate({
      classId: Number(classId),
      lessonId: lessonId ? Number(lessonId) : null,
      sessionDate,
      startTime: startTime.length === 5 ? `${startTime}:00` : startTime,
      endTime: endTime.length === 5 ? `${endTime}:00` : endTime,
      note: note.trim() || null,
    });
  };

  return (
    <Modal
      title={isEditing ? 'Chỉnh sửa thông tin buổi học' : 'Thêm buổi học mới'}
      onClose={onClose}
      closeDisabled={mutation.isPending}
      wide
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

        <div>
          <label htmlFor="sessionClass">
            Lớp học <span className="text-red-500">*</span>
          </label>
          {isEditing ? (
            <input
              type="text"
              value={`${session?.className} (${session?.classCode})`}
              disabled
              className="bg-slate-100"
            />
          ) : (
            <select
              id="sessionClass"
              value={classId}
              onChange={(e) => {
                setClassId(e.target.value ? Number(e.target.value) : '');
                setLessonId('');
              }}
              disabled={mutation.isPending}
              required
            >
              <option value="">-- Chọn lớp học --</option>
              {classes?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.tenLop} ({c.maLop})
                </option>
              ))}
            </select>
          )}
        </div>

        {lessons && lessons.items.length > 0 && (
          <div>
            <label htmlFor="sessionLesson">Bài học (tùy chọn)</label>
            <select
              id="sessionLesson"
              value={lessonId}
              onChange={(e) => setLessonId(e.target.value ? Number(e.target.value) : '')}
              disabled={mutation.isPending}
            >
              <option value="">-- Chưa gắn bài học cụ thể --</option>
              {lessons.items.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.code} - {l.name}
                </option>
              ))}
            </select>
          </div>
        )}

        <div>
          <label htmlFor="sessionDate">
            Ngày học <span className="text-red-500">*</span>
          </label>
          <input
            id="sessionDate"
            type="date"
            value={sessionDate}
            onChange={(e) => setSessionDate(e.target.value)}
            disabled={mutation.isPending}
            required
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label htmlFor="sStartTime">
              Giờ bắt đầu <span className="text-red-500">*</span>
            </label>
            <input
              id="sStartTime"
              type="time"
              value={startTime}
              onChange={(e) => setStartTime(e.target.value)}
              disabled={mutation.isPending}
              required
            />
          </div>

          <div>
            <label htmlFor="sEndTime">
              Giờ kết thúc <span className="text-red-500">*</span>
            </label>
            <input
              id="sEndTime"
              type="time"
              value={endTime}
              onChange={(e) => setEndTime(e.target.value)}
              disabled={mutation.isPending}
              required
            />
          </div>
        </div>

        <div>
          <label htmlFor="sNote">Ghi chú</label>
          <textarea
            id="sNote"
            rows={2}
            placeholder="Nội dung chuẩn bị, dặn dò học viên..."
            value={note}
            onChange={(e) => setNote(e.target.value)}
            disabled={mutation.isPending}
            maxLength={500}
          />
        </div>

        <div className="flex justify-end gap-2 pt-2 border-t border-slate-200">
          <button
            type="button"
            className="btn btn-secondary text-xs"
            onClick={onClose}
            disabled={mutation.isPending}
          >
            Hủy
          </button>
          <button
            type="submit"
            className="btn btn-primary text-xs"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Đang lưu...' : isEditing ? 'Lưu thay đổi' : 'Tạo buổi học'}
          </button>
        </div>
      </form>
    </Modal>
  );
};
