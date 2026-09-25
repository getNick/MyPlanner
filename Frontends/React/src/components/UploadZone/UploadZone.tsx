import React, { useRef, useState, useEffect } from "react";
import {
  Upload,
  Sparkles,
  Image as ImageIcon,
  AlertCircle,
} from "lucide-react";

interface UploadZoneProps {
  onImageSelected: (file: File) => void;
  isLoading: boolean;
  loadingStep: string;
  error: string | null;
  onClearImage?: () => void;
  // Called when a dropped/selected file is not an image. When provided the
  // component reports inline instead of using alert(); otherwise it falls back
  // to alert() so existing callers keep their current behavior.
  onFileTypeError?: (message: string) => void;
}

export default function UploadZone({
  onImageSelected,
  isLoading,
  loadingStep,
  error,
  onClearImage,
  onFileTypeError,
}: UploadZoneProps) {
  const [isDragOver, setIsDragOver] = useState(false);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    return () => {
      if (previewUrl) {
        URL.revokeObjectURL(previewUrl);
      }
    };
  }, [previewUrl]);

  const hasImage = !!previewUrl;
  const imageUrl = previewUrl;

  const processFile = (file: File) => {
    if (!file.type.startsWith("image/")) {
      const message = "Please upload an image file (PNG, JPEG, WEBP)."
      if (onFileTypeError) onFileTypeError(message);
      else alert(message);
      return;
    }

    if (previewUrl) {
      URL.revokeObjectURL(previewUrl);
    }
    const url = URL.createObjectURL(file);
    setPreviewUrl(url);
    onImageSelected(file);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  };

  const handleDragLeave = () => {
    setIsDragOver(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      processFile(e.dataTransfer.files[0]);
    }
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      processFile(e.target.files[0]);
    }
  };

  const triggerFileSelect = () => {
    fileInputRef.current?.click();
  };

  return (
    <div className="w-full">
      <div
        id="dropzone"
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={isLoading ? undefined : triggerFileSelect}
        className={`relative flex flex-col items-center justify-center min-h-[220px] rounded-sm border-2 border-dashed p-8 text-center transition-all cursor-pointer ${
          isDragOver
            ? "border-zinc-800 bg-slate-50"
            : isLoading
              ? "border-slate-200 bg-slate-50/50 cursor-not-allowed"
              : "border-slate-300 hover:border-zinc-800 bg-white hover:bg-slate-50"
        }`}
      >
        <input
          id="file-input"
          type="file"
          ref={fileInputRef}
          onChange={handleFileChange}
          accept="image/*"
          className="hidden"
          disabled={isLoading}
        />

        {hasImage && !isLoading ? (
          <div className="relative group w-full flex flex-col items-center">
            {/* The Image Preview Container */}
            <div className="relative max-h-[300px] overflow-hidden rounded-sm border border-slate-200 shadow-xs transition-all duration-300 group-hover:shadow-md group-hover:border-slate-300 bg-slate-50 flex items-center justify-center p-1">
              <img
                src={imageUrl!}
                alt="Uploaded receipt"
                className="max-h-[290px] object-contain w-auto rounded-sm"
                referrerPolicy="no-referrer"
              />
              {/* Glassmorphism Hover Overlay */}
              <div className="absolute inset-0 bg-slate-900/45 opacity-0 group-hover:opacity-100 transition-opacity flex flex-col items-center justify-center text-white rounded-sm gap-2">
                <Upload className="w-6 h-6 animate-bounce" />
                <span className="text-[11px] font-black uppercase tracking-wider">
                  Click or Drag to Replace Image
                </span>
              </div>
            </div>

            {/* Sub-label under the image */}
            <div className="mt-3 flex items-center gap-2">
              <span className="text-[9px] bg-zinc-900 border border-zinc-900 text-white px-2.5 py-1 rounded-sm font-bold uppercase tracking-wide">
                Active Scan
              </span>
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation(); // Avoid triggering file selection
                  if (previewUrl) {
                    URL.revokeObjectURL(previewUrl);
                    setPreviewUrl(null);
                  }
                  if (onClearImage) onClearImage();
                }}
                className="text-[9px] bg-slate-100 hover:bg-rose-50 hover:text-rose-600 text-slate-500 px-2.5 py-1 rounded-sm font-bold uppercase tracking-wide transition cursor-pointer border border-slate-200 hover:border-rose-200"
              >
                Clear Image
              </button>
            </div>
          </div>
        ) : isLoading ? (
          <div className="flex flex-col items-center space-y-4 w-full">
            {hasImage && imageUrl ? (
              <div className="relative w-full flex flex-col items-center justify-center">
                {/* Backing image blurred slightly */}
                <div className="opacity-40 blur-xs max-h-[180px] overflow-hidden rounded-sm">
                  <img
                    src={imageUrl}
                    alt="Processing background"
                    className="max-h-[180px] object-contain rounded-sm"
                  />
                </div>
                {/* Loader Overlay */}
                <div className="absolute inset-0 flex flex-col items-center justify-center">
                  <div className="relative flex items-center justify-center mb-3">
                    <div className="w-12 h-12 border-4 border-slate-200/60 border-t-zinc-900 rounded-full animate-spin"></div>
                    <Sparkles className="absolute w-5 h-5 text-zinc-900 animate-pulse" />
                  </div>
                  <p className="font-extrabold text-slate-800 text-xs tracking-wide uppercase">
                    Processing Bill...
                  </p>
                  <p className="text-[10px] text-slate-500 mt-1 select-none animate-pulse px-4 text-center max-w-[260px] leading-tight">
                    {loadingStep || "Analyzing receipt composition..."}
                  </p>
                </div>
              </div>
            ) : (
              <>
                <div className="relative flex items-center justify-center">
                  <div className="w-12 h-12 border-4 border-slate-100 border-t-zinc-900 rounded-full animate-spin"></div>
                  <Sparkles className="absolute w-5 h-5 text-zinc-900 animate-pulse" />
                </div>
                <div>
                  <p className="font-semibold text-slate-700 text-sm">
                    Processing Bill...
                  </p>
                  <p className="text-xs text-slate-500 mt-1 select-none animate-pulse">
                    {loadingStep || "Analyzing receipt composition..."}
                  </p>
                </div>
              </>
            )}
          </div>
        ) : (
          <div className="flex flex-col items-center">
            <div className="p-3 bg-zinc-900 rounded-sm mb-4 text-white transition-transform group-hover:scale-105">
              <Upload className="w-6 h-6" />
            </div>
            <h3 className="font-bold text-slate-900 text-sm">
              Upload your Receipt Image
            </h3>
            <p className="text-xs text-slate-500 mt-1 max-w-[280px]">
              Drag and drop your shopping bills (PNG/JPEG/WEBP) or click to
              browse files
            </p>
            <div className="mt-4 inline-flex items-center gap-1.5 px-2.5 py-1 bg-slate-100 rounded-sm text-[10px] font-mono text-slate-600">
              <ImageIcon className="w-3.5 h-3.5" />
              <span>Supports high-resolution camera scans</span>
            </div>
          </div>
        )}
      </div>

      {error && (
        <div
          id="upload-error"
          className="mt-3 p-3 bg-rose-50 border border-rose-100 rounded-sm flex items-start gap-2.5 text-xs text-rose-700"
        >
          <AlertCircle className="w-4 h-4 text-rose-500 shrink-0 mt-0.5" />
          <div className="flex-1">
            <span className="font-medium">Analysis Failed</span>
            <p className="mt-0.5 text-rose-600">{error}</p>
          </div>
        </div>
      )}
    </div>
  );
}
