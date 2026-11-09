import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const workspace = "C:/Users/shotg/Documents/LoadIx-Prod/.tmp/loadix-branding-20260828";
const source = "C:/Users/shotg/Documents/LoadIx-Prod/LoadIx-System-Demo-Overview.pptx";

async function writeBlob(filePath, blob) {
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

const presentation = await PresentationFile.importPptx(await FileBlob.load(source));
const out = path.join(workspace, "source-inventory");
await fs.mkdir(out, { recursive: true });

const inspection = await presentation.inspect({
  kind: "deck,slide,textbox,shape,image,notes,layout",
  include: "id,slide,name,title,text,textPreview,bbox,bboxUnit,alt,isPlaceholder,placeholders",
  maxChars: 100000,
});
await fs.writeFile(path.join(out, "source-inspect.ndjson"), inspection.ndjson);

for (const [index, slide] of presentation.slides.items.entries()) {
  const stem = `slide-${String(index + 1).padStart(2, "0")}`;
  await writeBlob(path.join(out, `${stem}.png`), await presentation.export({ slide, format: "png", scale: 1.5 }));
  const layout = await slide.export({ format: "layout" });
  await fs.writeFile(path.join(out, `${stem}.layout.json`), await layout.text());
}

await writeBlob(path.join(out, "source-montage.webp"), await presentation.export({ format: "webp", montage: true, scale: 1 }));
await fs.writeFile(path.join(out, "structure.txt"), [
  `slides=${presentation.slides.items.length}`,
  `masters=${presentation.masters.items.length}`,
  `layouts=${presentation.layouts.items.length}`,
  ...presentation.masters.items.map((m, i) => `master[${i}]=${m.id ?? "(no id)"}`),
  ...presentation.layouts.items.map((l, i) => `layout[${i}]=${l.id ?? "(no id)"};parent=${l.parentLayoutId ?? "(none)"}`),
].join("\n"));

console.log(`Inspected ${presentation.slides.items.length} slides.`);
