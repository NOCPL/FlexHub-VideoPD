"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { AssessmentNav } from "@/components/assessment-nav";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import type { TemplateDetail, TemplateField } from "@/lib/types";

export default function TemplateDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [template, setTemplate] = useState<TemplateDetail | null>(null);
  const [editing, setEditing] = useState<number | null>(null);

  useEffect(() => {
    if (!loading && (!user || user.role !== "Admin")) router.replace("/assessment");
  }, [loading, user, router]);

  useEffect(() => {
    if (user?.role !== "Admin" || !id) return;
    api.template(id).then(setTemplate).catch((err) => toast.error(err.message));
  }, [user, id]);

  const grouped = useMemo(() => {
    if (!template) return [];
    return template.sections.map((section) => ({
      section,
      fields: template.fields.filter((field) => field.sectionId === section.id),
    }));
  }, [template]);

  async function setStatus(status: string) {
    if (!template) return;
    try {
      setTemplate(await api.updateTemplate(template.id, {
        name: template.name,
        memoType: template.memoType,
        status,
        loanCategoryIds: template.loanCategoryIds,
        fileCategories: template.fileCategories,
      }));
      toast.success(status === "Active" ? "Template is active" : `Marked ${status}`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not update the template.");
    }
  }

  if (loading || !user) return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading template…</div>;

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-5xl space-y-4 px-4 py-6">
        <AssessmentNav />
        <Link href="/assessment/templates" className="text-sm text-[#29416f] underline">Back to templates</Link>
        {template ? (
          <>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h1 className="font-heading text-2xl font-semibold text-[#10264e]">{template.name}</h1>
                <p className="text-sm text-[#5a6a84]">
                  {template.memoType} · {template.code} · v{template.version} · {template.fields.length} fields in {template.sections.length} sections
                </p>
                <p className="mt-1 text-sm text-[#5a6a84]">File categories: {template.fileCategories.join(", ") || "—"}</p>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <Badge>{template.status}</Badge>
                {template.status !== "Active" ? (
                  <Button className="bg-[#005792] hover:bg-[#004675]" size="sm" onClick={() => void setStatus("Active")}>Make active</Button>
                ) : (
                  <Button variant="outline" size="sm" onClick={() => void setStatus("Archived")}>Archive</Button>
                )}
              </div>
            </div>
            {grouped.map(({ section, fields }) => (
              <Card key={section.id} className="border border-[#d7deea] bg-white">
                <CardHeader>
                  <CardTitle className="text-[#10264e]">{section.name}</CardTitle>
                  <p className="text-sm text-[#5a6a84]">{section.source} · {fields.length} fields</p>
                </CardHeader>
                <CardContent className="space-y-3">
                  {fields.map((field) => (
                    <FieldRow
                      key={field.id}
                      field={field}
                      open={editing === field.number}
                      onToggle={() => setEditing(editing === field.number ? null : field.number)}
                      onSaved={(next) => {
                        setTemplate(next);
                        setEditing(null);
                      }}
                      templateId={template.id}
                    />
                  ))}
                </CardContent>
              </Card>
            ))}
          </>
        ) : <p className="text-[#5a6a84]">Loading template…</p>}
      </main>
    </div>
  );
}

function FieldRow({
  field,
  open,
  onToggle,
  onSaved,
  templateId,
}: {
  field: TemplateField;
  open: boolean;
  onToggle: () => void;
  onSaved: (template: TemplateDetail) => void;
  templateId: string;
}) {
  const [label, setLabel] = useState(field.label);
  const [question, setQuestion] = useState(field.question);
  const [options, setOptions] = useState(field.options.join(", "));
  const [busy, setBusy] = useState(false);
  const datatype = field.datatype === "Options" ? `String (${field.options.length})` : field.datatype;

  async function save() {
    setBusy(true);
    try {
      const next = await api.updateTemplateField(templateId, field.number, {
        label,
        question,
        datatype: field.datatype,
        options: options.split(",").map((item) => item.trim()).filter(Boolean),
        allowMultiple: field.allowMultiple,
        conditionFieldNumber: field.conditionFieldNumber,
        conditionChoices: field.conditionChoices,
        source: field.source,
        photoType: field.photoType,
      });
      toast.success(`Field ${String(field.number).padStart(2, "0")} saved`);
      onSaved(next);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save the field.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-lg border border-[#d7deea] p-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <div className="text-sm font-semibold text-[#10264e]">
            {String(field.number).padStart(2, "0")} · {field.label}
          </div>
          <p className="mt-1 text-sm text-[#29416f]">{field.question}</p>
          <p className="mt-1 text-xs text-[#5a6a84]">
            {datatype}{field.allowMultiple ? " · multi-select" : ""} · {field.source}
            {field.conditionFieldNumber ? ` · when ${String(field.conditionFieldNumber).padStart(2, "0")} in (${field.conditionChoices.join(", ")})` : ""}
            {field.taskNumber ? ` · guide task ${String(field.taskNumber).padStart(3, "0")}` : ""}
            {field.ruleLink ? ` · ${field.ruleLink}` : ""}
            {field.jsonShape ? ` · ${field.jsonShape}` : ""}
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={onToggle}>{open ? "Close" : "Edit"}</Button>
      </div>
      {open ? (
        <div className="mt-3 space-y-2">
          <div className="space-y-1.5">
            <Label>Label (API key)</Label>
            <Input value={label} onChange={(e) => setLabel(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Question</Label>
            <Textarea rows={3} value={question} onChange={(e) => setQuestion(e.target.value)} />
          </div>
          {field.datatype === "Options" ? (
            <div className="space-y-1.5">
              <Label>Options, comma separated</Label>
              <Input value={options} onChange={(e) => setOptions(e.target.value)} />
            </div>
          ) : null}
          <Button className="bg-[#005792] hover:bg-[#004675]" size="sm" disabled={busy} onClick={() => void save()}>
            {busy ? "Saving…" : "Save field"}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
