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

export const KnowledgeScope = {
  Global: 1,
  Course: 2,
  Level: 3,
  Class: 4,
} as const;

export type KnowledgeScope = typeof KnowledgeScope[keyof typeof KnowledgeScope];

export interface KnowledgeDocument {
  id: string;
  title: string;
  fileName: string;
  fileSize: number;
  documentType: 'pdf' | 'docx';
  status: 'processing' | 'ready' | 'failed';
  errorMessage?: string | null;
  scope: KnowledgeScope;
  scopeName: string;
  courseId?: number | null;
  courseName?: string | null;
  levelId?: number | null;
  levelName?: string | null;
  classId?: number | null;
  className?: string | null;
  uploadedByUserId?: string | null;
  createdAt: string;
}

export interface ClassKnowledgeInfo {
  classId: number;
  className: string;
  classCode: string;
  levelName: string;
  courseName: string;
  documents: KnowledgeDocument[];
}

export interface ClassMessage {
  id: string;
  classId?: number | null;
  role: 'user' | 'assistant';
  content: string;
  citations: Citation[];
  createdAt: string;
}

export interface ClassChatResult {
  question: string;
  answer: string;
  citations: Citation[];
  createdAt: string;
}

export interface ChatClassRequest {
  question: string;
  selectedDocumentIds?: string[];
}

export interface UploadKnowledgeDocumentRequest {
  title?: string;
  scope: KnowledgeScope;
  courseId?: number;
  levelId?: number;
  classId?: number;
  file: File;
}
