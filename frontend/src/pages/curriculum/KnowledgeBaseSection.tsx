import React, { useState, useRef } from 'react';
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

interface UploadQueueItem {
  id: string;
  file: File;
  title: string;
  size: number;
  status: 'pending' | 'uploading' | 'success' | 'error';
  errorMessage?: string;
}

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

  // Upload Form State (Multiple files & folders)
  const [queue, setQueue] = useState<UploadQueueItem[]>([]);
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState({ current: 0, total: 0 });

  const fileInputRef = useRef<HTMLInputElement>(null);
  const folderInputRef = useRef<HTMLInputElement>(null);

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
    if (isUploading) {
      if (!window.confirm('Quá trình tải lên tài liệu đang diễn ra. Bạn có chắc muốn đóng?')) {
        return;
      }
    }
    setIsUploadModalOpen(false);
    setQueue([]);
    setIsUploading(false);
    setUploadProgress({ current: 0, total: 0 });
    setUploadScopeMode('global');
    setSelectedCourseId(undefined);
    setSelectedLevelId(undefined);
    setSelectedClassId(undefined);
  };

  const addFilesToQueue = (files: File[]) => {
    let invalidExtCount = 0;
    let oversizeCount = 0;
    const newItems: UploadQueueItem[] = [];

    for (const f of files) {
      const ext = f.name.split('.').pop()?.toLowerCase();
      if (ext !== 'pdf' && ext !== 'docx') {
        invalidExtCount++;
        continue;
      }
      if (f.size > 50 * 1024 * 1024) {
        oversizeCount++;
        continue;
      }

      // Avoid duplicate in queue
      const isDup = queue.some((q) => q.file.name === f.name && q.size === f.size);
      if (isDup) continue;

      newItems.push({
        id: `${f.name}-${f.size}-${Date.now()}-${Math.random()}`,
        file: f,
        title: f.name.replace(/\.[^/.]+$/, ''),
        size: f.size,
        status: 'pending',
      });
    }

    if (newItems.length > 0) {
      setQueue((prev) => [...prev, ...newItems]);
      let msg = `Đã thêm ${newItems.length} tài liệu vào danh sách.`;
      if (invalidExtCount > 0 || oversizeCount > 0) {
        const reasons = [];
        if (invalidExtCount > 0) reasons.push(`${invalidExtCount} tệp không phải PDF/DOCX`);
        if (oversizeCount > 0) reasons.push(`${oversizeCount} tệp vượt quá 50MB`);
        msg += ` (Bỏ qua: ${reasons.join(', ')})`;
      }
      showToast(msg, 'success');
    } else {
      if (invalidExtCount > 0 || oversizeCount > 0) {
        const reasons = [];
        if (invalidExtCount > 0) reasons.push(`${invalidExtCount} tệp không phải PDF/DOCX`);
        if (oversizeCount > 0) reasons.push(`${oversizeCount} tệp vượt quá 50MB`);
        showToast(`Không có tệp hợp lệ nào. (${reasons.join(', ')})`, 'error');
      }
    }
  };

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      addFilesToQueue(Array.from(e.target.files));
      e.target.value = '';
    }
  };

  const handleDrop = async (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(false);

    const items = e.dataTransfer.items;
    if (!items || items.length === 0) {
      if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
        addFilesToQueue(Array.from(e.dataTransfer.files));
      }
      return;
    }

    const traverseEntry = async (entry: any): Promise<File[]> => {
      return new Promise((resolve) => {
        if (!entry) return resolve([]);
        if (entry.isFile) {
          entry.file(
            (file: File) => resolve([file]),
            () => resolve([])
          );
        } else if (entry.isDirectory) {
          const reader = entry.createReader();
          const allEntries: any[] = [];
          const readEntries = () => {
            reader.readEntries(async (entries: any[]) => {
              if (!entries || entries.length === 0) {
                const subFiles = await Promise.all(allEntries.map((e) => traverseEntry(e)));
                resolve(subFiles.flat());
              } else {
                allEntries.push(...entries);
                readEntries();
              }
            });
          };
          readEntries();
        } else {
          resolve([]);
        }
      });
    };

    try {
      const promises: Promise<File[]>[] = [];
      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        if (typeof item.webkitGetAsEntry === 'function') {
          const entry = item.webkitGetAsEntry();
          if (entry) promises.push(traverseEntry(entry));
        } else {
          const f = item.getAsFile();
          if (f) promises.push(Promise.resolve([f]));
        }
      }
      const fileArrays = await Promise.all(promises);
      addFilesToQueue(fileArrays.flat());
    } catch {
      if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
        addFilesToQueue(Array.from(e.dataTransfer.files));
      }
    }
  };

  const removeQueueItem = (index: number) => {
    setQueue((prev) => prev.filter((_, idx) => idx !== index));
  };

  const updateQueueItemTitle = (index: number, newTitle: string) => {
    setQueue((prev) =>
      prev.map((it, idx) => (idx === index ? { ...it, title: newTitle } : it))
    );
  };

  const handleUploadSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (queue.length === 0) {
      showToast('Vui lòng chọn ít nhất một file tài liệu.', 'error');
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

    setIsUploading(true);
    setUploadProgress({ current: 0, total: queue.length });

    let successCount = 0;
    let failCount = 0;

    for (let i = 0; i < queue.length; i++) {
      const item = queue[i];
      if (item.status === 'success') {
        successCount++;
        setUploadProgress({ current: i + 1, total: queue.length });
        continue;
      }

      setQueue((prev) =>
        prev.map((it, idx) => (idx === i ? { ...it, status: 'uploading' } : it))
      );

      const formData = new FormData();
      formData.append('file', item.file);
      if (item.title.trim()) formData.append('title', item.title.trim());
      formData.append('scope', String(calculatedScope));
      if (selectedCourseId) formData.append('courseId', String(selectedCourseId));
      if (selectedLevelId) formData.append('levelId', String(selectedLevelId));
      if (selectedClassId) formData.append('classId', String(selectedClassId));

      try {
        const res = await knowledgeApi.uploadDocument(formData);
        if (res.status === 'failed') {
          failCount++;
          setQueue((prev) =>
            prev.map((it, idx) =>
              idx === i
                ? { ...it, status: 'error', errorMessage: res.errorMessage || 'Lập chỉ mục thất bại' }
                : it
            )
          );
        } else {
          successCount++;
          setQueue((prev) =>
            prev.map((it, idx) => (idx === i ? { ...it, status: 'success' } : it))
          );
        }
      } catch (err: unknown) {
        failCount++;
        const message = err instanceof Error ? err.message : 'Tải lên thất bại';
        setQueue((prev) =>
          prev.map((it, idx) =>
            idx === i ? { ...it, status: 'error', errorMessage: message } : it
          )
        );
      }

      setUploadProgress({ current: i + 1, total: queue.length });
    }

    setIsUploading(false);
    queryClient.invalidateQueries({ queryKey: ['knowledge-documents'] });

    if (failCount === 0) {
      showToast(`Đã tải lên và lập chỉ mục thành công ${successCount} tài liệu!`, 'success');
      closeUploadModal();
    } else {
      showToast(
        `Hoàn tất: ${successCount} tài liệu thành công, ${failCount} tài liệu thất bại.`,
        'error'
      );
    }
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
            {/* File Dropzone & Selection */}
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                Tài liệu học liệu <span className="text-rose-500">* (Định dạng PDF hoặc DOCX, tối đa 50MB/tệp)</span>
              </label>

              <div
                onDragOver={(e) => {
                  e.preventDefault();
                  if (!isUploading) setIsDragging(true);
                }}
                onDragLeave={(e) => {
                  e.preventDefault();
                  setIsDragging(false);
                }}
                onDrop={handleDrop}
                className={`border-2 border-dashed rounded-lg p-5 text-center transition-colors ${
                  isDragging
                    ? 'border-[#0284c7] bg-sky-50'
                    : 'border-slate-300 hover:border-slate-400 bg-slate-50/50'
                }`}
              >
                <div className="flex flex-col items-center justify-center gap-2">
                  <div className="w-12 h-12 rounded-full bg-sky-100 text-[#0284c7] flex items-center justify-center">
                    <svg className="w-7 h-7 text-[#0284c7]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                    </svg>
                  </div>
                  <div>
                    <p className="text-xs font-medium text-slate-700">
                      Kéo thả tệp hoặc thư mục vào đây
                    </p>
                  </div>

                  <div className="flex flex-wrap items-center justify-center gap-2 mt-1">
                    <button
                      type="button"
                      onClick={() => fileInputRef.current?.click()}
                      disabled={isUploading}
                      className="px-3.5 py-1.5 bg-white border border-slate-300 hover:bg-slate-100 text-slate-700 rounded text-xs font-medium shadow-2xs flex items-center gap-1.5 transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <MaterialIcon name="description" className="text-sm text-[#0284c7]" />
                      <span>Chọn tệp</span>
                    </button>

                    <button
                      type="button"
                      onClick={() => folderInputRef.current?.click()}
                      disabled={isUploading}
                      className="px-3.5 py-1.5 bg-white border border-slate-300 hover:bg-slate-100 text-slate-700 rounded text-xs font-medium shadow-2xs flex items-center gap-1.5 transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <svg className="w-4 h-4 text-amber-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                      </svg>
                      <span>Chọn thư mục</span>
                    </button>
                  </div>

                  {/* Hidden native inputs */}
                  <input
                    ref={fileInputRef}
                    type="file"
                    multiple
                    accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    onChange={handleFileInputChange}
                    className="hidden"
                  />
                  <input
                    ref={folderInputRef}
                    type="file"
                    // @ts-expect-error webkitdirectory is standard in HTML5 browsers
                    webkitdirectory=""
                    directory=""
                    multiple
                    onChange={handleFileInputChange}
                    className="hidden"
                  />
                </div>
              </div>
            </div>

            {/* Selected Files List */}
            {queue.length > 0 && (
              <div className="border border-slate-200 rounded-md overflow-hidden bg-white">
                <div className="bg-slate-100/90 px-3 py-2 border-b border-slate-200 flex items-center justify-between text-xs">
                  <div className="font-semibold text-slate-700 flex items-center gap-1.5">
                    <MaterialIcon name="assignment" className="text-base text-slate-500" />
                    <span>
                      Danh sách tệp đã chọn ({queue.length} tệp - {formatFileSize(queue.reduce((acc, it) => acc + it.size, 0))})
                    </span>
                  </div>
                  {!isUploading && (
                    <button
                      type="button"
                      onClick={() => setQueue([])}
                      className="text-[11px] text-rose-600 hover:text-rose-700 font-semibold hover:underline cursor-pointer"
                    >
                      Xóa tất cả
                    </button>
                  )}
                </div>

                <div className="max-h-48 overflow-y-auto divide-y divide-slate-100">
                  {queue.map((item, idx) => {
                    const ext = item.file.name.split('.').pop()?.toUpperCase() || 'PDF';
                    return (
                      <div key={item.id} className="p-2.5 px-3 flex items-center justify-between gap-3 text-xs hover:bg-slate-50">
                        <div className="flex items-center gap-2.5 min-w-0 flex-1">
                          <span className="w-7 h-7 rounded bg-slate-200 text-slate-700 font-bold text-[9px] flex items-center justify-center shrink-0 uppercase">
                            {ext}
                          </span>
                          <div className="min-w-0 flex-1">
                            <div className="font-medium text-slate-800 truncate" title={item.file.name}>
                              {item.title}
                            </div>
                            <div className="text-[11px] text-slate-400 font-mono flex items-center gap-2 mt-0.5">
                              <span>{formatFileSize(item.size)}</span>
                              {item.errorMessage && (
                                <span className="text-rose-500 font-sans truncate font-medium">
                                  • {item.errorMessage}
                                </span>
                              )}
                            </div>
                          </div>
                        </div>

                        {/* Status / Action */}
                        <div className="flex items-center gap-2 shrink-0">
                          {item.status === 'pending' && (
                            <span className="text-[11px] text-slate-400 flex items-center gap-1">
                              <span className="w-1.5 h-1.5 rounded-full bg-slate-400"></span>
                              Chờ nạp
                            </span>
                          )}
                          {item.status === 'uploading' && (
                            <span className="text-[11px] text-[#0284c7] font-semibold flex items-center gap-1">
                              <div className="w-3 h-3 border-2 border-[#0284c7] border-t-transparent rounded-full animate-spin"></div>
                              Đang lập chỉ mục...
                            </span>
                          )}
                          {item.status === 'success' && (
                            <span className="text-[11px] text-emerald-600 font-semibold flex items-center gap-1">
                              <MaterialIcon name="check" className="text-sm text-emerald-500" />
                              Sẵn sàng
                            </span>
                          )}
                          {item.status === 'error' && (
                            <span className="text-[11px] text-rose-600 font-semibold flex items-center gap-1" title={item.errorMessage}>
                              <MaterialIcon name="error" className="text-sm text-rose-500" />
                              Lỗi
                            </span>
                          )}

                          {!isUploading && item.status !== 'uploading' && (
                            <button
                              type="button"
                              onClick={() => removeQueueItem(idx)}
                              className="text-slate-400 hover:text-rose-500 p-1 rounded hover:bg-slate-200/60 transition-colors cursor-pointer"
                              title="Xóa khỏi danh sách"
                            >
                              <MaterialIcon name="close" className="text-sm" />
                            </button>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Optional Title input when exactly 1 file is selected */}
            {queue.length === 1 && (
              <div>
                <label className="block text-xs font-medium text-slate-700 mb-1">
                  Tiêu đề hiển thị (Tùy chọn)
                </label>
                <input
                  type="text"
                  placeholder="VD: Giáo trình Ucrea Step 1"
                  value={queue[0].title}
                  onChange={(e) => updateQueueItemTitle(0, e.target.value)}
                  disabled={isUploading}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded outline-none disabled:bg-slate-100"
                />
              </div>
            )}

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

            {/* Overall Progress Indicator during upload */}
            {isUploading && (
              <div className="space-y-1.5 p-3 bg-sky-50 rounded border border-sky-200">
                <div className="flex items-center justify-between text-xs font-semibold text-sky-900">
                  <span>
                    Đang nạp dữ liệu: {uploadProgress.current}/{uploadProgress.total} tài liệu...
                  </span>
                  <span>
                    {uploadProgress.total > 0
                      ? Math.round((uploadProgress.current / uploadProgress.total) * 100)
                      : 0}%
                  </span>
                </div>
                <div className="w-full bg-sky-200 rounded-full h-2 overflow-hidden">
                  <div
                    className="bg-[#0284c7] h-2 rounded-full transition-all duration-300"
                    style={{
                      width: `${uploadProgress.total > 0 ? (uploadProgress.current / uploadProgress.total) * 100 : 0}%`,
                    }}
                  />
                </div>
              </div>
            )}

            <div className="flex justify-end gap-2 pt-2 border-t border-slate-200">
              <button
                type="button"
                onClick={closeUploadModal}
                disabled={isUploading}
                className="px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 rounded border border-slate-300 cursor-pointer disabled:opacity-50"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={isUploading || queue.length === 0}
                className="btn btn-primary cursor-pointer disabled:opacity-50 flex items-center gap-1.5"
              >
                {isUploading ? (
                  <>
                    <div className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                    <span>Đang tải lên ({uploadProgress.current}/{uploadProgress.total})...</span>
                  </>
                ) : (
                  <>
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
                    </svg>
                    <span>Tải lên ngay {queue.length > 0 ? `(${queue.length} tệp)` : ''}</span>
                  </>
                )}
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
