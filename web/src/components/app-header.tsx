"use client";

import { Button } from "@/components/ui/button";
import { useAuth } from "@/components/auth-provider";
import { Video } from "lucide-react";

export function AppHeader() {
  const { user, logout } = useAuth();
  return (
    <header className="border-b border-border/80 bg-card/80 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        <a href="/dashboard" className="flex items-center gap-2 font-semibold tracking-tight">
          <span className="flex size-8 items-center justify-center rounded-md bg-primary text-primary-foreground">
            <Video className="size-4" />
          </span>
          Flexhub Video PD
        </a>
        {user ? (
          <div className="flex items-center gap-3 text-sm">
            <div className="hidden text-right sm:block">
              <div className="font-medium">{user.name}</div>
              <div className="text-muted-foreground text-xs">
                {user.role === "Admin" ? "Admin" : "Credit officer"}
              </div>
            </div>
            <Button variant="outline" size="sm" onClick={() => { logout(); window.location.href = "/"; }}>
              Sign out
            </Button>
          </div>
        ) : null}
      </div>
    </header>
  );
}
