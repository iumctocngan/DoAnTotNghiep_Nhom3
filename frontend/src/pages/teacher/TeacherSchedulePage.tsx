import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import './teacher-schedule.css';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { teacherApi } from '../../api/teacherApi';
import { sessionApi } from '../../api/sessionApi';
import { staffApi } from '../../api/staffApi';
import { useAuth } from '../../context/AuthContext';
import { TeacherSelect } from '../../components/TeacherSelect';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import { formatDateDisplay, formatTimeDisplay } from '../../utils/date';
import { addDays, mondayOf, todayInVietnam } from '../../utils/weekDates';
import { AttendanceModal } from '../sessions/AttendanceModal';
import type { SessionStatus, TeacherScheduleResponse } from '../../types/teacher';

const weekdays = ['Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy', 'Chủ nhật'];
const statuses: Record<SessionStatus, string> = {
  1: 'Đã lên lịch', 2: 'Đã hoàn tất', 3: 'Đã hủy',
};

function assessmentLink(session: TeacherScheduleResponse) {
  return `/assessments?classId=${session.classId}&sessionId=${session.sessionId}`;
}

function groupByTime(sessions: TeacherScheduleResponse[]) {
  const slots = new Map<string, Map<string, TeacherScheduleResponse[]>>();
  for (const session of sessions) {
    const time = `${session.startTime.slice(0, 5)}–${session.endTime.slice(0, 5)}`;
    if (!slots.has(time)) slots.set(time, new Map());
    const days = slots.get(time)!;
    days.set(session.sessionDate, [...(days.get(session.sessionDate) ?? []), session]);
  }
  return [...slots].sort(([a], [b]) => a.localeCompare(b));
}

export function TeacherSchedulePage() {
  const { user, hasPermission } = useAuth();
  const navigate = useNavigate();
  const canAssess = hasPermission('assessments.view');
  const isAdmin = user?.role === 'Admin';
  const [selectedTeacherId, setSelectedTeacherId] = useState('');
  const teacherId = isAdmin ? selectedTeacherId : user?.userId;
  const [teacherSearch, setTeacherSearch] = useState('');
  const debouncedTeacherSearch = useDebouncedValue(teacherSearch);
  const [weekStart, setWeekStart] = useState(() => mondayOf(todayInVietnam()));
  const today = todayInVietnam();
  const weekDates = weekdays.map((_, i) => addDays(weekStart, i));
  const weekEnd = weekDates[6];
  const [attendanceSession, setAttendanceSession] = useState<TeacherScheduleResponse | null>(null);
  const nextAssessment = attendanceSession && canAssess && attendanceSession.sessionDate <= today
    ? assessmentLink(attendanceSession)
    : null;

  const teachers = useQuery({
    queryKey: ['staff', 'teachers', debouncedTeacherSearch],
    queryFn: () => staffApi.getStaff({ role: 'Teacher', search: debouncedTeacherSearch, pageSize: 100 }),
    enabled: isAdmin,
    placeholderData: keepPreviousData,
  });
  const schedule = useQuery({
    queryKey: ['teacher-schedule', teacherId, weekStart, weekEnd],
    enabled: !!teacherId,
    queryFn: async () => {
      await sessionApi.synchronizeWeek(weekStart, weekEnd, { teacherId });
      const params = { fromDate: weekStart, toDate: weekEnd, page: 1, pageSize: 100 };
      const first = await teacherApi.getSchedule(teacherId!, params);
      const extra = await Promise.all(Array.from({ length: Math.max(0, Math.ceil(first.totalItems / params.pageSize) - 1) }, (_, i) =>
        teacherApi.getSchedule(teacherId!, { ...params, page: i + 2 })));
      return [...first.items, ...extra.flatMap(page => page.items)];
    },
  });
  const slots = groupByTime(schedule.data ?? []);

  return (
    <div>
      <div className="page-header"><h1>Lịch giảng dạy</h1></div>
      <div className="card flex flex-wrap items-end justify-between gap-4">
        {isAdmin && <div className="w-full sm:w-72">
          <label htmlFor="scheduleTeacherSelect">Giáo viên</label>
          <TeacherSelect id="scheduleTeacherSelect" teachers={teachers.data?.items ?? []} value={selectedTeacherId}
            search={teacherSearch} isLoading={teachers.isLoading && !teachers.data}
            isSearching={teachers.isFetching || teacherSearch !== debouncedTeacherSearch}
            disabled={teachers.isError} onSearchChange={setTeacherSearch} onChange={setSelectedTeacherId} />
          {teachers.isError && <p role="alert">Không tải được danh sách giáo viên.</p>}
        </div>}
        <div className="flex flex-wrap items-center gap-2">
          <button type="button" className="btn btn-secondary gap-1.5" onClick={() => setWeekStart(addDays(weekStart, -7))}>
            <MaterialIcon name="arrow_back" /> Tuần trước
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => setWeekStart(mondayOf(today))}>Tuần này</button>
          <button type="button" className="btn btn-secondary gap-1.5" onClick={() => setWeekStart(addDays(weekStart, 7))}>
            Tuần sau <MaterialIcon name="arrow_forward" />
          </button>
          <strong aria-live="polite">{formatDateDisplay(weekStart)} – {formatDateDisplay(weekEnd)}</strong>
        </div>
      </div>

      {teacherId && schedule.isError ? (
        <p className="alert alert-danger" role="alert">{schedule.error?.message || 'Không thể tải lịch giảng dạy.'}</p>
      ) : (
        <div className="table-container teaching-calendar" role="region" aria-label="Lịch giảng dạy theo tuần" tabIndex={0}>
          <table className="data-table table-fixed">
            <thead><tr>
              <th scope="col" className="teaching-calendar-time">Khung giờ</th>
              {weekDates.map((date, i) => <th scope="col" key={date} className={date === today ? 'teaching-calendar-today' : undefined}>
                <span className="block">{weekdays[i]}</span>
                <span className="block text-xs font-normal"><time dateTime={date}>{formatDateDisplay(date)}</time></span>
                {date === today && <span className="teaching-calendar-today-label">Hôm nay</span>}
              </th>)}
            </tr></thead>
            <tbody>
              {slots.map(([time, days]) => <tr key={time}>
                <th scope="row" className="!align-top">{time}</th>
                {weekDates.map(date => <td key={date} className={date === today ? 'teaching-calendar-today' : undefined}>
                  <div className="space-y-2">
                    {days.get(date)?.map(session => {
                      const status = statuses[session.status];
                      const actionContext = `${session.className}, ngày ${formatDateDisplay(date)}, ${formatTimeDisplay(session.startTime)}`;
                      const canOpenAssessment = canAssess && date <= today;
                      return <article key={session.sessionId} className="teaching-calendar-session" data-status={session.status}>
                        <p className="font-semibold break-words">{session.className}</p>
                        <p className="text-xs text-slate-600">{session.classCode}</p>
                        <span className="teaching-calendar-status">{status}</span>
                        {session.lessonName && <p className="mt-1 break-words">{session.lessonName}</p>}
                        {session.note && <p className="mt-1 break-words text-xs text-slate-600">{session.note}</p>}
                        {session.status === 2 && canOpenAssessment
                          ? <Link className="teaching-calendar-action" aria-label={`Điểm danh và đánh giá ${actionContext}`}
                              to={assessmentLink(session)}>Điểm danh và đánh giá</Link>
                          : session.status !== 3 && (
                            <button type="button" className="teaching-calendar-action"
                              aria-label={`${canOpenAssessment ? 'Điểm danh và đánh giá' : 'Điểm danh'} ${actionContext}`}
                              onClick={() => setAttendanceSession(session)}>
                              {canOpenAssessment ? 'Điểm danh và đánh giá' : 'Điểm danh'}
                            </button>
                          )}
                      </article>;
                    })}
                  </div>
                </td>)}
              </tr>)}
              {!slots.length && <tr><td colSpan={8} className="!py-8 text-center text-slate-500">
                {!teacherId ? 'Chọn giáo viên để xem lịch dạy.' : schedule.isLoading ? 'Đang tải lịch…' : 'Không có buổi học trong tuần này.'}
              </td></tr>}
            </tbody>
          </table>
        </div>
      )}
      <AttendanceModal sessionId={attendanceSession?.sessionId ?? null} isOpen={attendanceSession !== null}
        onClose={() => setAttendanceSession(null)}
        onContinueToAssessment={nextAssessment ? () => navigate(nextAssessment) : undefined} />
    </div>
  );
}
