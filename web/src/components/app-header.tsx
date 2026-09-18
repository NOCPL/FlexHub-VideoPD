"use client";

import Link from "next/link";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/components/auth-provider";
import { Bell, CalendarPlus, ListVideo, Users, Video } from "lucide-react";

export function AppHeader({
  waitingCount = 0,
  unreadCount = 0,
}: {
  waitingCount?: number;
  unreadCount?: number;
}) {
  const { user, logout } = useAuth();
  return (
    <header className="border-b border-border/80 bg-[#10264e] text-white">
      <div className="mx-auto flex min-h-16 max-w-7xl items-center gap-4 px-4">
        <Link href="/dashboard" className="flex items-center gap-2 font-semibold tracking-tight">
          <span className="flex size-9 items-center justify-center rounded-lg bg-gradient-to-br from-orange-400 to-[#f7481c]">
            <Video className="size-4" />
          </span>
          <span>
            <span className="block">Flexhub Video PD</span>
            <span className="block text-[11px] font-normal text-[#a9bdd9]">Personal discussion room</span>
          </span>
        </Link>
        {user ? (
          <nav className="ml-3 hidden items-center gap-1 md:flex">
            {user.role === "Admin" ? (
              <Button variant="ghost" size="sm" asChild className="text-white hover:bg-white/10 hover:text-white">
                <Link href="/officers"><Users /> Officers</Link>
              </Button>
            ) : null}
            <Button variant="ghost" size="sm" asChild className="text-white hover:bg-white/10 hover:text-white">
              <Link href="/dashboard"><CalendarPlus /> Schedule</Link>
            </Button>
            <Button variant="ghost" size="sm" asChild className="text-white hover:bg-white/10 hover:text-white">
              <Link href="/video-pd"><ListVideo /> Video PD</Link>
            </Button>
          </nav>
        ) : null}
        <div className="flex-1" />
        {user ? (
          <div className="flex items-center gap-3 text-sm">
            <Button
              variant="ghost"
              size="icon"
              className="relative text-white hover:bg-white/10 hover:text-white"
              title={`${waitingCount} waiting, ${unreadCount} unread`}
            >
              <Bell />
              {waitingCount + unreadCount > 0 ? (
                <span className="absolute -right-1 -top-1 min-w-4 rounded-full bg-[#f7481c] px-1 text-center text-[10px] leading-4 text-white">
                  {waitingCount + unreadCount}
                </span>
              ) : null}
            </Button>
            <div className="hidden text-right sm:block">
              <div className="font-medium">{user.name}</div>
              <div className="text-xs text-[#a9bdd9]">
                {user.role === "Admin" ? "Admin" : "Credit officer"}
              </div>
            </div>
            <Button className="border-white/20 bg-white/10 text-white hover:bg-white/20" variant="outline" size="sm" onClick={() => { logout(); window.location.href = "/"; }}>
              Sign out
            </Button>
          </div>
        ) : null}
      </div>
    </header>
  );
}
