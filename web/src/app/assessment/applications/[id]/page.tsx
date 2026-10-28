"use client";

import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { AssessmentNav } from "@/components/assessment-nav";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import type { Applicant, ApplicationDetail, RuleFlag } from "@/lib/types";

export default function ApplicationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [app, setApp] = useState<ApplicationDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [co, setCo] = useState({ name: "", pan: "", phone: "", dateOfBirth: "" });
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!loading && !user) router.replace("/");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user || !id) return;
    api.application(id).then(setApp).catch((err) => setError(err.message));
  }, [user, id]);

  async function addCo(e: FormEvent) {
    e.preventDefault();
    if (!app) return;
    setBusy(true);
    try {
      await api.addApplicant(app.id, { role: "CoApplicant", ...co });
      setApp(await api.application(app.id));
      setCo({ name: "", pan: "", phone: "", dateOfBirth: "" });
      toast.success("Co-applicant added");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not add co-applicant.");
    } finally {
      setBusy(false);
    }
  }

  if (loading || !user) return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading application…</div>;

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-5xl space-y-4 px-4 py-6">
        <AssessmentNav />
        <Link href="/assessment" className="text-sm text-[#29416f] underline">Back to applications</Link>
        {error ? <p className="text-destructive">{error}</p> : null}
        {app ? (
          <>
            <div>
              <h1 className="font-heading text-2xl font-semibold text-[#10264e]">{app.displayName}</h1>
              <p className="text-sm text-[#5a6a84]">
                {app.code} · {app.loanCategoryName} · ₹{app.loanAmount?.toLocaleString("en-IN") ?? "—"} · {app.bank} · {app.groupId || "—"} · {app.memberId || "—"}
              </p>
            </div>
            <Card className="border border-[#d7deea] bg-white">
              <CardHeader>
                <CardTitle className="text-[#10264e]">Engagement sessions</CardTitle>
                <CardDescription>Plan answers, probes and photos live on the host call and stay here for review.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2">
                {(app.sessions ?? []).length === 0 ? (
                  <p className="text-sm text-[#5a6a84]">No session yet. Open one here or from the host Plan tab.</p>
                ) : (
                  (app.sessions ?? []).map((item) => (
                    <Link key={item.id} href={`/assessment/sessions/${item.id}`} className="flex items-center justify-between rounded-lg border border-[#d7deea] px-3 py-2 hover:border-[#005792]">
                      <span className="text-sm text-[#10264e]">{item.guideName} v{item.guideVersion} · {item.mode}</span>
                      <Badge>{item.coveredCount}/{item.visibleCount} · {item.status}</Badge>
                    </Link>
                  ))
                )}
                <Button
                  className="bg-[#005792] hover:bg-[#004675]"
                  disabled={busy}
                  onClick={() => {
                    setBusy(true);
                    api.ensureSession({ applicationId: app.id })
                      .then((created) => router.push(`/assessment/sessions/${created.id}`))
                      .catch((err) => toast.error(err instanceof Error ? err.message : "Could not open a session."))
                      .finally(() => setBusy(false));
                  }}
                >
                  Open live / review session
                </Button>
              </CardContent>
            </Card>
            {app.applicants.map((person) => (
              <ApplicantCard key={person.id} person={person} onUploaded={async () => setApp(await api.application(app.id))} />
            ))}
            <Card className="border border-[#d7deea] bg-white">
              <CardHeader>
                <CardTitle className="text-[#10264e]">Add co-applicant</CardTitle>
                <CardDescription>Each co-applicant gets their own bureau and bank JSON runs.</CardDescription>
              </CardHeader>
              <CardContent>
                <form className="grid gap-3 sm:grid-cols-2" onSubmit={addCo}>
                  <div className="space-y-1.5 sm:col-span-2">
                    <Label>Name</Label>
                    <Input value={co.name} onChange={(e) => setCo({ ...co, name: e.target.value })} required />
                  </div>
                  <div className="space-y-1.5"><Label>PAN</Label><Input value={co.pan} onChange={(e) => setCo({ ...co, pan: e.target.value })} /></div>
                  <div className="space-y-1.5"><Label>Phone</Label><Input value={co.phone} onChange={(e) => setCo({ ...co, phone: e.target.value })} /></div>
                  <div className="space-y-1.5"><Label>Date of birth</Label><Input value={co.dateOfBirth} onChange={(e) => setCo({ ...co, dateOfBirth: e.target.value })} /></div>
                  <div className="flex items-end">
                    <Button className="bg-[#005792] hover:bg-[#004675]" disabled={busy}>{busy ? "Adding…" : "Add co-applicant"}</Button>
                  </div>
                </form>
              </CardContent>
            </Card>
          </>
        ) : !error ? <p className="text-[#5a6a84]">Loading record…</p> : null}
      </main>
    </div>
  );
}

function latestRunsByKind(person: Applicant) {
  const seen = new Set<string>();
  return person.ruleRuns.filter((run) => {
    if (seen.has(run.kind)) return false;
    seen.add(run.kind);
    return true;
  });
}

function ApplicantCard({ person, onUploaded }: { person: Applicant; onUploaded: () => Promise<void> }) {
  const [kind, setKind] = useState("Bureau");
  const [jsonText, setJsonText] = useState("");
  const [busy, setBusy] = useState(false);
  const currentRuns = latestRunsByKind(person);
  const fired = currentRuns
    .flatMap((r) => r.results)
    .filter((f) => f.fired)
    .sort((a, b) => {
      if (a.severity !== b.severity) return a.severity === "High" ? -1 : 1;
      return a.checkKey.localeCompare(b.checkKey);
    });
  const olderRuns = person.ruleRuns.length - currentRuns.length;

  async function upload(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      const document = JSON.parse(jsonText);
      await api.uploadApplicantInput(person.id, { kind, document, fileName: `${kind.toLowerCase()}.json` });
      toast.success(`${kind} JSON processed`);
      setJsonText("");
      await onUploaded();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "JSON could not be processed.");
    } finally {
      setBusy(false);
    }
  }

  async function fromFile(file: File) {
    setJsonText(await file.text());
  }

  return (
    <Card className="border border-[#d7deea] bg-white">
      <CardHeader>
        <CardTitle className="text-[#10264e]">{person.name}</CardTitle>
        <CardDescription>
          {person.role} · PAN {person.pan || "—"} · {person.phone || "—"} · DOB {person.dateOfBirth || "—"}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-2">
          <Badge>{fired.length} flags fired</Badge>
          {currentRuns.map((run) => (
            <Badge key={run.id} variant="secondary">{run.kind}</Badge>
          ))}
        </div>
        {currentRuns.length > 0 ? (
          <p className="text-sm text-[#5a6a84]">
            Latest {currentRuns.map((run) => `${run.kind} ${new Date(run.ranAt).toLocaleString("en-IN", { timeZone: "Asia/Calcutta" })}`).join(" · ")}
            {olderRuns > 0 ? ` · ${olderRuns} earlier run${olderRuns === 1 ? "" : "s"} kept, not shown` : ""}
          </p>
        ) : null}
        {fired.length === 0 ? (
          <p className="text-sm text-[#5a6a84]">
            {currentRuns.length === 0
              ? "No flags yet. Upload Equifax or Account Aggregator JSON."
              : "Latest bureau and bank runs did not fire any rules."}
          </p>
        ) : (
          <div className="space-y-2">
            {fired.map((flag) => <FlagRow key={flag.id} flag={flag} />)}
          </div>
        )}
        <form className="space-y-2 rounded-lg border border-[#d7deea] p-3" onSubmit={upload}>
          <div className="flex flex-wrap gap-2">
            <select className="h-9 rounded-lg border px-2 text-sm" value={kind} onChange={(e) => setKind(e.target.value)}>
              <option value="Bureau">Bureau (Equifax JSON)</option>
              <option value="BankStatement">Bank statement (AA JSON)</option>
            </select>
            <Input type="file" accept="application/json,.json" onChange={(e) => e.target.files?.[0] && void fromFile(e.target.files[0])} />
          </div>
          <Textarea rows={8} value={jsonText} onChange={(e) => setJsonText(e.target.value)} placeholder="Paste JSON or choose a file" />
          <Button className="bg-[#005792] hover:bg-[#004675]" disabled={busy || !jsonText.trim()}>
            {busy ? "Running rules…" : "Upload and run rules"}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function FlagRow({ flag }: { flag: RuleFlag }) {
  return (
    <div className="rounded-lg border border-[#d7deea] p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge className={flag.severity === "High" ? "bg-[#b42318]" : "bg-[#b54708]"}>{flag.severity}</Badge>
        <span className="font-medium text-[#10264e]">{flag.name}</span>
        {flag.feedsTaskNumber ? <span className="text-xs text-[#5a6a84]">Guide task {String(flag.feedsTaskNumber).padStart(3, "0")}</span> : null}
      </div>
      <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-[#29416f]">
        {(flag.evidence ?? []).map((item, i) => (
          <li key={i}>{item.summary || JSON.stringify(item)}</li>
        ))}
      </ul>
    </div>
  );
}
