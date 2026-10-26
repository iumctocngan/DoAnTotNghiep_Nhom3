import React, { useEffect, useState } from 'react';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { getAccessToken } from '../../api/apiClient';
import type { Citation } from '../../types/notebook';

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

  if (!citation || !citation.imageUrl) return null;

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-2xl max-w-4xl w-full max-h-[92vh] flex flex-col border border-slate-200">
        <div className="flex items-center justify-between px-5 py-3 border-b border-slate-200 bg-slate-50">
          <div className="flex items-center gap-2">
            <span className="p-1.5 bg-blue-100 text-blue-700 rounded"><MaterialIcon name="menu_book" /></span>
            <div>
              <h3 className="font-semibold text-slate-800 text-sm">{citation.documentName}</h3>
              <p className="text-xs text-slate-500">
                {citation.pageNumber ? `Trang ${citation.pageNumber}` : 'Minh chứng'}
                {citation.sectionTitle && ` • ${citation.sectionTitle}`}
              </p>
            </div>
          </div>
          <button type="button" onClick={onClose} className="text-slate-400 hover:text-slate-700 p-1.5 rounded-full hover:bg-slate-200">
            <MaterialIcon name="close" />
          </button>
        </div>

        <div className="flex-1 overflow-auto p-4 flex items-center justify-center bg-slate-100 min-h-[320px]">
          {loading && (
            <div className="flex flex-col items-center gap-2 text-slate-500 text-xs">
              <span className="animate-spin text-2xl text-blue-600"><MaterialIcon name="schedule" /></span>
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
        </div>

        <div className="px-5 py-2.5 border-t border-slate-200 flex justify-between items-center text-xs text-slate-500 bg-slate-50">
          <span>Minh chứng trang gốc phục vụ đối chiếu trực quan</span>
          {blobUrl && (
            <a href={blobUrl} target="_blank" rel="noreferrer" className="text-blue-600 hover:underline flex items-center gap-1 font-medium">
              <MaterialIcon name="arrow_forward" />
              Mở ảnh tab mới
            </a>
          )}
        </div>
      </div>
    </div>
  );
};
