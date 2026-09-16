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

  const tracks = useTracks(
    [
      { source: Track.Source.Camera, withPlaceholder: false },
      { source: Track.Source.ScreenShare, withPlaceholder: false },
    ],
    { onlySubscribed: false },
  );
  const cameraTracks = tracks.filter((t): t is TrackReference => Boolean(t.publication?.track));

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

  async function endVisit() {
    setEnding(true);
    try {
      await api.endMeeting(meeting.id);
      room.disconnect();
      onLeave();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not end the visit.");
      setEnding(false);
    }
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col bg-[#0f1614] text-white">
      <div className="flex items-center justify-between gap-3 border-b border-white/10 px-4 py-3 text-sm">
        <div>
          <div className="font-medium">{meeting.memberName || meeting.memberId || "Visit"}</div>
          <div className="text-white/60">
            {meeting.bank} · Group {meeting.groupId} · Member {meeting.memberId}
          </div>
        </div>
        <div className="rounded-full bg-white/10 px-3 py-1 text-xs">
          {waiting ? "Admitted — connecting" : room.state === ConnectionState.Connected ? "Live" : room.state}
        </div>
      </div>

      {mediaError ? <div className="bg-red-900/60 px-4 py-2 text-sm">{mediaError}</div> : null}

      <div ref={stageRef} className="relative min-h-0 flex-1 p-3">
        {waiting ? (
          <div className="absolute inset-3 z-10 flex flex-col items-center justify-center rounded-xl border border-dashed border-white/20 bg-black/55 text-center">
            <p className="text-lg font-medium">Waiting for the admitted officer to connect</p>
            <p className="mt-2 max-w-md text-sm text-white/70">
              Speak with them in this call. Chat stays on the lobby list for field officers who are still waiting.
            </p>
          </div>
        ) : null}
        <div className={`grid h-full gap-3 ${cameraTracks.length > 1 ? "grid-cols-1 md:grid-cols-2" : "grid-cols-1"}`}>
          {cameraTracks.length === 0 ? (
            <div className="flex items-center justify-center rounded-xl bg-black/40 text-white/60">
              Camera is starting…
            </div>
          ) : (
            cameraTracks.map((trackRef) => (
              <div
                key={`${trackRef.participant.sid}-${trackRef.source}`}
                data-identity={trackRef.participant.identity}
                className="relative overflow-hidden rounded-xl bg-black"
              >
                <VideoTrack trackRef={trackRef} className="h-full w-full object-cover" />
                <div className="absolute bottom-2 left-2 rounded bg-black/60 px-2 py-1 text-xs">
                  {trackRef.participant.name || trackRef.participant.identity}
                  {trackRef.participant.isLocal ? " (you)" : ""}
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      <div className="flex flex-wrap items-center justify-center gap-2 border-t border-white/10 px-4 py-3">
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
          <Button variant="destructive" onClick={endVisit} disabled={ending}>
            <PhoneOff />
            {ending ? "Ending…" : "End visit"}
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
        onSaved={() => toast.success("Cropped still saved to this visit.")}
      />
    </div>
  );
}
