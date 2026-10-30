import React, { useEffect, useState } from 'react';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { getAccessToken } from '../../api/apiClient';
import type { Citation } from '../../types/knowledge';

interface CitationPreviewModalProps {
  citation: Citation | null;
  onClose: () => void;
}

export const CitationPreviewModal: React.FC<CitationPreviewModalProps> = ({ citation, onClose }) => {
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!citation?.imageUrl) {
      setBlobUrl(null);
      return;
    }
    let active = true;
    let url = '';
    setLoading(true);
    setError(false);

    const token = getAccessToken();
    fetch(citation.imageUrl, { headers: token ? { Authorization: `Bearer ${token}` } : {} })
      .then((r) => (r.ok ? r.blob() : Promise.reject()))
      .then((b) => {
        if (!active) return;
        url = URL.createObjectURL(b);
        setBlobUrl(url);
        setLoading(false);
      })
      .catch(() => {
        if (!active) return;
        setError(true);
        setLoading(false);
      });

    return () => {
      active = false;
      if (url) URL.revokeObjectURL(url);
    };
  }, [citation]);

  if (!citation) return null;
  const isDocx = !citation.imageUrl;

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-2xl max-w-4xl w-full max-h-[92vh] flex flex-col border border-slate-200">
        <div className="flex items-center justify-between px-5 py-3 border-b border-slate-200 bg-slate-50">
          <div className="flex items-center gap-2">
            <span className="p-1.5 bg-blue-100 text-[#0284c7] rounded"><MaterialIcon name="menu_book" /></span>
            <div>
              <h3 className="font-semibold text-slate-800 text-sm">{citation.documentName}</h3>
              <p className="text-xs text-slate-500">
                {citation.pageNumber ? `Trang ${citation.pageNumber}` : 'Tài liệu kế hoạch / giáo án DOCX'}
                {citation.sectionTitle && ` • ${citation.sectionTitle}`}
              </p>
            </div>
          </div>
          <button type="button" onClick={onClose} className="text-slate-400 hover:text-slate-700 p-1.5 rounded-full hover:bg-slate-200">
            <MaterialIcon name="close" />
          </button>
        </div>

        <div className="flex-1 overflow-auto p-4 flex items-center justify-center bg-slate-100 min-h-[320px]">
          {isDocx ? (
            <div className="bg-white p-6 rounded-lg border border-slate-200 shadow-xs max-w-2xl w-full text-slate-800 space-y-3">
              <div className="flex items-center gap-2 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                <span className="w-2 h-2 rounded-full bg-[#0284c7]"></span>
                Trích dẫn từ file DOCX
              </div>
              {citation.sectionTitle && (
                <div className="font-bold text-sm text-slate-900 border-b border-slate-100 pb-2">
                  Mục / Tiêu đề: {citation.sectionTitle}
                </div>
              )}
              <div className="text-xs leading-relaxed text-slate-700 bg-slate-50 p-4 rounded border border-slate-200 whitespace-pre-wrap">
                {citation.label || 'Trích đoạn nội dung tài liệu phục vụ câu trả lời.'}
              </div>
            </div>
          ) : (
            <>
              {loading && (
                <div className="flex flex-col items-center gap-2 text-slate-500 text-xs">
                  <span className="animate-spin text-2xl text-[#0284c7]"><MaterialIcon name="schedule" /></span>
                  <span>Đang tải trang sách...</span>
                </div>
              )}
              {error && !loading && (
                <div className="text-center p-6 text-rose-600 text-xs">
                  <span className="text-3xl block mb-1"><MaterialIcon name="error" /></span>
                  <p className="font-medium">Không thể tải ảnh minh chứng trang này.</p>
                </div>
              )}
              {blobUrl && !loading && !error && (
                <img src={blobUrl} alt={citation.label} className="max-w-full max-h-[75vh] object-contain rounded shadow-sm border bg-white" />
              )}
            </>
          )}
        </div>

        <div className="px-5 py-2.5 border-t border-slate-200 flex justify-between items-center text-xs text-slate-500 bg-slate-50">
          <span>{isDocx ? 'Nội dung trích dẫn trực tiếp từ kế hoạch bài dạy' : 'Minh chứng trang gốc phục vụ đối chiếu trực quan'}</span>
          {blobUrl && (
            <a href={blobUrl} target="_blank" rel="noreferrer" className="text-[#0284c7] hover:underline flex items-center gap-1 font-medium">
              <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14 5l7 7m0 0l-7 7m7-7H3" /></svg>
              Mở ảnh tab mới
            </a>
          )}
        </div>
      </div>
    </div>
  );
};
