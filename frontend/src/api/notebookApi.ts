import { apiClient, getAccessToken } from './apiClient';
import type {
  ChatRequest,
  ChatResponse,
  CloRubricRequest,
  CreateNotebookRequest,
  DocumentItem,
  DocumentOutlineItem,
  NotebookDetail,
  NotebookSummary,
  UpdateNotebookRequest,
} from '../types/notebook';

export const notebookApi = {
  getNotebooks: (): Promise<NotebookSummary[]> =>
    apiClient<NotebookSummary[]>('/api/notebooks'),

  getNotebook: (id: string): Promise<NotebookDetail> =>
    apiClient<NotebookDetail>(`/api/notebooks/${id}`),

  createNotebook: (data: CreateNotebookRequest): Promise<NotebookDetail> =>
    apiClient<NotebookDetail>('/api/notebooks', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  updateNotebook: (id: string, data: UpdateNotebookRequest): Promise<NotebookDetail> =>
    apiClient<NotebookDetail>(`/api/notebooks/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  deleteNotebook: (id: string): Promise<void> =>
    apiClient<void>(`/api/notebooks/${id}`, {
      method: 'DELETE',
    }),

  uploadDocument: (notebookId: string, file: File): Promise<DocumentItem> => {
    const formData = new FormData();
    formData.append('file', file);
    return apiClient<DocumentItem>(`/api/notebooks/${notebookId}/documents`, {
      method: 'POST',
      body: formData,
    });
  },

  deleteDocument: (notebookId: string, documentId: string): Promise<void> =>
    apiClient<void>(`/api/notebooks/${notebookId}/documents/${documentId}`, {
      method: 'DELETE',
    }),

  chat: (notebookId: string, data: ChatRequest): Promise<ChatResponse> =>
    apiClient<ChatResponse>(`/api/notebooks/${notebookId}/chat`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  generateCloRubric: async (notebookId: string, data: CloRubricRequest): Promise<Blob> => {
    const token = getAccessToken();
    const response = await fetch(`/api/notebooks/${notebookId}/clo-rubric`, {
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

  getDocumentOutline: (notebookId: string, documentId: string): Promise<DocumentOutlineItem[]> =>
    apiClient<DocumentOutlineItem[]>(`/api/notebooks/${notebookId}/documents/${documentId}/outline`),
};

