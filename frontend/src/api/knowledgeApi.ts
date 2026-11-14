import { apiClient, getAccessToken } from './apiClient';
import type {
  ClassChatResult,
  ClassConversation,
  ClassKnowledgeInfo,
  ClassMessage,
  DocumentOutlineItem,
  GenerateCloRubricRequest,
  KnowledgeDocument,
  KnowledgeScope,
} from '../types/knowledge';

export const knowledgeApi = {
  getDocuments: (params?: {
    scopeGroup?: string;
    scope?: KnowledgeScope;
    courseId?: number;
    levelId?: number;
    classId?: number;
  }): Promise<KnowledgeDocument[]> => {
    const query = new URLSearchParams();
    if (params?.scopeGroup) query.append('scopeGroup', params.scopeGroup);
    if (params?.scope) query.append('scope', String(params.scope));
    if (params?.courseId) query.append('courseId', String(params.courseId));
    if (params?.levelId) query.append('levelId', String(params.levelId));
    if (params?.classId) query.append('classId', String(params.classId));
    const qs = query.toString();
    return apiClient<KnowledgeDocument[]>(`/api/knowledge-documents${qs ? `?${qs}` : ''}`);
  },

  uploadDocument: (formData: FormData): Promise<KnowledgeDocument> =>
    apiClient<KnowledgeDocument>('/api/knowledge-documents', {
      method: 'POST',
      body: formData,
    }),

  deleteDocument: (id: string): Promise<void> =>
    apiClient<void>(`/api/knowledge-documents/${id}`, {
      method: 'DELETE',
    }),

  getClassKnowledgeInfo: (classId: number): Promise<ClassKnowledgeInfo> =>
    apiClient<ClassKnowledgeInfo>(`/api/classes/${classId}/knowledge/info`),

  getClassConversations: (classId: number): Promise<ClassConversation[]> =>
    apiClient<ClassConversation[]>(`/api/classes/${classId}/knowledge/conversations`),

  createClassConversation: (classId: number, data?: { title?: string }): Promise<ClassConversation> =>
    apiClient<ClassConversation>(`/api/classes/${classId}/knowledge/conversations`, {
      method: 'POST',
      body: JSON.stringify(data ?? {}),
    }),

  updateClassConversationTitle: (classId: number, conversationId: string, title: string): Promise<ClassConversation> =>
    apiClient<ClassConversation>(`/api/classes/${classId}/knowledge/conversations/${conversationId}`, {
      method: 'PUT',
      body: JSON.stringify({ title }),
    }),

  deleteClassConversation: (classId: number, conversationId: string): Promise<void> =>
    apiClient<void>(`/api/classes/${classId}/knowledge/conversations/${conversationId}`, {
      method: 'DELETE',
    }),

  getClassConversationMessages: (classId: number, conversationId: string): Promise<ClassMessage[]> =>
    apiClient<ClassMessage[]>(`/api/classes/${classId}/knowledge/conversations/${conversationId}/messages`),

  getClassMessages: (classId: number): Promise<ClassMessage[]> =>
    apiClient<ClassMessage[]>(`/api/classes/${classId}/knowledge/messages`),

  clearClassMessages: (classId: number): Promise<void> =>
    apiClient<void>(`/api/classes/${classId}/knowledge/messages`, {
      method: 'DELETE',
    }),

  chatClass: (
    classId: number,
    data: {
      question: string;
      selectedDocumentIds?: string[];
      conversationId?: string | null;
    }
  ): Promise<ClassChatResult> =>
    apiClient<ClassChatResult>(`/api/classes/${classId}/knowledge/chat`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  generateCloRubric: async (
    documentId: string,
    data: GenerateCloRubricRequest
  ): Promise<Blob> => {
    const token = getAccessToken();
    const response = await fetch(`/api/knowledge-documents/${documentId}/clo-rubric`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      throw new Error('Không thể tạo file CLO & Rubric.');
    }
    return await response.blob();
  },

  getDocumentOutline: (documentId: string): Promise<DocumentOutlineItem[]> =>
    apiClient<DocumentOutlineItem[]>(`/api/knowledge-documents/${documentId}/outline`),
};
