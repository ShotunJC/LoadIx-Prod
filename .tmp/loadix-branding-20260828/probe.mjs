import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const source = "C:/Users/shotg/Documents/LoadIx-Prod/LoadIx-System-Demo-Overview.pptx";
const presentation = await PresentationFile.importPptx(await FileBlob.load(source));
for (const id of ["sh/7qp4be9c", "sh/z2d07mh4", "sh/fu94fe98", "sh/r65knqtk"]) {
  const target = presentation.resolve(id);
  console.log(id, JSON.stringify({
    name: target.name,
    fill: target.fill,
    frame: target.frame,
    text: target.text?.toString?.(),
    textStyle: target.text?.style,
  }, null, 2));
}
