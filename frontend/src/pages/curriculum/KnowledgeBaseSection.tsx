import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { knowledgeApi } from '../../api/knowledgeApi';
import { curriculumApi } from '../../api/curriculumApi';
import { classApi } from '../../api/classApi';
import { KnowledgeScope, type KnowledgeDocument } from '../../types/knowledge';
import { Modal } from '../../components/common/Modal';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { useToast } from '../../components/common/ToastProvider';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { useAuth } from '../../context/AuthContext';

export const KnowledgeBaseSection: React.FC = () => {
  const queryClient = useQueryClient();
  const showToast = useToast();
  const { user, hasPermission } = useAuth();
  const canManage = user?.role === 'Admin' || hasPermission('curriculum.manage');

  // Filter by Scope Group: undefined (All) | 'global' | 'curriculum' | 'class'
  const [selectedScopeGroup, setSelectedScopeGroup] = useState<string | undefined>(undefined);
  const [searchTerm, setSearchTerm] = useState('');
  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);
  const [deletingDoc, setDeletingDoc] = useState<KnowledgeDocument | null>(null);

  // Upload Form State (3 simplified scope groups)
  const [file, setFile] = useState<File | null>(null);
  const [docTitle, setDocTitle] = useState('');
  const [uploadScopeMode, setUploadScopeMode] = useState<'global' | 'curriculum' | 'class'>('global');
  const [selectedCourseId, setSelectedCourseId] = useState<number | undefined>(undefined);
  const [selectedLevelId, setSelectedLevelId] = useState<number | undefined>(undefined);
  const [selectedClassId, setSelectedClassId] = useState<number | undefined>(undefined);

  // Fetch Documents
  const { data: documents = [], isLoading } = useQuery({
    queryKey: ['knowledge-documents', selectedScopeGroup],
    queryFn: () => knowledgeApi.getDocuments({ scopeGroup: selectedScopeGroup }),
    refetchInterval: (q) =>
      q.state.data?.some((d) => d.status === 'processing') ? 3000 : false,
  });

  // Fetch Courses for Upload Dropdown
  const { data: coursesData } = useQuery({
    queryKey: ['courses-all'],
    queryFn: () => curriculumApi.getCourses({ pageSize: 100 }),
    enabled: isUploadModalOpen && uploadScopeMode === 'curriculum',
  });
  const courses = coursesData?.items || [];

  // Fetch Levels for Upload Dropdown (when course selected)
  const { data: levelsData } = useQuery({
    queryKey: ['levels-by-course', selectedCourseId],
    queryFn: () => (selectedCourseId ? curriculumApi.getLevels(selectedCourseId, { pageSize: 100 }) : null),
    enabled: isUploadModalOpen && uploadScopeMode === 'curriculum' && !!selectedCourseId,
  });
  const levels = levelsData?.items || [];

  // Fetch Classes for Upload Dropdown
  const { data: classes = [] } = useQuery({
    queryKey: ['classes-all'],
    queryFn: () => classApi.getClasses({ pageSize: 100 }),
    enabled: isUploadModalOpen && uploadScopeMode === 'class',
  });

  // Upload Mutation
  const uploadMutation = useMutation({
    mutationFn: (formData: FormData) => knowledgeApi.uploadDocument(formData),
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-documents'] });
      showToast(
        res.status === 'failed'
          ? (res.errorMessage || 'Lập chỉ mục tài liệu thất bại.')
          : 'Tải lên thành công. Hệ thống đang lập chỉ mục tài liệu.',
        res.status === 'failed' ? 'error' : 'success'
      );
      closeUploadModal();
    },
    onError: (err) => {
      showToast(err instanceof Error ? err.message : 'Tải lên tài liệu thất bại.', 'error');
    },
  });

  // Delete Mutation
  const deleteMutation = useMutation({
    mutationFn: (id: string) => knowledgeApi.deleteDocument(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-documents'] });
      showToast('Đã xóa tài liệu khỏi kho tri thức thành công.', 'success');
      setDeletingDoc(null);
    },
    onError: (err) => {
      showToast(err instanceof Error ? err.message : 'Xóa tài liệu thất bại.', 'error');
    },
  });

  const closeUploadModal = () => {
    setIsUploadModalOpen(false);
    setFile(null);
    setDocTitle('');
    setUploadScopeMode('global');
    setSelectedCourseId(undefined);
    setSelectedLevelId(undefined);
    setSelectedClassId(undefined);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    if (!f) return;
    const ext = f.name.split('.').pop()?.toLowerCase();
    if (ext !== 'pdf' && ext !== 'docx') {
      showToast('Hệ thống chỉ hỗ trợ định dạng PDF hoặc DOCX.', 'error');
      return;
    }
    if (f.size > 25 * 1024 * 1024) {
      showToast('Dung lượng file vượt quá giới hạn 25MB.', 'error');
      return;
    }
    setFile(f);
    if (!docTitle) {
      setDocTitle(f.name.replace(/\.[^/.]+$/, ''));
    }
  };

  const handleUploadSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) {
      showToast('Vui lòng chọn file để tải lên.', 'error');
      return;
    }

    let calculatedScope: KnowledgeScope = KnowledgeScope.Global;
    if (uploadScopeMode === 'curriculum') {
      if (!selectedCourseId) {
        showToast('Vui lòng chọn Khóa học áp dụng cho tài liệu.', 'error');
        return;
      }
      calculatedScope = selectedLevelId ? KnowledgeScope.Level : KnowledgeScope.Course;
    } else if (uploadScopeMode === 'class') {
      if (!selectedClassId) {
        showToast('Vui lòng chọn Lớp học áp dụng cho tài liệu.', 'error');
        return;
      }
      calculatedScope = KnowledgeScope.Class;
    }

    const formData = new FormData();
    formData.append('file', file);
    if (docTitle.trim()) formData.append('title', docTitle.trim());
    formData.append('scope', String(calculatedScope));
    if (selectedCourseId) formData.append('courseId', String(selectedCourseId));
    if (selectedLevelId) formData.append('levelId', String(selectedLevelId));
    if (selectedClassId) formData.append('classId', String(selectedClassId));

    uploadMutation.mutate(formData);
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const filteredDocs = documents.filter((d) => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.toLowerCase();
    return (
      d.title.toLowerCase().includes(q) ||
      d.fileName.toLowerCase().includes(q) ||
      (d.courseName && d.courseName.toLowerCase().includes(q)) ||
      (d.levelName && d.levelName.toLowerCase().includes(q)) ||
      (d.className && d.className.toLowerCase().includes(q))
    );
  });

  return (
    <div className="space-y-5">
      {/* Top Header Card */}
      <div className="bg-white p-5 rounded border border-slate-200 shadow-xs flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-800 flex items-center gap-2">
            <span className="text-[#0284c7]">
              <MaterialIcon name="menu_book" />
            </span>
            Kho Tri thức & Học liệu
          </h2>
          <p className="text-sm text-slate-500 mt-1">
            Tải lên giáo trình, kế hoạch bài dạy và tài liệu học tập. Khi phân công giáo viên vào lớp, giáo viên tự động được cấp quyền truy cập tài liệu tương ứng.
          </p>
        </div>
        {canManage && (
          <button
            type="button"
            onClick={() => setIsUploadModalOpen(true)}
            className="btn btn-primary shrink-0 flex items-center gap-1.5"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M12 4v16m8-8H4" />
            </svg>
            Tải lên tài liệu
          </button>
        )}
      </div>

      {/* Filter and Search Bar */}
      <div className="bg-white p-4 rounded border border-slate-200 shadow-xs flex flex-col md:flex-row justify-between items-stretch md:items-center gap-3">
        {/* Scope Group Filter Pills (3 simplified groups) */}
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider mr-1">Phạm vi:</span>
          <button
            type="button"
            onClick={() => setSelectedScopeGroup(undefined)}
            className={`px-3 py-1.5 text-xs font-medium rounded border transition-colors ${
              selectedScopeGroup === undefined
                ? 'bg-[#0284c7] text-white border-[#0284c7] shadow-xs'
                : 'bg-white text-slate-700 border-slate-300 hover:bg-slate-50'
            }`}
          >
            Tất cả ({documents.length})
          </button>
          <button
            type="button"
            onClick={() => setSelectedScopeGroup('global')}
            className={`px-3 py-1.5 text-xs font-medium rounded border transition-colors ${
              selectedScopeGroup === 'global'
                ? 'bg-[#0284c7] text-white border-[#0284c7] shadow-xs'
                : 'bg-white text-slate-700 border-slate-300 hover:bg-slate-50'
            }`}
          >
            Tài liệu chung
          </button>
          <button
            type="button"
            onClick={() => setSelectedScopeGroup('curriculum')}
            className={`px-3 py-1.5 text-xs font-medium rounded border transition-colors ${
              selectedScopeGroup === 'curriculum'
                ? 'bg-[#0284c7] text-white border-[#0284c7] shadow-xs'
                : 'bg-white text-slate-700 border-slate-300 hover:bg-slate-50'
            }`}
          >
            Chương trình học
          </button>
          <button
            type="button"
            onClick={() => setSelectedScopeGroup('class')}
            className={`px-3 py-1.5 text-xs font-medium rounded border transition-colors ${
              selectedScopeGroup === 'class'
                ? 'bg-[#0284c7] text-white border-[#0284c7] shadow-xs'
                : 'bg-white text-slate-700 border-slate-300 hover:bg-slate-50'
            }`}
          >
            Theo Lớp học
          </button>
        </div>

        {/* Search Input */}
        <div className="w-full md:w-72">
          <input
            type="text"
            placeholder="Tìm theo tên file, khóa, lớp..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full px-3 py-1.5 text-xs border border-slate-300 rounded outline-none"
          />
        </div>
      </div>

      {/* Documents Table or Empty State */}
      <div className="bg-white rounded border border-slate-200 shadow-xs overflow-hidden">
        {isLoading ? (
          <div className="text-center py-12 text-slate-400 text-sm">
            Đang tải danh sách tài liệu...
          </div>
        ) : filteredDocs.length === 0 ? (
          <div className="text-center py-12 px-4 text-slate-400">
            <div className="w-12 h-12 mx-auto mb-3 rounded-full bg-slate-50 flex items-center justify-center text-slate-400">
              <MaterialIcon name="description" />
            </div>
            <p className="font-medium text-slate-700 text-sm">Chưa có tài liệu nào trong phạm vi này.</p>
            {canManage && (
              <p className="text-xs text-slate-400 mt-1">
                Nhấn nút "Tải lên tài liệu" để thêm giáo trình vào kho học liệu.
              </p>
            )}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-slate-600">
              <thead className="bg-slate-50 border-b border-slate-200 text-xs uppercase text-slate-500 font-semibold">
                <tr>
                  <th className="px-5 py-3.5">Tài liệu</th>
                  <th className="px-4 py-3.5">Phạm vi áp dụng</th>
                  <th className="px-4 py-3.5">Đối tượng liên kết</th>
                  <th className="px-4 py-3.5">Dung lượng</th>
                  <th className="px-4 py-3.5">Trạng thái</th>
                  <th className="px-4 py-3.5">Ngày tải lên</th>
                  {canManage && <th className="px-4 py-3.5 text-right">Thao tác</th>}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredDocs.map((doc) => {
                  const isPdf = doc.documentType === 'pdf';
                  return (
                    <tr key={doc.id} className="hover:bg-slate-50/75 transition-colors">
                      <td className="px-5 py-4">
                        <div className="flex items-center gap-3">
                          <span className="w-9 h-9 rounded bg-slate-100 border border-slate-200 text-slate-700 font-bold text-xs uppercase flex items-center justify-center shrink-0">
                            {isPdf ? 'PDF' : 'DOCX'}
                          </span>
                          <div>
                            <div className="font-medium text-slate-900 line-clamp-1">{doc.title}</div>
                            <div className="text-xs text-slate-400 font-mono line-clamp-1">{doc.fileName}</div>
                          </div>
                        </div>
                      </td>

                      <td className="px-4 py-4">
                        <span className="inline-flex items-center px-2.5 py-0.5 rounded text-xs font-medium bg-slate-100 text-slate-700 border border-slate-200">
                          {doc.scopeName}
                        </span>
                      </td>

                      <td className="px-4 py-4">
                        {doc.scope === KnowledgeScope.Global && (
                          <span className="text-xs text-slate-500">Toàn bộ trung tâm</span>
                        )}
                        {doc.scope === KnowledgeScope.Course && (
                          <div className="text-xs font-medium text-slate-800">
                            Khóa: {doc.courseName || 'Khóa học'}
                          </div>
                        )}
                        {doc.scope === KnowledgeScope.Level && (
                          <div className="text-xs font-medium text-slate-800">
                            {doc.courseName ? `${doc.courseName} / ${doc.levelName}` : doc.levelName}
                          </div>
                        )}
                        {doc.scope === KnowledgeScope.Class && (
                          <div className="text-xs font-medium text-slate-800">
                            Lớp: {doc.className || 'Lớp học'}
                          </div>
                        )}
                      </td>

                      <td className="px-4 py-4 text-xs font-mono text-slate-500">
                        {formatFileSize(doc.fileSize)}
                      </td>

                      <td className="px-4 py-4">
                        {doc.status === 'ready' && (
                          <span className="inline-flex items-center gap-1.5 text-xs font-medium text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                            Sẵn sàng
                          </span>
                        )}
                        {doc.status === 'processing' && (
                          <span className="inline-flex items-center gap-1.5 text-xs font-medium text-amber-700 bg-amber-50 px-2 py-0.5 rounded border border-amber-200">
                            <span className="w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse"></span>
                            Đang xử lý
                          </span>
                        )}
                        {doc.status === 'failed' && (
                          <span
                            className="inline-flex items-center gap-1.5 text-xs font-medium text-rose-700 bg-rose-50 px-2 py-0.5 rounded border border-rose-200 cursor-help"
                            title={doc.errorMessage || 'Lỗi lập chỉ mục'}
                          >
                            <span className="w-1.5 h-1.5 rounded-full bg-rose-500"></span>
                            Lỗi
                          </span>
                        )}
                      </td>

                      <td className="px-4 py-4 text-xs text-slate-500 whitespace-nowrap">
                        {new Date(doc.createdAt).toLocaleDateString('vi-VN')}
                      </td>

                      {canManage && (
                        <td className="px-4 py-4 text-right">
                          <button
                            type="button"
                            onClick={() => setDeletingDoc(doc)}
                            className="p-1.5 text-slate-400 hover:text-rose-600 rounded hover:bg-rose-50 transition-colors"
                            title="Xóa tài liệu"
                          >
                            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                            </svg>
                          </button>
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Upload Document Modal (3 intuitive scope options) */}
      {isUploadModalOpen && (
        <Modal title="Tải lên tài liệu học liệu mới" onClose={closeUploadModal} wide>
          <form onSubmit={handleUploadSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1">
                File tài liệu <span className="text-rose-500">* (PDF hoặc DOCX, tối đa 25MB)</span>
              </label>
              <input
                type="file"
                accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                onChange={handleFileChange}
                className="block w-full text-sm text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded file:border-0 file:text-xs file:font-semibold file:bg-slate-100 file:text-slate-800 hover:file:bg-slate-200 border border-slate-300 rounded p-1"
                required
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1">
                Tiêu đề hiển thị (Tùy chọn)
              </label>
              <input
                type="text"
                placeholder="VD: Giáo trình Ucrea Step 1"
                value={docTitle}
                onChange={(e) => setDocTitle(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-slate-300 rounded outline-none"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700 mb-2">
                Phạm vi áp dụng <span className="text-rose-500">*</span>
              </label>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2.5 text-xs">
                {/* 1. Tài liệu chung */}
                <label
                  className={`p-3 rounded border cursor-pointer flex flex-col justify-between transition-all ${
                    uploadScopeMode === 'global'
                      ? 'border-[#0284c7] bg-sky-50/40 text-slate-900 font-medium'
                      : 'border-slate-200 hover:bg-slate-50 text-slate-700'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <input
                      type="radio"
                      name="uploadScopeMode"
                      checked={uploadScopeMode === 'global'}
                      onChange={() => setUploadScopeMode('global')}
                    />
                    <span className="font-semibold">Tài liệu chung</span>
                  </div>
                  <span className="text-[11px] text-slate-500 mt-1 pl-5">Quy chế, cẩm nang chung cho mọi lớp & giáo viên</span>
                </label>

                {/* 2. Chương trình học (Gộp Khóa học & Cấp độ) */}
                <label
                  className={`p-3 rounded border cursor-pointer flex flex-col justify-between transition-all ${
                    uploadScopeMode === 'curriculum'
                      ? 'border-[#0284c7] bg-sky-50/40 text-slate-900 font-medium'
                      : 'border-slate-200 hover:bg-slate-50 text-slate-700'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <input
                      type="radio"
                      name="uploadScopeMode"
                      checked={uploadScopeMode === 'curriculum'}
                      onChange={() => setUploadScopeMode('curriculum')}
                    />
                    <span className="font-semibold">Chương trình học</span>
                  </div>
                  <span className="text-[11px] text-slate-500 mt-1 pl-5">Giáo trình, slide bài giảng theo môn & cấp độ</span>
                </label>

                {/* 3. Theo Lớp học */}
                <label
                  className={`p-3 rounded border cursor-pointer flex flex-col justify-between transition-all ${
                    uploadScopeMode === 'class'
                      ? 'border-[#0284c7] bg-sky-50/40 text-slate-900 font-medium'
                      : 'border-slate-200 hover:bg-slate-50 text-slate-700'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <input
                      type="radio"
                      name="uploadScopeMode"
                      checked={uploadScopeMode === 'class'}
                      onChange={() => setUploadScopeMode('class')}
                    />
                    <span className="font-semibold">Theo Lớp học</span>
                  </div>
                  <span className="text-[11px] text-slate-500 mt-1 pl-5">Đề kiểm tra, tài liệu riêng cho một lớp cụ thể</span>
                </label>
              </div>
            </div>

            {/* Conditional Controls when 'curriculum' is selected */}
            {uploadScopeMode === 'curriculum' && (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 p-3 bg-slate-50 rounded border border-slate-200">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Khóa học áp dụng <span className="text-rose-500">*</span>
                  </label>
                  <select
                    value={selectedCourseId || ''}
                    onChange={(e) => {
                      setSelectedCourseId(e.target.value ? Number(e.target.value) : undefined);
                      setSelectedLevelId(undefined);
                    }}
                    className="w-full px-3 py-2 text-xs border border-slate-300 rounded bg-white outline-none"
                    required
                  >
                    <option value="">-- Chọn Khóa học --</option>
                    {courses.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Cấp độ áp dụng <span className="text-slate-400 font-normal">(Tùy chọn)</span>
                  </label>
                  <select
                    value={selectedLevelId || ''}
                    onChange={(e) => setSelectedLevelId(e.target.value ? Number(e.target.value) : undefined)}
                    className="w-full px-3 py-2 text-xs border border-slate-300 rounded bg-white outline-none"
                    disabled={!selectedCourseId}
                  >
                    <option value="">-- Tất cả cấp độ trong khóa học --</option>
                    {levels.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.code} - {l.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            )}

            {/* Conditional Controls when 'class' is selected */}
            {uploadScopeMode === 'class' && (
              <div className="p-3 bg-slate-50 rounded border border-slate-200">
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Chọn Lớp học áp dụng <span className="text-rose-500">*</span>
                </label>
                <select
                  value={selectedClassId || ''}
                  onChange={(e) => setSelectedClassId(e.target.value ? Number(e.target.value) : undefined)}
                  className="w-full px-3 py-2 text-xs border border-slate-300 rounded bg-white outline-none"
                  required
                >
                  <option value="">-- Chọn Lớp học --</option>
                  {classes.map((cls) => (
                    <option key={cls.id} value={cls.id}>
                      {cls.maLop} - {cls.tenLop}
                    </option>
                  ))}
                </select>
              </div>
            )}

            <div className="flex justify-end gap-2 pt-2 border-t border-slate-200">
              <button
                type="button"
                onClick={closeUploadModal}
                disabled={uploadMutation.isPending}
                className="px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 rounded border border-slate-300"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={uploadMutation.isPending}
                className="btn btn-primary"
              >
                {uploadMutation.isPending ? 'Đang tải lên...' : 'Tải lên ngay'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {/* Delete Document Confirm Dialog */}
      {deletingDoc && (
        <ConfirmDialog
          title="Xóa tài liệu khỏi kho"
          message={`Bạn có chắc muốn xóa tài liệu "${deletingDoc.title}"? Thao tác này sẽ xóa vĩnh viễn vector dữ liệu tra cứu tương ứng.`}
          confirmText="Xóa tài liệu"
          isDangerous={true}
          onConfirm={() => deleteMutation.mutate(deletingDoc.id)}
          onCancel={() => setDeletingDoc(null)}
        />
      )}
    </div>
  );
};
