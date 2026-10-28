import type {
  AdmitResponse,
  AuthResponse,
  ChatMessage,
  HostLobby,
  JoinTokenResponse,
  LobbyMessage,
  MeetingDetail,
  MeetingListItem,
  Snapshot,
  User,
  LoanCategory,
  GuideListItem,
  GuideDetail,
  DeviationRule,
  ApplicationListItem,
  ApplicationDetail,
  Applicant,
  SessionDetail,
  WaitResponse,
  WaitingOfficer,
  WaitingRoom,
} from "@/lib/types";

export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

export function hubUrl() {
  if (API_URL) return `${API_URL}/hubs/notifications`;
  if (typeof window !== "undefined") {
    const host = window.location.hostname;
    if (host === "127.0.0.1" || host === "localhost") {
      return "http://127.0.0.1:5088/hubs/notifications";
    }
  }
  return "/hubs/notifications";
}

const TOKEN_KEY = "visitmeet.token";
const GUEST_KEY = "visitmeet.guest";

export function getAuthToken() {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TOKEN_KEY);
}

export function getToken() {
  if (typeof window === "undefined") return null;
  return sessionStorage.getItem(GUEST_KEY) || getAuthToken();
}

export function setToken(token: string | null) {
  if (typeof window === "undefined") return;
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

export function setGuestToken(token: string | null) {
  if (typeof window === "undefined") return;
  if (token) sessionStorage.setItem(GUEST_KEY, token);
  else sessionStorage.removeItem(GUEST_KEY);
}

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init: RequestInit = {}, anonymous = false): Promise<T> {
  const token = anonymous ? null : getToken();
  const headers = new Headers(init.headers);
  if (!(init.body instanceof FormData) && !headers.has("Content-Type") && init.body) {
    headers.set("Content-Type", "application/json");
  }
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const res = await fetch(`${API_URL}${path}`, { ...init, headers });
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  const data = text ? JSON.parse(text) : null;
  if (!res.ok) {
    throw new ApiError(res.status, data?.message ?? `Request failed (${res.status})`);
  }
  return data as T;
}

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    }, true),
  me: () => request<User>("/api/auth/me"),
  creditOfficers: () => request<User[]>("/api/users/credit-officers"),
  meetings: () => request<MeetingListItem[]>("/api/meetings"),
  meeting: (id: string) => request<MeetingDetail>(`/api/meetings/${id}`),
  schedule: (body: {
    bank: string;
    branch: string;
    groupId: string;
    memberId: string;
    memberName: string;
    scheduledAt: string;
    creditOfficerId?: string;
  }) =>
    request<MeetingDetail>("/api/meetings", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  hostCurrent: (slug: string) => request<HostLobby>(`/api/host/${slug}`),
  hostToken: (slug: string) =>
    request<JoinTokenResponse>(`/api/host/${slug}/token`, { method: "POST" }),
  hostWaiting: (slug: string) => request<WaitingOfficer[]>(`/api/host/${slug}/waiting`),
  admit: (slug: string, waitingId: string) =>
    request<AdmitResponse>(`/api/host/${slug}/waiting/${waitingId}/admit`, {
      method: "POST",
    }),
  join: (body: {
    slug: string;
    bank: string;
    branch: string;
    groupId: string;
    memberId: string;
    displayName?: string;
  }) =>
    request<WaitResponse>(
      "/api/join",
      { method: "POST", body: JSON.stringify(body) },
      true,
    ),
  waiting: (id: string) => request<WaitingRoom>(`/api/waiting/${id}`),
  waitingChat: (id: string, body: string, recipientWaitingOfficerId: string | null) =>
    request<LobbyMessage>(`/api/waiting/${id}/chat`, {
      method: "POST",
      body: JSON.stringify({ body, recipientWaitingOfficerId }),
    }),
  waitingLeave: (id: string) =>
    request<void>(`/api/waiting/${id}/leave`, { method: "POST" }),
  waitingConnect: (id: string) =>
    request<JoinTokenResponse>(`/api/waiting/${id}/connect`, { method: "POST" }),
  reportGeotag: (
    id: string,
    body: {
      latitude: number | null;
      longitude: number | null;
      accuracyMeters: number | null;
      capturedAt: string | null;
      error: string | null;
    },
  ) =>
    request<WaitingOfficer>(`/api/waiting/${id}/geotag`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  denyWaiting: (id: string) =>
    request<void>(`/api/waiting/${id}/deny`, { method: "POST" }),
  createCreditOfficer: (body: { name: string; email: string; temporaryPassword: string }) =>
    request<User>("/api/users/credit-officers", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  endMeeting: (id: string) =>
    request<MeetingDetail>(`/api/meetings/${id}/end`, { method: "POST" }),
  postChat: (id: string, body: string) =>
    request<ChatMessage>(`/api/meetings/${id}/chat`, {
      method: "POST",
      body: JSON.stringify({ body }),
    }),
  uploadSnapshot: (
    id: string,
    file: Blob,
    crop: { x: number; y: number; width: number; height: number },
    category: string,
  ) => {
    const form = new FormData();
    form.append("file", file, "capture.png");
    form.append("cropX", String(Math.round(crop.x)));
    form.append("cropY", String(Math.round(crop.y)));
    form.append("cropWidth", String(Math.round(crop.width)));
    form.append("cropHeight", String(Math.round(crop.height)));
    form.append("category", category);
    return request<Snapshot>(`/api/meetings/${id}/snapshots`, { method: "POST", body: form });
  },
  loanCategories: () => request<LoanCategory[]>("/api/assessment/categories"),
  createLoanCategory: (body: { code: string; name: string; description?: string }) =>
    request<LoanCategory>("/api/assessment/categories", { method: "POST", body: JSON.stringify(body) }),
  updateLoanCategory: (id: string, body: { code: string; name: string; description?: string }) =>
    request<LoanCategory>(`/api/assessment/categories/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  guides: () => request<GuideListItem[]>("/api/assessment/guides"),
  guide: (id: string) => request<GuideDetail>(`/api/assessment/guides/${id}`),
  createGuide: (body: { code: string; name: string; eventType: string; loanCategoryIds: string[]; status?: string }) =>
    request<GuideDetail>("/api/assessment/guides", { method: "POST", body: JSON.stringify(body) }),
  updateGuide: (id: string, body: { code: string; name: string; eventType: string; loanCategoryIds: string[]; status?: string }) =>
    request<GuideDetail>(`/api/assessment/guides/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  saveGuideContent: (
    id: string,
    body: {
      groups: { id?: string; name: string; sortOrder: number }[];
      tasks: {
        id?: string;
        groupId?: string;
        groupName: string;
        number: number;
        title: string;
        description: string;
        choices: string[];
        allowMultiple: boolean;
        conditionTaskNumber: number | null;
        conditionChoices: string[];
        ruleLink: string | null;
        sortOrder: number;
      }[];
    },
  ) => request<GuideDetail>(`/api/assessment/guides/${id}/content`, { method: "PUT", body: JSON.stringify(body) }),
  cloneGuide: (id: string, body: { name: string; loanCategoryIds?: string[] }) =>
    request<GuideDetail>(`/api/assessment/guides/${id}/clone`, { method: "POST", body: JSON.stringify(body) }),
  deviationRules: () => request<DeviationRule[]>("/api/assessment/rules"),
  updateDeviationRule: (
    id: string,
    body: {
      description?: string;
      severity?: string;
      parameters?: unknown;
      loanCategoryIds?: string[];
      enabled?: boolean;
      feedsTaskNumber?: number | null;
    },
  ) => request<DeviationRule>(`/api/assessment/rules/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  applications: () => request<ApplicationListItem[]>("/api/assessment/applications"),
  application: (id: string) => request<ApplicationDetail>(`/api/assessment/applications/${id}`),
  createApplication: (body: {
    loanCategoryId: string;
    displayName: string;
    bank?: string;
    branch?: string;
    groupId?: string;
    memberId?: string;
    loanAmount?: number | null;
    pan?: string;
    phone?: string;
    dateOfBirth?: string;
  }) => request<ApplicationDetail>("/api/assessment/applications", { method: "POST", body: JSON.stringify(body) }),
  addApplicant: (id: string, body: { role: string; name: string; pan?: string; phone?: string; dateOfBirth?: string }) =>
    request<Applicant>(`/api/assessment/applications/${id}/applicants`, { method: "POST", body: JSON.stringify(body) }),
  uploadApplicantInput: (id: string, body: { kind: string; provider?: string; fileName?: string; document: unknown }) =>
    request<Applicant>(`/api/assessment/applicants/${id}/inputs`, { method: "POST", body: JSON.stringify(body) }),
  ensureSession: (body: {
    applicationId?: string;
    meetingId?: string;
    bank?: string;
    branch?: string;
    groupId?: string;
    memberId?: string;
  }) => request<SessionDetail>("/api/assessment/sessions/ensure", { method: "POST", body: JSON.stringify(body) }),
  session: (id: string) => request<SessionDetail>(`/api/assessment/sessions/${id}`),
  saveSessionTask: (
    id: string,
    number: number,
    body: { status?: string; choices?: string[]; notes?: string },
  ) => request<SessionDetail>(`/api/assessment/sessions/${id}/tasks/${number}`, { method: "PUT", body: JSON.stringify(body) }),
  completeSession: (id: string) =>
    request<SessionDetail>(`/api/assessment/sessions/${id}/complete`, { method: "POST" }),
};
