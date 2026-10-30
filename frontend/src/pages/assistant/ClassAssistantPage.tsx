import React, { useState, useEffect, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { knowledgeApi } from '../../api/knowledgeApi';
import { classApi } from '../../api/classApi';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../components/common/ToastProvider';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { CitationPreviewModal } from './CitationPreviewModal';
import { KnowledgeScope, type Citation, type ClassConversation, type ClassMessage } from '../../types/knowledge';

export const ClassAssistantPage: React.FC = () => {
  const showToast = useToast();
  const queryClient = useQueryClient();
  const chatBottomRef = useRef<HTMLDivElement>(null);
  const { user, hasPermission } = useAuth();
  const isAdmin = user?.role === 'Admin';
  const canChat = hasPermission('assistant.chat');

  // Active Scope/Class State: 0 = Toàn bộ trung tâm (Admin only), >0 = Lớp cụ thể
  const [selectedClassId, setSelectedClassId] = useState<number | null>(null);

  // Left Panel Tab: 'docs' | 'history'
  const [activeLeftTab, setActiveLeftTab] = useState<'docs' | 'history'>('docs');

  // Filter Selected Documents
  const [selectedDocIds, setSelectedDocIds] = useState<string[]>([]);

  // Active Conversation State
  const [currentConversationId, setCurrentConversationId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ClassMessage[]>([]);
  const [inputQuestion, setInputQuestion] = useState('');
  const [isAsking, setIsAsking] = useState(false);

  // Modals
  const [activeCitation, setActiveCitation] = useState<Citation | null>(null);
  const [deletingConv, setDeletingConv] = useState<ClassConversation | null>(null);

  // 1. Fetch Classes
  const { data: classes = [], isLoading: isClassesLoading } = useQuery({
    queryKey: ['teacher-classes'],
    queryFn: () => classApi.getClasses({ pageSize: 100 }),
  });

  // Auto-select: 0 (Toàn bộ trung tâm) for Admin, or first class for Teacher
  useEffect(() => {
    if (selectedClassId === null) {
      if (isAdmin) {
        setSelectedClassId(0);
      } else if (classes.length > 0) {
        setSelectedClassId(classes[0].id);
      }
    }
  }, [isAdmin, classes, selectedClassId]);

  // 2. Fetch Class/Center Knowledge Info (Inherited Documents)
  const { data: classKnowledge, isLoading: isKnowledgeLoading } = useQuery({
    queryKey: ['class-knowledge-info', selectedClassId],
    queryFn: () => (selectedClassId !== null ? knowledgeApi.getClassKnowledgeInfo(selectedClassId) : null),
    enabled: selectedClassId !== null,
  });

  // When knowledge loads, default select all ready docs
  useEffect(() => {
    if (classKnowledge?.documents) {
      setSelectedDocIds(classKnowledge.documents.map((d) => d.id));
    }
  }, [classKnowledge]);

  // 3. Fetch Conversations
  const { data: conversations = [], isLoading: isConversationsLoading } = useQuery({
    queryKey: ['class-conversations', selectedClassId],
    queryFn: () => (selectedClassId !== null ? knowledgeApi.getClassConversations(selectedClassId) : []),
    enabled: selectedClassId !== null,
  });

  // Auto-select latest conversation or null for new chat
  useEffect(() => {
    if (conversations.length > 0 && !currentConversationId) {
      setCurrentConversationId(conversations[0].id);
    }
  }, [conversations, currentConversationId]);

  // 4. Fetch Messages of Current Conversation
  const { data: conversationMessages } = useQuery({
    queryKey: ['class-conversation-messages', selectedClassId, currentConversationId],
    queryFn: () =>
      selectedClassId !== null && currentConversationId
        ? knowledgeApi.getClassConversationMessages(selectedClassId, currentConversationId)
        : [],
    enabled: selectedClassId !== null && !!currentConversationId,
  });

  useEffect(() => {
    if (conversationMessages) {
      setMessages(conversationMessages);
    } else if (!currentConversationId) {
      setMessages([]);
    }
  }, [conversationMessages, currentConversationId]);

  // Scroll to bottom on messages update
  useEffect(() => {
    chatBottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isAsking]);

  // Delete Conversation Mutation
  const deleteConvMutation = useMutation({
    mutationFn: (convId: string) =>
      selectedClassId !== null ? knowledgeApi.deleteClassConversation(selectedClassId, convId) : Promise.resolve(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['class-conversations', selectedClassId] });
      showToast('Đã xóa phiên trò chuyện.', 'success');
      if (deletingConv?.id === currentConversationId) {
        setCurrentConversationId(null);
        setMessages([]);
      }
      setDeletingConv(null);
    },
    onError: (err) => showToast(err instanceof Error ? err.message : 'Xóa thất bại.', 'error'),
  });

  // Handle Send Question
  const handleSend = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    const q = inputQuestion.trim();
    if (!q || selectedClassId === null || isAsking) return;

    const tempUserMsg: ClassMessage = {
      id: `temp-${Date.now()}`,
      conversationId: currentConversationId || '',
      role: 'user',
      content: q,
      citations: [],
      isFromCache: false,
      createdAt: new Date().toISOString(),
    };

    setMessages((prev) => [...prev, tempUserMsg]);
    setInputQuestion('');
    setIsAsking(true);

    try {
      const res = await knowledgeApi.chatClass(selectedClassId, {
        question: q,
        conversationId: currentConversationId || undefined,
        selectedDocumentIds: selectedDocIds.length > 0 ? selectedDocIds : undefined,
      });

      if (!currentConversationId) {
        setCurrentConversationId(res.conversationId);
      }

      const assistantMsg: ClassMessage = {
        id: `assistant-${Date.now()}`,
        conversationId: res.conversationId,
        role: 'assistant',
        content: res.answer,
        citations: res.citations,
        isFromCache: res.isFromCache,
        createdAt: res.createdAt,
      };

      setMessages((prev) => [...prev, assistantMsg]);
      queryClient.invalidateQueries({ queryKey: ['class-conversations', selectedClassId] });
    } catch (err) {
      const errorMsg: ClassMessage = {
        id: `error-${Date.now()}`,
        conversationId: currentConversationId || '',
        role: 'assistant',
        content: err instanceof Error ? `Lỗi: ${err.message}` : 'Không thể kết nối đến Trợ lý AI.',
        citations: [],
        isFromCache: false,
        createdAt: new Date().toISOString(),
      };
      setMessages((prev) => [...prev, errorMsg]);
    } finally {
      setIsAsking(false);
    }
  };

  const handleStartNewChat = () => {
    setCurrentConversationId(null);
    setMessages([]);
  };

  const toggleDocSelection = (id: string) => {
    setSelectedDocIds((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id]
    );
  };

  const toggleSelectAllDocs = () => {
    if (!classKnowledge?.documents) return;
    if (selectedDocIds.length === classKnowledge.documents.length) {
      setSelectedDocIds([]);
    } else {
      setSelectedDocIds(classKnowledge.documents.map((d) => d.id));
    }
  };

  return (
    <div className="p-4 md:p-6 max-w-7xl mx-auto space-y-4 flex flex-col h-[calc(100vh-4rem)]">
      {/* Top Header Card & Scope/Class Selector */}
      <div className="bg-white p-4 rounded border border-slate-200 shadow-xs flex flex-col md:flex-row justify-between items-start md:items-center gap-4 shrink-0">
        <div>
          <h1 className="text-lg font-bold text-slate-800 flex items-center gap-2">
            <span className="text-[#0284c7]">
              <MaterialIcon name="menu_book" />
            </span>
            {isAdmin && selectedClassId === 0
              ? 'Trợ lý Tri thức Toàn Trung tâm'
              : 'Trợ lý Học liệu Lớp học'}
          </h1>
          <p className="text-xs text-slate-500 mt-0.5">
            {isAdmin && selectedClassId === 0
              ? 'Tra cứu trên toàn bộ kho chunks tài liệu, giáo trình và quy chế của trung tâm.'
              : 'Tra cứu trên toàn bộ giáo trình, kế hoạch bài dạy và mục tiêu học tập thuộc phạm vi lớp học được phân công.'}
          </p>
        </div>

        {/* Scope / Class Selector Dropdown */}
        <div className="flex items-center gap-2 w-full md:w-auto">
          <label className="text-xs font-semibold text-slate-600 whitespace-nowrap mb-0">
            {isAdmin ? 'Phạm vi tra cứu:' : 'Lớp phụ trách:'}
          </label>
          <select
            value={selectedClassId === null ? '' : selectedClassId}
            onChange={(e) => {
              setSelectedClassId(Number(e.target.value));
              setCurrentConversationId(null);
              setMessages([]);
            }}
            disabled={isClassesLoading && !isAdmin}
            className="w-full md:w-80 px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white focus:outline-none shadow-xs"
          >
            {isAdmin && (
              <option value="0">
                🌐 Toàn bộ Trung tâm (Kho chunks tổng hợp)
              </option>
            )}
            {classes.map((cls) => (
              <option key={cls.id} value={cls.id}>
                {cls.maLop} - {cls.tenLop}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Main Two-Column Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 flex-1 min-h-0">
        {/* LEFT SIDEBAR (4 cols on lg, 3 on xl) */}
        <div className="lg:col-span-4 xl:col-span-3 bg-white rounded border border-slate-200 shadow-xs flex flex-col min-h-0 overflow-hidden">
          {/* Left Tabs */}
          <div className="flex border-b border-slate-200 bg-slate-50 text-xs font-semibold shrink-0">
            <button
              type="button"
              onClick={() => setActiveLeftTab('docs')}
              className={`flex-1 py-3 text-center border-b-2 transition-colors flex items-center justify-center gap-1.5 ${
                activeLeftTab === 'docs'
                  ? 'border-[#0284c7] text-[#0284c7] bg-white font-bold'
                  : 'border-transparent text-slate-500 hover:text-slate-800'
              }`}
            >
              <MaterialIcon name="description" />
              Tài liệu ({classKnowledge?.documents.length || 0})
            </button>
            <button
              type="button"
              onClick={() => setActiveLeftTab('history')}
              className={`flex-1 py-3 text-center border-b-2 transition-colors flex items-center justify-center gap-1.5 ${
                activeLeftTab === 'history'
                  ? 'border-[#0284c7] text-[#0284c7] bg-white font-bold'
                  : 'border-transparent text-slate-500 hover:text-slate-800'
              }`}
            >
              <MaterialIcon name="schedule" />
              Lịch sử ({conversations.length})
            </button>
          </div>

          {/* TAB 1: Class Documents */}
          {activeLeftTab === 'docs' && (
            <div className="flex-1 overflow-y-auto p-3 space-y-3">
              <div className="flex items-center justify-between text-xs text-slate-500 pb-2 border-b border-slate-100">
                <span>Tài liệu áp dụng:</span>
                {classKnowledge && classKnowledge.documents.length > 0 && (
                  <button
                    type="button"
                    onClick={toggleSelectAllDocs}
                    className="text-[#0284c7] hover:underline font-medium"
                  >
                    {selectedDocIds.length === classKnowledge.documents.length ? 'Bỏ chọn' : 'Chọn tất cả'}
                  </button>
                )}
              </div>

              {isKnowledgeLoading ? (
                <div className="text-center py-8 text-xs text-slate-400">Đang nạp học liệu...</div>
              ) : !classKnowledge || classKnowledge.documents.length === 0 ? (
                <div className="text-center py-8 text-xs text-slate-400 space-y-1">
                  <p className="font-medium text-slate-600">Chưa có tài liệu nào sẵn sàng trong phạm vi này.</p>
                  <p className="text-[11px]">Admin có thể tải lên tài liệu trong phần Quản lý Chương trình học.</p>
                </div>
              ) : (
                <div className="space-y-2">
                  {classKnowledge.documents.map((doc) => {
                    const isSelected = selectedDocIds.includes(doc.id);
                    return (
                      <div
                        key={doc.id}
                        onClick={() => toggleDocSelection(doc.id)}
                        className={`p-2.5 rounded border text-xs cursor-pointer transition-all flex items-start gap-2.5 ${
                          isSelected
                            ? 'border-[#0284c7] bg-sky-50/40'
                            : 'border-slate-200 hover:bg-slate-50 opacity-70'
                        }`}
                      >
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => {}}
                          className="mt-0.5 rounded text-[#0284c7] focus:ring-[#0284c7]"
                        />
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-1.5">
                            <span className="px-1 py-0.2 rounded text-[10px] font-bold uppercase shrink-0 bg-slate-100 text-slate-700 border border-slate-200">
                              {doc.documentType}
                            </span>
                            <span className="font-semibold text-slate-800 truncate" title={doc.title}>
                              {doc.title}
                            </span>
                          </div>

                          <div className="flex items-center gap-2 mt-1 text-[11px] text-slate-500">
                            {doc.scope === KnowledgeScope.Global && (
                              <span className="text-slate-600 font-medium">Tài liệu chung</span>
                            )}
                            {doc.scope === KnowledgeScope.Course && (
                              <span className="text-slate-600 font-medium">Khóa: {doc.courseName}</span>
                            )}
                            {doc.scope === KnowledgeScope.Level && (
                              <span className="text-slate-600 font-medium">Cấp độ: {doc.levelName}</span>
                            )}
                            {doc.scope === KnowledgeScope.Class && (
                              <span className="text-slate-600 font-medium">Lớp riêng</span>
                            )}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}

          {/* TAB 2: Conversation History */}
          {activeLeftTab === 'history' && (
            <div className="flex-1 overflow-y-auto flex flex-col min-h-0">
              <div className="p-3 border-b border-slate-100">
                <button
                  type="button"
                  onClick={handleStartNewChat}
                  className="w-full py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded font-medium text-xs flex items-center justify-center gap-1.5 transition-colors border border-slate-200"
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                  </svg>
                  Đoạn chat mới
                </button>
              </div>

              <div className="flex-1 overflow-y-auto p-2 space-y-1">
                {isConversationsLoading ? (
                  <div className="text-center py-8 text-xs text-slate-400">Đang tải lịch sử...</div>
                ) : conversations.length === 0 ? (
                  <div className="text-center py-8 text-xs text-slate-400">Chưa có phiên trò chuyện nào.</div>
                ) : (
                  conversations.map((conv) => {
                    const isActive = conv.id === currentConversationId;
                    return (
                      <div
                        key={conv.id}
                        className={`group p-2.5 rounded text-xs cursor-pointer flex items-center justify-between transition-colors ${
                          isActive
                            ? 'bg-[#0284c7] text-white font-medium'
                            : 'hover:bg-slate-100 text-slate-700'
                        }`}
                        onClick={() => setCurrentConversationId(conv.id)}
                      >
                        <div className="flex-1 min-w-0 pr-2">
                          <p className="truncate text-xs">{conv.title}</p>
                          <p className={`text-[10px] mt-0.5 ${isActive ? 'text-sky-100' : 'text-slate-400'}`}>
                            {new Date(conv.updatedAt).toLocaleDateString('vi-VN')} • {conv.messageCount} tin nhắn
                          </p>
                        </div>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setDeletingConv(conv);
                          }}
                          className={`p-1 rounded opacity-0 group-hover:opacity-100 transition-opacity ${
                            isActive
                              ? 'text-white hover:bg-[#0369a1]'
                              : 'text-slate-400 hover:text-rose-600 hover:bg-slate-200'
                          }`}
                          title="Xóa phiên"
                        >
                          <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                          </svg>
                        </button>
                      </div>
                    );
                  })
                )}
              </div>
            </div>
          )}
        </div>

        {/* RIGHT MAIN CHAT WORKSPACE (8 cols on lg, 9 on xl) */}
        <div className="lg:col-span-8 xl:col-span-9 bg-white rounded border border-slate-200 shadow-xs flex flex-col min-h-0 overflow-hidden">
          {/* Chat Workspace Header */}
          <div className="px-5 py-3 border-b border-slate-200 bg-slate-50 flex items-center justify-between shrink-0">
            <div className="flex items-center gap-2">
              <span className={`w-2.5 h-2.5 rounded-full ${selectedClassId === 0 ? 'bg-sky-500' : 'bg-emerald-500'}`}></span>
              <span className="text-sm font-bold text-slate-800">
                {selectedClassId === 0
                  ? '🌐 Toàn bộ Trung tâm'
                  : (classKnowledge ? `${classKnowledge.className} (${classKnowledge.classCode})` : 'Chọn lớp giảng dạy')}
              </span>
              <span className="text-xs text-slate-500 hidden sm:inline">
                • {selectedClassId === 0 ? 'Tra cứu trên toàn bộ kho chunks tri thức' : `${classKnowledge?.courseName} / ${classKnowledge?.levelName}`}
              </span>
            </div>

            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-600 bg-white px-2.5 py-1 rounded border border-slate-200 shadow-2xs">
                {selectedDocIds.length} tài liệu
              </span>
            </div>
          </div>

          {/* Messages Stream */}
          <div className="flex-1 overflow-y-auto p-4 md:p-6 space-y-4">
            {messages.length === 0 ? (
              <div className="h-full flex flex-col items-center justify-center text-center p-6 text-slate-400 space-y-3">
                <div className="w-12 h-12 rounded-full bg-slate-100 text-slate-600 flex items-center justify-center text-xl">
                  <MaterialIcon name="menu_book" />
                </div>
                <div>
                  <h3 className="font-semibold text-slate-700 text-sm">
                    {selectedClassId === 0 ? 'Trợ lý Tri thức Toàn Trung tâm' : 'Trợ lý Học liệu Lớp học'}
                  </h3>
                  <p className="text-xs text-slate-400 max-w-md mt-1">
                    {selectedClassId === 0
                      ? 'Admin có thể đặt bất kỳ câu hỏi nào trên toàn bộ kho chunks tài liệu, giáo trình và quy chế của trung tâm.'
                      : 'Thầy/cô có thể tra cứu nội dung bài học, chuẩn đầu ra (CLO), hoặc các phương pháp giải toán trong kho tài liệu lớp.'}
                  </p>
                </div>
              </div>
            ) : (
              messages.map((m) => {
                const isUser = m.role === 'user';
                return (
                  <div key={m.id} className={`flex ${isUser ? 'justify-end' : 'justify-start'}`}>
                    <div
                      className={`max-w-[85%] sm:max-w-[78%] rounded-lg px-4 py-3 shadow-xs ${
                        isUser
                          ? 'bg-[#0284c7] text-white'
                          : 'bg-slate-50 border border-slate-200 text-slate-800'
                      }`}
                    >
                      {/* Assistant Header & Cache Badge */}
                      {!isUser && (
                        <div className="flex items-center justify-between gap-2 mb-2 pb-1.5 border-b border-slate-200/70">
                          <span className="text-[11px] font-bold text-slate-700">
                            {selectedClassId === 0 ? 'Trợ lý Trung tâm' : 'Trợ lý Học liệu'}
                          </span>
                          {m.isFromCache && (
                            <span
                              className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-medium bg-emerald-50 text-emerald-800 border border-emerald-200"
                              title="Câu hỏi trùng khớp đã được lưu trong bộ nhớ đệm, phản hồi tức thì."
                            >
                              ⚡ Đã lưu trong bộ nhớ đệm
                            </span>
                          )}
                        </div>
                      )}

                      {/* Content */}
                      <div className="text-xs sm:text-sm leading-relaxed whitespace-pre-wrap">
                        {m.content}
                      </div>

                      {/* Citations */}
                      {!isUser && m.citations && m.citations.length > 0 && (
                        <div className="mt-3 pt-2.5 border-t border-slate-200">
                          <div className="text-[11px] font-semibold text-slate-500 mb-1.5">Nguồn tài liệu:</div>
                          <div className="flex flex-wrap gap-1.5">
                            {m.citations.map((c, i) => (
                              <button
                                key={c.chunkId || i}
                                type="button"
                                onClick={() => setActiveCitation(c)}
                                className="inline-flex items-center gap-1 px-2 py-1 bg-white hover:bg-slate-100 border border-slate-200 rounded text-[11px] text-slate-700 transition-colors shadow-2xs"
                              >
                                <span className="font-bold text-[#0284c7]">[{i + 1}]</span>
                                <span className="max-w-[150px] truncate">{c.documentName}</span>
                                {c.pageNumber && <span className="text-slate-400 font-mono">P.{c.pageNumber}</span>}
                              </button>
                            ))}
                          </div>
                        </div>
                      )}

                      <div
                        className={`text-[10px] mt-1.5 text-right ${
                          isUser ? 'text-sky-100' : 'text-slate-400'
                        }`}
                      >
                        {new Date(m.createdAt).toLocaleTimeString('vi-VN', {
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                      </div>
                    </div>
                  </div>
                );
              })
            )}

            {isAsking && (
              <div className="flex justify-start">
                <div className="bg-slate-50 border border-slate-200 rounded-lg px-4 py-3 shadow-xs text-xs text-slate-500 flex items-center gap-2">
                  <div className="w-4 h-4 border-2 border-[#0284c7] border-t-transparent rounded-full animate-spin"></div>
                  <span>
                    {selectedClassId === 0
                      ? 'Trợ lý đang tra cứu toàn bộ kho chunks tri thức...'
                      : 'Trợ lý đang tra cứu học liệu lớp học...'}
                  </span>
                </div>
              </div>
            )}
            <div ref={chatBottomRef} />
          </div>

          {/* Bottom Chat Input Bar */}
          <div className="p-3 md:p-4 border-t border-slate-200 bg-white shrink-0">
            <form onSubmit={handleSend} className="flex items-center gap-2">
              <input
                type="text"
                placeholder={
                  !canChat
                    ? 'Bạn chỉ có quyền xem tài liệu, không có quyền đặt câu hỏi...'
                    : selectedClassId === 0
                    ? 'Đặt câu hỏi trên toàn bộ kho tri thức trung tâm...'
                    : selectedClassId !== null
                    ? `Đặt câu hỏi cho ${classKnowledge?.className || 'lớp học'}...`
                    : 'Vui lòng chọn phạm vi để bắt đầu...'
                }
                value={inputQuestion}
                onChange={(e) => setInputQuestion(e.target.value)}
                disabled={!canChat || selectedClassId === null || isAsking}
                className="flex-1 px-4 py-2 text-xs sm:text-sm border border-slate-300 rounded outline-none disabled:bg-slate-100 disabled:cursor-not-allowed"
              />
              <button
                type="submit"
                disabled={!canChat || !inputQuestion.trim() || selectedClassId === null || isAsking}
                className="btn btn-primary"
              >
                Gửi
              </button>
            </form>
          </div>
        </div>
      </div>

      {/* Citation Preview Modal */}
      {activeCitation && (
        <CitationPreviewModal
          citation={activeCitation}
          onClose={() => setActiveCitation(null)}
        />
      )}

      {/* Delete Conversation Confirm Dialog */}
      {deletingConv && (
        <ConfirmDialog
          title="Xóa phiên hỏi đáp"
          message={`Bạn có chắc muốn xóa phiên trò chuyện "${deletingConv.title}"?`}
          confirmText="Xóa phiên"
          isDangerous={true}
          onConfirm={() => deleteConvMutation.mutate(deletingConv.id)}
          onCancel={() => setDeletingConv(null)}
        />
      )}
    </div>
  );
};
