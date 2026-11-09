export interface ShapDriverResponse {
  feature: string;
  direction: 'positive' | 'negative';
  impact: number;
  description: string;
}

export interface RenewalPredictionResponse {
  studentId: number;
  enrollmentId: number;
  renewalProbability: number;
  riskLevel: 'HighRisk' | 'MediumRisk' | 'LowRisk';
  daysUntilPeriodEnd: number;
  topDrivers: ShapDriverResponse[];
  modelVersion: string;
  predictionDate: string;
}

export interface AtRiskStudentResponse extends RenewalPredictionResponse {
  studentCode: string;
  studentName: string;
  className: string | null;
}

export interface StudentContextResponse {
  student_id: number;
  student_name: string;
  student_code: string;
  class_name: string | null;
  level_name: string | null;
  teacher_name: string | null;
  primary_guardian_name: string | null;
  primary_guardian_phone: string | null;
  attendance: {
    attendance_rate_last_60d: number | null;
    consecutive_absent_count: number | null;
    recent_5_absences: number | null;
  };
  financial: {
    current_debt: number | null;
    days_until_period_end: number | null;
    due_date: string | null;
    has_overdue: boolean | null;
  };
  assessment: {
    recent_score: number | null;
    score_trend: string | null;
    teacher_note: string | null;
    weak_warning_count: number;
  };
  renewal_prediction: {
    renewal_probability: number | null;
    risk_level: string | null;
    top_drivers: ShapDriverResponse[];
    predicted_at: string | null;
  };
  retrieved_at: string;
  data_is_complete: boolean;
  missing_data_notes: string[];
}

export interface CskhChatSessionResponse {
  id: string;
  studentId: number;
  title: string;
  startedAt: string;
}

export interface CskhChatMessageResponse {
  id: number;
  role: 'User' | 'Assistant';
  content: string;
  suggestedCallScript?: string[] | null;
  sentAt: string;
}

export interface CreateCskhInteractionRequest {
  studentId: number;
  guardianId: number;
  channel: 1 | 2 | 3;
  guardianFeedback: 1 | 2 | 3 | 4 | 5;
  notes: string;
  followUpDate: string | null;
}
