export type Role = "CreditOfficer" | "Admin" | "FieldGuest";

export type User = {
  id: string;
  name: string;
  email: string;
  role: Role;
  hostSlug?: string | null;
  hostUrl?: string | null;
};

export type AuthResponse = {
  token: string;
  user: User;
};

export type MeetingListItem = {
  id: string;
  code: string;
  bank: string;
  branch: string;
  groupId: string;
  memberId: string;
  memberName: string;
  scheduledAt: string;
  status: string;
  creditOfficerName: string;
  creditOfficerId: string;
  hostSlug: string | null;
  hostUrl: string;
  fieldUrl: string;
  durationSeconds: number | null;
  recordingStatus: string | null;
  recordingSegments: number;
};

export type Recording = {
  id: string;
  status: string;
  sequence: number;
  bank: string;
  branch: string;
  groupId: string;
  memberId: string;
  filePath: string | null;
  durationSeconds: number | null;
  error: string | null;
  createdAt: string;
  joinedAt: string | null;
  leftAt: string | null;
};

export type Snapshot = {
  id: string;
  bank: string;
  branch: string;
  groupId: string;
  memberId: string;
  category: string;
  fileName: string;
  url: string;
  cropX: number | null;
  cropY: number | null;
  cropWidth: number | null;
  cropHeight: number | null;
  createdAt: string;
  latitude: number | null;
  longitude: number | null;
  accuracyMeters: number | null;
  geoCapturedAt: string | null;
  geoError: string | null;
};

export type ChatMessage = {
  id: string;
  senderUserId: string;
  senderName: string;
  senderRole: string;
  body: string;
  sentAt: string;
};

export type MeetingDetail = {
  id: string;
  code: string;
  bank: string;
  branch: string;
  groupId: string;
  memberId: string;
  memberName: string;
  scheduledAt: string;
  status: string;
  liveKitRoomName: string;
  startedAt: string | null;
  endedAt: string | null;
  durationSeconds: number | null;
  hostUrl: string;
  fieldUrl: string;
  creditOfficer: User;
  recordings: Recording[];
  snapshots: Snapshot[];
  chat: ChatMessage[];
};

export type JoinTokenResponse = {
  token: string;
  guestToken: string;
  liveKitUrl: string;
  identity: string;
  meeting: MeetingDetail;
};

export type WaitingOfficer = {
  id: string;
  displayName: string;
  bank: string;
  branch: string;
  groupId: string;
  memberId: string;
  status: string;
  createdAt: string;
  admittedAt: string | null;
  connectedAt: string | null;
  leftAt: string | null;
  callDurationSeconds: number | null;
  latitude: number | null;
  longitude: number | null;
  accuracyMeters: number | null;
  geoCapturedAt: string | null;
  geoError: string | null;
  tokenNumber: number | null;
  estimatedWaitMinutes: number | null;
};

export type LobbyMessage = {
  id: string;
  senderUserId: string;
  senderName: string;
  senderRole: string;
  recipientWaitingOfficerId: string | null;
  body: string;
  sentAt: string;
};

export type WaitingRoom = {
  waiting: WaitingOfficer;
  chat: LobbyMessage[];
};

export type WaitResponse = {
  guestToken: string;
  waiting: WaitingOfficer;
  creditOfficerName: string;
  hostSlug: string;
};

export type HostLobby = {
  officer: User;
  meeting: MeetingDetail | null;
  waiting: WaitingOfficer[];
  active: WaitingOfficer | null;
  chat: LobbyMessage[];
};

export type AdmitResponse = {
  field: JoinTokenResponse;
  host: JoinTokenResponse;
  waiting: WaitingOfficer;
};

export type NotificationPayload = {
  type: string;
  meetingId: string;
  code: string;
  message: string;
  bank?: string | null;
  branch?: string | null;
  groupId?: string | null;
  memberId?: string | null;
};

export const STATUS_LABEL: Record<string, string> = {
  Scheduled: "Scheduled",
  WaitingForFieldOfficer: "Waiting room",
  InProgress: "In progress",
  Completed: "Completed",
  Cancelled: "Cancelled",
};

export type LoanCategory = {
  id: string;
  code: string;
  name: string;
  description: string | null;
};

export type GuideListItem = {
  id: string;
  code: string;
  name: string;
  eventType: string;
  version: number;
  status: string;
  loanCategoryIds: string[];
  createdAt: string;
  updatedAt: string;
};

export type GuideGroup = {
  id: string;
  name: string;
  sortOrder: number;
  taskCount: number;
};

export type GuideTask = {
  id: string;
  groupId: string;
  number: number;
  title: string;
  description: string;
  choices: string[];
  allowMultiple: boolean;
  conditionTaskNumber: number | null;
  conditionChoices: string[];
  ruleLink: string | null;
  sortOrder: number;
};

export type GuideDetail = GuideListItem & {
  groups: GuideGroup[];
  tasks: GuideTask[];
};

export type DeviationRule = {
  id: string;
  checkKey: string;
  name: string;
  description: string;
  severity: string;
  parameters: Record<string, unknown>;
  loanCategoryIds: string[];
  enabled: boolean;
  feedsTaskNumber: number | null;
  updatedAt: string;
};

export type RuleFlag = {
  id: string;
  ruleId: string;
  checkKey: string;
  name: string;
  severity: string;
  fired: boolean;
  evidence: { kind?: string; summary?: string; fields?: Record<string, string | null> }[];
  feedsTaskNumber: number | null;
};

export type RuleRun = {
  id: string;
  kind: string;
  ranAt: string;
  results: RuleFlag[];
};

export type ApplicantInput = {
  id: string;
  kind: string;
  provider: string;
  fileName: string;
  uploadedAt: string;
};

export type Applicant = {
  id: string;
  role: string;
  name: string;
  pan: string | null;
  phone: string | null;
  dateOfBirth: string | null;
  inputs: ApplicantInput[];
  ruleRuns: RuleRun[];
};

export type ApplicationListItem = {
  id: string;
  code: string;
  displayName: string;
  loanCategoryName: string;
  bank: string;
  branch: string;
  groupId: string;
  memberId: string;
  loanAmount: number | null;
  applicantCount: number;
  firedFlagCount: number;
  createdAt: string;
};

export type ApplicationDetail = {
  id: string;
  code: string;
  displayName: string;
  loanCategoryId: string;
  loanCategoryName: string;
  bank: string;
  branch: string;
  groupId: string;
  memberId: string;
  loanAmount: number | null;
  createdAt: string;
  applicants: Applicant[];
};
