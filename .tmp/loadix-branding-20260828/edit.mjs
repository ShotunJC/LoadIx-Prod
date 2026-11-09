import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const TMP_DIR = "C:/Users/shotg/Documents/LoadIx-Prod/.tmp/loadix-branding-20260828";
const STARTER = path.join(TMP_DIR, "template-starter.pptx");
const LOGO = path.join(TMP_DIR, "loadix-logo.png");
const FINAL = "C:/Users/shotg/Documents/LoadIx-Prod/LoadIx-System-Demo-Overview-Branded.pptx";
const RENDER_DIR = path.join(TMP_DIR, "final-rendered");

const BLUE = "#2A78BE";
const ORANGE = "#F45A27";
const NAVY = "#1D3468";
const LIGHT_BLUE = "#D9F2FC";

async function readBytes(filePath) {
  const bytes = await fs.readFile(filePath);
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
}

async function writeBlob(filePath, blob) {
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

const presentation = await PresentationFile.importPptx(await FileBlob.load(STARTER));
const logoBytes = await readBytes(LOGO);

// Element IDs are regenerated when a PPTX is imported. Resolve the stable
// element names from this in-memory presentation rather than reusing IDs from
// a previous inspection pass.
const currentSnapshot = await presentation.inspect({
  kind: "textbox,shape,image",
  include: "id,slide,name,textPreview,bbox",
  maxChars: 100000,
});
const currentRecords = currentSnapshot.ndjson
  .trim()
  .split(/\r?\n/)
  .filter(Boolean)
  .map((line) => JSON.parse(line));
const idByName = new Map(
  currentRecords
    .filter((record) => record.name && record.id)
    .map((record) => [record.name, record.id]),
);
function getByName(name) {
  const id = idByName.get(name);
  if (!id) throw new Error(`Missing inherited element: ${name}`);
  return presentation.resolve(id);
}

const brandTextNames = [
  "cover-brand",
  "brand-2",
  "brand-3",
  "brand-4",
  "brand-5",
  "brand-6",
  "brand-7",
  "brand-8",
  "brand-9",
];
for (const name of brandTextNames) getByName(name).delete();

const blueShapeNames = [
  "callout-accent-2a", "callout-accent-2b", "callout-accent-2c",
  "callout-accent-4a", "callout-accent-4b", "callout-accent-4c",
  "callout-accent-5a", "callout-accent-5b", "callout-accent-5c",
  "control-rail",
  "callout-accent-8a", "callout-accent-8b", "callout-accent-8c",
];
for (const name of blueShapeNames) getByName(name).fill = BLUE;

for (const name of ["cover-accent", "workflow-node-6", "finance-node-3", "handoff-accent"]) {
  getByName(name).fill = ORANGE;
}
getByName("handoff-panel").fill = NAVY;

getByName("workflow-audit").text.color = BLUE;
getByName("pilot-demo-note").text.color = BLUE;
for (const name of [
  "demo-step-number-1", "demo-step-number-2", "demo-step-number-3",
  "demo-step-number-4", "demo-step-number-5", "demo-step-number-6",
]) getByName(name).text.color = LIGHT_BLUE;

for (const [index, slide] of presentation.slides.items.entries()) {
  const isCover = index === 0;
  slide.images.add({
    blob: logoBytes,
    contentType: "image/png",
    alt: "Official LoadIx logo supplied by the owner",
    fit: "contain",
    position: isCover
      ? { left: 58, top: 34, width: 250, height: 65 }
      : { left: 58, top: 14, width: 132, height: 35 },
    name: `official-loadix-logo-${index + 1}`,
  });
  slide.speakerNotes.append("\n- User-provided official LoadIx logo: G:/BackUp/Documents/LoadIX/LoadIx r1-01.png.");
}

await fs.mkdir(RENDER_DIR, { recursive: true });
for (const [index, slide] of presentation.slides.items.entries()) {
  const stem = `slide-${String(index + 1).padStart(2, "0")}`;
  await writeBlob(path.join(RENDER_DIR, `${stem}.png`), await presentation.export({ slide, format: "png", scale: 1.5 }));
  const layout = await slide.export({ format: "layout" });
  await fs.writeFile(path.join(RENDER_DIR, `${stem}.layout.json`), await layout.text());
}

const after = await presentation.inspect({
  kind: "slide,textbox,shape,image,notes,layout",
  include: "id,slide,name,title,text,textPreview,bbox,bboxUnit,alt,isPlaceholder,placeholders",
  maxChars: 100000,
});
await fs.writeFile(path.join(TMP_DIR, "final-inspect.ndjson"), after.ndjson);

const pptx = await PresentationFile.exportPptx(presentation);
await pptx.save(FINAL);
console.log(`Created ${FINAL}`);
