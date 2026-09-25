import React, { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { curriculumApi } from '../../api/curriculumApi';
import { useAuth } from '../../context/AuthContext';
import type { ApiError } from '../../types/auth';
import type { CourseResponse, LessonResponse, LevelResponse } from '../../types/curriculum';
import { CourseModal } from './CourseModal';
import { LevelModal } from './LevelModal';
import { LessonModal } from './LessonModal';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { Pagination, PAGE_SIZE } from '../../components/common/Pagination';

export const CurriculumPage: React.FC = () => {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';
  const queryClient = useQueryClient();

  // Navigation hierarchy state
  const [selectedCourse, setSelectedCourse] = useState<CourseResponse | null>(null);
  const [selectedLevel, setSelectedLevel] = useState<LevelResponse | null>(null);
  const [coursePage, setCoursePage] = useState(1);
  const [levelPage, setLevelPage] = useState(1);
  const [lessonPage, setLessonPage] = useState(1);

  // Search & Filter state for Courses
  const [courseSearch, setCourseSearch] = useState('');
  const debouncedCourseSearch = useDebouncedValue(courseSearch);
  const [courseIsActive, setCourseIsActive] = useState<boolean | undefined>(undefined);

  // Modal states
  const [isCourseModalOpen, setIsCourseModalOpen] = useState(false);
  const [editingCourse, setEditingCourse] = useState<CourseResponse | null>(null);

  const [isLevelModalOpen, setIsLevelModalOpen] = useState(false);
  const [editingLevel, setEditingLevel] = useState<LevelResponse | null>(null);

  const [isLessonModalOpen, setIsLessonModalOpen] = useState(false);
  const [editingLesson, setEditingLesson] = useState<LessonResponse | null>(null);

  const [actionError, setActionError] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<{ message: string; action: () => void } | null>(null);

  // 1. Fetch Courses
  const {
    data: coursesData,
    isLoading: isCoursesLoading,
    isError: isCoursesError,
    error: coursesError,
  } = useQuery({
    queryKey: ['courses', debouncedCourseSearch, courseIsActive, coursePage],
    queryFn: () =>
      curriculumApi.getCourses({
        search: debouncedCourseSearch || undefined,
        isActive: courseIsActive,
        page: coursePage,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: keepPreviousData,
  });

  // 2. Fetch Levels (when course is selected)
  const {
    data: levelsData,
    isLoading: isLevelsLoading,
    isError: isLevelsError,
    error: levelsError,
  } = useQuery({
    queryKey: ['levels', selectedCourse?.id, levelPage],
    queryFn: () => (selectedCourse ? curriculumApi.getLevels(selectedCourse.id, { page: levelPage, pageSize: PAGE_SIZE }) : null),
    enabled: !!selectedCourse,
  });

  // 3. Fetch Lessons (when level is selected)
  const {
    data: lessonsData,
    isLoading: isLessonsLoading,
    isError: isLessonsError,
    error: lessonsError,
  } = useQuery({
    queryKey: ['lessons', selectedLevel?.id, lessonPage],
    queryFn: () => (selectedLevel ? curriculumApi.getLessons(selectedLevel.id, { page: lessonPage, pageSize: PAGE_SIZE }) : null),
    enabled: !!selectedLevel,
  });

  // Deactivate mutations
  const deactivateCourseMutation = useMutation({
    mutationFn: (id: number) => curriculumApi.deactivateCourse(id),
    onSuccess: () => {
      setActionError(null);
      queryClient.invalidateQueries({ queryKey: ['courses'] });
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setActionError(apiErr.message || 'Ngừng áp dụng khóa học thất bại.');
    },
  });

  const deactivateLevelMutation = useMutation({
    mutationFn: (id: number) => curriculumApi.deactivateLevel(id),
    onSuccess: () => {
      setActionError(null);
      queryClient.invalidateQueries({ queryKey: ['levels', selectedCourse?.id] });
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setActionError(apiErr.message || 'Ngừng áp dụng cấp độ thất bại.');
    },
  });

  const deactivateLessonMutation = useMutation({
    mutationFn: (id: number) => curriculumApi.deactivateLesson(id),
    onSuccess: () => {
      setActionError(null);
      queryClient.invalidateQueries({ queryKey: ['lessons', selectedLevel?.id] });
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setActionError(apiErr.message || 'Ngừng áp dụng bài học thất bại.');
    },
  });

  const handleDeactivateCourse = (c: CourseResponse) => {
    setActionError(null);
    setConfirmation({
      message: `Bạn có chắc chắn muốn ngừng áp dụng khóa học "${c.name}" (${c.code})? Danh mục đã dùng sẽ không bị xóa, chỉ chuyển sang không hoạt động.`,
      action: () => deactivateCourseMutation.mutate(c.id),
    });
  };

  const handleDeactivateLevel = (l: LevelResponse) => {
    setActionError(null);
    setConfirmation({
      message: `Bạn có chắc chắn muốn ngừng áp dụng cấp độ "${l.name}" (${l.code})?`,
      action: () => deactivateLevelMutation.mutate(l.id),
    });
  };

  const handleDeactivateLesson = (les: LessonResponse) => {
    setActionError(null);
    setConfirmation({
      message: `Bạn có chắc chắn muốn ngừng áp dụng bài học "${les.name}" (${les.code})?`,
      action: () => deactivateLessonMutation.mutate(les.id),
    });
  };

  return (
    <div>
      {/* Header & Breadcrumbs */}
      <div className="page-header">
        <div className="flex items-center gap-2 flex-wrap text-sm">
          <h1>
            <button
              type="button"
              className={`bg-transparent border-0 p-0 ${selectedCourse ? 'text-primary cursor-pointer' : 'text-slate-900 cursor-default'}`}
              onClick={() => {
                setSelectedCourse(null);
                setSelectedLevel(null);
              }}
            >
              Chương trình học
            </button>
          </h1>

          {selectedCourse && (
            <>
              <span className="text-slate-400">/</span>
              <button
                type="button"
                className={`bg-transparent border-0 p-0 ${selectedLevel ? 'text-primary font-medium cursor-pointer' : 'text-slate-800 font-bold cursor-default'}`}
                onClick={() => setSelectedLevel(null)}
              >
                Khóa: {selectedCourse.name}
              </button>
            </>
          )}

          {selectedLevel && (
            <>
              <span className="text-slate-400">/</span>
              <span className="font-bold text-slate-800">
                Cấp độ: {selectedLevel.name}
              </span>
            </>
          )}
        </div>

      </div>

      {actionError && <div className="alert alert-danger">{actionError}</div>}

      {/* VIEW 1: COURSES VIEW (Khi chưa chọn Course) */}
      {!selectedCourse && (
        <div>
          <div className="flex justify-between items-center mb-4 flex-wrap gap-2">
            <h2 className="text-lg font-semibold text-slate-800">Danh mục Khóa học</h2>
            {isAdmin && (
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  setEditingCourse(null);
                  setIsCourseModalOpen(true);
                }}
              >
                + Thêm Khóa học
              </button>
            )}
          </div>

          {/* Filter Khóa học */}
          <div className="card grid grid-cols-1 sm:grid-cols-2 gap-3 px-4 py-3 mb-4">
            <div>
              <label htmlFor="courseSearch">Tìm kiếm khóa học:</label>
              <input
                id="courseSearch"
                type="text"
                placeholder="Mã hoặc tên khóa học..."
                value={courseSearch}
                onChange={(e) => { setCourseSearch(e.target.value); setCoursePage(1); }}
              />
            </div>
            <div>
              <label htmlFor="courseActiveFilter">Trạng thái áp dụng:</label>
              <select
                id="courseActiveFilter"
                value={courseIsActive === undefined ? '' : courseIsActive.toString()}
                onChange={(e) => {
                  const val = e.target.value;
                  setCourseIsActive(val === '' ? undefined : val === 'true');
                  setCoursePage(1);
                }}
              >
                <option value="">Tất cả</option>
                <option value="true">Đang áp dụng</option>
                <option value="false">Ngừng áp dụng</option>
              </select>
            </div>
          </div>

          {isCoursesError && (
            <div className="alert alert-danger">
              {coursesError?.message || 'Lỗi tải danh mục khóa học.'}
            </div>
          )}

          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Mã khóa học</th>
                  <th>Tên khóa học</th>
                  <th>Mô tả</th>
                  <th>Trạng thái</th>
                  <th className="text-right">Hành động</th>
                </tr>
              </thead>
              <tbody>
                {isCoursesLoading ? (
                  <tr>
                    <td colSpan={5} className="text-center py-8 text-slate-400">
                      Đang tải danh sách khóa học...
                    </td>
                  </tr>
                ) : coursesData?.items && coursesData.items.length > 0 ? (
                  coursesData.items.map((course) => (
                    <tr key={course.id}>
                      <td className="font-semibold font-mono">{course.code}</td>
                      <td className="font-medium">{course.name}</td>
                      <td className="text-slate-500 text-xs">
                        {course.description || '—'}
                      </td>
                      <td>
                        {course.isActive ? (
                          <span className="badge badge-active">Áp dụng</span>
                        ) : (
                          <span className="badge badge-inactive">Ngừng áp dụng</span>
                        )}
                      </td>
                      <td className="text-right whitespace-nowrap">
                        <button
                          type="button"
                          className="btn btn-primary px-2.5 py-1 text-xs mr-1"
                          onClick={() => {
                            setSelectedCourse(course);
                            setSelectedLevel(null);
                            setLevelPage(1);
                            setLessonPage(1);
                          }}
                        >
                          Xem cấp độ →
                        </button>

                        {isAdmin && (
                          <>
                            <button
                              type="button"
                              className="btn btn-secondary px-2 py-1 text-xs mr-1"
                              onClick={() => {
                                setEditingCourse(course);
                                setIsCourseModalOpen(true);
                              }}
                            >
                              Sửa
                            </button>
                            {course.isActive && (
                              <button
                                type="button"
                                className="btn btn-danger px-2 py-1 text-xs"
                                onClick={() => handleDeactivateCourse(course)}
                                disabled={deactivateCourseMutation.isPending}
                              >
                                Ngừng áp dụng
                              </button>
                            )}
                          </>
                        )}
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={5} className="text-center py-8 text-slate-400">
                      Chưa có khóa học nào được cấu hình.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          {coursesData && <Pagination totalItems={coursesData.totalItems} page={coursePage} onPageChange={setCoursePage} itemLabel="mục" hideSinglePage />}
        </div>
      )}

      {/* VIEW 2: LEVELS VIEW (Khi đã chọn Course nhưng chưa chọn Level) */}
      {selectedCourse && !selectedLevel && (
        <div>
          <div className="card bg-sky-50 border-sky-200 flex justify-between items-center px-4 py-3 mb-4">
            <div>
              <span className="text-xs uppercase text-slate-400 font-semibold">
                Khóa học đang xem:
              </span>
              <h2 className="text-lg font-bold text-primary">
                {selectedCourse.name} ({selectedCourse.code})
              </h2>
            </div>

            <button
              type="button"
              className="btn btn-secondary text-xs"
              onClick={() => setSelectedCourse(null)}
            >
              ← Quay lại danh sách Khóa học
            </button>
          </div>

          <div className="flex justify-between items-center mb-4">
            <h3 className="text-base font-semibold text-slate-800">Danh sách cấp độ thuộc khóa học</h3>
            {isAdmin && (
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  setEditingLevel(null);
                  setIsLevelModalOpen(true);
                }}
                disabled={!selectedCourse.isActive}
                title={!selectedCourse.isActive ? 'Không thể thêm cấp độ vào khóa học đã ngừng áp dụng' : ''}
              >
                + Thêm Cấp độ mới
              </button>
            )}
          </div>

          {isLevelsError && (
            <div className="alert alert-danger">
              {levelsError?.message || 'Lỗi tải danh sách cấp độ.'}
            </div>
          )}

          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th className="w-[60px]">STT</th>
                  <th>Mã cấp độ</th>
                  <th>Tên cấp độ</th>
                  <th>Trạng thái</th>
                  <th className="text-right">Hành động</th>
                </tr>
              </thead>
              <tbody>
                {isLevelsLoading ? (
                  <tr>
                    <td colSpan={5} className="text-center py-8 text-slate-400">
                      Đang tải danh sách cấp độ...
                    </td>
                  </tr>
                ) : levelsData?.items && levelsData.items.length > 0 ? (
                  levelsData.items.map((level) => (
                    <tr key={level.id}>
                      <td className="font-semibold">#{level.sortOrder}</td>
                      <td className="font-mono font-semibold">{level.code}</td>
                      <td className="font-medium">{level.name}</td>
                      <td>
                        {level.isActive ? (
                          <span className="badge badge-active">Áp dụng</span>
                        ) : (
                          <span className="badge badge-inactive">Ngừng áp dụng</span>
                        )}
                      </td>
                      <td className="text-right whitespace-nowrap">
                        <button
                          type="button"
                          className="btn btn-primary px-2.5 py-1 text-xs mr-1"
                          onClick={() => { setSelectedLevel(level); setLessonPage(1); }}
                        >
                          Xem bài học →
                        </button>

                        {isAdmin && (
                          <>
                            <button
                              type="button"
                              className="btn btn-secondary px-2 py-1 text-xs mr-1"
                              onClick={() => {
                                setEditingLevel(level);
                                setIsLevelModalOpen(true);
                              }}
                              disabled={!selectedCourse.isActive}
                              title={!selectedCourse.isActive ? 'Không thể sửa cấp độ khi khóa học đã ngừng áp dụng' : ''}
                            >
                              Sửa
                            </button>
                            {level.isActive && (
                              <button
                                type="button"
                                className="btn btn-danger px-2 py-1 text-xs"
                                onClick={() => handleDeactivateLevel(level)}
                                disabled={deactivateLevelMutation.isPending}
                              >
                                Ngừng áp dụng
                              </button>
                            )}
                          </>
                        )}
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={5} className="text-center py-8 text-slate-400">
                      Chưa có cấp độ nào trong khóa học này.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          {levelsData && <Pagination totalItems={levelsData.totalItems} page={levelPage} onPageChange={setLevelPage} itemLabel="mục" hideSinglePage />}
        </div>
      )}

      {/* VIEW 3: LESSONS VIEW (Khi đã chọn Level) */}
      {selectedCourse && selectedLevel && (
        <div>
          <div className="card bg-sky-50 border-sky-200 flex justify-between items-center px-4 py-3 mb-4">
            <div>
              <span className="text-xs uppercase text-slate-400 font-semibold">
                Khóa {selectedCourse.name} → Cấp độ:
              </span>
              <h2 className="text-lg font-bold text-primary">
                {selectedLevel.name} ({selectedLevel.code})
              </h2>
            </div>

            <button
              type="button"
              className="btn btn-secondary text-xs"
              onClick={() => setSelectedLevel(null)}
            >
              ← Quay lại danh sách Cấp độ
            </button>
          </div>

          <div className="flex justify-between items-center mb-4">
            <h3 className="text-base font-semibold text-slate-800">Danh sách bài học thuộc cấp độ</h3>
            {isAdmin && (
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  setEditingLesson(null);
                  setIsLessonModalOpen(true);
                }}
                disabled={!selectedLevel.isActive || !selectedCourse.isActive}
                title={!selectedLevel.isActive ? 'Không thể thêm bài học vào cấp độ đã ngừng áp dụng' : ''}
              >
                + Thêm Bài học mới
              </button>
            )}
          </div>

          {isLessonsError && (
            <div className="alert alert-danger">
              {lessonsError?.message || 'Lỗi tải danh sách bài học.'}
            </div>
          )}

          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th className="w-[60px]">STT</th>
                  <th>Mã bài học</th>
                  <th>Tên bài học</th>
                  <th>Mục tiêu bài học</th>
                  <th>Trạng thái</th>
                  {isAdmin && <th className="text-right">Hành động</th>}
                </tr>
              </thead>
              <tbody>
                {isLessonsLoading ? (
                  <tr>
                    <td colSpan={6} className="text-center py-8 text-slate-400">
                      Đang tải danh sách bài học...
                    </td>
                  </tr>
                ) : lessonsData?.items && lessonsData.items.length > 0 ? (
                  lessonsData.items.map((lesson) => (
                    <tr key={lesson.id}>
                      <td className="font-semibold">#{lesson.sortOrder}</td>
                      <td className="font-mono font-semibold">{lesson.code}</td>
                      <td className="font-medium">{lesson.name}</td>
                      <td className="text-slate-500 text-xs">
                        {lesson.objective || '—'}
                      </td>
                      <td>
                        {lesson.isActive ? (
                          <span className="badge badge-active">Áp dụng</span>
                        ) : (
                          <span className="badge badge-inactive">Ngừng áp dụng</span>
                        )}
                      </td>
                      {isAdmin && (
                        <td className="text-right whitespace-nowrap">
                          <button
                            type="button"
                            className="btn btn-secondary px-2 py-1 text-xs mr-1"
                            onClick={() => {
                              setEditingLesson(lesson);
                              setIsLessonModalOpen(true);
                            }}
                            disabled={!selectedLevel.isActive || !selectedCourse.isActive}
                            title={!selectedLevel.isActive || !selectedCourse.isActive ? 'Không thể sửa bài học khi cấp độ hoặc khóa học đã ngừng áp dụng' : ''}
                          >
                            Sửa
                          </button>
                          {lesson.isActive && (
                            <button
                              type="button"
                              className="btn btn-danger px-2 py-1 text-xs"
                              onClick={() => handleDeactivateLesson(lesson)}
                              disabled={deactivateLessonMutation.isPending}
                            >
                              Ngừng áp dụng
                            </button>
                          )}
                        </td>
                      )}
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={6} className="text-center py-8 text-slate-400">
                      Chưa có bài học nào trong cấp độ này.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          {lessonsData && <Pagination totalItems={lessonsData.totalItems} page={lessonPage} onPageChange={setLessonPage} itemLabel="mục" hideSinglePage />}
        </div>
      )}

      {/* Modals */}
      <CourseModal
        isOpen={isCourseModalOpen}
        course={editingCourse}
        onClose={() => setIsCourseModalOpen(false)}
        onSuccess={() => {
          queryClient.invalidateQueries({ queryKey: ['courses'] });
        }}
      />

      {selectedCourse && (
        <LevelModal
          isOpen={isLevelModalOpen}
          courseId={selectedCourse.id}
          level={editingLevel}
          onClose={() => setIsLevelModalOpen(false)}
          onSuccess={() => {
            queryClient.invalidateQueries({ queryKey: ['levels', selectedCourse.id] });
          }}
        />
      )}

      {selectedLevel && (
        <LessonModal
          isOpen={isLessonModalOpen}
          levelId={selectedLevel.id}
          lesson={editingLesson}
          onClose={() => setIsLessonModalOpen(false)}
          onSuccess={() => {
            queryClient.invalidateQueries({ queryKey: ['lessons', selectedLevel.id] });
          }}
        />
      )}

      {confirmation && (
        <ConfirmDialog
          title="Xác nhận ngừng áp dụng"
          message={confirmation.message}
          confirmText="Ngừng áp dụng"
          onCancel={() => setConfirmation(null)}
          onConfirm={() => {
            confirmation.action();
            setConfirmation(null);
          }}
        />
      )}
    </div>
  );
};
