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
import {
  ConnectionState,
  Room,
  RoomEvent,
  Track,
  facingModeFromLocalTrack,
} from "livekit-client";
import {
  Camera,
  Mic,
  MicOff,
  MonitorUp,
  PhoneOff,
  SwitchCamera,
  Video,
  VideoOff,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api";
import { captureVideoFrame } from "@/lib/crop";
import type { MeetingDetail, User, WaitingOfficer } from "@/lib/types";
import { SnapshotCropDialog } from "@/components/snapshot-crop-dialog";

type Props = {
  token: string;
  serverUrl: string;
  meeting: MeetingDetail;
  user: User;
  fieldOfficer?: WaitingOfficer | null;
  gpsLabel?: string | null;
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

function MeetingBody({ meeting, user, fieldOfficer, gpsLabel, onLeave }: Props) {
  const room = useRoomContext();
  const participants = useParticipants();
  const { localParticipant, isMicrophoneEnabled, isCameraEnabled, isScreenShareEnabled } = useLocalParticipant();
  const [mediaError, setMediaError] = useState<string | null>(null);
  const [captureSrc, setCaptureSrc] = useState<string | null>(null);
  const [ending, setEnding] = useState(false);
  const [flipping, setFlipping] = useState(false);
  const [startedAt, setStartedAt] = useState<number | null>(null);
  const now = useNow();
  const stageRef = useRef<HTMLDivElement>(null);

  const remoteOfficerPresent = participants.some(
    (p) => !p.isLocal && p.identity.startsWith("fo-"),
  );
  const waitingForField =
    (user.role === "CreditOfficer" || user.role === "Admin") && !remoteOfficerPresent;
  const timerRunning =
    user.role === "FieldGuest"
      ? room.state === ConnectionState.Connected
      : remoteOfficerPresent;

  useEffect(() => {
    if (!timerRunning) return;
    setStartedAt((current) => current ?? Date.now());
  }, [timerRunning]);

  const callSeconds = startedAt ? Math.max(0, Math.floor((now - startedAt) / 1000)) : 0;

  useEffect(() => {
    const onMedia = () => {
      setMediaError("Camera or microphone was blocked. Allow access in the browser and refresh.");
    };
    room.on(RoomEvent.MediaDevicesError, onMedia);
    return () => {
      room.off(RoomEvent.MediaDevicesError, onMedia);
    };
  }, [room]);

  const tracks = useTracks(
    [
      { source: Track.Source.Camera, withPlaceholder: false },
      { source: Track.Source.ScreenShare, withPlaceholder: false },
    ],
    { onlySubscribed: false },
  );
  const cameraTracks = tracks.filter((t): t is TrackReference => Boolean(t.publication?.track));
  const selfTrack = cameraTracks.find((track) => track.participant.isLocal);
  const remoteTrack = cameraTracks.find((track) => !track.participant.isLocal);
  const mainTrack = remoteTrack ?? selfTrack;

  function capture() {
    const videos = stageRef.current?.querySelectorAll("video") ?? [];
    const preferred =
      Array.from(videos).find((v) => {
        const identity = v.parentElement?.getAttribute("data-identity") ?? "";
        return identity && !identity.startsWith("local-");
      }) ?? videos[0];
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

  async function flipCamera() {
    setFlipping(true);
    try {
      const publication = localParticipant.getTrackPublication(Track.Source.Camera);
      const localTrack = publication?.track;
      const devices = await Room.getLocalDevices("videoinput");
      const currentId = localTrack?.mediaStreamTrack.getSettings().deviceId;
      const currentIndex = Math.max(
        0,
        devices.findIndex((device) => device.deviceId === currentId),
      );

      if (devices.length > 1) {
        const next = devices[(currentIndex + 1) % devices.length];
        await room.switchActiveDevice("videoinput", next.deviceId);
        return;
      }

      const currentFacing =
        localTrack && facingModeFromLocalTrack(localTrack).facingMode === "environment"
          ? "environment"
          : "user";
      const nextFacing = currentFacing === "user" ? "environment" : "user";
      await localParticipant.setCameraEnabled(false);
      await localParticipant.setCameraEnabled(true, { facingMode: nextFacing });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not switch camera.");
      try {
        await localParticipant.setCameraEnabled(true);
      } catch {
        /* ignore */
      }
    } finally {
      setFlipping(false);
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
      {mediaError ? <div className="bg-red-900/60 px-4 py-2 text-sm">{mediaError}</div> : null}

      <div ref={stageRef} className="relative min-h-0 flex-1 bg-black">
        {waitingForField ? (
          <div className="absolute inset-0 z-10 flex flex-col items-center justify-center bg-black/55 text-center">
            <p className="text-lg font-medium">Waiting for the admitted officer to connect</p>
            <p className="mt-2 max-w-md text-sm text-white/70">
              Their video fills this screen. Chat stays on the waiting list.
            </p>
          </div>
        ) : null}
        {mainTrack ? (
          <div
            data-identity={mainTrack.participant.isLocal ? "local-main" : mainTrack.participant.identity}
            className="h-full overflow-hidden bg-black"
          >
            <VideoTrack trackRef={mainTrack} className="h-full w-full object-cover" />
          </div>
        ) : (
          <div className="flex h-full flex-col items-center justify-center text-[#9fb4d4]">
            <VideoOff className="mb-3 size-8" />
            <div className="font-medium">
              {waitingForField ? "Waiting for the admitted officer to connect" : "Camera is starting…"}
            </div>
          </div>
        )}
        {selfTrack && remoteTrack ? (
          <div
            data-identity="local-pip"
            className="absolute right-3 top-14 h-28 w-44 overflow-hidden rounded-xl border border-white/15 bg-[#132a50] shadow-lg"
          >
            <VideoTrack trackRef={selfTrack} className="h-full w-full object-cover" />
            <span className="absolute bottom-2 left-2 text-xs font-semibold">You</span>
          </div>
        ) : null}
        <div className="absolute left-3 top-3 z-20 flex items-center gap-2">
          {timerRunning ? (
            <div className="flex items-center gap-2 rounded-full border border-white/15 bg-[#0b1b36]/80 px-3 py-1 text-xs">
              <span className="size-2 animate-pulse rounded-full bg-[#f7481c]" />
              <span>REC</span>
            </div>
          ) : null}
          <div className="rounded-full bg-[#0b1b36]/80 px-3 py-1 font-mono text-xs">
            {timerRunning
              ? formatDuration(callSeconds)
              : room.state === ConnectionState.Connected
                ? "Connected"
                : room.state}
          </div>
          {user.role === "FieldGuest" && gpsLabel ? (
            <div className="max-w-[min(100%,18rem)] truncate rounded-full bg-[#0b1b36]/80 px-3 py-1 text-xs">
              {gpsLabel}
            </div>
          ) : null}
        </div>
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
        <Button variant="secondary" onClick={() => void flipCamera()} disabled={flipping || !isCameraEnabled}>
          <SwitchCamera />
          {flipping ? "Switching…" : "Flip camera"}
        </Button>
        {user.role === "CreditOfficer" || user.role === "Admin" ? (
          <>
            <Button variant="secondary" onClick={() => localParticipant.setScreenShareEnabled(!isScreenShareEnabled)}>
              <MonitorUp />
              Share
            </Button>
            <Button variant="secondary" onClick={capture}>
              <Camera />
              Capture still
            </Button>
            <Button variant="destructive" onClick={endVideoPd} disabled={ending}>
              <PhoneOff />
              {ending ? "Ending…" : "End PD"}
            </Button>
          </>
        ) : (
          <Button variant="destructive" onClick={() => room.disconnect()}>
            <PhoneOff />
            Leave
          </Button>
        )}
      </div>

      <SnapshotCropDialog
        meetingId={meeting.id}
        fieldOfficerId={fieldOfficer?.id}
        geotag={fieldOfficer}
        imageSrc={captureSrc}
        onClose={() => setCaptureSrc(null)}
        onSaved={() => toast.success("Cropped still saved with the field officer’s GPS and time.")}
      />
    </div>
  );
}

function useNow() {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const tick = () => setNow(Date.now());
    tick();
    const timer = window.setInterval(tick, 1000);
    const onVisible = () => {
      if (document.visibilityState === "visible") tick();
    };
    document.addEventListener("visibilitychange", onVisible);
    window.addEventListener("focus", tick);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
      window.removeEventListener("focus", tick);
    };
  }, []);
  return now;
}

function formatDuration(totalSeconds: number) {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return [hours, minutes, seconds].map((part) => String(part).padStart(2, "0")).join(":");
}
