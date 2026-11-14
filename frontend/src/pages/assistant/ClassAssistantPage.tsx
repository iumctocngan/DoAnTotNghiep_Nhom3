import React, { useState, useEffect, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { knowledgeApi } from '../../api/knowledgeApi';
import { classApi } from '../../api/classApi';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../components/common/ToastProvider';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { CitationPreviewModal } from './CitationPreviewModal';
import { CloRubricModal } from './CloRubricModal';
import { KnowledgeScope, type Citation, type ClassConversation } from '../../types/knowledge';

export const ClassAssistantPage: React.FC = () => {
  const showToast = useToast();
  const queryClient = useQueryClient();
  const chatBottomRef = useRef<HTMLDivElement>(null);
  const { user, hasPermission } = useAuth();
  const isAdmin = user?.role === 'Admin';
  const canChat = hasPermission('assistant.chat');

  // Active Scope/Class State: 0 = Toàn bộ trung tâm (Admin only), >0 = Lớp cụ thể
  const [selectedClassId, setSelectedClassId] = useState<number | null>(null);

  // Left Panel Tab: 'conversations' | 'docs'
  const [activeLeftTab, setActiveLeftTab] = useState<'conversations' | 'docs'>('conversations');

  // Active Conversation State (null = New Chat)
  const [currentConversationId, setCurrentConversationId] = useState<string | null>(null);
  const [isNewChatMode, setIsNewChatMode] = useState(false);

  // Filter Selected Documents
  const [selectedDocIds, setSelectedDocIds] = useState<string[]>([]);

  // Input & Asking State
  const [inputQuestion, setInputQuestion] = useState('');
  const [isAsking, setIsAsking] = useState(false);

  // Modals / Dialogs
  const [activeCitation, setActiveCitation] = useState<Citation | null>(null);
  const [deletingConv, setDeletingConv] = useState<ClassConversation | null>(null);
  const [editingConv, setEditingConv] = useState<ClassConversation | null>(null);
  const [editTitleInput, setEditTitleInput] = useState('');
  const [isCloModalOpen, setIsCloModalOpen] = useState(false);

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

  // Reset conversation selection when changing class
  useEffect(() => {
    setCurrentConversationId(null);
    setIsNewChatMode(false);
  }, [selectedClassId]);

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

  // 3. Fetch Conversations of Selected Class
  const { data: conversations = [], isLoading: isConversationsLoading } = useQuery({
    queryKey: ['class-conversations', selectedClassId],
    queryFn: () => (selectedClassId !== null ? knowledgeApi.getClassConversations(selectedClassId) : []),
    enabled: selectedClassId !== null,
  });

  // Auto-select first conversation if exists and none selected or invalid (and not in new chat mode)
  useEffect(() => {
    if (isConversationsLoading) return;

    if (isNewChatMode) {
      return;
    }

    if (conversations.length === 0) {
      if (currentConversationId !== null) {
        setCurrentConversationId(null);
      }
      return;
    }

    const currentExists = conversations.some((c) => c.id === currentConversationId);
    if (!currentConversationId || !currentExists) {
      setCurrentConversationId(conversations[0].id);
    }
  }, [conversations, currentConversationId, isNewChatMode, isConversationsLoading]);

  // 4. Fetch Messages of Active Conversation
  const { data: messages = [], isLoading: isMessagesLoading } = useQuery({
    queryKey: ['class-conversation-messages', selectedClassId, currentConversationId],
    queryFn: () =>
      selectedClassId !== null && currentConversationId
        ? knowledgeApi.getClassConversationMessages(selectedClassId, currentConversationId)
        : [],
    enabled: selectedClassId !== null && currentConversationId !== null,
  });

  // Scroll to bottom on messages update
  useEffect(() => {
    chatBottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isAsking]);

  // Delete Conversation Mutation
  const deleteConvMutation = useMutation({
    mutationFn: (convId: string) =>
      selectedClassId !== null ? knowledgeApi.deleteClassConversation(selectedClassId, convId) : Promise.resolve(),
    onSuccess: (_, convId) => {
      queryClient.setQueryData<ClassConversation[]>(
        ['class-conversations', selectedClassId],
        (old) => (old ? old.filter((c) => c.id !== convId) : [])
      );

      queryClient.removeQueries({
        queryKey: ['class-conversation-messages', selectedClassId, convId],
      });

      if (currentConversationId === convId) {
        const remaining = (conversations || []).filter((c) => c.id !== convId);
        if (remaining.length > 0) {
          setCurrentConversationId(remaining[0].id);
          setIsNewChatMode(false);
        } else {
          setCurrentConversationId(null);
          setIsNewChatMode(true);
        }
      }

      queryClient.invalidateQueries({ queryKey: ['class-conversations', selectedClassId] });

      showToast('Đã xóa phiên trò chuyện.', 'success');
      setDeletingConv(null);
    },
    onError: (err) => showToast(err instanceof Error ? err.message : 'Xóa thất bại.', 'error'),
  });

  // Rename Conversation Mutation
  const renameConvMutation = useMutation({
    mutationFn: ({ convId, title }: { convId: string; title: string }) =>
      selectedClassId !== null
        ? knowledgeApi.updateClassConversationTitle(selectedClassId, convId, title)
        : Promise.resolve({} as ClassConversation),
    onSuccess: (updated) => {
      queryClient.setQueryData<ClassConversation[]>(
        ['class-conversations', selectedClassId],
        (old) => (old ? old.map((c) => (c.id === updated.id ? updated : c)) : [])
      );
      queryClient.invalidateQueries({ queryKey: ['class-conversations', selectedClassId] });
      showToast('Đã cập nhật tiêu đề.', 'success');
      setEditingConv(null);
    },
    onError: (err) => showToast(err instanceof Error ? err.message : 'Đổi tên thất bại.', 'error'),
  });

  // Create New Empty Conversation
  const handleStartNewChat = () => {
    setIsNewChatMode(true);
    setCurrentConversationId(null);
    setInputQuestion('');
  };

  // Handle Send Question
  const handleSend = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    const q = inputQuestion.trim();
    if (!q || selectedClassId === null || isAsking) return;

    setInputQuestion('');
    setIsAsking(true);

    try {
      const res = await knowledgeApi.chatClass(selectedClassId, {
        question: q,
        selectedDocumentIds: selectedDocIds.length > 0 ? selectedDocIds : undefined,
        conversationId: currentConversationId,
      });

      // Update active conversation ID if it was newly created
      if (res.conversationId && res.conversationId !== currentConversationId) {
        setCurrentConversationId(res.conversationId);
        setIsNewChatMode(false);
      }

      await queryClient.invalidateQueries({ queryKey: ['class-conversations', selectedClassId] });
      await queryClient.invalidateQueries({
        queryKey: ['class-conversation-messages', selectedClassId, res.conversationId || currentConversationId],
      });
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Không thể kết nối đến Trợ lý AI.', 'error');
    } finally {
      setIsAsking(false);
    }
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

  const currentConv = conversations.find((c) => c.id === currentConversationId);

  return (
    <div className="p-4 md:p-6 max-w-7xl mx-auto space-y-4 flex flex-col h-[calc(100vh-4rem)]">
      {/* Top Header Card & Scope/Class Selector */}
      <div className="bg-white p-4 rounded border border-slate-200 shadow-xs flex flex-col lg:flex-row justify-between items-start lg:items-center gap-4 shrink-0">
        <div className="min-w-0 flex-1">
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
              ? 'Tra cứu trên toàn bộ tài liệu, giáo trình và quy chế của trung tâm.'
              : 'Tra cứu trên toàn bộ giáo trình, kế hoạch bài dạy và mục tiêu học tập thuộc phạm vi lớp học.'}
          </p>
        </div>

        {/* Scope / Class Selector Dropdown & Actions */}
        <div className="flex items-center gap-2.5 shrink-0 w-full lg:w-auto justify-start lg:justify-end">
          <div className="flex items-center gap-1.5 shrink-0">
            <label className="text-xs font-semibold text-slate-600 whitespace-nowrap mb-0">
              {isAdmin ? 'Phạm vi tra cứu:' : 'Lớp phụ trách:'}
            </label>
            <select
              value={selectedClassId === null ? '' : selectedClassId}
              onChange={(e) => setSelectedClassId(Number(e.target.value))}
              disabled={isClassesLoading && !isAdmin}
              className="w-auto min-w-[170px] max-w-[230px] px-2.5 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white focus:outline-none shadow-xs truncate"
            >
              {isAdmin && (
                <option value="0">
                  Toàn bộ Trung tâm
                </option>
              )}
              {classes.map((cls) => (
                <option key={cls.id} value={cls.id}>
                  {cls.maLop} - {cls.tenLop}
                </option>
              ))}
            </select>
          </div>

          <button
            type="button"
            onClick={() => setIsCloModalOpen(true)}
            disabled={!classKnowledge || classKnowledge.documents.length === 0}
            className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300 text-white rounded text-xs font-semibold flex items-center gap-1.5 shadow-xs shrink-0 whitespace-nowrap transition-colors cursor-pointer disabled:cursor-not-allowed"
            title="Xuất Ma trận Chuẩn đầu ra CLO & Rubric đánh giá ra file Excel (.xlsx)"
          >
            <MaterialIcon name="assignment" className="text-sm" />
            <span>Xuất CLO & Rubric (.xlsx)</span>
          </button>
        </div>
      </div>

      {/* Main Two-Column Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 flex-1 min-h-0">
        {/* LEFT SIDEBAR: Conversations & Documents (4 cols on lg, 3 on xl) */}
        <div className="lg:col-span-4 xl:col-span-3 bg-white rounded border border-slate-200 shadow-xs flex flex-col min-h-0 overflow-hidden">
          {/* Tab Switcher: Conversations vs Documents */}
          <div className="flex border-b border-slate-200 bg-slate-50 shrink-0">
            <button
              type="button"
              onClick={() => setActiveLeftTab('conversations')}
              className={`flex-1 py-2.5 px-3 text-xs font-bold flex items-center justify-center gap-1.5 border-b-2 transition-all ${
                activeLeftTab === 'conversations'
                  ? 'border-[#0284c7] text-[#0284c7] bg-white'
                  : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100/70'
              }`}
            >
              <MaterialIcon name="chat" className="text-sm" />
              <span>Phiên chat ({conversations.length})</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveLeftTab('docs')}
              className={`flex-1 py-2.5 px-3 text-xs font-bold flex items-center justify-center gap-1.5 border-b-2 transition-all ${
                activeLeftTab === 'docs'
                  ? 'border-[#0284c7] text-[#0284c7] bg-white'
                  : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100/70'
              }`}
            >
              <MaterialIcon name="description" className="text-sm" />
              <span>Học liệu ({classKnowledge?.documents.length || 0})</span>
            </button>
          </div>

          {/* TAB 1: CONVERSATIONS LIST */}
          {activeLeftTab === 'conversations' && (
            <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
              {/* New Chat Button */}
              <div className="p-3 border-b border-slate-100 bg-white shrink-0">
                <button
                  type="button"
                  onClick={handleStartNewChat}
                  disabled={!canChat || selectedClassId === null}
                  className="w-full py-2 px-3 bg-[#0284c7] hover:bg-sky-700 text-white rounded text-xs font-semibold flex items-center justify-center gap-1.5 shadow-2xs transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <MaterialIcon name="add" className="text-sm" />
                  <span>Cuộc trò chuyện mới</span>
                </button>
              </div>

              {/* Conversations Stream */}
              <div className="flex-1 overflow-y-auto p-2 space-y-1">
                {isConversationsLoading ? (
                  <div className="text-center py-8 text-xs text-slate-400">Đang tải danh sách...</div>
                ) : conversations.length === 0 ? (
                  <div className="text-center py-8 px-3 text-slate-400">
                    <MaterialIcon name="chat_bubble_outline" className="text-3xl text-slate-300 mx-auto mb-2" />
                    <p className="text-xs font-medium text-slate-600">Chưa có phiên trò chuyện nào</p>
                    <p className="text-[11px] text-slate-400 mt-1">
                      Bấm &quot;Cuộc trò chuyện mới&quot; hoặc gõ câu hỏi để bắt đầu.
                    </p>
                  </div>
                ) : (
                  conversations.map((c) => {
                    const isActive = c.id === currentConversationId;
                    return (
                      <div
                        key={c.id}
                        onClick={() => {
                          setIsNewChatMode(false);
                          setCurrentConversationId(c.id);
                        }}
                        className={`group relative p-2.5 rounded border text-xs cursor-pointer transition-all flex items-start justify-between gap-2 ${
                          isActive
                            ? 'border-[#0284c7] bg-sky-50/50 shadow-2xs'
                            : 'border-slate-100 hover:border-slate-300 hover:bg-slate-50'
                        }`}
                      >
                        <div className="flex-1 min-w-0 pr-1">
                          <p
                            className={`font-semibold truncate text-[12px] ${
                              isActive ? 'text-[#0284c7]' : 'text-slate-800'
                            }`}
                            title={c.title}
                          >
                            {c.title}
                          </p>
                          <p className="text-[10px] text-slate-400 mt-1 flex items-center gap-1.5">
                            <span>{c.messageCount} tin nhắn</span>
                            <span>•</span>
                            <span>
                              {new Date(c.updatedAt).toLocaleTimeString('vi-VN', {
                                hour: '2-digit',
                                minute: '2-digit',
                              })}
                            </span>
                          </p>
                        </div>

                        {/* Actions: Rename & Delete */}
                        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
                          <button
                            type="button"
                            disabled={isAsking}
                            onClick={(e) => {
                              e.stopPropagation();
                              if (isAsking) return;
                              setEditingConv(c);
                              setEditTitleInput(c.title);
                            }}
                            className="p-1 text-slate-400 hover:text-slate-700 hover:bg-white rounded transition-colors disabled:opacity-30 disabled:cursor-not-allowed"
                            title="Đổi tên phiên"
                          >
                            <MaterialIcon name="edit" className="text-xs" />
                          </button>
                          <button
                            type="button"
                            disabled={isAsking}
                            onClick={(e) => {
                              e.stopPropagation();
                              if (isAsking) return;
                              setDeletingConv(c);
                            }}
                            className="p-1 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded transition-colors disabled:opacity-30 disabled:cursor-not-allowed"
                            title="Xóa phiên này"
                          >
                            <MaterialIcon name="delete" className="text-xs" />
                          </button>
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </div>
          )}

          {/* TAB 2: DOCUMENTS SELECTION */}
          {activeLeftTab === 'docs' && (
            <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
              <div className="p-2.5 border-b border-slate-100 bg-slate-50 flex items-center justify-between shrink-0">
                <span className="text-[11px] font-semibold text-slate-600">Áp dụng trong phạm vi</span>
                {classKnowledge && classKnowledge.documents.length > 0 && (
                  <button
                    type="button"
                    onClick={toggleSelectAllDocs}
                    className="text-[11px] text-[#0284c7] hover:underline font-medium"
                  >
                    {selectedDocIds.length === classKnowledge.documents.length ? 'Bỏ chọn' : 'Chọn tất cả'}
                  </button>
                )}
              </div>

              <div className="flex-1 overflow-y-auto p-2.5 space-y-2">
                {isKnowledgeLoading ? (
                  <div className="text-center py-8 text-xs text-slate-400">Đang nạp học liệu...</div>
                ) : !classKnowledge || classKnowledge.documents.length === 0 ? (
                  <div className="text-center py-8 px-3">
                    <h4 className="font-semibold text-slate-700 text-xs">Chưa có tài liệu nào sẵn sàng.</h4>
                    <p className="text-[11px] text-slate-400 mt-1">
                      Admin có thể tải lên tài liệu trong phần Quản lý Chương trình học.
                    </p>
                  </div>
                ) : (
                  classKnowledge.documents.map((doc) => {
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
            <div className="flex items-center gap-2 min-w-0">
              <span className={`w-2.5 h-2.5 rounded-full shrink-0 ${selectedClassId === 0 ? 'bg-sky-500' : 'bg-emerald-500'}`}></span>
              <div className="min-w-0">
                <span className="text-sm font-bold text-slate-800 truncate block">
                  {currentConv ? currentConv.title : 'Cuộc trò chuyện mới'}
                </span>
                <span className="text-[11px] text-slate-500 truncate block">
                  {selectedClassId === 0
                    ? 'Trợ lý Tri thức Toàn Trung tâm'
                    : classKnowledge
                    ? `${classKnowledge.className} (${classKnowledge.classCode}) • ${classKnowledge.courseName}`
                    : 'Chọn lớp'}
                </span>
              </div>
            </div>

            <div className="flex items-center gap-2 shrink-0">
              <span className="text-xs text-slate-600 bg-white px-2.5 py-1 rounded border border-slate-200 shadow-2xs">
                {selectedDocIds.length} tài liệu áp dụng
              </span>
              {currentConversationId && canChat && (
                <button
                  type="button"
                  onClick={handleStartNewChat}
                  className="px-2.5 py-1 text-xs text-[#0284c7] hover:bg-sky-50 border border-sky-200 rounded transition-colors flex items-center gap-1 shadow-2xs font-medium"
                >
                  <MaterialIcon name="add" className="text-sm" />
                  <span>Chat mới</span>
                </button>
              )}
            </div>
          </div>

          {/* Messages Stream */}
          <div className="flex-1 overflow-y-auto p-4 md:p-6 space-y-4">
            {isMessagesLoading ? (
              <div className="h-full flex items-center justify-center text-xs text-slate-400">
                Đang tải tin nhắn...
              </div>
            ) : messages.length === 0 ? (
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
                      ? 'Admin có thể đặt bất kỳ câu hỏi nào trên toàn bộ tài liệu, giáo trình và quy chế của trung tâm.'
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
                      {/* Assistant Header */}
                      {!isUser && (
                        <div className="flex items-center justify-between gap-2 mb-2 pb-1.5 border-b border-slate-200/70">
                          <span className="text-[11px] font-bold text-slate-700">
                            {selectedClassId === 0 ? 'Trợ lý Trung tâm' : 'Trợ lý Học liệu'}
                          </span>
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
                      ? 'Trợ lý đang tra cứu toàn bộ kho tri thức...'
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

      {/* CLO & Rubric Export Modal */}
      {isCloModalOpen && classKnowledge && (
        <CloRubricModal
          isOpen={isCloModalOpen}
          onClose={() => setIsCloModalOpen(false)}
          documents={classKnowledge.documents}
        />
      )}

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
          title="Xóa phiên trò chuyện"
          message={`Bạn có chắc chắn muốn xóa phiên "${deletingConv.title}"? Toàn bộ tin nhắn trong phiên này sẽ bị xóa và không thể hoàn tác.`}
          confirmText="Xóa phiên"
          isDangerous={true}
          isPending={deleteConvMutation.isPending}
          onConfirm={() => deleteConvMutation.mutate(deletingConv.id)}
          onCancel={() => setDeletingConv(null)}
        />
      )}

      {/* Rename Conversation Modal */}
      {editingConv && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-sm w-full p-5 space-y-4">
            <h3 className="text-sm font-bold text-slate-800">Đổi tên phiên trò chuyện</h3>
            <input
              type="text"
              value={editTitleInput}
              onChange={(e) => setEditTitleInput(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded focus:border-[#0284c7] outline-none"
              placeholder="Nhập tiêu đề mới..."
              autoFocus
            />
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setEditingConv(null)}
                className="px-3 py-1.5 text-xs text-slate-600 hover:bg-slate-100 rounded"
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={!editTitleInput.trim() || renameConvMutation.isPending}
                onClick={() =>
                  renameConvMutation.mutate({ convId: editingConv.id, title: editTitleInput.trim() })
                }
                className="px-3 py-1.5 text-xs bg-[#0284c7] hover:bg-sky-700 text-white rounded font-medium disabled:opacity-50"
              >
                Lưu
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
