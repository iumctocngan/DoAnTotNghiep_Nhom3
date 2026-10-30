import { apiClient } from './apiClient';
import type {
  ClassChatResult,
  ClassConversation,
  ClassKnowledgeInfo,
  ClassMessage,
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

  getClassConversationMessages: (classId: number, conversationId: string): Promise<ClassMessage[]> =>
    apiClient<ClassMessage[]>(`/api/classes/${classId}/knowledge/conversations/${conversationId}`),

  createClassConversation: (classId: number, title?: string): Promise<ClassConversation> =>
    apiClient<ClassConversation>(`/api/classes/${classId}/knowledge/conversations`, {
      method: 'POST',
      body: JSON.stringify({ title }),
    }),

  deleteClassConversation: (classId: number, conversationId: string): Promise<void> =>
    apiClient<void>(`/api/classes/${classId}/knowledge/conversations/${conversationId}`, {
      method: 'DELETE',
    }),

  chatClass: (
    classId: number,
    data: {
      question: string;
      conversationId?: string;
      selectedDocumentIds?: string[];
    }
  ): Promise<ClassChatResult> =>
    apiClient<ClassChatResult>(`/api/classes/${classId}/knowledge/chat`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),
};
