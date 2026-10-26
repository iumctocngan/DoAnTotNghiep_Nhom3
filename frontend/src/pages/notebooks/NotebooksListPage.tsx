import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { notebookApi } from '../../api/notebookApi';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { Modal } from '../../components/common/Modal';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { useToast } from '../../components/common/ToastProvider';
import type { NotebookSummary } from '../../types/notebook';

export const NotebooksListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const showToast = useToast();
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<NotebookSummary | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');

  const { data: notebooks = [], isLoading } = useQuery({
    queryKey: ['notebooks'],
    queryFn: notebookApi.getNotebooks,
  });

  const saveMutation = useMutation({
    mutationFn: (d: { id?: string; title: string; description?: string }) =>
      d.id ? notebookApi.updateNotebook(d.id, d) : notebookApi.createNotebook(d),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: ['notebooks'] });
      showToast(vars.id ? 'Đã cập nhật thông tin.' : 'Đã tạo Không gian học liệu mới.', 'success');
      closeModal();
    },
    onError: (err) => showToast(err instanceof Error ? err.message : 'Thao tác thất bại.', 'error'),
  });

  const deleteMutation = useMutation({
    mutationFn: notebookApi.deleteNotebook,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notebooks'] });
      showToast('Đã xóa Notebook thành công.', 'success');
      setDeletingId(null);
    },
    onError: (err) => showToast(err instanceof Error ? err.message : 'Xóa thất bại.', 'error'),
  });

  const openModal = (nb?: NotebookSummary) => {
    setEditing(nb || null);
    setTitle(nb?.title || '');
    setDescription(nb?.description || '');
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditing(null);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;
    saveMutation.mutate({ id: editing?.id, title: title.trim(), description: description.trim() || undefined });
  };

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-white p-6 rounded-lg border border-slate-200 shadow-xs">
        <div>
          <h1 className="text-xl font-bold text-slate-800 flex items-center gap-2">
            <span className="text-blue-600"><MaterialIcon name="menu_book" /></span>
            Không gian Học liệu Thông minh
          </h1>
          <p className="text-sm text-slate-500 mt-1">
            Không gian học liệu cá nhân phục vụ hỏi đáp ngữ cảnh (RAG) và xuất ma trận chuẩn đầu ra (CLO & Rubric).
          </p>
        </div>
        <button
          type="button"
          onClick={() => openModal()}
          className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md font-medium flex items-center gap-2 shadow-xs shrink-0"
        >
          <MaterialIcon name="add" /> Tạo Notebook mới
        </button>
      </div>

      {isLoading ? (
        <div className="p-12 text-center text-slate-500 flex flex-col items-center gap-2">
          <span className="animate-spin text-blue-600"><MaterialIcon name="schedule" /></span>
          <span>Đang tải danh sách học liệu...</span>
        </div>
      ) : notebooks.length === 0 ? (
        <div className="p-12 bg-white rounded-lg border border-dashed border-slate-300 text-center flex flex-col items-center">
          <div className="w-16 h-16 bg-blue-50 text-blue-600 rounded-full flex items-center justify-center mb-3">
            <MaterialIcon name="school" />
          </div>
          <h3 className="font-semibold text-slate-700 text-base">Chưa có Không gian học liệu nào</h3>
          <p className="text-sm text-slate-500 max-w-md mt-1 mb-4">
            Tạo Notebook đầu tiên để nạp tài liệu giáo trình và trải nghiệm tính năng trợ lý học thuật thông minh.
          </p>
          <button type="button" onClick={() => openModal()} className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded font-medium text-sm flex items-center gap-2">
            <MaterialIcon name="add" /> Tạo ngay
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {notebooks.map((nb) => (
            <div key={nb.id} className="bg-white rounded-lg border border-slate-200 hover:border-blue-400 hover:shadow-md transition-all flex flex-col justify-between group">
              <div className="p-5">
                <div className="flex justify-between items-start gap-2 mb-2">
                  <Link to={`/notebooks/${nb.id}`} className="font-bold text-slate-800 text-base group-hover:text-blue-600 transition-colors line-clamp-1">
                    {nb.title}
                  </Link>
                  <div className="flex items-center gap-1 opacity-80 group-hover:opacity-100 shrink-0">
                    <button type="button" onClick={(e) => { e.preventDefault(); openModal(nb); }} className="p-1 text-slate-400 hover:text-slate-700 rounded hover:bg-slate-100" title="Chỉnh sửa">
                      <MaterialIcon name="edit" />
                    </button>
                    <button type="button" onClick={(e) => { e.preventDefault(); setDeletingId(nb.id); }} className="p-1 text-slate-400 hover:text-rose-600 rounded hover:bg-rose-50" title="Xóa">
                      <MaterialIcon name="cancel" />
                    </button>
                  </div>
                </div>
                <p className="text-xs text-slate-500 line-clamp-2 min-h-[32px] mb-4">
                  {nb.description || 'Chưa có mô tả chi tiết cho học liệu này.'}
                </p>
                <div className="flex items-center justify-between text-xs text-slate-400 pt-3 border-t border-slate-100">
                  <span className="flex items-center gap-1 font-medium text-slate-600">
                    <span className="text-blue-500"><MaterialIcon name="description" /></span>
                    {nb.documentCount} tài liệu
                  </span>
                  <span>{new Date(nb.updatedAt).toLocaleDateString('vi-VN')}</span>
                </div>
              </div>
              <div className="px-5 py-3 bg-slate-50 border-t border-slate-100 rounded-b-lg">
                <Link to={`/notebooks/${nb.id}`} className="w-full py-1.5 px-3 bg-white hover:bg-blue-50 text-blue-600 border border-slate-200 hover:border-blue-300 rounded text-xs font-semibold flex items-center justify-center gap-1.5 transition-colors">
                  <span>Mở không gian học liệu</span>
                  <MaterialIcon name="arrow_forward" />
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}

      {modalOpen && (
        <Modal title={editing ? 'Cập nhật Không gian Học liệu' : 'Tạo Không gian Học liệu Mới'} onClose={closeModal}>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4 text-sm">
            <div>
              <label className="block font-medium text-slate-700 mb-1">Tên Notebook <span className="text-rose-500">*</span></label>
              <input
                type="text"
                required
                maxLength={200}
                placeholder="VD: Giáo trình Toán tư duy Lớp 3 - Tập 1"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                className="w-full border border-slate-300 rounded px-3 py-2 text-sm focus:outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700 mb-1">Mô tả học liệu</label>
              <textarea
                rows={3}
                maxLength={1000}
                placeholder="Mô tả mục tiêu môn học, ghi chú dành riêng cho giáo viên..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="w-full border border-slate-300 rounded px-3 py-2 text-sm focus:outline-none focus:border-blue-500"
              />
            </div>
            <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
              <button type="button" onClick={closeModal} className="px-4 py-2 text-slate-600 hover:bg-slate-100 rounded">Hủy</button>
              <button type="submit" disabled={saveMutation.isPending} className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded font-medium disabled:opacity-50">
                {editing ? 'Lưu thay đổi' : 'Tạo Notebook'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {deletingId && (
        <ConfirmDialog
          title="Xác nhận xóa Không gian Học liệu"
          message="Bạn có chắc chắn muốn xóa Notebook này? Toàn bộ tài liệu và vector chỉ mục liên quan sẽ bị xóa vĩnh viễn."
          confirmText="Xác nhận xóa"
          isDangerous
          isPending={deleteMutation.isPending}
          onConfirm={() => deleteMutation.mutate(deletingId)}
          onCancel={() => setDeletingId(null)}
        />
      )}
    </div>
  );
};
