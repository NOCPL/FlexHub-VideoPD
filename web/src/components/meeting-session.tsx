"use client";

import "@livekit/components-styles";
import {
  LiveKitRoom,
  RoomAudioRenderer,
  useLocalParticipant,
  useParticipants,
  useRoomContext,
  useTracks,
  VideoTrack,
  type TrackReference,
} from "@livekit/components-react";
import { ConnectionState, RoomEvent, Track } from "livekit-client";
import { Camera, Mic, MicOff, MonitorUp, PhoneOff, Video, VideoOff } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api";
import { captureVideoFrame } from "@/lib/crop";
import type { MeetingDetail, User } from "@/lib/types";
import { SnapshotCropDialog } from "@/components/snapshot-crop-dialog";

type Props = {
  token: string;
  serverUrl: string;
  meeting: MeetingDetail;
  user: User;
  onLeave: () => void;
};

export function MeetingSession(props: Props) {
  if (!props.meeting || !props.token) return null;
  return (
    <LiveKitRoom
      token={props.token}
      serverUrl={props.serverUrl}
      connect
      audio
      video
      className="flex min-h-0 flex-1 flex-col"
      onDisconnected={props.onLeave}
      onError={(err) => toast.error(err.message)}
    >
      <RoomAudioRenderer />
      <MeetingBody {...props} />
    </LiveKitRoom>
  );
}

function MeetingBody({ meeting, user, onLeave }: Props) {
  const room = useRoomContext();
  const participants = useParticipants();
  const { localParticipant, isMicrophoneEnabled, isCameraEnabled, isScreenShareEnabled } = useLocalParticipant();
  const [mediaError, setMediaError] = useState<string | null>(null);
  const [captureSrc, setCaptureSrc] = useState<string | null>(null);
  const [ending, setEnding] = useState(false);
  const [callSeconds, setCallSeconds] = useState(0);
  const stageRef = useRef<HTMLDivElement>(null);

  const fieldPresent = participants.some((p) => p.identity.startsWith("fo-"));
  const waiting = (user.role === "CreditOfficer" || user.role === "Admin") && !fieldPresent;

  useEffect(() => {
    const onMedia = () => {
      setMediaError("Camera or microphone was blocked. Allow access in the browser and refresh.");
    };
    room.on(RoomEvent.MediaDevicesError, onMedia);
    return () => {
      room.off(RoomEvent.MediaDevicesError, onMedia);
    };
  }, [room]);

  useEffect(() => {
    if (!fieldPresent) return;
    const timer = window.setInterval(() => setCallSeconds((seconds) => seconds + 1), 1000);
    return () => window.clearInterval(timer);
  }, [fieldPresent]);

  const tracks = useTracks(
    [
      { source: Track.Source.Camera, withPlaceholder: false },
      { source: Track.Source.ScreenShare, withPlaceholder: false },
    ],
    { onlySubscribed: false },
  );
  const cameraTracks = tracks.filter((t): t is TrackReference => Boolean(t.publication?.track));
  const fieldTrack = cameraTracks.find((track) => track.participant.identity.startsWith("fo-"));
  const selfTrack = cameraTracks.find((track) => track.participant.isLocal);

  function capture() {
    const videos = stageRef.current?.querySelectorAll("video") ?? [];
    const remote = Array.from(videos).find((v) => !v.srcObject || v.classList.contains("remote")) ?? videos[0];
    const preferred =
      Array.from(videos).find((v) => (v.parentElement?.getAttribute("data-identity") ?? "").startsWith("fo-")) ??
      remote;
    if (!preferred) {
      toast.error("No video frame is available yet.");
      return;
    }
    try {
      setCaptureSrc(captureVideoFrame(preferred));
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not capture video.");
    }
  }

  async function endVideoPd() {
    setEnding(true);
    try {
      await api.endMeeting(meeting.id);
      room.disconnect();
      onLeave();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not end Video PD.");
      setEnding(false);
    }
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-2xl bg-[#0b1b36] text-white shadow-2xl">
      <div className="flex items-center justify-between gap-3 border-b border-white/10 bg-[#10264e] px-4 py-3 text-sm">
        <div>
          <div className="font-medium">{meeting.memberName || meeting.memberId || "Video PD"}</div>
          <div className="text-[#a9bdd9]">
            {meeting.bank || "—"} · {meeting.branch || "—"} · {meeting.groupId || "—"} · {meeting.memberId || "—"}
          </div>
        </div>
        <div className="flex items-center gap-2">
          {fieldPresent ? (
            <div className="flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1 text-xs">
              <span className="size-2 animate-pulse rounded-full bg-[#f7481c]" />
              <span>REC</span>
            </div>
          ) : null}
          <div className="rounded-full bg-white/10 px-3 py-1 font-mono text-xs">
            {fieldPresent ? formatDuration(callSeconds) : room.state === ConnectionState.Connected ? "Connected" : room.state}
          </div>
        </div>
      </div>

      {mediaError ? <div className="bg-red-900/60 px-4 py-2 text-sm">{mediaError}</div> : null}

      <div ref={stageRef} className="relative min-h-0 flex-1 bg-[radial-gradient(120%_90%_at_50%_15%,#16315c_0%,#0b1b36_60%,#08142a_100%)] p-3">
        {waiting ? (
          <div className="absolute inset-3 z-10 flex flex-col items-center justify-center rounded-xl border border-dashed border-white/20 bg-black/55 text-center">
            <p className="text-lg font-medium">Waiting for the admitted officer to connect</p>
            <p className="mt-2 max-w-md text-sm text-white/70">
              Speak with them in this call. Chat stays on the lobby list for field officers who are still waiting.
            </p>
          </div>
        ) : null}
        {fieldTrack ? (
          <div data-identity={fieldTrack.participant.identity} className="h-full overflow-hidden rounded-xl bg-black">
            <VideoTrack trackRef={fieldTrack} className="h-full w-full object-cover" />
            <div className="absolute bottom-5 left-5 rounded-lg bg-[#0b1b36]/80 px-3 py-2">
              <div className="font-semibold">{fieldTrack.participant.name || "Field officer"}</div>
              <div className="text-xs text-[#a9bdd9]">Field officer</div>
            </div>
          </div>
        ) : (
          <div className="flex h-full flex-col items-center justify-center rounded-xl text-[#9fb4d4]">
            <VideoOff className="mb-3 size-8" />
            <div className="font-medium">{waiting ? "Waiting for the admitted officer to connect" : "Camera is starting…"}</div>
          </div>
        )}
        {selfTrack && fieldTrack && selfTrack !== fieldTrack ? (
          <div data-identity={selfTrack.participant.identity} className="absolute right-5 top-5 h-28 w-44 overflow-hidden rounded-xl border border-white/15 bg-[#132a50]">
            <VideoTrack trackRef={selfTrack} className="h-full w-full object-cover" />
            <span className="absolute bottom-2 left-2 text-xs font-semibold">You</span>
          </div>
        ) : null}
      </div>

      <div className="flex flex-wrap items-center justify-center gap-2 border-t border-white/10 bg-[#08142a] px-4 py-3">
        <Button variant="secondary" onClick={() => localParticipant.setMicrophoneEnabled(!isMicrophoneEnabled)}>
          {isMicrophoneEnabled ? <Mic /> : <MicOff />}
          {isMicrophoneEnabled ? "Mute" : "Unmute"}
        </Button>
        <Button variant="secondary" onClick={() => localParticipant.setCameraEnabled(!isCameraEnabled)}>
          {isCameraEnabled ? <Video /> : <VideoOff />}
          Camera
        </Button>
        <Button variant="secondary" onClick={() => localParticipant.setScreenShareEnabled(!isScreenShareEnabled)}>
          <MonitorUp />
          Share
        </Button>
        <Button variant="secondary" onClick={capture}>
          <Camera />
          Capture still
        </Button>
        {user.role === "CreditOfficer" || user.role === "Admin" ? (
          <Button variant="destructive" onClick={endVideoPd} disabled={ending}>
            <PhoneOff />
            {ending ? "Ending…" : "End PD"}
          </Button>
        ) : (
          <Button variant="destructive" onClick={() => room.disconnect()}>
            <PhoneOff />
            Leave
          </Button>
        )}
      </div>

      <SnapshotCropDialog
        meetingId={meeting.id}
        imageSrc={captureSrc}
        onClose={() => setCaptureSrc(null)}
        onSaved={() => toast.success("Cropped still saved to this Video PD.")}
      />
    </div>
  );
}

function formatDuration(totalSeconds: number) {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return [hours, minutes, seconds].map((part) => String(part).padStart(2, "0")).join(":");
}
