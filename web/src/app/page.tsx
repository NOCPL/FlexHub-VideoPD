"use client";

import { FormEvent, Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Video } from "lucide-react";
import { useAuth } from "@/components/auth-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api";

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="p-8 text-muted-foreground">Loading…</div>}>
      <LoginForm />
    </Suspense>
  );
}

function LoginForm() {
  const { user, loading, login } = useAuth();
  const router = useRouter();
  const search = useSearchParams();
  const next = search.get("next") || "/dashboard";
  const [email, setEmail] = useState("credit@visit.local");
  const [password, setPassword] = useState("Credit@123");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!loading && user) router.replace(next);
  }, [loading, user, router, next]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(email, password);
      router.replace(next);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not sign in.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex min-h-full flex-1 items-center justify-center bg-[radial-gradient(1200px_circle_at_10%_0%,#d7efe6,transparent_45%),linear-gradient(#f6f1e8,#eef3ef)] px-4 py-10">
      <div className="grid w-full max-w-4xl gap-8 md:grid-cols-[1.1fr_0.9fr] md:items-center">
        <div className="space-y-4">
          <div className="flex items-center gap-2 text-sm font-medium text-emerald-900/80">
            <Video className="size-4" />
            Links for Angular and Kotlin
          </div>
          <h1 className="text-4xl font-semibold tracking-tight text-emerald-950">Flexhub Video PD</h1>
          <p className="max-w-md text-emerald-950/70">
            Credit officers sign in to schedule a visit. That mints a static host URL for Angular and an
            open field-officer URL for Kotlin with bank, group, and member query parameters.
          </p>
        </div>
        <Card>
          <CardHeader>
            <CardTitle>Sign in</CardTitle>
            <CardDescription>Schedule meetings or open your static host room.</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="space-y-3" onSubmit={onSubmit}>
              <div className="space-y-1.5">
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                />
              </div>
              {error ? <p className="text-destructive text-sm">{error}</p> : null}
              <Button type="submit" className="w-full" disabled={busy}>
                {busy ? "Signing in…" : "Continue"}
              </Button>
            </form>
            <div className="mt-4 grid gap-2 text-sm">
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setEmail("credit@visit.local");
                  setPassword("Credit@123");
                }}
              >
                Credit officer — Priya Shah
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setEmail("admin@visit.local");
                  setPassword("Admin@123");
                }}
              >
                Admin — schedule on behalf of a CO
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
