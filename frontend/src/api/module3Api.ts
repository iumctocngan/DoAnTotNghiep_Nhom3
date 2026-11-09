import { apiClient } from './apiClient';
import type {
  AtRiskStudentResponse,
  CskhChatMessageResponse,
  CskhChatSessionResponse,
  CreateCskhInteractionRequest,
  RenewalPredictionResponse,
  StudentContextResponse,
} from '../types/module3';

export const module3Api = {
  getRetentionQueue: () =>
    apiClient<AtRiskStudentResponse[]>('/api/ai/dashboard/retention-queue'),

  getStudentContext: (studentId: number) =>
    apiClient<StudentContextResponse>(`/api/ai/cskh/student-context/${studentId}`),

  getStudentPrediction: (studentId: number) =>
    apiClient<RenewalPredictionResponse>(`/api/ai/predictions/students/${studentId}`),

  createChatSession: (studentId: number) =>
    apiClient<CskhChatSessionResponse>('/api/ai/cskh-chat/sessions', {
      method: 'POST',
      body: JSON.stringify({ studentId }),
    }),

  sendChatMessage: (sessionId: string, content: string) =>
    apiClient<CskhChatMessageResponse>(`/api/ai/cskh-chat/sessions/${sessionId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ content }),
    }),

  getChatHistory: (sessionId: string) =>
    apiClient<CskhChatMessageResponse[]>(`/api/ai/cskh-chat/sessions/${sessionId}/history`),

  saveInteraction: (request: CreateCskhInteractionRequest) =>
    apiClient<void>('/api/cskh/interactions', {
      method: 'POST',
      body: JSON.stringify(request),
    }),
};
