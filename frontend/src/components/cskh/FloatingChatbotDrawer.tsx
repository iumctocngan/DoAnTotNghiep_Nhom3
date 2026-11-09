import React, { useEffect, useMemo, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocation } from "react-router-dom";
import { module3Api } from "../../api/module3Api";
import { studentApi } from "../../api/studentApi";
import { useToast } from "../common/ToastProvider";
import { MaterialIcon } from "../common/MaterialIcon";
import type { StudentGuardianResponse } from "../../types/student";
import type {
  CskhChatMessageResponse,
  StudentContextResponse,
} from "../../types/module3";

type DrawerTab = "snapshot" | "chat" | "script";
type ContactChannel = 1 | 2 | 3;
type GuardianFeedback = 1 | 2 | 3 | 4 | 5;

function getErrorMessage(error: unknown): string {
  if (typeof error === "object" && error !== null && "message" in error) {
    const message = error.message;
    if (typeof message === "string") return message;
  }
  return "Đã xảy ra lỗi khi kết nối máy chủ.";
}

function formatMissingValue(
  value: number | string | null | undefined,
  suffix = "",
): string {
  return value === null || value === undefined || value === ""
    ? "Chưa có dữ liệu"
    : `${value}${suffix}`;
}

function riskLabel(riskLevel: string | null | undefined): string {
  switch (riskLevel) {
    case "HighRisk":
    case "High":
      return "Nguy cơ cao";
    case "MediumRisk":
    case "Medium":
      return "Nguy cơ trung bình";
    case "LowRisk":
    case "Low":
      return "Nguy cơ thấp";
    default:
      return "Chưa có dữ liệu";
  }
}

function channelFromValue(value: string): ContactChannel {
  if (value === "2") return 2;
  if (value === "3") return 3;
  return 1;
}

function feedbackFromValue(value: string): GuardianFeedback {
  if (value === "2") return 2;
  if (value === "3") return 3;
  if (value === "4") return 4;
  if (value === "5") return 5;
  return 1;
}

export const CskhChatbotPage: React.FC = () => {
  const location = useLocation();
  const queryClient = useQueryClient();
  const showToast = useToast();
  const {
    data: retentionQueue,
    isLoading: isRetentionQueueLoading,
    isError: isRetentionQueueError,
  } = useQuery({
    queryKey: ["ai", "retention-queue"],
    queryFn: module3Api.getRetentionQueue,
    staleTime: 60_000,
  });
  const [studentId, setStudentId] = useState<number | null>(null);
  const [activeTab, setActiveTab] = useState<DrawerTab>("snapshot");
  const [studentSearch, setStudentSearch] = useState("");
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [question, setQuestion] = useState("");
  const [sending, setSending] = useState(false);
  const [chatError, setChatError] = useState<string | null>(null);
  const [latestScript, setLatestScript] =
    useState<CskhChatMessageResponse | null>(null);
  const [selectedGuardianId, setSelectedGuardianId] = useState("");
  const [channel, setChannel] = useState<ContactChannel>(1);
  const [feedback, setFeedback] = useState<GuardianFeedback>(2);
  const [interactionNotes, setInteractionNotes] = useState("");
  const [followUpDate, setFollowUpDate] = useState("");
  const [savingInteraction, setSavingInteraction] = useState(false);

  useEffect(() => {
    const studentPathMatch = location.pathname.match(/^\/students\/(\d+)$/);
    const queryStudentId = new URLSearchParams(location.search).get(
      "studentId",
    );
    const contextualId = studentPathMatch?.[1] ?? queryStudentId;
    const parsedStudentId = Number(contextualId);
    setStudentId(
      contextualId &&
        Number.isSafeInteger(parsedStudentId) &&
        parsedStudentId > 0
        ? parsedStudentId
        : null,
    );
  }, [location.pathname, location.search]);

  useEffect(() => {
    setSessionId(null);
    setLatestScript(null);
    setChatError(null);
  }, [studentId]);

  const searchTerm = studentSearch.trim();
  const {
    data: students,
    isLoading: isSearching,
    error: searchError,
  } = useQuery({
    queryKey: ["students", "chatbot-search", searchTerm],
    queryFn: () =>
      studentApi.getStudents({ search: searchTerm, page: 1, pageSize: 8 }),
    enabled: searchTerm.length >= 2,
  });

  const {
    data: context,
    isLoading: isContextLoading,
    error: contextError,
  } = useQuery({
    queryKey: ["ai", "student-context", studentId],
    queryFn: () => module3Api.getStudentContext(studentId ?? 0),
    enabled: studentId !== null,
  });

  const {
    data: chatHistory,
    isLoading: isHistoryLoading,
    error: historyError,
  } = useQuery({
    queryKey: ["ai", "cskh-chat-history", sessionId],
    queryFn: () => module3Api.getChatHistory(sessionId ?? ""),
    enabled: sessionId !== null,
  });

  const {
    data: guardians,
    isLoading: isGuardiansLoading,
    error: guardiansError,
  } = useQuery({
    queryKey: ["students", studentId, "guardians"],
    queryFn: () => studentApi.getStudentGuardians(studentId ?? 0),
    enabled: activeTab === "script" && studentId !== null,
  });

  useEffect(() => {
    if (selectedGuardianId || !guardians?.length) return;
    setSelectedGuardianId(String(guardians[0].guardianId));
  }, [guardians, selectedGuardianId]);

  const conversation = useMemo(() => chatHistory ?? [], [chatHistory]);

  const sendMessage = async (content: string, isScriptRequest = false) => {
    if (!studentId || !content.trim()) return;

    setSending(true);
    setChatError(null);
    try {
      let activeSessionId = sessionId;
      if (!activeSessionId) {
        const session = await module3Api.createChatSession(studentId);
        activeSessionId = session.id;
        setSessionId(session.id);
      }
      const response = await module3Api.sendChatMessage(
        activeSessionId,
        content.trim(),
      );
      await queryClient.invalidateQueries({
        queryKey: ["ai", "cskh-chat-history", activeSessionId],
      });
      if (isScriptRequest) setLatestScript(response);
      setQuestion("");
    } catch (error: unknown) {
      setChatError(getErrorMessage(error));
    } finally {
      setSending(false);
    }
  };

  const handleSendQuestion = async (event: React.FormEvent) => {
    event.preventDefault();
    await sendMessage(question);
  };

  const handleGenerateScript = async () => {
    setActiveTab("script");
    await sendMessage(
      "Hãy tạo kịch bản cuộc gọi tư vấn 4 chặng dựa trên dữ liệu học viên đã được xác thực.",
      true,
    );
  };

  const handleCopyScript = async () => {
    if (!latestScript) return;
    const script =
      latestScript.suggestedCallScript?.join("\n\n") || latestScript.content;
    try {
      await navigator.clipboard.writeText(script);
      showToast("Đã sao chép kịch bản cuộc gọi.", "success");
    } catch {
      showToast(
        "Không thể sao chép kịch bản. Hãy kiểm tra quyền truy cập clipboard của trình duyệt.",
        "error",
      );
    }
  };

  const handleSaveInteraction = async (event: React.FormEvent) => {
    event.preventDefault();
    const guardianId = Number(selectedGuardianId);
    if (
      !studentId ||
      !Number.isSafeInteger(guardianId) ||
      guardianId <= 0 ||
      !interactionNotes.trim()
    )
      return;

    setSavingInteraction(true);
    try {
      await module3Api.saveInteraction({
        studentId,
        guardianId,
        channel,
        guardianFeedback: feedback,
        notes: interactionNotes.trim(),
        followUpDate: followUpDate || null,
      });
      showToast("Đã lưu nhật ký chăm sóc học viên.", "success");
      setInteractionNotes("");
      setFollowUpDate("");
      await queryClient.invalidateQueries({
        queryKey: ["ai", "retention-queue"],
      });
    } catch (error: unknown) {
      showToast(
        `Không lưu được nhật ký CSKH: ${getErrorMessage(error)}`,
        "error",
      );
    } finally {
      setSavingInteraction(false);
    }
  };

  return (
    <section
      aria-labelledby="cskh-chatbot-title"
      className="mx-auto flex h-[min(760px,calc(100vh-13rem))] min-h-[540px] max-w-6xl flex-col overflow-hidden rounded border border-slate-200 bg-white shadow-sm"
    >
      <header className="flex items-center gap-2 bg-sky-800 px-4 py-3 text-white">
        <div className="flex min-w-0 flex-1 items-center gap-2">
          <MaterialIcon name="school" />
          <div className="min-w-0">
            <h1
              id="cskh-chatbot-title"
              className="truncate text-lg font-semibold"
            >
              Chatbot CSKH
            </h1>
            <p className="text-xs text-sky-100">
              Thông tin dựa trên dữ liệu đã ghi nhận
            </p>
          </div>
        </div>
        <span
          className="shrink-0 rounded border border-white/30 px-2 py-1 text-xs text-white"
          aria-live="polite"
        >
          {isRetentionQueueError
            ? "Không tải được cảnh báo"
            : isRetentionQueueLoading
              ? "Đang tải cảnh báo..."
              : `${retentionQueue?.filter((student) => student.riskLevel === "HighRisk").length ?? 0} học viên nguy cơ cao`}
        </span>
      </header>

      <div className="border-b border-slate-200 p-3">
        <label
          htmlFor="chatbot-student-search"
          className="mb-1 block text-xs font-medium text-slate-600"
        >
          Tra cứu học viên
        </label>
        <input
          id="chatbot-student-search"
          className="w-full"
          type="search"
          placeholder="Nhập tên hoặc mã học viên (từ 2 ký tự)"
          value={studentSearch}
          onChange={(event) => setStudentSearch(event.target.value)}
        />
        {isSearching && (
          <p className="mt-2 text-xs text-slate-500">Đang tìm học viên...</p>
        )}
        {searchError && (
          <p role="alert" className="mt-2 text-xs text-red-700">
            {getErrorMessage(searchError)}
          </p>
        )}
        {students && students.items.length > 0 && (
          <ul className="mt-2 max-h-32 overflow-y-auto rounded border border-slate-200">
            {students.items.map((student) => (
              <li key={student.id}>
                <button
                  type="button"
                  className={`w-full px-3 py-2 text-left text-sm hover:bg-sky-50 ${studentId === student.id ? "bg-sky-50" : ""}`}
                  onClick={() => {
                    setStudentId(student.id);
                    setStudentSearch("");
                  }}
                >
                  <span className="font-medium">{student.fullName}</span>
                  <span className="ml-2 font-mono text-xs text-slate-500">
                    {student.studentCode}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
        {students && students.items.length === 0 && (
          <p className="mt-2 text-xs text-slate-500">
            Không tìm thấy học viên.
          </p>
        )}
      </div>

      <nav
        className="grid grid-cols-3 border-b border-slate-200"
        aria-label="Nội dung trợ lý"
      >
        {(
          [
            ["snapshot", "Hồ sơ"],
            ["chat", "Hỏi đáp"],
            ["script", "Kịch bản"],
          ] as const
        ).map(([tab, label]) => (
          <button
            key={tab}
            type="button"
            role="tab"
            aria-selected={activeTab === tab}
            className={`border-b-2 px-2 py-2 text-sm font-medium ${activeTab === tab ? "border-sky-700 text-sky-800" : "border-transparent text-slate-500 hover:text-slate-800"}`}
            onClick={() => setActiveTab(tab)}
          >
            {label}
          </button>
        ))}
      </nav>

      <div className="min-h-0 flex-1 overflow-y-auto p-4">
        {!studentId ? (
          <div className="rounded-lg border border-dashed border-slate-300 p-6 text-center text-sm text-slate-500">
            Chọn học viên để xem hồ sơ và bắt đầu tư vấn.
          </div>
        ) : activeTab === "snapshot" ? (
          <StudentContextCard
            context={context}
            isLoading={isContextLoading}
            error={contextError}
          />
        ) : activeTab === "chat" ? (
          <div className="flex h-full flex-col gap-3">
            <p className="rounded bg-amber-50 p-2 text-xs text-amber-800">
              Trợ lý chỉ trả lời dựa trên hồ sơ được cung cấp; dữ liệu thiếu sẽ
              được ghi rõ là chưa có.
            </p>
            {historyError && (
              <p role="alert" className="text-xs text-red-700">
                {getErrorMessage(historyError)}
              </p>
            )}
            {isHistoryLoading && (
              <p className="text-sm text-slate-500">Đang tải hội thoại...</p>
            )}
            <div className="flex-1 space-y-3">
              {conversation.map((message) => (
                <div
                  key={message.id}
                  className={`max-w-[90%] whitespace-pre-wrap rounded-lg p-3 text-sm ${message.role === "User" ? "ml-auto bg-sky-100 text-sky-950" : "bg-slate-100 text-slate-800"}`}
                >
                  {message.content}
                </div>
              ))}
              {conversation.length === 0 && !isHistoryLoading && (
                <p className="text-sm text-slate-500">
                  Chưa có tin nhắn. Hãy hỏi về tình hình học tập của học viên.
                </p>
              )}
            </div>
            {chatError && (
              <p
                role="alert"
                className="rounded bg-red-50 p-2 text-xs text-red-700"
              >
                {chatError}
              </p>
            )}
            <form
              onSubmit={(event) => {
                void handleSendQuestion(event);
              }}
              className="flex items-end gap-2 border-t border-slate-200 pt-3"
            >
              <textarea
                className="min-h-10 flex-1 resize-y"
                rows={2}
                maxLength={1000}
                placeholder="Nhập câu hỏi về học viên..."
                value={question}
                onChange={(event) => setQuestion(event.target.value)}
                disabled={sending}
              />
              <button
                type="submit"
                className="btn btn-primary px-3"
                disabled={sending || !question.trim()}
              >
                {sending ? "Đang gửi..." : "Gửi"}
              </button>
            </form>
          </div>
        ) : (
          <div className="space-y-4">
            <button
              type="button"
              className="btn btn-primary w-full"
              onClick={() => {
                void handleGenerateScript();
              }}
              disabled={sending}
            >
              {sending ? "Đang tạo kịch bản..." : "Tạo kịch bản cuộc gọi"}
            </button>
            {chatError && (
              <p
                role="alert"
                className="rounded bg-red-50 p-2 text-xs text-red-700"
              >
                {chatError}
              </p>
            )}
            {latestScript && (
              <div className="space-y-3 rounded-lg border border-slate-200 p-3">
                {latestScript.suggestedCallScript?.length ? (
                  <ol className="list-inside list-decimal space-y-2 text-sm text-slate-800">
                    {latestScript.suggestedCallScript.map((step, index) => (
                      <li key={`${index}-${step}`}>{step}</li>
                    ))}
                  </ol>
                ) : (
                  <p className="whitespace-pre-wrap text-sm text-slate-800">
                    {latestScript.content}
                  </p>
                )}
                <button
                  type="button"
                  className="btn btn-secondary text-sm"
                  onClick={() => {
                    void handleCopyScript();
                  }}
                >
                  Sao chép kịch bản
                </button>
              </div>
            )}
            <QuickInteractionForm
              guardians={guardians}
              isLoading={isGuardiansLoading}
              error={guardiansError}
              selectedGuardianId={selectedGuardianId}
              onGuardianChange={setSelectedGuardianId}
              channel={channel}
              onChannelChange={setChannel}
              feedback={feedback}
              onFeedbackChange={setFeedback}
              notes={interactionNotes}
              onNotesChange={setInteractionNotes}
              followUpDate={followUpDate}
              onFollowUpDateChange={setFollowUpDate}
              isSaving={savingInteraction}
              onSubmit={(event) => {
                void handleSaveInteraction(event);
              }}
            />
          </div>
        )}
      </div>
    </section>
  );
};

const StudentContextCard: React.FC<{
  context?: StudentContextResponse;
  isLoading: boolean;
  error: unknown;
}> = ({ context, isLoading, error }) => {
  if (isLoading)
    return <p className="text-sm text-slate-500">Đang tải hồ sơ học viên...</p>;
  if (error) {
    return (
      <p
        role="alert"
        className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900"
      >
        Chưa thể tải hồ sơ AI. API Module 3 có thể chưa sẵn sàng:{" "}
        {getErrorMessage(error)}
      </p>
    );
  }
  if (!context)
    return (
      <p className="text-sm text-slate-500">Chưa có dữ liệu hồ sơ học viên.</p>
    );

  const attendance = context.attendance.attendance_rate_last_60d;
  const probability = context.renewal_prediction.renewal_probability;

  return (
    <div className="space-y-3">
      {!context.data_is_complete && context.missing_data_notes.length > 0 && (
        <div className="rounded border border-slate-300 bg-slate-50 p-3 text-xs text-slate-700">
          Thiếu dữ liệu: {context.missing_data_notes.join("; ")}
        </div>
      )}
      <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
        <h3 className="font-semibold text-slate-900">{context.student_name}</h3>
        <p className="text-xs text-slate-500">
          {context.student_code} · {formatMissingValue(context.class_name)}
        </p>
        <dl className="mt-3 grid grid-cols-2 gap-3 text-xs">
          <ContextMetric
            label="Chuyên cần 60 ngày"
            value={
              attendance === null
                ? "Chưa có dữ liệu"
                : `${(attendance * 100).toLocaleString("vi-VN", { maximumFractionDigits: 1 })}%`
            }
          />
          <ContextMetric
            label="Điểm Module 1"
            value={formatMissingValue(context.assessment.recent_score, "/10")}
          />
          <ContextMetric
            label="Công nợ"
            value={
              context.financial.current_debt === null
                ? "Chưa có dữ liệu"
                : `${context.financial.current_debt.toLocaleString("vi-VN")} ₫`
            }
          />
          <ContextMetric
            label="Nguy cơ AI"
            value={riskLabel(context.renewal_prediction.risk_level)}
          />
          <ContextMetric
            label="Xác suất tái phí"
            value={
              probability === null
                ? "Chưa có dữ liệu"
                : `${(probability * 100).toLocaleString("vi-VN", { maximumFractionDigits: 1 })}%`
            }
          />
          <ContextMetric
            label="Ngày đến hạn kỳ"
            value={formatMissingValue(
              context.financial.days_until_period_end,
              " ngày",
            )}
          />
        </dl>
      </div>
      <div>
        <h4 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-600">
          Top SHAP
        </h4>
        {context.renewal_prediction.top_drivers.length ? (
          <div className="flex flex-wrap gap-2">
            {context.renewal_prediction.top_drivers
              .slice(0, 3)
              .map((driver) => (
                <span
                  key={`${driver.feature}-${driver.description}`}
                  title={driver.feature}
                  className={`rounded-full px-2.5 py-1 text-xs ${driver.direction === "negative" ? "bg-red-100 text-red-800" : driver.direction === "positive" ? "bg-emerald-100 text-emerald-800" : "bg-slate-100 text-slate-700"}`}
                >
                  {driver.description}
                </span>
              ))}
          </div>
        ) : (
          <p className="text-sm text-slate-500">Chưa có dữ liệu SHAP.</p>
        )}
      </div>
      {context.assessment.teacher_note && (
        <p className="rounded bg-sky-50 p-3 text-xs text-sky-900">
          Nhận xét giáo viên: {context.assessment.teacher_note}
        </p>
      )}
    </div>
  );
};

const ContextMetric: React.FC<{ label: string; value: string }> = ({
  label,
  value,
}) => (
  <div>
    <dt className="text-slate-500">{label}</dt>
    <dd className="mt-0.5 font-semibold text-slate-800">{value}</dd>
  </div>
);

const QuickInteractionForm: React.FC<{
  guardians?: StudentGuardianResponse[];
  isLoading: boolean;
  error: unknown;
  selectedGuardianId: string;
  onGuardianChange: (value: string) => void;
  channel: ContactChannel;
  onChannelChange: (value: ContactChannel) => void;
  feedback: GuardianFeedback;
  onFeedbackChange: (value: GuardianFeedback) => void;
  notes: string;
  onNotesChange: (value: string) => void;
  followUpDate: string;
  onFollowUpDateChange: (value: string) => void;
  isSaving: boolean;
  onSubmit: (event: React.FormEvent) => void;
}> = ({
  guardians,
  isLoading,
  error,
  selectedGuardianId,
  onGuardianChange,
  channel,
  onChannelChange,
  feedback,
  onFeedbackChange,
  notes,
  onNotesChange,
  followUpDate,
  onFollowUpDateChange,
  isSaving,
  onSubmit,
}) => (
  <form
    onSubmit={onSubmit}
    className="space-y-3 rounded-lg border border-slate-200 p-3"
  >
    <h3 className="text-sm font-semibold text-slate-800">Lưu tương tác CSKH</h3>
    {isLoading && (
      <p className="text-xs text-slate-500">Đang tải người giám hộ...</p>
    )}
    {error && (
      <p role="alert" className="text-xs text-red-700">
        {getErrorMessage(error)}
      </p>
    )}
    {!isLoading && guardians?.length === 0 && (
      <p className="text-xs text-amber-800">
        Học viên chưa có người giám hộ để gắn nhật ký.
      </p>
    )}
    <label className="block text-xs font-medium text-slate-600">
      Người giám hộ
      <select
        className="mt-1 w-full"
        value={selectedGuardianId}
        onChange={(event) => onGuardianChange(event.target.value)}
        required
      >
        {(guardians ?? []).map((guardian) => (
          <option key={guardian.guardianId} value={guardian.guardianId}>
            {guardian.fullName}
          </option>
        ))}
      </select>
    </label>
    <div className="grid grid-cols-2 gap-2">
      <label className="text-xs font-medium text-slate-600">
        Kênh liên hệ
        <select
          className="mt-1 w-full"
          value={channel}
          onChange={(event) =>
            onChannelChange(channelFromValue(event.target.value))
          }
        >
          <option value={1}>Điện thoại</option>
          <option value={2}>Zalo</option>
          <option value={3}>Gặp trực tiếp</option>
        </select>
      </label>
      <label className="text-xs font-medium text-slate-600">
        Phản hồi
        <select
          className="mt-1 w-full"
          value={feedback}
          onChange={(event) =>
            onFeedbackChange(feedbackFromValue(event.target.value))
          }
        >
          <option value={1}>Đồng ý tái phí</option>
          <option value={2}>Đang cân nhắc</option>
          <option value={3}>Khó khăn tài chính</option>
          <option value={4}>Chưa hài lòng</option>
          <option value={5}>Dừng học</option>
        </select>
      </label>
    </div>
    <label className="block text-xs font-medium text-slate-600">
      Ghi chú kết quả
      <textarea
        className="mt-1 w-full"
        rows={3}
        maxLength={1000}
        value={notes}
        onChange={(event) => onNotesChange(event.target.value)}
        required
      />
    </label>
    <label className="block text-xs font-medium text-slate-600">
      Ngày hẹn liên hệ lại (không bắt buộc)
      <input
        className="mt-1 w-full"
        type="date"
        value={followUpDate}
        onChange={(event) => onFollowUpDateChange(event.target.value)}
      />
    </label>
    <button
      type="submit"
      className="btn btn-primary w-full"
      disabled={isSaving || isLoading || !guardians?.length || !notes.trim()}
    >
      {isSaving ? "Đang lưu..." : "Lưu kết quả cuộc gọi"}
    </button>
  </form>
);
