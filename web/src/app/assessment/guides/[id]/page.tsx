"use client";

import { useEffect, useMemo, useState, type ReactNode } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { AssessmentNav } from "@/components/assessment-nav";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import type { GuideDetail, LoanCategory } from "@/lib/types";

type DraftTask = {
  id?: string;
  groupName: string;
  number: number;
  title: string;
  description: string;
  choices: string;
  allowMultiple: boolean;
  conditionTaskNumber: string;
  conditionChoices: string;
  ruleLink: string;
};

export default function GuideEditorPage() {
  const { id } = useParams<{ id: string }>();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [guide, setGuide] = useState<GuideDetail | null>(null);
  const [categories, setCategories] = useState<LoanCategory[]>([]);
  const [name, setName] = useState("");
  const [status, setStatus] = useState("Draft");
  const [categoryIds, setCategoryIds] = useState<string[]>([]);
  const [groups, setGroups] = useState<string[]>([]);
  const [tasks, setTasks] = useState<DraftTask[]>([]);
  const [open, setOpen] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!loading && (!user || user.role !== "Admin")) router.replace("/assessment");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user || !id) return;
    Promise.all([api.guide(id), api.loanCategories()]).then(([g, cats]) => {
      setGuide(g);
      setCategories(cats);
      setName(g.name);
      setStatus(g.status);
      setCategoryIds(g.loanCategoryIds);
      const groupNames = [...g.groups].sort((a, b) => a.sortOrder - b.sortOrder).map((x) => x.name);
      setGroups(groupNames);
      const byId = Object.fromEntries(g.groups.map((x) => [x.id, x.name]));
      setTasks(g.tasks.map((t) => ({
        id: t.id,
        groupName: byId[t.groupId] ?? groupNames[0] ?? "",
        number: t.number,
        title: t.title,
        description: t.description,
        choices: t.choices.join(", "),
        allowMultiple: t.allowMultiple,
        conditionTaskNumber: t.conditionTaskNumber ? String(t.conditionTaskNumber) : "",
        conditionChoices: t.conditionChoices.join(", "),
        ruleLink: t.ruleLink ?? "",
      })));
    }).catch((err) => toast.error(err.message));
  }, [user, id]);

  const grouped = useMemo(() => {
    return groups.map((group) => ({
      group,
      tasks: tasks.filter((t) => t.groupName === group).sort((a, b) => a.number - b.number),
    }));
  }, [groups, tasks]);

  async function saveMeta() {
    if (!guide) return;
    setBusy(true);
    try {
      const saved = await api.updateGuide(guide.id, {
        code: guide.code,
        name,
        eventType: guide.eventType,
        loanCategoryIds: categoryIds,
        status,
      });
      setGuide(saved);
      toast.success("Guide details saved");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed.");
    } finally {
      setBusy(false);
    }
  }

  async function saveContent() {
    if (!guide) return;
    setBusy(true);
    try {
      const saved = await api.saveGuideContent(guide.id, {
        groups: groups.map((g, i) => ({ name: g, sortOrder: i })),
        tasks: tasks.map((t, i) => ({
          id: t.id,
          groupName: t.groupName,
          number: t.number,
          title: t.title,
          description: t.description,
          choices: split(t.choices),
          allowMultiple: t.allowMultiple,
          conditionTaskNumber: t.conditionTaskNumber ? Number(t.conditionTaskNumber) : null,
          conditionChoices: split(t.conditionChoices),
          ruleLink: t.ruleLink || null,
          sortOrder: i,
        })),
      });
      setGuide(saved);
      toast.success("Tasks saved");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save tasks.");
    } finally {
      setBusy(false);
    }
  }

  if (loading || !user || !guide) {
    return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading guide…</div>;
  }

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-5xl space-y-4 px-4 py-6">
        <AssessmentNav />
        <Link href="/assessment/guides" className="text-sm text-[#29416f] underline">Back to guides</Link>
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="font-heading text-2xl font-semibold text-[#10264e]">{guide.name}</h1>
          <Badge>v{guide.version}</Badge>
          <Badge>{status}</Badge>
        </div>
        <div className="grid gap-3 rounded-xl border border-[#d7deea] bg-white p-4 sm:grid-cols-2">
          <div className="space-y-1.5"><Label>Name</Label><Input value={name} onChange={(e) => setName(e.target.value)} /></div>
          <div className="space-y-1.5">
            <Label>Status</Label>
            <select className="h-9 w-full rounded-lg border px-2 text-sm" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option>Draft</option>
              <option>Active</option>
              <option>Archived</option>
            </select>
          </div>
          <div className="space-y-1.5 sm:col-span-2">
            <Label>Loan categories</Label>
            <div className="flex flex-wrap gap-2">
              {categories.map((c) => (
                <label key={c.id} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={categoryIds.includes(c.id)}
                    onChange={(e) => setCategoryIds(e.target.checked ? [...categoryIds, c.id] : categoryIds.filter((x) => x !== c.id))}
                  />
                  {c.name}
                </label>
              ))}
            </div>
          </div>
          <Button className="bg-[#005792] hover:bg-[#004675]" disabled={busy} onClick={() => void saveMeta()}>Save details</Button>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setGroups((g) => [...g, `Group ${g.length + 1}`])}>Add group</Button>
          <Button
            variant="outline"
            onClick={() => {
              const group = groups[0] ?? "Introduction";
              const number = tasks.reduce((max, t) => Math.max(max, t.number), 0) + 1;
              setTasks((list) => [...list, emptyTask(group, number)]);
              setOpen(number);
            }}
          >
            Add task
          </Button>
          <Button className="bg-[#005792] hover:bg-[#004675]" disabled={busy} onClick={() => void saveContent()}>
            {busy ? "Saving…" : "Save tasks and conditions"}
          </Button>
        </div>

        {grouped.map(({ group, tasks: rows }) => (
          <section key={group} className="overflow-hidden rounded-xl border border-[#d7deea] bg-white">
            <div className="flex items-center gap-2 border-b px-3 py-2">
              <Input
                className="h-8 max-w-xs font-semibold"
                value={group}
                onChange={(e) => renameGroup(group, e.target.value, setGroups, setTasks)}
              />
              <span className="text-xs text-[#5a6a84]">{rows.length} tasks</span>
              <Button type="button" variant="outline" size="sm" onClick={() => moveGroup(group, -1, setGroups)}>Up</Button>
              <Button type="button" variant="outline" size="sm" onClick={() => moveGroup(group, 1, setGroups)}>Down</Button>
            </div>
            {rows.map((task) => (
              <div key={`${task.number}-${task.title}`} className="border-b px-3 py-2 last:border-b-0">
                <button type="button" className="flex w-full items-center gap-2 text-left text-sm" onClick={() => setOpen(open === task.number ? null : task.number)}>
                  <span className="w-10 font-mono text-[#5a6a84]">{String(task.number).padStart(3, "0")}</span>
                  <span className="flex-1 font-medium text-[#10264e]">{task.title}</span>
                  {task.allowMultiple ? <Badge>Multi</Badge> : null}
                  {task.conditionTaskNumber ? <Badge variant="secondary">when {task.conditionTaskNumber}</Badge> : null}
                </button>
                {open === task.number ? (
                  <div className="mt-2 grid gap-2 sm:grid-cols-2">
                    <Labeled label="Title"><Input value={task.title} onChange={(e) => patch(task.number, { title: e.target.value }, setTasks)} /></Labeled>
                    <Labeled label="Number"><Input type="number" value={task.number} onChange={(e) => patch(task.number, { number: Number(e.target.value) }, setTasks)} /></Labeled>
                    <Labeled label="Group">
                      <select className="h-9 w-full rounded-lg border px-2 text-sm" value={task.groupName} onChange={(e) => patch(task.number, { groupName: e.target.value }, setTasks)}>
                        {groups.map((g) => <option key={g}>{g}</option>)}
                      </select>
                    </Labeled>
                    <Labeled label="Choices (comma separated)"><Input value={task.choices} onChange={(e) => patch(task.number, { choices: e.target.value }, setTasks)} /></Labeled>
                    <Labeled label="Show when task #"><Input value={task.conditionTaskNumber} onChange={(e) => patch(task.number, { conditionTaskNumber: e.target.value }, setTasks)} /></Labeled>
                    <Labeled label="When choice is"><Input value={task.conditionChoices} onChange={(e) => patch(task.number, { conditionChoices: e.target.value }, setTasks)} /></Labeled>
                    <Labeled label="Rule link"><Input value={task.ruleLink} onChange={(e) => patch(task.number, { ruleLink: e.target.value }, setTasks)} /></Labeled>
                    <label className="flex items-center gap-2 text-sm">
                      <input type="checkbox" checked={task.allowMultiple} onChange={(e) => patch(task.number, { allowMultiple: e.target.checked }, setTasks)} />
                      Allow multiple choices
                    </label>
                    <div className="sm:col-span-2">
                      <Labeled label="What the interviewer asks">
                        <Textarea value={task.description} onChange={(e) => patch(task.number, { description: e.target.value }, setTasks)} />
                      </Labeled>
                    </div>
                  </div>
                ) : null}
              </div>
            ))}
          </section>
        ))}
      </main>
    </div>
  );
}

function emptyTask(groupName: string, number: number): DraftTask {
  return {
    groupName,
    number,
    title: "New task",
    description: "",
    choices: "",
    allowMultiple: false,
    conditionTaskNumber: "",
    conditionChoices: "",
    ruleLink: "",
  };
}

function split(value: string) {
  return value.split(",").map((x) => x.trim()).filter(Boolean);
}

function patch(number: number, next: Partial<DraftTask>, setTasks: (fn: (list: DraftTask[]) => DraftTask[]) => void) {
  setTasks((list) => list.map((t) => (t.number === number ? { ...t, ...next } : t)));
}

function renameGroup(
  from: string,
  to: string,
  setGroups: (fn: (g: string[]) => string[]) => void,
  setTasks: (fn: (list: DraftTask[]) => DraftTask[]) => void,
) {
  setGroups((g) => g.map((name) => (name === from ? to : name)));
  setTasks((list) => list.map((t) => (t.groupName === from ? { ...t, groupName: to } : t)));
}

function moveGroup(name: string, delta: number, setGroups: (fn: (g: string[]) => string[]) => void) {
  setGroups((g) => {
    const i = g.indexOf(name);
    const j = i + delta;
    if (i < 0 || j < 0 || j >= g.length) return g;
    const next = [...g];
    const [item] = next.splice(i, 1);
    next.splice(j, 0, item);
    return next;
  });
}

function Labeled({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1">
      <Label>{label}</Label>
      {children}
    </div>
  );
}
