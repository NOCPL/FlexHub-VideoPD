"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { AssessmentNav } from "@/components/assessment-nav";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import type { MemoDetail, MemoField } from "@/lib/types";

function displayValue(value: string | null) {
  if (!value) return "";
  const trimmed = value.trim();
  if (trimmed.startsWith("[")) {
    try {
      const parsed = JSON.parse(trimmed) as unknown;
      if (Array.isArray(parsed) && parsed.every((item) => typeof item === "string")) return parsed.join(", ");
    } catch {
      return value;
    }
  }
  return value;
}

export default function MemoPage() {
  const { id } = useParams<{ id: string }>();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [memo, setMemo] = useState<MemoDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!loading && !user) router.replace("/");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user || !id) return;
    api.memo(id).then(setMemo).catch((err) => setError(err.message));
  }, [user, id]);

  async function process() {
    if (!memo) return;
    setBusy(true);
    try {
      setMemo(await api.processMemo(memo.id));
      toast.success("Extraction finished");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not process the memo.");
    } finally {
      setBusy(false);
    }
  }

  async function download() {
    if (!memo) return;
    try {
      await api.downloadMemo(memo.id, `${memo.applicationCode}-cam-v${memo.templateVersion}.json`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not download the memo.");
    }
  }

  if (loading || !user) return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading memo…</div>;

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-5xl space-y-4 px-4 py-6">
        <AssessmentNav />
        {error ? <p className="text-destructive">{error}</p> : null}
        {memo ? (
          <>
            <Link href={`/assessment/applications/${memo.applicationId}`} className="text-sm text-[#29416f] underline">
              Back to {memo.applicationCode}
            </Link>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h1 className="font-heading text-2xl font-semibold text-[#10264e]">{memo.displayName}</h1>
                <p className="text-sm text-[#5a6a84]">
                  {memo.templateName} · v{memo.templateVersion} · {memo.applicationCode}
                </p>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <Badge>{memo.status}</Badge>
                {memo.status === "Processed" ? (
                  <Button variant="outline" onClick={() => void download()}>Download CAM</Button>
                ) : null}
                <Button className="bg-[#005792] hover:bg-[#004675]" disabled={busy} onClick={() => void process()}>
                  {busy ? "Running…" : memo.status === "Processed" ? "Re-run extraction" : "Run extraction"}
                </Button>
              </div>
            </div>
            {memo.status !== "Processed" ? (
              <Card className="border border-[#d7deea] bg-white">
                <CardHeader>
                  <CardTitle className="text-[#10264e]">Memo is pending</CardTitle>
                </CardHeader>
                <CardContent className="space-y-2 text-sm text-[#29416f]">
                  <p>The CAM can be viewed and downloaded after extraction. This template has {memo.fieldCount} fields.</p>
                  <p>Run extraction to fill fields from the guided session, bureau and bank flags, and captured photos. Transcript questions stay empty until a transcript is stored.</p>
                </CardContent>
              </Card>
            ) : (
              <>
                <p className="text-sm text-[#5a6a84]">
                  {memo.filledCount} of {memo.fieldCount} fields have a value.
                  {memo.processedAt ? ` Processed ${new Date(memo.processedAt).toLocaleString("en-IN", { timeZone: "Asia/Calcutta" })}.` : ""}
                </p>
                {memo.transcriptNote ? (
                  <p className="rounded-lg border border-[#d7deea] bg-white px-3 py-2 text-sm text-[#29416f]">{memo.transcriptNote}</p>
                ) : null}
                {memo.sections.map((section) => (
                  <Card key={section.name} className="border border-[#d7deea] bg-white">
                    <CardHeader>
                      <CardTitle className="text-[#10264e]">{section.name}</CardTitle>
                      <p className="text-sm text-[#5a6a84]">{section.source}</p>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      {section.fields.map((field) => (
                        <MemoFieldRow key={field.number} memoId={memo.id} field={field} onSaved={setMemo} />
                      ))}
                    </CardContent>
                  </Card>
                ))}
              </>
            )}
          </>
        ) : !error ? <p className="text-[#5a6a84]">Loading memo…</p> : null}
      </main>
    </div>
  );
}

function MemoFieldRow({ memoId, field, onSaved }: { memoId: string; field: MemoField; onSaved: (memo: MemoDetail) => void }) {
  const [draft, setDraft] = useState(displayValue(field.value));
  const [busy, setBusy] = useState(false);
  const shown = displayValue(field.value);

  useEffect(() => {
    setDraft(displayValue(field.value));
  }, [field.value]);

  if (!field.applicable) {
    return (
      <div className="rounded-lg border border-dashed border-[#d7deea] px-3 py-2 text-sm text-[#5a6a84]">
        {String(field.number).padStart(2, "0")} · {field.label} — hidden until field {String(field.conditionFieldNumber).padStart(2, "0")} is {field.conditionChoices.join(" or ")}
      </div>
    );
  }

  async function save() {
    setBusy(true);
    try {
      onSaved(await api.saveMemoField(memoId, field.number, draft));
      toast.success(`Field ${String(field.number).padStart(2, "0")} saved`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save the field.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-lg border border-[#d7deea] p-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-semibold text-[#10264e]">{String(field.number).padStart(2, "0")} · {field.label}</span>
        {field.stage === "manual" ? <Badge variant="secondary">Credit officer</Badge> : null}
        {field.stage === "extraction" ? <Badge variant="secondary">Extracted</Badge> : null}
      </div>
      <p className="mt-1 text-sm text-[#29416f]">{field.question}</p>
      {field.jsonShape ? <p className="mt-1 text-xs text-[#5a6a84]">{field.jsonShape}</p> : null}
      {shown ? (
        <pre className="mt-2 whitespace-pre-wrap rounded-md bg-[#f7f9fc] p-2 text-sm text-[#10264e]">{field.datatype === "Json" ? field.value : shown}</pre>
      ) : (
        <p className="mt-2 text-sm text-[#5a6a84]">No value yet.</p>
      )}
      {field.sourceLabel ? (
        <p className="mt-1 text-xs text-[#5a6a84]">Source: {field.sourceLabel}{field.sourceRef ? ` · ${field.sourceRef}` : ""}</p>
      ) : null}
      <div className="mt-2 space-y-2">
        <Textarea rows={field.datatype === "Json" ? 6 : 2} value={draft} onChange={(e) => setDraft(e.target.value)} />
        <Button variant="outline" size="sm" disabled={busy || draft.trim() === shown.trim()} onClick={() => void save()}>
          {busy ? "Saving…" : "Save as credit officer value"}
        </Button>
      </div>
    </div>
  );
}
