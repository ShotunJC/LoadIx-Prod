import fs from "node:fs/promises";
import path from "node:path";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const TMP_DIR = "C:/Users/shotg/Documents/LoadIx-Prod/.tmp/loadix-system-demo-20260828";
const FINAL_PPTX = "C:/Users/shotg/Documents/LoadIx-Prod/LoadIx-System-Demo-Overview.pptx";
const HERO_PATH = path.join(TMP_DIR, "loadix-hero.png");

const W = 1280;
const H = 720;
const C = {
  ink: "#0B1215",
  charcoal: "#172126",
  muted: "#5B676C",
  soft: "#F3F5F4",
  panel: "#E9EEEC",
  rule: "#CBD5D1",
  teal: "#0F9F77",
  tealDark: "#087E60",
  tealPale: "#D7F8EC",
  white: "#FFFFFF",
  amber: "#C57A16",
};
const FONT = "Arial";

function addText(slide, text, position, style = {}, name = undefined) {
  const shape = slide.shapes.add({
    geometry: "textbox",
    name,
    position,
    fill: "none",
    line: { style: "solid", fill: "none", width: 0 },
  });
  shape.text = text;
  shape.text.style = {
    typeface: FONT,
    fontSize: 24,
    color: C.ink,
    verticalAlignment: "top",
    ...style,
  };
  return shape;
}

function addRect(slide, position, fill, options = {}) {
  return slide.shapes.add({
    geometry: options.geometry ?? "rect",
    name: options.name,
    position,
    fill,
    line: options.line ?? { style: "solid", fill: "none", width: 0 },
    ...(options.borderRadius ? { borderRadius: options.borderRadius } : {}),
  });
}

function addChrome(slide, number, section = "SYSTEM OVERVIEW") {
  addText(slide, "LOADIX", { left: 58, top: 28, width: 120, height: 24 }, {
    fontSize: 15,
    bold: true,
    color: C.tealDark,
    characterSpacing: 2,
  }, `brand-${number}`);
  addText(slide, section, { left: 910, top: 29, width: 300, height: 22 }, {
    fontSize: 13,
    bold: true,
    color: C.muted,
    alignment: "right",
    characterSpacing: 2,
  }, `section-${number}`);
  addRect(slide, { left: 58, top: 674, width: 1164, height: 1 }, C.rule, { name: `footer-rule-${number}` });
  addText(slide, String(number).padStart(2, "0"), { left: 1168, top: 682, width: 54, height: 20 }, {
    fontSize: 13,
    bold: true,
    color: C.muted,
    alignment: "right",
  }, `slide-number-${number}`);
}

function addTitle(slide, title, number) {
  addText(slide, title, { left: 58, top: 72, width: 1164, height: 68 }, {
    fontSize: 48,
    bold: true,
    color: C.ink,
  }, `title-${number}`);
}

function addCallout(slide, x, y, w, h, index, title, body, options = {}) {
  if (options.panel !== false) {
    addRect(slide, { left: x, top: y, width: w, height: h }, options.fill ?? C.soft, {
      name: `callout-panel-${index}`,
      line: { style: "solid", fill: options.line ?? C.rule, width: 1 },
      geometry: "roundRect",
      borderRadius: "rounded-lg",
    });
  }
  addRect(slide, { left: x + 24, top: y + 24, width: 42, height: 6 }, options.accent ?? C.teal, { name: `callout-accent-${index}` });
  addText(slide, title, { left: x + 24, top: y + 52, width: w - 48, height: 62 }, {
    fontSize: 27,
    bold: true,
    color: C.ink,
  }, `callout-title-${index}`);
  addText(slide, body, { left: x + 24, top: y + 120, width: w - 48, height: h - 142 }, {
    fontSize: 20,
    color: C.muted,
  }, `callout-body-${index}`);
}

function addNotes(slide, cue, sources) {
  slide.speakerNotes.textFrame.setText(`${cue}\n\n[Sources]\n${sources.map((s) => `- ${s}`).join("\n")}`);
  slide.speakerNotes.setVisible(true);
}

async function readImageBlob(imagePath) {
  const bytes = await fs.readFile(imagePath);
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
}

async function main() {
  await fs.mkdir(path.join(TMP_DIR, "rendered"), { recursive: true });
  const presentation = Presentation.create({ slideSize: { width: W, height: H } });
  const hero = await readImageBlob(HERO_PATH);

  // 1 — Cover image field (Codex Grid slide 08 hierarchy)
  {
    const slide = presentation.slides.add();
    slide.background.fill = C.white;
    addText(slide, "LOADIX", { left: 58, top: 54, width: 180, height: 28 }, {
      fontSize: 16,
      bold: true,
      color: C.tealDark,
      characterSpacing: 3,
    }, "cover-brand");
    addText(slide, "One operating\nrecord\nfor every load", { left: 58, top: 142, width: 540, height: 220 }, {
      fontSize: 60,
      bold: true,
      color: C.ink,
    }, "cover-title");
    addText(slide, "From contract and dispatch to weight evidence, reconciliation and invoice preparation.", { left: 58, top: 388, width: 500, height: 105 }, {
      fontSize: 28,
      color: C.muted,
    }, "cover-subtitle");
    addRect(slide, { left: 58, top: 540, width: 94, height: 6 }, C.teal, { name: "cover-accent" });
    addText(slide, "System overview · Demo meeting", { left: 58, top: 566, width: 420, height: 30 }, {
      fontSize: 18,
      bold: true,
      color: C.charcoal,
    }, "cover-context");
    addRect(slide, { left: 646, top: 34, width: 576, height: 620 }, C.tealPale, {
      geometry: "roundRect",
      borderRadius: "rounded-xl",
      name: "cover-image-backing",
    });
    slide.images.add({
      blob: hero,
      contentType: "image/png",
      alt: "Bulk haul truck on a weighbridge in a South African industrial landscape",
      fit: "cover",
      position: { left: 646, top: 34, width: 576, height: 620 },
      geometry: "roundRect",
      borderRadius: "rounded-xl",
      name: "cover-hero",
    });
    addNotes(slide,
      "Open with the outcome: LoadIx is the operating record that connects commercial intent, physical execution and financial preparation. Keep this to about 30 seconds before moving to the problem.",
      [
        "Repository: docs/product-scope.md — purpose, product outcomes and MVP flow.",
        "Asset: AI-generated LoadIx cover hero; prompt recorded in .tmp/loadix-system-demo-20260828/hero-prompt.txt.",
      ]);
  }

  // 2 — Three-callout message (Codex Grid slide 09 hierarchy)
  {
    const slide = presentation.slides.add();
    slide.background.fill = C.white;
    addChrome(slide, 2, "WHY LOADIX");
    addTitle(slide, "Fragmented tools break the operating story", 2);
    addText(slide, "When commercial rules, live execution and finance evidence live apart, every handoff creates another version of the truth.", { left: 58, top: 158, width: 1120, height: 62 }, {
      fontSize: 24,
      color: C.muted,
    }, "problem-intro");
    addCallout(slide, 58, 270, 350, 326, "2a", "Commercial intent", "Routes, rates, quantities and tolerances need to remain connected to the work they govern.");
    addCallout(slide, 465, 270, 350, 326, "2b", "Operational proof", "Assignments, milestones, weights, documents and exceptions need one current operational record.");
    addCallout(slide, 872, 270, 350, 326, "2c", "Financial confidence", "Revenue, cost and invoice preparation should start from approved trip facts—not reconstructed evidence.");
    addNotes(slide,
      "Frame this as the coordination problem LoadIx solves. Avoid claiming a quantified saving; the point is that the same load should not be reassembled across separate commercial, operational and finance records.",
      ["Repository: docs/product-scope.md — product outcomes, personas and auditable end-to-end flow."]);
  }

  // 3 — Process timeline (Codex Grid slide 17 hierarchy)
  {
    const slide = presentation.slides.add();
    slide.background.fill = C.white;
    addChrome(slide, 3, "CONNECTED WORKFLOW");
    addTitle(slide, "LoadIx keeps one record from contract to cash", 3);
    addText(slide, "Rules are snapshotted into operational work, evidence follows the trip, and approved facts flow forward.", { left: 58, top: 154, width: 1110, height: 52 }, {
      fontSize: 23,
      color: C.muted,
    }, "workflow-intro");

    // Connector first so the path stays behind the nodes.
    slide.shapes.add({
      geometry: "straightConnector1",
      name: "workflow-connector",
      position: { left: 92, top: 344, width: 1092, height: 0 },
      fill: "none",
      line: { style: "solid", fill: C.rule, width: 3 },
    });
    const stages = [
      ["01", "Contract", "Route, rate and tolerance"],
      ["02", "Instruction", "Demand and timing"],
      ["03", "Plan", "Driver, truck and trailers"],
      ["04", "Execute", "Milestones, weights and POD"],
      ["05", "Reconcile", "Quantity and exceptions"],
      ["06", "Prepare", "Profitability and invoice batch"],
    ];
    stages.forEach((stage, i) => {
      const x = 78 + i * 209;
      addRect(slide, { left: x, top: 320, width: 50, height: 50 }, i === 5 ? C.teal : C.ink, {
        geometry: "ellipse",
        name: `workflow-node-${i + 1}`,
      });
      addText(slide, stage[0], { left: x, top: 333, width: 50, height: 24 }, {
        fontSize: 16,
        bold: true,
        color: C.white,
        alignment: "center",
        verticalAlignment: "middle",
      }, `workflow-number-${i + 1}`);
      addText(slide, stage[1], { left: x - 14, top: 396, width: 158, height: 36 }, {
        fontSize: 24,
        bold: true,
        color: C.ink,
      }, `workflow-title-${i + 1}`);
      addText(slide, stage[2], { left: x - 14, top: 440, width: 166, height: 92 }, {
        fontSize: 18,
        color: C.muted,
      }, `workflow-body-${i + 1}`);
    });
    addText(slide, "One tenant-scoped audit trail", { left: 420, top: 576, width: 440, height: 40 }, {
      fontSize: 24,
      bold: true,
      color: C.tealDark,
      alignment: "center",
    }, "workflow-audit");
    addNotes(slide,
      "Walk left to right. Emphasize that trip snapshots protect operational history: later contract edits affect future work, not trips already generated. This is the bridge from the problem to the system.",
      [
        "Repository: docs/product-scope.md — 13-step MVP business flow.",
        "Repository: docs/workflow-improvements.md — contract/load workflows and immutable route-specific weight snapshots.",
      ]);
  }

  // 4 — Role-specific workspaces
  {
    const slide = presentation.slides.add();
    slide.background.fill = C.white;
    addChrome(slide, 4, "ROLE-BASED EXPERIENCE");
    addTitle(slide, "Each role sees the work that matters", 4);
    addText(slide, "A shared record does not mean a shared screen. LoadIx presents the same facts through purpose-built workspaces.", { left: 58, top: 154, width: 1110, height: 54 }, {
      fontSize: 23,
      color: C.muted,
    }, "roles-intro");
    addCallout(slide, 58, 264, 350, 326, "4a", "Controllers", "Plan and assign trips, move work through legal states, monitor the live board and resolve exceptions.", { panel: false });
    addCallout(slide, 465, 264, 350, 326, "4b", "Drivers", "Use a touch-first PWA for next actions, weights, location updates and camera-friendly document capture—even with intermittent connectivity.", { panel: false });
    addCallout(slide, 872, 264, 350, 326, "4c", "Finance & management", "Review reconciliation, charges, costs, estimated profitability, reports and invoice-preparation batches.", { panel: false });
    addRect(slide, { left: 438, top: 274, width: 1, height: 270 }, C.rule, { name: "roles-divider-1" });
    addRect(slide, { left: 845, top: 274, width: 1, height: 270 }, C.rule, { name: "roles-divider-2" });
    addNotes(slide,
      "Use this slide to orient the audience before the live demo login. Mention that permissions shape what each user can see and do; the demo can therefore move between operations, driver and finance perspectives without changing the underlying trip story.",
      [
        "Repository: docs/product-scope.md — personas and primary needs.",
        "Repository: README.md and apps/loadix-web/src/router.tsx — current Operations, Driver, Finance, Reports and administration workspaces.",
      ]);
  }

  // 5 — Exception handling
  {
    const slide = presentation.slides.add();
    slide.background.fill = C.white;
    addChrome(slide, 5, "OPERATIONS & RECONCILIATION");
    addTitle(slide, "Exceptions surface before billing", 5);
    addText(slide, "LoadIx keeps operational state, quantity evidence and review context together, so issues remain visible and explainable.", { left: 58, top: 154, width: 1110, height: 56 }, {
      fontSize: 23,
      color: C.muted,
    }, "exceptions-intro");
    addCallout(slide, 58, 266, 350, 330, "5a", "See the state", "The live operations board groups active trips by stage, exposes freshness and supports delayed or exception-only views.");
    addCallout(slide, 465, 266, 350, 330, "5b", "Trust the quantity", "Route-specific weighing, explicit quantity sources and snapshotted tolerance rules make reconciliation reproducible.");
    addCallout(slide, 872, 266, 350, 330, "5c", "Resolve with context", "Reasoned reviews and overrides preserve the actor, time and decision trail instead of hiding the exception.");
    addNotes(slide,
      "This is a key demo promise: do not only show the happy path. Point out how stale positions, quantity sources, tolerance outcomes and reasoned overrides remain visible rather than being silently normalized.",
      [
        "Repository: README.md — Operations control tower and trip lifecycle sections.",
        "Repository: docs/workflow-improvements.md — route-specific capture, tolerance outcomes and manual quantity controls.",
      ]);
  }

  // 6 — Embedded controls, flat composition
  {
    const slide = presentation.slides.add();
    slide.background.fill = C.white;
    addChrome(slide, 6, "TRUST & CONTROL");
    addTitle(slide, "Control is part of every workflow", 6);
    addRect(slide, { left: 58, top: 186, width: 12, height: 392 }, C.teal, { name: "control-rail" });
    addText(slide, "The system protects the record at the moment work happens—not as a clean-up exercise after the fact.", { left: 104, top: 188, width: 530, height: 126 }, {
      fontSize: 34,
      bold: true,
      color: C.ink,
    }, "control-message");
    addText(slide, "Tenant-scoped access, server-side permissions and explicit support boundaries", { left: 704, top: 190, width: 478, height: 68 }, {
      fontSize: 25,
      bold: true,
      color: C.ink,
    }, "control-1-title");
    addText(slide, "Optimistic concurrency prevents stale edits from silently overwriting newer decisions.", { left: 704, top: 274, width: 478, height: 68 }, {
      fontSize: 21,
      color: C.muted,
    }, "control-1-body");
    addRect(slide, { left: 704, top: 366, width: 478, height: 1 }, C.rule, { name: "control-divider" });
    addText(slide, "Idempotent commands, immutable events and reasoned overrides", { left: 704, top: 394, width: 478, height: 68 }, {
      fontSize: 25,
      bold: true,
      color: C.ink,
    }, "control-2-title");
    addText(slide, "Retries produce one business outcome; sensitive operational and financial actions remain traceable.", { left: 704, top: 482, width: 478, height: 76 }, {
      fontSize: 21,
      color: C.muted,
    }, "control-2-body");
    addNotes(slide,
      "Keep this audience-facing. The architecture details matter because they change confidence in the demo: tenant identity is trusted from authentication, editable records reject stale versions, retry-prone commands are replay-safe, and material changes leave audit evidence.",
      [
        "Repository: docs/product-scope.md — assurance, security/privacy and resilience requirements.",
        "Repository: docs/architecture.md — tenancy, authorization, concurrency, idempotency and audit design.",
      ]);
  }

  // 7 — Finance timeline
  {
    const slide = presentation.slides.add();
    slide.background.fill = C.white;
    addChrome(slide, 7, "FINANCE PREPARATION");
    addTitle(slide, "Finance starts with a trusted trip", 7);
    addText(slide, "Completed work moves forward only when its operational evidence and quantity outcome are ready for financial review.", { left: 58, top: 154, width: 1110, height: 52 }, {
      fontSize: 23,
      color: C.muted,
    }, "finance-intro");
    slide.shapes.add({
      geometry: "straightConnector1",
      name: "finance-connector",
      position: { left: 136, top: 350, width: 1000, height: 0 },
      fill: "none",
      line: { style: "solid", fill: C.rule, width: 3 },
    });
    const steps = [
      ["Operational evidence", "Reconciled quantity, required documents and immutable rate snapshot"],
      ["Financial review", "Revenue, explicit costs, tax treatment and estimated profitability"],
      ["Approved output", "Customer-grouped invoice batch with CSV/PDF handoff"],
    ];
    steps.forEach((step, i) => {
      const x = 104 + i * 405;
      addRect(slide, { left: x, top: 326, width: 48, height: 48 }, i === 2 ? C.teal : C.ink, {
        geometry: "ellipse",
        name: `finance-node-${i + 1}`,
      });
      addText(slide, String(i + 1), { left: x, top: 338, width: 48, height: 22 }, {
        fontSize: 16,
        bold: true,
        color: C.white,
        alignment: "center",
        verticalAlignment: "middle",
      }, `finance-number-${i + 1}`);
      addText(slide, step[0], { left: x - 2, top: 404, width: 330, height: 42 }, {
        fontSize: 27,
        bold: true,
        color: C.ink,
      }, `finance-title-${i + 1}`);
      addText(slide, step[1], { left: x - 2, top: 458, width: 330, height: 96 }, {
        fontSize: 20,
        color: C.muted,
      }, `finance-body-${i + 1}`);
    });
    addNotes(slide,
      "Clarify the boundary: LoadIx prepares an approved and auditable invoice batch; it does not replace the accounting ledger. In the live demo, connect the completed trip to its rate snapshot, costs, approval and export-ready batch.",
      [
        "Repository: docs/product-scope.md — finance preparation boundary and explicit MVP exclusions.",
        "Repository: README.md — financial calculation and invoice preparation endpoints and controls.",
      ]);
  }

  // 8 — Pilot credibility
  {
    const slide = presentation.slides.add();
    slide.background.fill = C.white;
    addChrome(slide, 8, "PILOT FOUNDATION");
    addTitle(slide, "Built for pilot operations", 8);
    addText(slide, "The demo sits on the same domain model, permissions and infrastructure boundaries intended for the pilot path.", { left: 58, top: 154, width: 1110, height: 54 }, {
      fontSize: 23,
      color: C.muted,
    }, "pilot-intro");
    addCallout(slide, 58, 264, 350, 326, "8a", "Deployable", "A modular monolith with a documented dedicated-server deployment path, database migrations and health checks.", { panel: false });
    addCallout(slide, 465, 264, 350, 326, "8b", "Observable & recoverable", "Structured telemetry, durable background work, reporting queues, backups and rollback guidance support pilot operation.", { panel: false });
    addCallout(slide, 872, 264, 350, 326, "8c", "Integration-ready", "Versioned APIs and events, object storage, notifications and an edge-device/weighbridge foundation preserve clear boundaries.", { panel: false });
    addRect(slide, { left: 438, top: 274, width: 1, height: 270 }, C.rule, { name: "pilot-divider-1" });
    addRect(slide, { left: 845, top: 274, width: 1, height: 270 }, C.rule, { name: "pilot-divider-2" });
    addText(slide, "Deterministic synthetic data makes the demo repeatable without creating a separate fake product.", { left: 58, top: 602, width: 1080, height: 34 }, {
      fontSize: 21,
      bold: true,
      color: C.tealDark,
    }, "pilot-demo-note");
    addNotes(slide,
      "Use this as credibility, not an infrastructure deep dive. The important point is that the repeatable demo uses normal domain rules and a deterministic synthetic tenant; it is not a disconnected click-through prototype.",
      [
        "Repository: docs/architecture.md — modular monolith and deterministic demonstration environment.",
        "Repository: docs/milestones.md — M16 security/governance, M17 reliability, M18 pilot deployment and M19 demonstration environment.",
        "Repository: README.md — dedicated-server pilot deployment and edge-device foundation.",
      ]);
  }

  // 9 — Demo handoff using split-field hierarchy
  {
    const slide = presentation.slides.add();
    slide.background.fill = C.white;
    addChrome(slide, 9, "LIVE DEMO");
    addText(slide, "Now, let’s follow\none load end-to-end", { left: 58, top: 112, width: 520, height: 154 }, {
      fontSize: 50,
      bold: true,
      color: C.ink,
    }, "handoff-title");
    addText(slide, "Watch how rules, evidence, exceptions and approvals remain connected as the load moves.", { left: 58, top: 304, width: 500, height: 90 }, {
      fontSize: 26,
      color: C.muted,
    }, "handoff-subtitle");
    addRect(slide, { left: 58, top: 470, width: 92, height: 6 }, C.teal, { name: "handoff-accent" });
    addText(slide, "Contract → load instruction → trip → driver → reconciliation → finance", { left: 58, top: 500, width: 505, height: 76 }, {
      fontSize: 22,
      bold: true,
      color: C.charcoal,
    }, "handoff-path");

    addRect(slide, { left: 646, top: 82, width: 576, height: 536 }, C.ink, {
      geometry: "roundRect",
      borderRadius: "rounded-xl",
      name: "handoff-panel",
    });
    const demoSteps = [
      "Create and activate the commercial agreement",
      "Plan the customer load instruction",
      "Generate, assign and dispatch a trip",
      "Capture driver milestones, weights and POD",
      "Review quantity outcome and exceptions",
      "Approve profitability and invoice preparation",
    ];
    demoSteps.forEach((item, i) => {
      const y = 124 + i * 76;
      addText(slide, String(i + 1).padStart(2, "0"), { left: 686, top: y, width: 42, height: 28 }, {
        fontSize: 15,
        bold: true,
        color: C.tealPale,
      }, `demo-step-number-${i + 1}`);
      addText(slide, item, { left: 746, top: y - 3, width: 420, height: 50 }, {
        fontSize: 22,
        bold: true,
        color: C.white,
      }, `demo-step-${i + 1}`);
      if (i < demoSteps.length - 1) addRect(slide, { left: 686, top: y + 52, width: 480, height: 1 }, "#354147", { name: `demo-step-rule-${i + 1}` });
    });
    addNotes(slide,
      "Pause here, then switch to the live system. The audience now knows what to watch for: inherited commercial rules, visible operational state, evidence-led reconciliation, and controlled finance preparation.",
      [
        "Repository: docs/product-scope.md — MVP business flow.",
        "Repository: docs/workflow-improvements.md — guided contract and load flows.",
      ]);
  }

  const inspection = await presentation.inspect({
    kind: "slide,textbox,shape,image,notes",
    maxChars: 50000,
  });
  await fs.writeFile(path.join(TMP_DIR, "deck-inspect.ndjson"), inspection.ndjson);

  for (const [index, slide] of presentation.slides.items.entries()) {
    const stem = `slide-${String(index + 1).padStart(2, "0")}`;
    const png = await presentation.export({ slide, format: "png", scale: 1.5 });
    await fs.writeFile(path.join(TMP_DIR, "rendered", `${stem}.png`), new Uint8Array(await png.arrayBuffer()));
    const layout = await slide.export({ format: "layout" });
    await fs.writeFile(path.join(TMP_DIR, "rendered", `${stem}.layout.json`), await layout.text());
  }

  const montage = await presentation.export({ format: "webp", montage: true, scale: 1 });
  await fs.writeFile(path.join(TMP_DIR, "deck-montage.webp"), new Uint8Array(await montage.arrayBuffer()));

  const pptx = await PresentationFile.exportPptx(presentation);
  await pptx.save(FINAL_PPTX);
  console.log(`Created ${FINAL_PPTX}`);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
