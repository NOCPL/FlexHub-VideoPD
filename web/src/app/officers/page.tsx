"use client";

import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Copy, ExternalLink, Plus, UserRound } from "lucide-react";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { useAuth } from "@/components/auth-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api";
import type { User } from "@/lib/types";

export default function OfficersPage() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [officers, setOfficers] = useState<User[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState({ name: "", email: "", temporaryPassword: "" });

  useEffect(() => {
    if (!loading && (!user || user.role !== "Admin")) router.replace("/dashboard");
  }, [loading, user, router]);

  useEffect(() => {
    if (user?.role !== "Admin") return;
    api.creditOfficers().then(setOfficers).catch((err) => setError(err.message));
  }, [user]);

  async function create(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const officer = await api.createCreditOfficer(form);
      setOfficers((list) => [...list, officer].sort((a, b) => a.name.localeCompare(b.name)));
      setForm({ name: "", email: "", temporaryPassword: "" });
      toast.success(`${officer.name} created`, { description: "Their permanent host URL is ready." });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create credit officer.");
    } finally {
      setBusy(false);
    }
  }

  if (loading || !user || user.role !== "Admin") {
    return <div className="min-h-full bg-[#eef1f6] p-8 text-[#5a6a84]">Loading officers…</div>;
  }

  return (
    <div className="min-h-full bg-[#eef1f6]">
      <AppHeader />
      <main className="mx-auto grid max-w-7xl gap-5 px-4 py-6 lg:grid-cols-[minmax(0,1fr)_360px]">
        <section>
          <div className="mb-4">
            <h1 className="font-heading text-2xl font-semibold text-[#10264e]">Credit officers</h1>
            <p className="text-sm text-[#5a6a84]">Manage permanent host URLs for Flexhub Video PD.</p>
          </div>
          <div className="grid gap-3">
            {officers.map((officer) => (
              <Card key={officer.id} className="border border-[#d7deea] bg-white">
                <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center">
                  <div className="flex size-11 items-center justify-center rounded-full bg-[#eaf0f8] text-[#29416f]">
                    <UserRound className="size-5" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="font-semibold text-[#10264e]">{officer.name}</div>
                    <div className="text-sm text-[#5a6a84]">{officer.email}</div>
                    <div className="mt-1 truncate text-xs text-[#5a6a84]">{officer.hostUrl}</div>
                  </div>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        void navigator.clipboard.writeText(officer.hostUrl ?? "");
                        toast.success("Host URL copied");
                      }}
                    >
                      <Copy /> Copy
                    </Button>
                    <Button size="sm" className="bg-[#f7481c] text-white hover:bg-[#d63a11]" asChild>
                      <Link href={officer.hostUrl ? new URL(officer.hostUrl).pathname : "/dashboard"}><ExternalLink /> Open</Link>
                    </Button>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </section>

        <Card className="h-fit border border-[#d7deea] bg-white">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-[#10264e]"><Plus className="size-5" /> Add credit officer</CardTitle>
            <CardDescription className="text-[#5a6a84]">A permanent, unguessable host URL is created automatically.</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="space-y-3" onSubmit={create}>
              <div className="space-y-1.5">
                <Label htmlFor="officer-name">Name</Label>
                <Input id="officer-name" required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="officer-email">Email</Label>
                <Input id="officer-email" type="email" required value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="temporary-password">Temporary password</Label>
                <Input id="temporary-password" type="password" minLength={8} required value={form.temporaryPassword} onChange={(e) => setForm({ ...form, temporaryPassword: e.target.value })} />
              </div>
              {error ? <p className="text-sm text-destructive">{error}</p> : null}
              <Button type="submit" className="w-full bg-[#f7481c] text-white hover:bg-[#d63a11]" disabled={busy}>
                {busy ? "Creating…" : "Create officer"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </main>
    </div>
  );
}
