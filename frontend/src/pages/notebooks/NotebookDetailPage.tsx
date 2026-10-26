import React, { useRef, useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notebookApi } from '../../api/notebookApi';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { useToast } from '../../components/common/ToastProvider';
import { CloRubricModal } from './CloRubricModal';
import { CitationPreviewModal } from './CitationPreviewModal';
import type { Citation, DocumentItem } from '../../types/notebook';

interface ChatMessage {
  id: string;
  sender: 'user' | 'assistant';
  content: string;
  citations?: Citation[];
  time: string;
}

export const NotebookDetailPage: React.FC = () => {
  const { id = '' } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const showToast = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const chatEndRef = useRef<HTMLDivElement>(null);

  const [selectedDocs, setSelectedDocs] = useState<string[]>([]);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [question, setQuestion] = useState('');
  const [asking, setAsking] = useState(false);
  const [cloOpen, setCloOpen] = useState(false);
  const [citation, setCitation] = useState<Citation | null>(null);
  const [deletingDoc, setDeletingDoc] = useState<DocumentItem | null>(null);

  const { data: notebook, isLoading } = useQuery({
    queryKey: ['notebook', id],
    queryFn: () => notebookApi.getNotebook(id),
    enabled: !!id,
    refetchInterval: (q) => q.state.data?.documents.some((d) => d.status === 'processing') ? 3000 : false,
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => notebookApi.uploadDocument(id, file),
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ['notebook', id] });
      queryClient.invalidateQueries({ queryKey: ['notebooks'] });
      showToast(res.status === 'failed' ? (res.errorMessage || 'Lập chỉ mục thất bại.') : 'Đang lập chỉ mục vector...', res.status === 'failed' ? 'error' : 'success');
      if (fileInputRef.current) fileInputRef.current.value = '';
    },
    onError: (err) => {
      showToast(err instanceof Error ? err.message : 'Tải lên thất bại.', 'error');
      if (fileInputRef.current) fileInputRef.current.value = '';
    },
  });

  const deleteDocMutation = useMutation({
    mutationFn: (docId: string) => notebookApi.deleteDocument(id, docId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notebook', id] });
      queryClient.invalidateQueries({ queryKey: ['notebooks'] });
      showToast('Đã xóa tài liệu thành công.', 'success');
      setDeletingDoc(null);
    },
    onError: (err) => showToast(err instanceof Error ? err.message : 'Xóa thất bại.', 'error'),
  });

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, asking]);

  const handleFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const ext = file.name.split('.').pop()?.toLowerCase();
    if (ext !== 'pdf' && ext !== 'docx') return showToast('Chỉ hỗ trợ file PDF hoặc DOCX.', 'error');
    if (file.size > 25 * 1024 * 1024) return showToast('File vượt quá giới hạn 25MB.', 'error');
    uploadMutation.mutate(file);
  };

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    const q = question.trim();
    if (!q || asking) return;

    const time = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    setMessages((prev) => [...prev, { id: String(Date.now()), sender: 'user', content: q, time }]);
    setQuestion('');
    setAsking(true);

    try {
      const res = await notebookApi.chat(id, { question: q, documentIds: selectedDocs.length ? selectedDocs : undefined });
      setMessages((prev) => [...prev, { id: String(Date.now() + 1), sender: 'assistant', content: res.answer, citations: res.citations, time }]);
    } catch (err) {
      setMessages((prev) => [...prev, { id: String(Date.now() + 1), sender: 'assistant', content: err instanceof Error ? `Lỗi: ${err.message}` : 'Lỗi kết nối tới trợ lý RAG.', time }]);
    } finally {
      setAsking(false);
    }
  };

  if (isLoading) {
    return (
      <div className="p-12 text-center text-slate-500 flex flex-col items-center gap-2">
        <span className="animate-spin text-blue-600"><MaterialIcon name="schedule" /></span>
        <span>Đang tải thông tin học liệu...</span>
      </div>
    );
  }

  if (!notebook) {
    return (
      <div className="p-12 text-center text-slate-600">
        <p>Không tìm thấy không gian học liệu này.</p>
        <Link to="/notebooks" className="text-blue-600 hover:underline mt-2 inline-block">Quay lại danh sách</Link>
      </div>
    );
  }

  const readyPdfCount = notebook.documents.filter((d) => d.status === 'ready' && d.documentType === 'pdf').length;

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-xs flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <div className="flex items-center gap-2 text-xs text-slate-500 mb-1">
            <Link to="/notebooks" className="hover:text-blue-600 flex items-center gap-1"><MaterialIcon name="arrow_back" /> Danh sách Notebooks</Link>
            <span>/</span>
            <span className="text-slate-800 font-medium">Chi tiết học liệu</span>
          </div>
          <h1 className="text-xl font-bold text-slate-800">{notebook.title}</h1>
          <p className="text-xs text-slate-500 mt-0.5">{notebook.description || 'Chưa có mô tả thêm cho học liệu này.'}</p>
        </div>
        <button
          type="button"
          onClick={() => setCloOpen(true)}
          disabled={readyPdfCount === 0}
          className="px-3.5 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300 text-white rounded-md text-xs font-semibold flex items-center gap-1.5 shadow-xs shrink-0"
        >
          <MaterialIcon name="assignment" /> Xuất CLO & Rubric (.xlsx)
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-5 bg-white rounded-lg border border-slate-200 shadow-xs flex flex-col overflow-hidden">
          <div className="p-4 border-b border-slate-200 bg-slate-50 flex justify-between items-center">
            <div className="flex items-center gap-2">
              <span className="text-blue-600"><MaterialIcon name="menu_book" /></span>
              <h2 className="font-semibold text-slate-800 text-sm">Tài liệu học tập ({notebook.documents.length})</h2>
            </div>
            <label className="cursor-pointer px-2.5 py-1 bg-blue-600 hover:bg-blue-700 text-white rounded text-xs font-medium flex items-center gap-1">
              <MaterialIcon name="add" /> {uploadMutation.isPending ? 'Đang nạp...' : 'Thêm tài liệu'}
              <input ref={fileInputRef} type="file" accept=".pdf,.docx" onChange={handleFile} disabled={uploadMutation.isPending} className="hidden" />
            </label>
          </div>

          <div className="p-3 text-xs text-slate-500 bg-blue-50/50 border-b border-slate-100 flex items-center justify-between">
            <span>Tick chọn để giới hạn phạm vi hỏi đáp</span>
            {selectedDocs.length > 0 && (
              <button type="button" onClick={() => setSelectedDocs([])} className="text-blue-600 hover:underline font-medium">
                Bỏ chọn tất cả ({selectedDocs.length})
              </button>
            )}
          </div>

          <div className="divide-y divide-slate-100 max-h-[580px] overflow-y-auto">
            {notebook.documents.length === 0 ? (
              <div className="p-8 text-center text-slate-400">
                <span className="text-3xl text-slate-300 mb-1 block"><MaterialIcon name="description" /></span>
                <p className="text-xs">Chưa có tài liệu nào trong Notebook này (Hỗ trợ PDF/DOCX tối đa 25MB).</p>
              </div>
            ) : (
              notebook.documents.map((doc) => {
                const selected = selectedDocs.includes(doc.id);
                return (
                  <div key={doc.id} className={`p-3.5 flex items-center justify-between gap-3 hover:bg-slate-50 ${selected ? 'bg-blue-50/40' : ''}`}>
                    <div className="flex items-center gap-2.5 min-w-0">
                      {doc.status === 'ready' && (
                        <input
                          type="checkbox"
                          checked={selected}
                          onChange={() => setSelectedDocs((p) => p.includes(doc.id) ? p.filter((x) => x !== doc.id) : [...p, doc.id])}
                          className="rounded border-slate-300 text-blue-600 cursor-pointer"
                        />
                      )}
                      <span className={`text-xl shrink-0 ${doc.documentType === 'pdf' ? 'text-rose-500' : 'text-blue-500'}`}>
                        <MaterialIcon name="description" />
                      </span>
                      <div className="min-w-0">
                        <p className="text-xs font-medium text-slate-800 truncate" title={doc.fileName}>{doc.fileName}</p>
                        <div className="flex items-center gap-2 mt-0.5 text-[11px] text-slate-400">
                          <span>{(doc.fileSize / (1024 * 1024)).toFixed(1)} MB</span>
                          <span className={`px-1.5 py-0.2 rounded text-[10px] font-medium border ${
                            doc.status === 'ready' ? 'bg-emerald-50 text-emerald-700 border-emerald-200' :
                            doc.status === 'processing' ? 'bg-amber-50 text-amber-700 border-amber-200' : 'bg-rose-50 text-rose-700 border-rose-200'
                          }`}>
                            {doc.status === 'ready' ? 'Sẵn sàng' : doc.status === 'processing' ? 'Đang nạp...' : 'Lỗi'}
                          </span>
                        </div>
                      </div>
                    </div>
                    <button type="button" onClick={() => setDeletingDoc(doc)} className="text-slate-400 hover:text-rose-600 p-1 rounded hover:bg-rose-50" title="Xóa tài liệu">
                      <MaterialIcon name="cancel" />
                    </button>
                  </div>
                );
              })
            )}
          </div>
        </div>

        <div className="lg:col-span-7 bg-white rounded-lg border border-slate-200 shadow-xs flex flex-col h-[650px]">
          <div className="p-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="text-blue-600"><MaterialIcon name="assignment" /></span>
              <h2 className="font-semibold text-slate-800 text-sm">Trợ lý Học thuật (RAG Chat)</h2>
            </div>
            {selectedDocs.length > 0 && (
              <span className="text-[11px] font-medium px-2 py-0.5 rounded bg-blue-100 text-blue-700">
                Giới hạn trong {selectedDocs.length} tài liệu
              </span>
            )}
          </div>

          <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-slate-50/50">
            {messages.length === 0 ? (
              <div className="h-full flex flex-col items-center justify-center text-center text-slate-400 p-6">
                <div className="w-12 h-12 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center mb-2"><MaterialIcon name="school" /></div>
                <h3 className="font-semibold text-slate-700 text-sm">Hỏi đáp tài liệu thông minh</h3>
                <p className="text-xs text-slate-500 max-w-sm mt-1">Đặt câu hỏi về nội dung giáo trình, định nghĩa, bài tập hoặc công thức. Hệ thống sẽ trích xuất kèm minh chứng trang gốc.</p>
              </div>
            ) : (
              messages.map((m) => (
                <div key={m.id} className={`flex flex-col ${m.sender === 'user' ? 'items-end' : 'items-start'}`}>
                  <div className={`max-w-[85%] rounded-lg p-3 text-sm leading-relaxed ${m.sender === 'user' ? 'bg-blue-600 text-white rounded-br-none' : 'bg-white border border-slate-200 text-slate-800 shadow-xs rounded-bl-none'}`}>
                    <div className="whitespace-pre-wrap">{m.content}</div>
                    {m.citations && m.citations.length > 0 && (
                      <div className="mt-3 pt-2.5 border-t border-slate-100 flex flex-col gap-1.5">
                        <span className="text-[11px] font-semibold text-slate-500 flex items-center gap-1">
                          <span className="text-blue-500"><MaterialIcon name="menu_book" /></span> Minh chứng:
                        </span>
                        <div className="flex flex-wrap gap-1.5">
                          {m.citations.map((c, i) => (
                            <button
                              key={c.chunkId || i}
                              type="button"
                              onClick={() => c.imageUrl && setCitation(c)}
                              disabled={!c.imageUrl}
                              className={`text-[11px] px-2 py-0.5 rounded border flex items-center gap-1 ${c.imageUrl ? 'bg-blue-50 hover:bg-blue-100 text-blue-700 border-blue-200 cursor-pointer' : 'bg-slate-50 text-slate-600 border-slate-200'}`}
                            >
                              <MaterialIcon name={c.imageUrl ? 'assignment' : 'menu_book'} />
                              <span className="font-medium">{c.pageNumber ? `Trang ${c.pageNumber}` : c.documentName}</span>
                            </button>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                  <span className="text-[10px] text-slate-400 mt-1 px-1">{m.time}</span>
                </div>
              ))
            )}
            {asking && (
              <div className="flex items-center gap-2 text-slate-500 text-xs bg-white border border-slate-200 rounded-lg p-3 w-fit shadow-xs">
                <span className="animate-spin text-blue-600"><MaterialIcon name="schedule" /></span>
                <span>Trợ lý đang đọc tài liệu và phân tích câu trả lời...</span>
              </div>
            )}
            <div ref={chatEndRef} />
          </div>

          <form onSubmit={handleSend} className="p-3 border-t border-slate-200 bg-white flex gap-2">
            <input
              type="text"
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              placeholder="Đặt câu hỏi về bài toán, phương pháp tư duy, định nghĩa..."
              disabled={asking}
              className="flex-1 border border-slate-300 rounded px-3 py-2 text-sm focus:outline-none focus:border-blue-500 disabled:bg-slate-50"
            />
            <button type="submit" disabled={asking || !question.trim()} className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white rounded font-medium text-sm flex items-center gap-1 shrink-0">
              <MaterialIcon name="arrow_forward" /> Gửi
            </button>
          </form>
        </div>
      </div>

      {cloOpen && <CloRubricModal isOpen={cloOpen} onClose={() => setCloOpen(false)} notebookId={notebook.id} documents={notebook.documents} />}
      {citation && <CitationPreviewModal citation={citation} onClose={() => setCitation(null)} />}
      {deletingDoc && (
        <ConfirmDialog
          title="Xác nhận xóa tài liệu"
          message={`Bạn có chắc muốn xóa "${deletingDoc.fileName}"? Toàn bộ vector chỉ mục liên quan sẽ bị xóa.`}
          confirmText="Xóa tài liệu"
          isDangerous
          isPending={deleteDocMutation.isPending}
          onConfirm={() => deleteDocMutation.mutate(deletingDoc.id)}
          onCancel={() => setDeletingDoc(null)}
        />
      )}
    </div>
  );
};
