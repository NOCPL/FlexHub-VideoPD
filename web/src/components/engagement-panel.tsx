"use client";

import { useMemo, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import { SNAPSHOT_CATEGORIES } from "@/lib/snapshot-categories";
import type { SessionDetail, SessionGroupView, SessionTaskView, Snapshot } from "@/lib/types";
import { AuthImage } from "@/components/auth-image";

function stamp(value: string | null | undefined) {
  if (!value) return "—";
  return new Date(value).toLocaleString("en-IN", { timeZone: "Asia/Calcutta" });
}

export function PlanWorkspace({
  session,
  onChange,
  compact,
}: {
  session: SessionDetail;
  onChange: (next: SessionDetail) => void;
  compact?: boolean;
}) {
  const [openGroup, setOpenGroup] = useState<string>(session.groups[0]?.name ?? "");
  const locked = session.status === "Completed";

  async function save(number: number, body: { status?: string; choices?: string[]; notes?: string }) {
    try {
      onChange(await api.saveSessionTask(session.id, number, body));
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save the task.");
    }
  }

  async function complete() {
    try {
      onChange(await api.completeSession(session.id));
      toast.success("Session marked completed.");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Cannot close this session yet.");
    }
  }

  return (
    <div className={`flex min-h-0 flex-1 flex-col ${compact ? "" : "rounded-xl border border-[#d7deea] bg-white"}`}>
      <div className="border-b border-[#d7deea] px-3 py-3">
        <div className="flex flex-wrap items-center gap-2">
          <div className="font-semibold text-[#10264e]">{session.displayName}</div>
          <Badge>{session.mode}</Badge>
          <Badge variant={session.status === "Completed" ? "secondary" : "default"}>{session.status}</Badge>
        </div>
        <p className="mt-1 text-xs text-[#5a6a84]">
          {session.applicationCode} · {session.guideName} v{session.guideVersion} · {session.coveredCount}/{session.visibleCount} Complete
        </p>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto">
        {session.groups.map((group) => (
          <PlanGroup
            key={group.name}
            group={group}
            open={openGroup === group.name}
            locked={locked}
            onToggle={() => setOpenGroup(openGroup === group.name ? "" : group.name)}
            onSave={save}
          />
        ))}
      </div>
      <div className="space-y-2 border-t border-[#d7deea] bg-white p-3">
        {session.blockers.length > 0 ? (
          <ul className="list-disc space-y-1 pl-4 text-xs text-[#b42318]">
            {session.blockers.map((item) => <li key={item}>{item}</li>)}
          </ul>
        ) : (
          <p className="text-xs text-[#29416f]">Consent is recorded and high flags have explanations. You can close the session.</p>
        )}
        <Button
          className="w-full bg-[#005792] hover:bg-[#004675]"
          disabled={locked || session.blockers.length > 0}
          onClick={() => void complete()}
        >
          {locked ? "Session completed" : "Mark session completed"}
        </Button>
      </div>
    </div>
  );
}

function PlanGroup({
  group,
  open,
  locked,
  onToggle,
  onSave,
}: {
  group: SessionGroupView;
  open: boolean;
  locked: boolean;
  onToggle: () => void;
  onSave: (number: number, body: { status?: string; choices?: string[]; notes?: string }) => Promise<void>;
}) {
  return (
    <section className="border-b border-[#d7deea]">
      <button type="button" className="flex w-full items-center gap-2 px-3 py-2 text-left" onClick={onToggle}>
        <span className="flex-1 text-sm font-semibold text-[#10264e]">{group.name}</span>
        <span className="text-xs font-medium text-[#005792]">{group.coveredCount}/{group.visibleCount} Complete</span>
      </button>
      {open ? (
        <div className="space-y-2 px-3 pb-3">
          {group.tasks.map((task) => (
            <TaskCard key={task.number} task={task} locked={locked} onSave={onSave} />
          ))}
        </div>
      ) : null}
    </section>
  );
}

function TaskCard({
  task,
  locked,
  onSave,
}: {
  task: SessionTaskView;
  locked: boolean;
  onSave: (number: number, body: { status?: string; choices?: string[]; notes?: string }) => Promise<void>;
}) {
  const high = task.probes.some((p) => p.severity === "High");
  return (
    <div className="rounded-lg border border-[#d7deea] bg-[#fafbfd] p-2">
      <div className="flex items-start gap-2">
        <span className="w-8 shrink-0 font-mono text-xs text-[#5a6a84]">{String(task.number).padStart(3, "0")}</span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-1">
            <span className="text-sm font-medium text-[#10264e]">{task.title}</span>
            <CoverageBadge coverage={task.coverage} />
            {high ? <Badge className="bg-[#b42318]">High</Badge> : null}
            {task.probes.some((p) => p.severity === "Medium") ? <Badge className="bg-[#b54708]">Probe</Badge> : null}
          </div>
          <p className="mt-1 text-xs text-[#29416f]">{task.description}</p>
          {task.choices.length > 0 ? (
            <div className="mt-2 flex flex-wrap gap-1">
              {task.choices.map((choice) => {
                const on = task.selectedChoices.includes(choice);
                return (
                  <button
                    key={choice}
                    type="button"
                    disabled={locked}
                    className={`rounded-full px-2 py-0.5 text-xs font-semibold ${on ? "bg-[#005792] text-white" : "border border-[#d7deea] bg-white text-[#29416f]"}`}
                    onClick={() => {
                      const next = task.allowMultiple
                        ? on ? task.selectedChoices.filter((c) => c !== choice) : [...task.selectedChoices, choice]
                        : on ? [] : [choice];
                      void onSave(task.number, { choices: next, status: next.length ? "Covered" : "Pending" });
                    }}
                  >
                    {choice}
                  </button>
                );
              })}
            </div>
          ) : null}
          <div className="mt-2 flex gap-1">
            <Button size="sm" variant={task.coverage === "Covered" ? "default" : "outline"} className={task.coverage === "Covered" ? "bg-[#005792] hover:bg-[#004675]" : ""} disabled={locked} onClick={() => void onSave(task.number, { status: "Covered" })}>
              Covered
            </Button>
            <Button size="sm" variant={task.coverage === "NotCovered" ? "default" : "outline"} disabled={locked} onClick={() => void onSave(task.number, { status: "NotCovered" })}>
              Not covered
            </Button>
          </div>
          {task.probes.map((probe) => (
            <div key={probe.flagId} className="mt-2 rounded-md border border-[#eadfd2] bg-white p-2 text-xs text-[#29416f]">
              <div className="font-semibold text-[#10264e]">{probe.severity}: {probe.name}</div>
              <ul className="mt-1 list-disc pl-4">
                {(probe.evidence ?? []).map((item, i) => (
                  <li key={i}>{item.summary || JSON.stringify(item)}</li>
                ))}
              </ul>
            </div>
          ))}
          {task.probes.length > 0 || high ? (
            <Textarea
              className="mt-2 text-xs"
              rows={2}
              disabled={locked}
              defaultValue={task.notes}
              key={`${task.number}-${task.notes}`}
              placeholder={high ? "Recorded explanation required for High flags" : "Notes from the probe"}
              onBlur={(e) => {
                if (e.target.value !== task.notes) void onSave(task.number, { notes: e.target.value, status: task.coverage === "Pending" ? "Covered" : task.coverage });
              }}
            />
          ) : null}
        </div>
      </div>
    </div>
  );
}

function CoverageBadge({ coverage }: { coverage: string }) {
  if (coverage === "Covered") return <Badge className="bg-[#005792]">Covered</Badge>;
  if (coverage === "NotCovered") return <Badge className="bg-[#b54708]">Not covered</Badge>;
  return <Badge variant="secondary">Pending</Badge>;
}

export function PhotosWorkspace({ photos }: { photos: Snapshot[] }) {
  const byType = useMemo(() => {
    return SNAPSHOT_CATEGORIES.map((category) => ({
      category,
      items: photos.filter((p) => p.category === category),
    }));
  }, [photos]);

  return (
    <div className="min-h-0 flex-1 overflow-y-auto p-3">
      <p className="mb-3 text-xs text-[#5a6a84]">
        Capture stills from the call controls. Memo sections read these types: house, business site, dairy, kirana, tailoring, barber, clothing store.
      </p>
      {photos.length === 0 ? (
        <p className="text-sm text-[#5a6a84]">No stills yet. Use Capture still on the video.</p>
      ) : null}
      {byType.filter((row) => row.items.length > 0).map((row) => (
        <div key={row.category} className="mb-3">
          <div className="mb-1 text-sm font-semibold text-[#10264e]">{row.category} · {row.items.length}</div>
          <div className="grid grid-cols-2 gap-2">
            {row.items.map((photo) => (
              <div key={photo.id} className="overflow-hidden rounded-lg border border-[#d7deea] bg-white">
                <AuthImage src={photo.url} alt={photo.fileName} className="aspect-video w-full object-cover" />
                <div className="truncate px-2 py-1 text-[11px] text-[#5a6a84]">{photo.fileName}</div>
              </div>
            ))}
          </div>
        </div>
      ))}
      <div className="mt-2 text-xs text-[#5a6a84]">
        Types with no stills: {byType.filter((row) => row.items.length === 0).map((row) => row.category).join(", ") || "none"}
      </div>
    </div>
  );
}

export function MetadataWorkspace({ session }: { session: SessionDetail }) {
  const rows = [
    ["Event", session.eventType],
    ["Mode", session.mode === "Live" ? "Video" : session.mode],
    ["Status", session.status],
    ["Duration", session.durationSeconds != null ? formatDuration(session.durationSeconds) : "—"],
    ["Scheduled", stamp(session.scheduledAt)],
    ["Assignee", session.assigneeName || "—"],
    ["Session ID", session.id],
    ["Application", `${session.applicationCode} · ${session.displayName}`],
    ["Guide", `${session.guideName} v${session.guideVersion}`],
    ["Tags", `${session.bank || "—"} · ${session.groupId || "—"} · ${session.memberId || "—"}`],
    ["Started", stamp(session.startedAt)],
    ["Completed", stamp(session.completedAt)],
    ["Recordings", String(session.recordings.length)],
    ["Stills", String(session.photos.length)],
  ];
  return (
    <div className="min-h-0 flex-1 overflow-y-auto p-3">
      <dl className="space-y-2 text-sm">
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt className="text-xs uppercase tracking-wide text-[#5a6a84]">{label}</dt>
            <dd className="break-all text-[#10264e]">{value}</dd>
          </div>
        ))}
      </dl>
      {session.recordings.length > 0 ? (
        <ul className="mt-3 space-y-1 text-xs text-[#29416f]">
          {session.recordings.map((r) => (
            <li key={r.id}>seg {r.sequence} · {r.status}{r.durationSeconds ? ` · ${formatDuration(r.durationSeconds)}` : ""}</li>
          ))}
        </ul>
      ) : (
        <p className="mt-3 text-xs text-[#5a6a84]">No recording files yet. Egress writes them when the field officer is in the room.</p>
      )}
    </div>
  );
}

function formatDuration(totalSeconds: number) {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return [hours, minutes, seconds].map((part) => String(part).padStart(2, "0")).join(":");
}
