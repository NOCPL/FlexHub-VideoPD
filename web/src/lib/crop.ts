import type { Area } from "react-easy-crop";
import { geotagOverlayLines, type FieldGeotag } from "@/lib/geotag";

export function captureVideoFrame(video: HTMLVideoElement) {
  if (!video.videoWidth || !video.videoHeight) {
    throw new Error("Video is not ready to capture.");
  }
  const canvas = document.createElement("canvas");
  canvas.width = video.videoWidth;
  canvas.height = video.videoHeight;
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("Could not read the video frame.");
  ctx.drawImage(video, 0, 0);
  return canvas.toDataURL("image/png");
}

export async function getCroppedPng(
  imageSrc: string,
  crop: Area,
  geotag?: FieldGeotag | null,
): Promise<Blob> {
  const image = await loadImage(imageSrc);
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(crop.width));
  canvas.height = Math.max(1, Math.round(crop.height));
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("Could not crop the image.");
  ctx.drawImage(
    image,
    crop.x,
    crop.y,
    crop.width,
    crop.height,
    0,
    0,
    canvas.width,
    canvas.height,
  );
  stampGeotag(ctx, canvas.width, canvas.height, geotag);
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (!blob) reject(new Error("Failed to encode cropped image."));
      else resolve(blob);
    }, "image/png");
  });
}

export function stampGeotag(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  geotag?: FieldGeotag | null,
) {
  const lines = geotagOverlayLines(geotag);
  const pad = Math.max(10, Math.round(width * 0.025));
  const fontSize = Math.max(14, Math.round(width * 0.035));
  const lineHeight = Math.round(fontSize * 1.28);
  const barHeight = pad * 2 + lineHeight * lines.length;
  ctx.fillStyle = "rgba(8, 20, 42, 0.82)";
  ctx.fillRect(0, height - barHeight, width, barHeight);
  ctx.fillStyle = "#f7481c";
  ctx.fillRect(0, height - barHeight, 4, barHeight);
  ctx.font = `600 ${fontSize}px ui-sans-serif, system-ui, sans-serif`;
  ctx.fillStyle = "#ffffff";
  ctx.textBaseline = "alphabetic";
  lines.forEach((line, index) => {
    ctx.fillText(line, pad + 4, height - barHeight + pad + fontSize + index * lineHeight, width - pad * 2);
  });
}

function loadImage(src: string) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error("Could not load captured frame."));
    img.src = src;
  });
}
