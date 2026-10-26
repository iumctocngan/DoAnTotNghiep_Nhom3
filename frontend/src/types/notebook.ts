export interface NotebookSummary {
  id: string;
  title: string;
  description?: string | null;
  createdAt: string;
  updatedAt: string;
  documentCount: number;
}

export interface DocumentItem {
  id: string;
  notebookId: string;
  fileName: string;
  fileSize: number;
  documentType: string;
  status: 'processing' | 'ready' | 'failed';
  errorMessage?: string | null;
  createdAt: string;
}

export interface NotebookDetail {
  id: string;
  title: string;
  description?: string | null;
  createdAt: string;
  updatedAt: string;
  documents: DocumentItem[];
}

export interface CreateNotebookRequest {
  title: string;
  description?: string | null;
}

export type UpdateNotebookRequest = CreateNotebookRequest;

export interface Citation {
  chunkId: string;
  documentId: string;
  documentName: string;
  label: string;
  pageNumber?: number | null;
  sectionTitle?: string | null;
  score?: number | null;
  imageUrl?: string | null;
}

export interface ChatRequest {
  question: string;
  documentIds?: string[];
}

export interface ChatResponse {
  answer: string;
  citations: Citation[];
}

export interface CloRubricRequest {
  notebookId: string;
  documentId: string;
  outcomeCount?: number;
  startPage?: number;
  endPage?: number;
}

export interface DocumentOutlineItem {
  title: string;
  startPage: number;
  endPage: number;
}

