"use client";

import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { AssessmentNav } from "@/components/assessment-nav";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api";
import type { ApplicationListItem, LoanCategory } from "@/lib/types";

export default function AssessmentPage() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [apps, setApps] = useState<ApplicationListItem[]>([]);
  const [categories, setCategories] = useState<LoanCategory[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    loanCategoryId: "",
    displayName: "",
    bank: "SBI",
    branch: "Mumbai Central",
    groupId: "",
    memberId: "",
    loanAmount: "900000",
    pan: "",
    phone: "",
    dateOfBirth: "",
  });

  useEffect(() => {
    if (!loading && !user) router.replace("/");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user) return;
    Promise.all([api.applications(), api.loanCategories()])
      .then(([list, cats]) => {
        setApps(list);
        setCategories(cats);
        setForm((f) => ({ ...f, loanCategoryId: f.loanCategoryId || cats[0]?.id || "" }));
      })
      .catch((err) => setError(err.message));
  }, [user]);

  async function create(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const created = await api.createApplication({
        loanCategoryId: form.loanCategoryId,
        displayName: form.displayName,
        bank: form.bank,
        branch: form.branch,
        groupId: form.groupId,
        memberId: form.memberId,
        loanAmount: form.loanAmount ? Number(form.loanAmount) : null,
        pan: form.pan || undefined,
        phone: form.phone || undefined,
        dateOfBirth: form.dateOfBirth || undefined,
      });
      toast.success(`Application ${created.code} created`);
      router.push(`/assessment/applications/${created.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create the application.");
    } finally {
      setBusy(false);
    }
  }

  if (loading || !user) return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading assessment…</div>;

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-7xl px-4 py-6">
        <AssessmentNav />
        <div className="mb-4">
          <h1 className="font-heading text-2xl font-semibold text-[#10264e]">Guided assessment</h1>
          <p className="text-sm text-[#5a6a84]">
            Upload bureau and bank JSON to raise deviation flags before Video PD. Guides and rules are configuration, not code.
          </p>
        </div>
        <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_360px]">
          <section className="space-y-3">
            {error ? <p className="text-destructive text-sm">{error}</p> : null}
            {apps.length === 0 ? (
              <Card className="border border-[#d7deea] bg-white">
                <CardContent className="py-8 text-sm text-[#5a6a84]">No applications yet.</CardContent>
              </Card>
            ) : (
              apps.map((app) => (
                <Link key={app.id} href={`/assessment/applications/${app.id}`}>
                  <Card className="border border-[#d7deea] bg-white hover:border-[#005792]">
                    <CardContent className="flex flex-col gap-2 py-4 sm:flex-row sm:items-center">
                      <div className="min-w-0 flex-1">
                        <div className="font-semibold text-[#10264e]">{app.displayName}</div>
                        <div className="text-sm text-[#5a6a84]">
                          {app.code} · {app.loanCategoryName} · {app.bank || "—"} · {app.groupId || "—"} · {app.memberId || "—"}
                        </div>
                      </div>
                      <Badge>{app.firedFlagCount} flags</Badge>
                    </CardContent>
                  </Card>
                </Link>
              ))
            )}
          </section>
          <Card className="h-fit border border-[#d7deea] bg-white">
            <CardHeader>
              <CardTitle className="text-[#10264e]">New application</CardTitle>
              <CardDescription>Creates a primary applicant. Upload Equifax and AA JSON on the next screen.</CardDescription>
            </CardHeader>
            <CardContent>
              <form className="space-y-3" onSubmit={create}>
                <div className="space-y-1.5">
                  <Label htmlFor="cat">Loan category</Label>
                  <select
                    id="cat"
                    className="h-9 w-full rounded-lg border border-[#d7deea] bg-white px-2 text-sm"
                    value={form.loanCategoryId}
                    onChange={(e) => setForm({ ...form, loanCategoryId: e.target.value })}
                  >
                    {categories.map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>
                <Field label="Household / applicant" value={form.displayName} onChange={(v) => setForm({ ...form, displayName: v })} required />
                <Field label="Loan amount (₹)" value={form.loanAmount} onChange={(v) => setForm({ ...form, loanAmount: v })} />
                <div className="grid grid-cols-2 gap-2">
                  <Field label="Bank" value={form.bank} onChange={(v) => setForm({ ...form, bank: v })} />
                  <Field label="Branch" value={form.branch} onChange={(v) => setForm({ ...form, branch: v })} />
                  <Field label="Group ID" value={form.groupId} onChange={(v) => setForm({ ...form, groupId: v })} />
                  <Field label="Member ID" value={form.memberId} onChange={(v) => setForm({ ...form, memberId: v })} />
                  <Field label="PAN" value={form.pan} onChange={(v) => setForm({ ...form, pan: v })} />
                  <Field label="Phone" value={form.phone} onChange={(v) => setForm({ ...form, phone: v })} />
                </div>
                <Field label="Date of birth" value={form.dateOfBirth} onChange={(v) => setForm({ ...form, dateOfBirth: v })} />
                <Button className="w-full bg-[#005792] hover:bg-[#004675]" disabled={busy || !form.displayName || !form.loanCategoryId}>
                  {busy ? "Creating…" : "Create application"}
                </Button>
              </form>
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  required,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  required?: boolean;
}) {
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <Input value={value} required={required} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}
