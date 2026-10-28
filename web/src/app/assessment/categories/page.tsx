"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { AssessmentNav } from "@/components/assessment-nav";
import { useAuth } from "@/components/auth-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api";
import type { LoanCategory } from "@/lib/types";

export default function CategoriesPage() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [categories, setCategories] = useState<LoanCategory[]>([]);
  const [form, setForm] = useState({ code: "", name: "", description: "" });
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!loading && (!user || user.role !== "Admin")) router.replace("/assessment");
  }, [loading, user, router]);

  useEffect(() => {
    if (user?.role !== "Admin") return;
    api.loanCategories().then(setCategories).catch((err) => toast.error(err.message));
  }, [user]);

  async function create(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      const created = await api.createLoanCategory(form);
      setCategories((list) => [...list, created].sort((a, b) => a.name.localeCompare(b.name)));
      setForm({ code: "", name: "", description: "" });
      toast.success("Loan category added");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not add category.");
    } finally {
      setBusy(false);
    }
  }

  if (loading || !user) return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading categories…</div>;

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-4xl px-4 py-6">
        <AssessmentNav />
        <h1 className="mb-4 font-heading text-2xl font-semibold text-[#10264e]">Loan categories</h1>
        <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_300px]">
          <div className="space-y-3">
            {categories.length === 0 ? (
              <Card className="border border-[#d7deea] bg-white">
                <CardContent className="py-8 text-sm text-[#5a6a84]">No loan categories yet.</CardContent>
              </Card>
            ) : null}
            {categories.map((c) => (
              <CategoryCard
                key={c.id}
                category={c}
                onSaved={(saved) => setCategories((list) => list.map((x) => (x.id === saved.id ? saved : x)))}
              />
            ))}
          </div>
          <Card className="h-fit border border-[#d7deea] bg-white">
            <CardHeader><CardTitle className="text-[#10264e]">Add category</CardTitle></CardHeader>
            <CardContent>
              <form className="space-y-3" onSubmit={create}>
                <div className="space-y-1.5"><Label>Code</Label><Input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} required /></div>
                <div className="space-y-1.5"><Label>Name</Label><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></div>
                <div className="space-y-1.5"><Label>Description</Label><Input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
                <Button className="w-full bg-[#005792] hover:bg-[#004675]" disabled={busy}>{busy ? "Saving…" : "Add"}</Button>
              </form>
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}

function CategoryCard({
  category,
  onSaved,
}: {
  category: LoanCategory;
  onSaved: (category: LoanCategory) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({
    code: category.code,
    name: category.name,
    description: category.description ?? "",
  });
  const [busy, setBusy] = useState(false);

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      const saved = await api.updateLoanCategory(category.id, form);
      onSaved(saved);
      setEditing(false);
      toast.success("Category updated");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not update category.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card className="border border-[#d7deea] bg-white">
      <CardContent className="py-4">
        {editing ? (
          <form className="space-y-3" onSubmit={save}>
            <div className="space-y-1.5"><Label>Code</Label><Input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label>Name</Label><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label>Description</Label><Input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
            <div className="flex gap-2">
              <Button className="bg-[#005792] hover:bg-[#004675]" disabled={busy}>{busy ? "Saving…" : "Save"}</Button>
              <Button type="button" variant="outline" onClick={() => setEditing(false)}>Cancel</Button>
            </div>
          </form>
        ) : (
          <div className="flex flex-col gap-2 sm:flex-row sm:items-start">
            <div className="min-w-0 flex-1">
              <div className="font-semibold text-[#10264e]">{category.name}</div>
              <div className="text-sm text-[#5a6a84]">{category.code}</div>
              {category.description ? <p className="mt-1 text-sm text-[#29416f]">{category.description}</p> : null}
            </div>
            <Button variant="outline" size="sm" onClick={() => setEditing(true)}>Edit</Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
