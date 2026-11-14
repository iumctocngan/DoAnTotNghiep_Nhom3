import React, { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Modal } from '../../components/common/Modal';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { useToast } from '../../components/common/ToastProvider';
import { knowledgeApi } from '../../api/knowledgeApi';
import type { KnowledgeDocument } from '../../types/knowledge';

interface CloRubricModalProps {
  isOpen: boolean;
  onClose: () => void;
  documents: KnowledgeDocument[];
  defaultDocId?: string;
}

export const CloRubricModal: React.FC<CloRubricModalProps> = ({
  isOpen,
  onClose,
  documents,
  defaultDocId,
}) => {
  const readyPdfs = documents.filter((d) => d.status === 'ready' && d.documentType === 'pdf');
  const [docId, setDocId] = useState(defaultDocId || readyPdfs[0]?.id || '');
  const [startPage, setStartPage] = useState('');
  const [endPage, setEndPage] = useState('');
  const [exporting, setExporting] = useState(false);
  const showToast = useToast();

  const { data: outline = [] } = useQuery({
    queryKey: ['document', docId, 'outline'],
    queryFn: () => knowledgeApi.getDocumentOutline(docId),
    enabled: isOpen && !!docId,
  });

  useEffect(() => {
    if (!docId && readyPdfs.length > 0) {
      setDocId(defaultDocId && readyPdfs.some((d) => d.id === defaultDocId) ? defaultDocId : readyPdfs[0].id);
    }
  }, [docId, defaultDocId, readyPdfs]);

  if (!isOpen) return null;

  const handleSelectOutline = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const item = outline[Number(e.target.value)];
    if (item) {
      setStartPage(String(item.startPage));
      setEndPage(String(item.endPage));
    }
  };

  const handleGenerate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!docId) return showToast('Vui lòng chọn tài liệu PDF.', 'error');

    setExporting(true);
    try {
      const blob = await knowledgeApi.generateCloRubric(docId, {
        startPage: startPage ? parseInt(startPage, 10) : undefined,
        endPage: endPage ? parseInt(endPage, 10) : undefined,
      });

      const selectedDoc = readyPdfs.find((d) => d.id === docId);
      const docBaseName = selectedDoc?.fileName.replace(/\.[^/.]+$/, '') || docId.slice(0, 8);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `CLO_Rubric_${docBaseName}.xlsx`;
      a.click();
      URL.revokeObjectURL(url);

      showToast('Đã tạo và tải bảng ma trận CLO & Rubric thành công.', 'success');
      onClose();
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Xuất CLO & Rubric thất bại.', 'error');
    } finally {
      setExporting(false);
    }
  };

  return (
    <Modal title="Xuất Ma trận Chuẩn đầu ra CLO & Rubric (.xlsx)" onClose={onClose} wide closeDisabled={exporting}>
      <form onSubmit={handleGenerate} className="flex flex-col gap-3.5 text-sm">
        <div>
          <label className="block font-medium text-slate-700 mb-1">Tài liệu tham chiếu</label>
          <select
            value={docId}
            onChange={(e) => {
              setDocId(e.target.value);
              setStartPage('');
              setEndPage('');
            }}
            disabled={exporting}
            className="w-full border border-slate-300 rounded px-3 py-2 bg-white text-slate-800"
            required
          >
            {readyPdfs.length === 0 ? (
              <option value="">Không có tài liệu PDF nào sẵn sàng</option>
            ) : (
              readyPdfs.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.title || d.fileName} ({d.fileName})
                </option>
              ))
            )}
          </select>
        </div>

        {outline.length > 0 && (
          <div>
            <label className="block font-medium text-slate-700 mb-1">
              Bài học trong sách (tự động điền trang)
            </label>
            <select
              onChange={handleSelectOutline}
              defaultValue=""
              disabled={exporting}
              className="w-full border border-blue-300 bg-blue-50/50 rounded px-3 py-2 text-slate-800 font-medium"
            >
              <option value="">-- Chọn bài học để tự động điền số trang --</option>
              {outline.map((item, idx) => (
                <option key={idx} value={idx}>
                  {item.title} (Trang {item.startPage} - {item.endPage})
                </option>
              ))}
            </select>
          </div>
        )}

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="block font-medium text-slate-700 mb-1">Từ trang (tùy chọn)</label>
            <input
              type="number"
              min={1}
              placeholder="VD: 1"
              value={startPage}
              onChange={(e) => setStartPage(e.target.value)}
              disabled={exporting}
              className="w-full border border-slate-300 rounded px-3 py-1.5"
            />
          </div>
          <div>
            <label className="block font-medium text-slate-700 mb-1">Đến trang (tùy chọn)</label>
            <input
              type="number"
              min={1}
              placeholder="VD: 50"
              value={endPage}
              onChange={(e) => setEndPage(e.target.value)}
              disabled={exporting}
              className="w-full border border-slate-300 rounded px-3 py-1.5"
            />
          </div>
        </div>

        <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
          <button
            type="button"
            onClick={onClose}
            disabled={exporting}
            className="px-4 py-2 text-slate-600 hover:bg-slate-100 rounded"
          >
            Hủy
          </button>
          <button
            type="submit"
            disabled={exporting || !docId}
            className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded font-medium flex items-center gap-2 disabled:opacity-50 cursor-pointer"
          >
            {exporting ? (
              <>
                <span className="animate-spin">
                  <MaterialIcon name="schedule" />
                </span>
                Đang tạo file...
              </>
            ) : (
              <>
                <MaterialIcon name="assignment" />
                Tạo và tải Excel (.xlsx)
              </>
            )}
          </button>
        </div>
      </form>
    </Modal>
  );
};
