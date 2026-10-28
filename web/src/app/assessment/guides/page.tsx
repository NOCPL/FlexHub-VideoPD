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
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api";
import type { GuideListItem, LoanCategory } from "@/lib/types";

export default function GuidesPage() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [guides, setGuides] = useState<GuideListItem[]>([]);
  const [categories, setCategories] = useState<LoanCategory[]>([]);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({ code: "", name: "", loanCategoryId: "" });

  useEffect(() => {
    if (!loading && (!user || user.role !== "Admin")) router.replace("/assessment");
  }, [loading, user, router]);

  useEffect(() => {
    if (user?.role !== "Admin") return;
    Promise.all([api.guides(), api.loanCategories()]).then(([g, c]) => {
      setGuides(g);
      setCategories(c);
      setForm((f) => ({ ...f, loanCategoryId: f.loanCategoryId || c[0]?.id || "" }));
    }).catch((err) => toast.error(err.message));
  }, [user]);

  async function create(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      const guide = await api.createGuide({
        code: form.code,
        name: form.name,
        eventType: "Interview",
        loanCategoryIds: form.loanCategoryId ? [form.loanCategoryId] : [],
        status: "Draft",
      });
      toast.success("Draft guide created");
      router.push(`/assessment/guides/${guide.id}`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not create guide.");
    } finally {
      setBusy(false);
    }
  }

  async function clone(id: string, name: string) {
    try {
      const copy = await api.cloneGuide(id, { name: `${name} copy` });
      toast.success(`Version ${copy.version} created`);
      router.push(`/assessment/guides/${copy.id}`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not clone.");
    }
  }

  if (loading || !user) return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading guides…</div>;

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-7xl px-4 py-6">
        <AssessmentNav />
        <h1 className="mb-4 font-heading text-2xl font-semibold text-[#10264e]">Engagement guides</h1>
        <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_320px]">
          <div className="space-y-3">
            {guides.length === 0 ? (
              <Card className="border border-[#d7deea] bg-white">
                <CardContent className="py-8 text-sm text-[#5a6a84]">No guides yet. Create a draft or clone an existing one.</CardContent>
              </Card>
            ) : null}
            {guides.map((guide) => (
              <Card key={guide.id} className="border border-[#d7deea] bg-white">
                <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center">
                  <div className="min-w-0 flex-1">
                    <div className="font-semibold text-[#10264e]">{guide.name}</div>
                    <div className="text-sm text-[#5a6a84]">{guide.code} · {guide.eventType} · v{guide.version}</div>
                  </div>
                  <Badge>{guide.status}</Badge>
                  <Button variant="outline" size="sm" asChild><Link href={`/assessment/guides/${guide.id}`}>Edit</Link></Button>
                  <Button variant="outline" size="sm" onClick={() => void clone(guide.id, guide.name)}>Clone / version</Button>
                </CardContent>
              </Card>
            ))}
          </div>
          <Card className="h-fit border border-[#d7deea] bg-white">
            <CardHeader><CardTitle className="text-[#10264e]">New guide</CardTitle></CardHeader>
            <CardContent>
              <form className="space-y-3" onSubmit={create}>
                <div className="space-y-1.5"><Label>Code</Label><Input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} required /></div>
                <div className="space-y-1.5"><Label>Name</Label><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></div>
                <div className="space-y-1.5">
                  <Label>Loan category</Label>
                  <select className="h-9 w-full rounded-lg border px-2 text-sm" value={form.loanCategoryId} onChange={(e) => setForm({ ...form, loanCategoryId: e.target.value })}>
                    {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </select>
                </div>
                <Button className="w-full bg-[#005792] hover:bg-[#004675]" disabled={busy}>{busy ? "Saving…" : "Create draft"}</Button>
              </form>
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}
