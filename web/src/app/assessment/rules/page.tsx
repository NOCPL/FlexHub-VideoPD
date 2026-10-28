"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { AssessmentNav } from "@/components/assessment-nav";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import type { DeviationRule, LoanCategory } from "@/lib/types";

export default function RulesPage() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [rules, setRules] = useState<DeviationRule[]>([]);
  const [categories, setCategories] = useState<LoanCategory[]>([]);
  const [editing, setEditing] = useState<string | null>(null);

  useEffect(() => {
    if (!loading && (!user || user.role !== "Admin")) router.replace("/assessment");
  }, [loading, user, router]);

  useEffect(() => {
    if (user?.role !== "Admin") return;
    Promise.all([api.deviationRules(), api.loanCategories()])
      .then(([r, c]) => {
        setRules(r);
        setCategories(c);
      })
      .catch((err) => toast.error(err.message));
  }, [user]);

  if (loading || !user) return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading rules…</div>;

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-5xl space-y-4 px-4 py-6">
        <AssessmentNav />
        <div>
          <h1 className="font-heading text-2xl font-semibold text-[#10264e]">Deviation rules</h1>
          <p className="text-sm text-[#5a6a84]">
            Each rule is a coded check plus editable thresholds. Admins change parameters, severity, categories and on/off. A new kind of check is still development work.
          </p>
        </div>
        {rules.length === 0 ? (
          <Card className="border border-[#d7deea] bg-white">
            <CardContent className="py-8 text-sm text-[#5a6a84]">No deviation rules are configured yet.</CardContent>
          </Card>
        ) : null}
        {rules.map((rule) => (
          <Card key={rule.id} className="border border-[#d7deea] bg-white">
            <CardContent className="space-y-3 py-4">
              <div className="flex flex-wrap items-center gap-2">
                <Badge className={rule.severity === "High" ? "bg-[#b42318]" : "bg-[#b54708]"}>{rule.severity}</Badge>
                <span className="font-semibold text-[#10264e]">{rule.name}</span>
                <span className="text-xs text-[#5a6a84]">task {rule.feedsTaskNumber ? String(rule.feedsTaskNumber).padStart(3, "0") : "—"}</span>
                <Badge variant={rule.enabled ? "default" : "secondary"}>{rule.enabled ? "On" : "Off"}</Badge>
                <Button variant="outline" size="sm" onClick={() => setEditing(editing === rule.id ? null : rule.id)}>
                  {editing === rule.id ? "Close" : "Edit"}
                </Button>
              </div>
              <p className="text-sm text-[#29416f]">{rule.description}</p>
              <pre className="overflow-x-auto rounded-lg bg-[#f0f2f5] p-2 text-xs text-[#10264e]">{JSON.stringify(rule.parameters, null, 2)}</pre>
              {editing === rule.id ? (
                <RuleEditor
                  rule={rule}
                  categories={categories}
                  onSaved={(saved) => {
                    setRules((list) => list.map((r) => (r.id === saved.id ? saved : r)));
                    setEditing(null);
                  }}
                />
              ) : null}
            </CardContent>
          </Card>
        ))}
      </main>
    </div>
  );
}

function RuleEditor({
  rule,
  categories,
  onSaved,
}: {
  rule: DeviationRule;
  categories: LoanCategory[];
  onSaved: (rule: DeviationRule) => void;
}) {
  const [description, setDescription] = useState(rule.description);
  const [severity, setSeverity] = useState(rule.severity);
  const [enabled, setEnabled] = useState(rule.enabled);
  const [feeds, setFeeds] = useState(rule.feedsTaskNumber ? String(rule.feedsTaskNumber) : "");
  const [parameters, setParameters] = useState(JSON.stringify(rule.parameters, null, 2));
  const [loanCategoryIds, setLoanCategoryIds] = useState(rule.loanCategoryIds);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true);
    try {
      const parsed = JSON.parse(parameters);
      const saved = await api.updateDeviationRule(rule.id, {
        description,
        severity,
        enabled,
        parameters: parsed,
        loanCategoryIds,
        feedsTaskNumber: feeds ? Number(feeds) : null,
      });
      toast.success("Rule updated");
      onSaved(saved);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Invalid JSON or save failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="grid gap-3 border-t pt-3">
      <div className="space-y-1.5"><Label>Description</Label><Textarea value={description} onChange={(e) => setDescription(e.target.value)} /></div>
      <div className="grid gap-3 sm:grid-cols-3">
        <div className="space-y-1.5">
          <Label>Severity</Label>
          <select className="h-9 w-full rounded-lg border px-2 text-sm" value={severity} onChange={(e) => setSeverity(e.target.value)}>
            <option>High</option>
            <option>Medium</option>
          </select>
        </div>
        <div className="space-y-1.5"><Label>Feeds task</Label><Input value={feeds} onChange={(e) => setFeeds(e.target.value)} /></div>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} /> Enabled</label>
      </div>
      <div className="flex flex-wrap gap-3 text-sm">
        {categories.map((c) => (
          <label key={c.id} className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={loanCategoryIds.includes(c.id)}
              onChange={(e) => setLoanCategoryIds(e.target.checked ? [...loanCategoryIds, c.id] : loanCategoryIds.filter((id) => id !== c.id))}
            />
            {c.name}
          </label>
        ))}
      </div>
      <div className="space-y-1.5"><Label>Parameters JSON</Label><Textarea className="font-mono text-xs" rows={8} value={parameters} onChange={(e) => setParameters(e.target.value)} /></div>
      <Button className="w-fit bg-[#005792] hover:bg-[#004675]" disabled={busy} onClick={() => void save()}>
        {busy ? "Saving…" : "Save rule"}
      </Button>
    </div>
  );
}
