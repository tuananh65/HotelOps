import { attachmentService } from "./api/modules";
let pendingUploads = 0;
export async function readEvidence(files: File[]) {
  if (files.length > 5) throw Error("Use at most 5 files per upload.");
  for (const file of files) {
    if (file.size > 10 * 1024 * 1024)
      throw Error(`${file.name}: maximum file size is 10 MB.`);
    const bytes = new Uint8Array(await file.slice(0, 8).arrayBuffer());
    const jpg = bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255;
    const png = [137, 80, 78, 71, 13, 10, 26, 10].every(
      (v, i) => bytes[i] === v,
    );
    const pdf = String.fromCharCode(...bytes.slice(0, 5)) === "%PDF-";
    const allowed =
      (jpg && /\.jpe?g$/i.test(file.name)) ||
      (png && /\.png$/i.test(file.name)) ||
      (pdf && /\.pdf$/i.test(file.name));
    if (!allowed)
      throw Error(
        `${file.name}: extension and content must match JPG, PNG or PDF.`,
      );
  }
  pendingUploads++;
  window.dispatchEvent(
    new CustomEvent("hotelops-upload", { detail: pendingUploads }),
  );
  try {
    const uploaded = await attachmentService.upload(files);
    return uploaded.map((file) => ({
      id: file.id,
      name: file.name,
      type: file.contentType,
      data: file.url,
    }));
  } finally {
    pendingUploads--;
    window.dispatchEvent(
      new CustomEvent("hotelops-upload", { detail: pendingUploads }),
    );
  }
}
