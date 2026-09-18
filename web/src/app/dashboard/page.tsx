"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { api } from "@/lib/api";
import type { MeetingListItem, User } from "@/lib/types";
import { STATUS_LABEL } from "@/lib/types";

export default function DashboardPage() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [meetings, setMeetings] = useState<MeetingListItem[] | null>(null);
  const [officers, setOfficers] = useState<User[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    bank: "SBI",
    branch: "Mumbai Central",
    groupId: "G-22",
    memberId: "M-10482",
    memberName: "Lakshmi Self-Help Group — Meena Devi",
    creditOfficerId: "",
    scheduledAt: toLocalInput(new Date(Date.now() + 15 * 60 * 1000)),
  });

  useEffect(() => {
    if (!loading && !user) router.replace("/");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user) return;
    let cancelled = false;
    Promise.all([
      api.meetings(),
      user.role === "Admin" ? api.creditOfficers() : Promise.resolve([] as User[]),
    ])
      .then(([list, cos]) => {
        if (cancelled) return;
        setMeetings(list);
        setOfficers(cos);
        if (cos[0]) {
          setForm((f) => (f.creditOfficerId ? f : { ...f, creditOfficerId: cos[0].id }));
        }
      })
      .catch((err) => setError(err instanceof Error ? err.message : "Could not load Video PD records."));
    return () => {
      cancelled = true;
    };
  }, [user]);

  const upcoming = useMemo(
    () => meetings?.filter((m) => m.status !== "Completed" && m.status !== "Cancelled") ?? [],
    [meetings],
  );
  const past = useMemo(
    () => meetings?.filter((m) => m.status === "Completed" || m.status === "Cancelled") ?? [],
    [meetings],
  );

  const selectedOfficer =
    officers.find((o) => o.id === form.creditOfficerId) ?? officers[0] ?? null;
  const hostUrl =
    user?.role === "Admin" ? selectedOfficer?.hostUrl || "" : user?.hostUrl || "";

  async function schedule(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const created = await api.schedule({
        bank: form.bank,
        branch: form.branch,
        groupId: form.groupId,
        memberId: form.memberId,
        memberName: form.memberName,
        scheduledAt: new Date(form.scheduledAt).toISOString(),
        creditOfficerId: user?.role === "Admin" ? form.creditOfficerId : undefined,
      });
      toast.success("Field-officer link generated. Host URL is unchanged.");
      setMeetings((list) =>
        list
          ? [
              {
                id: created.id,
                code: created.code,
                bank: created.bank,
                branch: created.branch,
                groupId: created.groupId,
                memberId: created.memberId,
                memberName: created.memberName,
                scheduledAt: created.scheduledAt,
                status: created.status,
                creditOfficerName: created.creditOfficer.name,
                creditOfficerId: created.creditOfficer.id,
                hostSlug: created.creditOfficer.hostSlug ?? null,
                hostUrl: created.hostUrl,
                fieldUrl: created.fieldUrl,
                durationSeconds: created.durationSeconds,
                recordingStatus: created.recordings[0]?.status ?? null,
                recordingSegments: created.recordings.length,
              },
              ...list,
            ]
          : list,
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not schedule Video PD.");
    } finally {
      setBusy(false);
    }
  }

  function copy(text: string, label: string) {
    navigator.clipboard.writeText(text);
    toast.success(`${label} copied.`);
  }

  if (loading || !user) {
    return <div className="min-h-full bg-[#eef1f6] p-8 text-[#5a6a84]">Loading dashboard…</div>;
  }

  return (
    <div className="min-h-full bg-[#eef1f6]">
      <AppHeader />
      <main className="mx-auto grid max-w-6xl gap-6 px-4 py-6 lg:grid-cols-[minmax(0,1fr)_360px]">
        <section className="space-y-4">
          <div>
            <h1 className="font-heading text-2xl font-semibold tracking-tight text-[#10264e]">Schedule Video PD</h1>
            <p className="text-sm text-[#5a6a84]">
              This only generates links. Angular always opens the static host URL. Kotlin opens the field
              URL with bank, branch, groupId, and memberId.
            </p>
          </div>
          {hostUrl ? (
            <Card className="border border-[#d7deea] bg-white">
              <CardHeader>
                <CardTitle className="text-[#10264e]">Static host URL</CardTitle>
                <CardDescription className="text-[#5a6a84]">
                  {user.role === "Admin" && selectedOfficer
                    ? `${selectedOfficer.name}'s permanent host URL. It does not change when you schedule.`
                    : "Does not change when you schedule another Video PD."}
                </CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2 sm:flex-row">
                <code className="flex-1 truncate rounded-md bg-[#eaf0f8] px-3 py-2 text-xs text-[#29416f]">{hostUrl}</code>
                <Button variant="outline" size="sm" onClick={() => copy(hostUrl, "Host URL")}>
                  Copy
                </Button>
                <Button
                  size="sm"
                  className="bg-[#f7481c] text-white hover:bg-[#d63a11]"
                  onClick={() => router.push(new URL(hostUrl).pathname)}
                >
                  Open room
                </Button>
              </CardContent>
            </Card>
          ) : null}
          {error ? <p className="text-destructive text-sm">{error}</p> : null}
          {meetings === null ? (
            <p className="text-[#5a6a84]">Loading Video PD…</p>
          ) : meetings.length === 0 ? (
            <Card className="border border-[#d7deea] bg-white">
              <CardHeader>
                <CardTitle className="text-[#10264e]">No Video PD records yet</CardTitle>
                <CardDescription className="text-[#5a6a84]">Use the form to mint a field-officer link.</CardDescription>
              </CardHeader>
            </Card>
          ) : (
            <>
              <VisitList title="Upcoming" items={upcoming} empty="No open Video PD." onCopy={copy} />
              <VisitList title="Completed" items={past} empty="No completed Video PD yet." onCopy={copy} />
            </>
          )}
        </section>

        <Card className="h-fit border border-[#d7deea] bg-white">
          <CardHeader>
            <CardTitle className="text-[#10264e]">Generate field-officer link</CardTitle>
            <CardDescription className="text-[#5a6a84]">
              Query parameters are stored as-is. Flexhub Video PD does not validate them.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form className="space-y-3" onSubmit={schedule}>
              {user.role === "Admin" ? (
                <Field label="Credit officer">
                  <Select
                    value={form.creditOfficerId || selectedOfficer?.id || ""}
                    onValueChange={(value) => setForm({ ...form, creditOfficerId: value })}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select officer" />
                    </SelectTrigger>
                    <SelectContent>
                      {officers.map((o) => (
                        <SelectItem key={o.id} value={o.id}>
                          {o.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
              ) : null}
              <Field label="Bank">
                <Input value={form.bank} onChange={(e) => setForm({ ...form, bank: e.target.value })} />
              </Field>
              <Field label="Branch">
                <Input value={form.branch} onChange={(e) => setForm({ ...form, branch: e.target.value })} />
              </Field>
              <Field label="Group ID">
                <Input value={form.groupId} onChange={(e) => setForm({ ...form, groupId: e.target.value })} />
              </Field>
              <Field label="Member ID">
                <Input value={form.memberId} onChange={(e) => setForm({ ...form, memberId: e.target.value })} />
              </Field>
              <Field label="Member name">
                <Input value={form.memberName} onChange={(e) => setForm({ ...form, memberName: e.target.value })} />
              </Field>
              <Field label="Scheduled time">
                <Input
                  type="datetime-local"
                  required
                  value={form.scheduledAt}
                  onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })}
                />
              </Field>
              <Button
                type="submit"
                className="w-full bg-[#f7481c] text-white hover:bg-[#d63a11]"
                disabled={busy || (user.role === "Admin" && !form.creditOfficerId)}
              >
                {busy ? "Generating…" : "Schedule and mint FO link"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </main>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      {children}
    </div>
  );
}

function VisitList({
  title,
  items,
  empty,
  onCopy,
}: {
  title: string;
  items: MeetingListItem[];
  empty: string;
  onCopy: (text: string, label: string) => void;
}) {
  return (
    <div className="space-y-3">
      <h2 className="text-sm font-medium uppercase tracking-wide text-[#5a6a84]">{title}</h2>
      {items.length === 0 ? (
        <p className="text-sm text-[#5a6a84]">{empty}</p>
      ) : (
        items.map((m) => (
          <Card key={m.id} className="border border-[#d7deea] bg-white">
            <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium text-[#10264e]">{m.memberName || m.memberId || "Video PD"}</span>
                  <Badge variant="secondary">{STATUS_LABEL[m.status] ?? m.status}</Badge>
                </div>
                <p className="mt-1 text-sm text-[#5a6a84]">
                  {m.bank} · {m.branch} · {m.groupId} · {m.memberId} · {new Date(m.scheduledAt).toLocaleString()}
                </p>
                <p className="text-xs text-[#5a6a84]">
                  Duration {m.durationSeconds != null ? `${m.durationSeconds}s` : "—"} · Segments{" "}
                  {m.recordingSegments} · {m.creditOfficerName}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button variant="outline" size="sm" asChild>
                  <Link href={`/video-pd/${m.id}`}>Details</Link>
                </Button>
                <Button variant="outline" size="sm" onClick={() => onCopy(m.fieldUrl, "Field-officer URL")}>
                  Copy FO link
                </Button>
              </div>
            </CardContent>
          </Card>
        ))
      )}
    </div>
  );
}

function toLocalInput(date: Date) {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
