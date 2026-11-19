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
  defaultDate?: string;
  onSaved?: (date: string) => void;
}

export const SessionModal: React.FC<SessionModalProps> = ({
  isOpen,
  onClose,
  session,
  defaultClassId,
  defaultDate,
  onSaved,
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
  const followsClassSchedule = session?.followsClassSchedule === true;

  // Danh sách lớp học để chọn
  const { data: classes } = useQuery({
    queryKey: ['classes'],
    queryFn: () => classApi.getClasses(),
    enabled: isOpen,
  });

  // Tìm lớp được chọn để lấy cấp độ
  const selectedClass = classes?.find((c) => c.id === Number(classId));

  // Danh sách bài học của cấp độ lớp
  const { data: lessons, isError: lessonsError } = useQuery({
    queryKey: ['lessons', selectedClass?.capDoId, 'active-options'],
    queryFn: ({ signal }) => curriculumApi.getActiveLessons(selectedClass!.capDoId, signal),
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
      setSessionDate(defaultDate ?? '');
      setStartTime('08:00');
      setEndTime('09:30');
      setNote('');
    }
    setErrorMessage(null);
  }, [session, defaultClassId, defaultDate, isOpen]);

  const classStartTime = selectedClass?.gioBatDau;
  const classEndTime = selectedClass?.gioKetThuc;
  useEffect(() => {
    if (isOpen && !isEditing && classStartTime && classEndTime) {
      setStartTime(classStartTime.slice(0, 5));
      setEndTime(classEndTime.slice(0, 5));
    }
  }, [isOpen, isEditing, classId, classStartTime, classEndTime]);

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
    onSuccess: (_, data) => {
      showToast(
        isEditing ? 'Cập nhật buổi học thành công!' : 'Đã thêm buổi học bù.',
        'success'
      );
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['teacher-schedule'] });
      onSaved?.(data.sessionDate);
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
      startTime: followsClassSchedule && session ? session.startTime : startTime.length === 5 ? `${startTime}:00` : startTime,
      endTime: followsClassSchedule && session ? session.endTime : endTime.length === 5 ? `${endTime}:00` : endTime,
      note: note.trim() || null,
    });
  };

  return (
    <Modal
      title={isEditing ? 'Chỉnh sửa thông tin buổi học' : 'Thêm buổi học bù'}
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
              {classes?.filter(c => c.trangThai === 2).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.tenLop} ({c.maLop})
                </option>
              ))}
            </select>
          )}
        </div>

        {(selectedClass || isEditing) && (
          <div>
            <label htmlFor="sessionLesson">Bài học (tùy chọn)</label>
            <select
              id="sessionLesson"
              value={lessonId}
              onChange={(e) => setLessonId(e.target.value ? Number(e.target.value) : '')}
              disabled={mutation.isPending || !lessons}
            >
              <option value="">{lessonsError ? 'Không tải được danh sách bài' : !lessons ? 'Đang tải bài học…' : '-- Chưa gắn bài học cụ thể --'}</option>
              {lessonId !== '' && !lessons?.some(bai => bai.id === lessonId) && (
                <option value={lessonId}>{session?.lessonName ?? 'Bài hiện tại'}</option>
              )}
              {lessons?.map((l) => (
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
            disabled={mutation.isPending || followsClassSchedule}
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
              disabled={mutation.isPending || followsClassSchedule}
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
              disabled={mutation.isPending || followsClassSchedule}
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
            {mutation.isPending ? 'Đang lưu...' : isEditing ? 'Lưu thay đổi' : 'Tạo buổi học bù'}
          </button>
        </div>
      </form>
    </Modal>
  );
};
