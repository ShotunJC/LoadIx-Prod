import fs from "node:fs/promises";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const OUT = "C:/Users/shotg/Documents/LoadIx-Prod/LoadIx-Initial-User-Training.pptx";
const SCREEN = "C:/Users/shotg/Documents/LoadIx-Prod/.tmp/training-module/screens";
const RENDER = "C:/Users/shotg/Documents/LoadIx-Prod/.tmp/training-module/rendered";

const W = 1280;
const H = 720;
const M = 54;
const C = {
  ink: "#0B1220",
  muted: "#556070",
  panel: "#EEF1F4",
  rule: "#B8BCC4",
  blue: "#3D8DFF",
  blueSoft: "#DCEBFF",
  navy: "#09091A",
  green: "#18A66A",
  amber: "#D97706",
  red: "#C2413A",
  white: "#FFFFFF",
};

const deck = Presentation.create({ slideSize: { width: W, height: H } });
const imageCache = new Map();

async function bytes(name) {
  if (!imageCache.has(name)) imageCache.set(name, await fs.readFile(`${SCREEN}/${name}.png`));
  return imageCache.get(name);
}

function box(slide, x, y, w, h, fill = C.panel, line = "none", radius = 0) {
  return slide.shapes.add({
    geometry: radius ? "roundRect" : "rect",
    position: { left: x, top: y, width: w, height: h },
    fill,
    line: line === "none" ? { style: "solid", fill: "none", width: 0 } : { style: "solid", fill: line, width: 1 },
    ...(radius ? { borderRadius: radius } : {}),
  });
}

function textBox(slide, text, x, y, w, h, opts = {}) {
  const s = slide.shapes.add({
    geometry: "textbox",
    position: { left: x, top: y, width: w, height: h },
    fill: "none",
    line: { style: "solid", fill: "none", width: 0 },
  });
  s.text = text;
  s.text.style = {
    fontSize: opts.size ?? 20,
    typeface: "Arial",
    color: opts.color ?? C.ink,
    bold: opts.bold ?? false,
    alignment: opts.align ?? "left",
    verticalAlignment: opts.valign ?? "top",
    autoFit: opts.autoFit ?? "shrinkText",
    insets: { top: opts.inset ?? 0, right: opts.inset ?? 0, bottom: opts.inset ?? 0, left: opts.inset ?? 0 },
  };
  return s;
}

function line(slide, x, y, w, h = 0, color = C.rule, width = 1) {
  return slide.shapes.add({
    geometry: "straightConnector1",
    position: { left: x, top: y, width: w, height: h },
    fill: "none",
    line: { style: "solid", fill: color, width },
  });
}

function addFooter(slide, n) {
  line(slide, M, 675, W - 2 * M, 0, C.rule, 1);
  textBox(slide, "LOADIX INITIAL USER TRAINING  •  SYNTHETIC DEMONSTRATION DATA", M, 686, 900, 18, { size: 11, color: C.muted, bold: true });
  textBox(slide, String(n).padStart(2, "0"), 1160, 684, 66, 20, { size: 12, color: C.ink, bold: true, align: "right" });
}

function addHeader(slide, title, section, n, subtitle = "") {
  slide.background.fill = C.white;
  textBox(slide, section.toUpperCase(), M, 34, 420, 22, { size: 13, color: C.blue, bold: true });
  textBox(slide, title, M, 67, W - 2 * M, 58, { size: 35, bold: true, autoFit: "shrinkText" });
  if (subtitle) textBox(slide, subtitle, M, 128, W - 2 * M, 34, { size: 17, color: C.muted });
  addFooter(slide, n);
}

function notes(slide, trainer, sources) {
  slide.speakerNotes.textFrame.setText(`${trainer}\n\n[Sources]\n${sources.map((s) => `- ${s}`).join("\n")}`);
  slide.speakerNotes.setVisible(true);
}

function step(slide, n, title, detail, x, y, width = 470, accent = C.blue) {
  box(slide, x, y + 1, 34, 34, accent, "none", 17);
  textBox(slide, String(n), x, y + 4, 34, 27, { size: 17, color: C.white, bold: true, align: "center", valign: "middle" });
  textBox(slide, title, x + 50, y, width - 50, 27, { size: 19, bold: true });
  textBox(slide, detail, x + 50, y + 29, width - 50, 47, { size: 16, color: C.muted });
}

async function addShot(slide, name, x, y, w, h, alt, fit = "contain") {
  box(slide, x - 4, y - 4, w + 8, h + 8, C.white, C.rule, 4);
  slide.images.add({ blob: await bytes(name), contentType: "image/png", alt, fit, position: { left: x, top: y, width: w, height: h }, geometry: "rect" });
}

function tag(slide, label, x, y, w, fill = C.blueSoft, color = C.ink) {
  box(slide, x, y, w, 34, fill, "none", 5);
  textBox(slide, label, x + 10, y + 7, w - 20, 20, { size: 14, color, bold: true, align: "center" });
}

function twoColBullets(slide, leftTitle, leftItems, rightTitle, rightItems, y = 195) {
  textBox(slide, leftTitle, M, y, 510, 32, { size: 24, bold: true });
  textBox(slide, rightTitle, 690, y, 510, 32, { size: 24, bold: true });
  line(slide, 640, y, 0, 400, C.rule, 1);
  leftItems.forEach((v, i) => step(slide, i + 1, v[0], v[1], M, y + 55 + i * 84, 520, C.blue));
  rightItems.forEach((v, i) => step(slide, i + 1, v[0], v[1], 690, y + 55 + i * 84, 520, C.green));
}

// 1 — Cover
{
  const s = deck.slides.add();
  s.background.fill = C.white;
  textBox(s, "LOADIX", M, 40, 240, 24, { size: 14, bold: true, color: C.blue });
  textBox(s, "Initial user\ntraining", M, 132, 485, 150, { size: 56, bold: true });
  textBox(s, "A practical guide from tenant setup to invoice-ready operations", M, 308, 470, 76, { size: 22, color: C.muted });
  tag(s, "ADMIN • OPERATIONS • FLEET • FINANCE", M, 430, 424, C.navy, C.white);
  textBox(s, "Training environment: synthetic LoadIx Demo Transport tenant", M, 500, 480, 46, { size: 16, color: C.muted });
  await addShot(s, "dashboard", 620, 42, 610, 610, "LoadIx operations dashboard", "cover");
  notes(s, "Set expectations: this is the first module. Demonstrate in the synthetic tenant and keep live customer data out of practice exercises.", ["docs/product-scope.md", "docs/demo-guide.md", "Local LoadIx dashboard screenshot captured 2026-08-24"]);
}

// 2 — Outcomes
{
  const s = deck.slides.add(); addHeader(s, "What you will be able to do", "Module overview", 2, "The module follows the same sequence as a real movement: configure, plan, execute, reconcile and prepare finance.");
  const items = [
    ["Find the right workspace", "Use the sidebar, connection state, notifications and filters confidently."],
    ["Prepare dependable master data", "Create and maintain customers, sites, commodities and fleet resources."],
    ["Create commercial work", "Build an active contract, then plan a load instruction against it."],
    ["Dispatch and control trips", "Assign eligible resources and move only through legal lifecycle actions."],
    ["Complete evidence and reconciliation", "Capture weights, documents and reviewed exception decisions."],
    ["Hand work to finance", "Approve eligible trips, build invoice batches and run operational reports."],
  ];
  items.forEach((v, i) => {
    const col = i % 2; const row = Math.floor(i / 2); const x = col ? 670 : M; const y = 190 + row * 142;
    step(s, i + 1, v[0], v[1], x, y, 540, col ? C.green : C.blue);
  });
  notes(s, "Ask learners which outcomes match their role. Emphasize that permissions may hide actions they do not own.", ["docs/product-scope.md", "docs/domain-glossary.md"]);
}

// 3 — End-to-end lifecycle
{
  const s = deck.slides.add(); addHeader(s, "Every record supports one auditable movement", "System flow", 3, "Work progresses from reusable setup through commercial planning, execution evidence and finance preparation.");
  const nodes = [
    ["1", "Master data", "customers • sites • fleet"], ["2", "Contract", "route • rate • tolerance"], ["3", "Load instruction", "quantity • windows • priority"],
    ["4", "Trips", "assign • dispatch • execute"], ["5", "Reconcile", "weights • variance • POD"], ["6", "Finance", "costs • approve • batch"], ["7", "Reports", "performance • exceptions"],
  ];
  const xs = [48, 218, 388, 558, 728, 898, 1068];
  for (let i = 0; i < xs.length - 1; i++) line(s, xs[i] + 130, 356, 40, 0, C.blue, 3);
  nodes.forEach((v, i) => {
    box(s, xs[i], 260, 140, 188, i === 3 ? C.blueSoft : C.panel, i === 3 ? C.blue : C.rule, 8);
    textBox(s, v[0], xs[i] + 10, 278, 120, 46, { size: 31, bold: true, color: i === 3 ? C.blue : C.ink, align: "center" });
    textBox(s, v[1], xs[i] + 10, 334, 120, 52, { size: 19, bold: true, align: "center", valign: "middle" });
    textBox(s, v[2], xs[i] + 10, 397, 120, 42, { size: 13, color: C.muted, align: "center" });
  });
  textBox(s, "Key rule", M, 500, 160, 30, { size: 24, bold: true });
  textBox(s, "Never skip ahead by editing status directly. Use the named action for the current state so the timeline, permissions and audit trail remain correct.", 210, 500, 990, 72, { size: 21, color: C.muted });
  notes(s, "Use this slide as the mental model for the whole module. Each later slide zooms into one record or handoff.", ["docs/product-scope.md", "docs/domain-glossary.md"]);
}

// 4 — Access and security
{
  const s = deck.slides.add(); addHeader(s, "Sign in with your own role and tenant context", "Access and security", 4, "LoadIx is invitation-only. What you can view and change is controlled by tenant membership and permissions.");
  twoColBullets(s, "Start safely", [
    ["Open the approved LoadIx URL", "Confirm the page says LoadIx and the expected environment."],
    ["Enter your work email and password", "Use Forgot your password? when needed; never share credentials."],
    ["Confirm the workspace", "Check company/branch context, your name and API connected status."],
    ["Report unexpected access", "Stop if another tenant, customer scope or role appears."],
  ], "Session habits", [
    ["Use one named account", "Actions are actor-stamped; shared accounts destroy accountability."],
    ["Keep browser data private", "Do not paste tokens, documents or passwords into support tickets."],
    ["Expect permission boundaries", "A missing or disabled action usually means role or state control."],
    ["Sign out on shared devices", "Logout clears the browser session and the driver's local queue."],
  ], 188);
  notes(s, "Demonstrate sign-in only with the local synthetic account. Do not place training passwords on the slide or distribute them outside the controlled environment.", ["README.md", "docs/product-scope.md", "docs/demo-guide.md"]);
}

// 5 — Navigation screenshot
{
  const s = deck.slides.add(); addHeader(s, "Use the shell to orient before you act", "Navigation", 5, "The sidebar chooses the business area; the header confirms context, connectivity and attention items.");
  await addShot(s, "dashboard", 500, 178, 710, 440, "LoadIx dashboard and navigation shell", "contain");
  step(s, 1, "Choose a business area", "Operations, Commercial, Fleet, Sites, Finance and Administration are grouped in the sidebar.", M, 185, 405);
  step(s, 2, "Confirm workspace context", "Read the branch/workspace label and synthetic/demo banner before editing.", M, 285, 405);
  step(s, 3, "Check API connected", "If disconnected, pause mutations and follow the recovery path.", M, 385, 405);
  step(s, 4, "Open attention items", "Use the notification bell and dashboard exception counts as your starting queue.", M, 485, 405);
  notes(s, "Invite learners to point to the current tenant, branch, connection state and notification count before navigating away.", ["Local LoadIx dashboard screenshot captured 2026-08-24", "README.md"]);
}

// 6 — Roles
{
  const s = deck.slides.add(); addHeader(s, "Ownership changes as work moves through LoadIx", "Roles and handoffs", 6, "Permissions protect high-impact actions; the next role receives a complete, traceable record rather than an informal message.");
  const rows = [
    ["Tenant administrator", "Company, branches, users, policies, pilot configuration", "Configured tenant and authorised users"],
    ["Commercial / controller", "Customers, contracts, load instructions and trip generation", "Dispatchable trips with frozen commercial context"],
    ["Fleet / controller", "Eligible drivers, trucks, trailers and assignments", "A resource-valid assigned trip"],
    ["Driver / controller", "Milestones, weights, location evidence and POD", "A delivery-complete evidence record"],
    ["Operations manager", "Exceptions, overrides and reconciliation decisions", "Completed, reviewed trip"],
    ["Finance user", "Costs, approvals, invoice batches and exports", "Auditable invoice-preparation package"],
  ];
  const y0 = 184; const widths = [255, 425, 490]; const xs = [M, M + widths[0], M + widths[0] + widths[1]];
  ["ROLE", "PRIMARY RESPONSIBILITY", "HANDOFF STANDARD"].forEach((h, i) => { box(s, xs[i], y0, widths[i], 45, C.navy); textBox(s, h, xs[i] + 12, y0 + 13, widths[i] - 24, 20, { size: 14, color: C.white, bold: true }); });
  rows.forEach((r, ri) => r.forEach((v, ci) => { const y = y0 + 45 + ri * 67; box(s, xs[ci], y, widths[ci], 67, ri % 2 ? C.white : C.panel, C.rule); textBox(s, v, xs[ci] + 12, y + 10, widths[ci] - 24, 48, { size: ci === 0 ? 17 : 15, bold: ci === 0 }); }));
  notes(s, "Explain segregation of duties using one trip: planning, execution review and finance approval are distinct responsibilities even when a small team holds multiple roles.", ["docs/product-scope.md", "docs/domain-glossary.md"]);
}

// 7 — Onboarding
{
  const s = deck.slides.add(); addHeader(s, "Complete onboarding in dependency order", "Tenant administration", 7, "The onboarding workspace groups 16 readiness steps and blocks activation until its checklist is satisfied.");
  await addShot(s, "onboarding", 550, 178, 660, 430, "LoadIx pilot onboarding progress", "contain");
  step(s, 1, "Organisation", "Company details, branches and users establish ownership and access.", M, 185, 430);
  step(s, 2, "Operational masters", "Customers, locations, commodities and fleet records supply valid choices.", M, 285, 430);
  step(s, 3, "Configuration", "Review weighbridge, Edge, notification, finance and document rules.", M, 385, 430);
  step(s, 4, "Scope and activate", "Limit the pilot allow-list, review all gates, then activate once.", M, 485, 430, C.green);
  notes(s, "Demonstrate that each onboarding card links to its management screen. Explain dry-run imports: review all errors, then confirm an all-or-nothing import.", ["Local LoadIx onboarding screenshot captured 2026-08-24", "apps/loadix-web/src/features/onboarding/onboarding-page.tsx", "docs/product-scope.md"]);
}

// 8 — Master data order
{
  const s = deck.slides.add(); addHeader(s, "Create master data before commercial work", "Master data", 8, "Active contracts and trip assignments depend on clean, active records; deactivation protects history without deleting it.");
  const cols = [
    ["1", "Structure", "Branches\nUsers and roles"], ["2", "Commercial", "Customers\nContacts"], ["3", "Movement", "Locations\nCommodities"], ["4", "Fleet", "Drivers\nTrucks\nTrailers"], ["5", "Controls", "Statuses\nCompliance\nAvailability"],
  ];
  cols.forEach((v, i) => { const x = M + i * 230; box(s, x, 220, 198, 250, i === 4 ? C.blueSoft : C.panel, i === 4 ? C.blue : C.rule, 7); textBox(s, v[0], x + 16, 238, 40, 40, { size: 29, bold: true, color: C.blue }); textBox(s, v[1], x + 16, 295, 165, 34, { size: 23, bold: true }); textBox(s, v[2], x + 16, 350, 165, 94, { size: 18, color: C.muted }); });
  textBox(s, "Data quality gate", M, 510, 210, 32, { size: 24, bold: true });
  textBox(s, "Use business references people recognise—customer names, site codes, fleet numbers, registrations and licence numbers. Search before creating to avoid duplicates.", 280, 508, 920, 64, { size: 20, color: C.muted });
  notes(s, "Explain that every tenant-owned record is isolated and status-controlled. Existing trip snapshots remain valid when future master data changes.", ["README.md", "docs/product-scope.md", "docs/domain-glossary.md"]);
}

// 9 — Master data actions
{
  const s = deck.slides.add(); addHeader(s, "Search, verify, then add or edit a record", "Master data", 9, "The same safe pattern applies across customers, locations, commodities, drivers, trucks and trailers.");
  await addShot(s, "customers", 500, 177, 710, 440, "LoadIx customer list", "contain");
  step(s, 1, "Search first", "Use reference/name and status filters; confirm the record does not already exist.", M, 180, 405);
  step(s, 2, "Add the minimum complete record", "Enter the required legal/operational fields and a stable business reference.", M, 278, 405);
  step(s, 3, "Review and save", "Correct validation errors; confirm the success state and list result.", M, 376, 405);
  step(s, 4, "Edit with the latest version", "If a conflict appears, refresh and reapply only the intended change.", M, 474, 405, C.green);
  notes(s, "Open Customers, then repeat the pattern in one fleet screen. Stress that a 409 conflict protects another user's newer edit.", ["Local LoadIx customers screenshot captured 2026-08-24", "README.md"]);
}

// 10 — Contract overview
{
  const s = deck.slides.add(); addHeader(s, "A contract defines the commercial rules future trips will snapshot", "Contracts", 10, "Use the five-step guided flow; save a draft for review or activate only when every route and rate is valid.");
  const stages = [
    ["1", "Customer & details", "Number, name, period, currency, payment terms"], ["2", "Route", "Collection and delivery sites; weighbridge requirements"],
    ["3", "Commodity & quantity", "Permitted commodity, tolerance, manual-quantity policy"], ["4", "Rates", "Typed pricing rule, effective dates, surcharge/minimum"],
    ["5", "Review", "Validate summary, save draft or save and activate"],
  ];
  stages.forEach((v, i) => { const x = M + i * 230; box(s, x, 210, 202, 280, i === 4 ? C.blueSoft : C.panel, i === 4 ? C.blue : C.rule, 7); textBox(s, v[0], x + 15, 228, 45, 45, { size: 30, bold: true, color: C.blue }); textBox(s, v[1], x + 15, 292, 172, 62, { size: 22, bold: true }); textBox(s, v[2], x + 15, 370, 172, 87, { size: 16, color: C.muted }); });
  textBox(s, "Activation check", M, 527, 200, 30, { size: 24, bold: true });
  textBox(s, "The effective period must be valid, every route needs a non-overlapping effective rate, and measured pricing must have its required quantity source.", 270, 524, 930, 64, { size: 20, color: C.muted });
  notes(s, "Clarify that active contract edits affect future trips only. Existing route/rate snapshots are historical evidence and cannot be destructively rewritten.", ["docs/workflow-improvements.md", "docs/domain-glossary.md"]);
}

// 11 — Contract wizard
{
  const s = deck.slides.add(); addHeader(s, "Build and activate a contract step by step", "Contracts", 11, "Inline creation keeps the wizard open when a permitted customer, location or commodity is missing.");
  await addShot(s, "contract-wizard", 545, 175, 665, 444, "LoadIx five-step contract wizard", "contain");
  step(s, 1, "Start in Contracts", "Select New contract and choose the customer—or create it inline.", M, 180, 445);
  step(s, 2, "Complete each guided page", "Route, commodity/quantity rules and rates must align with how the movement is weighed and billed.", M, 280, 445);
  step(s, 3, "Use review as a control", "Check dates, currency, tolerance, quantity source, rounding and effective rates.", M, 380, 445);
  step(s, 4, "Choose the correct finish", "Save as draft for incomplete review; Save and activate only when ready for load planning.", M, 480, 445, C.green);
  notes(s, "Demonstrate with Training Infrastructure, Demo Mine North to Midrand Customer Site, Aggregate, ZAR and a reconciled-tonne rate as described in the demo guide.", ["Local LoadIx contract wizard screenshot captured 2026-08-24", "docs/workflow-improvements.md", "docs/demo-guide.md"]);
}

// 12 — Load instruction overview
{
  const s = deck.slides.add(); addHeader(s, "A load instruction turns customer demand into planned capacity", "Load instructions", 12, "One active contract route and commodity anchor the request; one instruction can generate several trips.");
  const stages = [
    ["1", "Contract selection", "Instruction number, active contract, route and commodity"], ["2", "Requirements", "Planned quantity, unit, trip guidance and windows"],
    ["3", "Operations", "Customer PO/reference, priority and special instructions"], ["4", "Review", "Save draft or Plan load for trip generation"],
  ];
  stages.forEach((v, i) => { const x = M + i * 290; box(s, x, 225, 255, 260, i === 3 ? C.blueSoft : C.panel, i === 3 ? C.blue : C.rule, 7); textBox(s, v[0], x + 15, 244, 50, 42, { size: 29, bold: true, color: C.blue }); textBox(s, v[1], x + 15, 310, 225, 56, { size: 22, bold: true }); textBox(s, v[2], x + 15, 385, 225, 77, { size: 17, color: C.muted }); });
  textBox(s, "Planning rule", M, 525, 180, 30, { size: 24, bold: true });
  textBox(s, "Draft remains editable. Plan load confirms the request. Generate trips only after quantity, windows, priority and references have been checked.", 250, 522, 950, 66, { size: 20, color: C.muted });
  notes(s, "Explain the difference between customer demand (instruction) and execution units (trips). The instruction remains the fulfilment summary.", ["docs/workflow-improvements.md", "docs/domain-glossary.md"]);
}

// 13 — Load wizard
{
  const s = deck.slides.add(); addHeader(s, "Create a load instruction with complete operational context", "Load instructions", 13, "The selected contract controls available routes, commodities and commercial rules.");
  await addShot(s, "instruction-wizard", 545, 175, 665, 444, "LoadIx four-step load instruction wizard", "contain");
  step(s, 1, "Select the active contract", "Choose its route and allowed commodity; do not substitute a similar record.", M, 180, 445);
  step(s, 2, "Enter quantity and timing", "Use the explicit unit, realistic trip count and collection/delivery windows.", M, 280, 445);
  step(s, 3, "Add dispatch context", "Capture customer reference, priority and actionable special instructions.", M, 380, 445);
  step(s, 4, "Review the plan", "Save draft if uncertain; Plan load only when the request is authorised.", M, 480, 445, C.green);
  notes(s, "Use the demo guide example: 136 tonnes, four trips, fictional PO and current collection/delivery windows.", ["Local LoadIx load instruction wizard screenshot captured 2026-08-24", "docs/workflow-improvements.md", "docs/demo-guide.md"]);
}

// 14 — Generate trips
{
  const s = deck.slides.add(); addHeader(s, "Plan first; generate trips once", "Load instructions", 14, "Trip generation divides remaining quantity and creates stable operational references while protecting retries from duplicates.");
  twoColBullets(s, "Before generation", [
    ["Open the instruction", "Confirm status, requested quantity, unit and remaining quantity."],
    ["Review fulfilment inputs", "Check trip-count guidance, windows, priority and customer reference."],
    ["Select Plan load", "This confirms the instruction for operational generation."],
    ["Choose the trip count", "Use capacity and shift realities; generated quantities total the remaining amount."],
  ], "After generation", [
    ["Confirm references", "Generated trips appear under the instruction with stable LI-…-T… identifiers."],
    ["Check planned totals", "Requested, planned and remaining quantities must reconcile."],
    ["Do not double-submit", "A repeated safe request returns the prior result; conflicting reuse is blocked."],
    ["Continue in Trips", "Assignments and execution now happen on each trip, not on the instruction."],
  ], 180);
  notes(s, "Explain idempotency in plain language: retrying the same action should not create duplicate trips. If the first response is uncertain, refresh before trying a different count.", ["README.md", "docs/product-scope.md", "docs/demo-guide.md"]);
}

// 15 — Dispatch
{
  const s = deck.slides.add(); addHeader(s, "Dispatch only eligible resources", "Trips", 15, "The system checks active, available and compliant drivers, trucks and up to two distinct trailers against overlapping work.");
  await addShot(s, "trips", 520, 175, 690, 444, "LoadIx trips list", "contain");
  step(s, 1, "Filter to Planned", "Search by trip, contract or route and open the correct operational record.", M, 180, 420);
  step(s, 2, "Assign resources", "Select an eligible driver, truck and zero to two trailers; check the planned window.", M, 280, 420);
  step(s, 3, "Confirm the assignment", "Verify names, registrations/fleet numbers and the audit/timeline update.", M, 380, 420);
  step(s, 4, "Dispatch", "Use Dispatch only when the driver and equipment are genuinely ready to begin.", M, 480, 420, C.green);
  notes(s, "Demonstrate a planned synthetic trip. Do not use a post-dispatch reassignment to fix ordinary data-entry mistakes; it requires override permission and a reason.", ["Local LoadIx trips screenshot captured 2026-08-24", "docs/workflow-improvements.md", "docs/demo-guide.md"]);
}

// 16 — Lifecycle controls
{
  const s = deck.slides.add(); addHeader(s, "Use only the next legal lifecycle action", "Trip execution", 16, " The API enforces the same state machine as the screen; cancellation and rejection are reasoned terminal branches.");
  const row1 = ["Planned", "Assigned", "Dispatched", "At collection", "Loading", "Collection complete"];
  const row2 = ["In transit", "At delivery", "Unloading", "Delivery complete", "Reconciliation required", "Completed"];
  for (let i = 0; i < 5; i++) { line(s, 155 + i * 205, 291, 70, 0, C.blue, 3); line(s, 155 + i * 205, 485, 70, 0, C.blue, 3); }
  line(s, 1125, 291, 0, 80, C.blue, 3); line(s, 155, 371, 970, 0, C.blue, 3); line(s, 155, 371, 0, 45, C.blue, 3);
  row1.forEach((v, i) => { const x = M + i * 205; box(s, x, 250, 170, 82, C.panel, C.rule, 6); textBox(s, v, x + 10, 273, 150, 40, { size: 18, bold: true, align: "center", valign: "middle" }); });
  row2.forEach((v, i) => { const x = M + i * 205; box(s, x, 444, 170, 82, i === 5 ? C.blueSoft : C.panel, i === 5 ? C.blue : C.rule, 6); textBox(s, v, x + 10, 465, 150, 42, { size: 18, bold: true, align: "center", valign: "middle" }); });
  tag(s, "CANCELLED — specific reason required", M, 575, 350, "#FDE8E8", C.red);
  tag(s, "REJECTED — specific reason required", 460, 575, 350, "#FFF1D8", C.amber);
  tag(s, "OVERRIDE — permission + reason + audit", 866, 575, 344, C.blueSoft, C.ink);
  notes(s, "Ask learners to name the next action from InTransit and from DeliveryComplete. Reinforce that status is never inferred only from timestamps.", ["README.md", "docs/domain-glossary.md"]);
}

// 17 — Trip workspace
{
  const s = deck.slides.add(); addHeader(s, "Read the full trip before taking action", "Trip execution", 17, "The workspace combines immutable context, documents, finance readiness, assignment controls, lifecycle actions and timeline evidence.");
  await addShot(s, "trip-detail", 505, 175, 705, 444, "LoadIx active trip detail workspace", "contain");
  step(s, 1, "Confirm identity and status", "Check trip reference, customer, route, planned window and current state.", M, 180, 405);
  step(s, 2, "Review assignment and rate snapshot", "Verify driver/equipment and the contract/rate context frozen onto this trip.", M, 280, 405);
  step(s, 3, "Complete evidence panels", "Documents, weights and reconciliation readiness must match the route requirements.", M, 380, 405);
  step(s, 4, "Use the named action", "Then confirm the new state and actor-stamped timeline entry.", M, 480, 405, C.green);
  notes(s, "Scroll through the live trip dialog during delivery. Show that operational notes add context but do not replace structured milestones or captured weights.", ["Local LoadIx trip detail screenshot captured 2026-08-24", "README.md"]);
}

// 18 — Weights and reconciliation
{
  const s = deck.slides.add(); addHeader(s, "Capture weights exactly; reconcile the outcome visibly", "Weights and reconciliation", 18, "Each trip snapshots whether collection weighing, delivery weighing, both or neither are required.");
  const q = [
    ["Both sites", "Compare source and destination net weights against tolerance."],
    ["One site", "The configured measured net becomes the reconciled quantity automatically."],
    ["Neither site", "Reconciliation is not applicable for fixed billing."],
    ["Manual per-tonne", "Requires contract opt-in, override permission, reason and audit evidence."],
  ];
  q.forEach((v, i) => { const x = M + i * 290; box(s, x, 205, 255, 136, i === 0 ? C.blueSoft : C.panel, i === 0 ? C.blue : C.rule, 6); textBox(s, v[0], x + 15, 224, 225, 30, { size: 22, bold: true }); textBox(s, v[1], x + 15, 270, 225, 54, { size: 16, color: C.muted }); });
  step(s, 1, "Record gross and tare", "Use the correct checkpoint, unit, timestamp and weighbridge/ticket reference.", M, 385, 540);
  step(s, 2, "Verify net", "Net = gross − tare. Stop if the unit, sign or plausible quantity is wrong.", M, 480, 540);
  step(s, 3, "Compare and review", "Within tolerance can complete normally; exceptions require evidence and a specific decision reason.", 670, 385, 540, C.green);
  step(s, 4, "Preserve captured facts", "Approval records the decision—it does not rewrite the source or destination weights.", 670, 480, 540, C.green);
  notes(s, "Demonstrate one within-tolerance trip and one seeded 1.250-tonne variance. Explain the trip's QuantitySource in reporting.", ["docs/workflow-improvements.md", "docs/demo-guide.md", "docs/domain-glossary.md"]);
}

// 19 — Documents
{
  const s = deck.slides.add(); addHeader(s, "Attach the right evidence before completion", "Operational documents", 19, "Documents are authorised by tenant and trip, validated on upload, and become immutable when the trip completes.");
  twoColBullets(s, "Upload procedure", [
    ["Open Operational documents", "Work from the correct trip so ownership is unambiguous."],
    ["Choose the document type", "Collection ticket, delivery ticket, proof of delivery, invoice support or Other."],
    ["Select or capture the file", "PDF, JPEG, PNG or WebP; maximum 15 MB in the current configuration."],
    ["Add a useful note and upload", "Keep notes operational; never include passwords, tokens or unnecessary personal data."],
  ], "Verification", [
    ["Watch the upload state", "Use retry only with the same intended file and stable key."],
    ["Open the preview", "Confirm orientation, legibility, document type and trip reference."],
    ["Correct before completion", "Delete/replace only while policy permits and the trip is not complete."],
    ["Confirm required evidence", "POD and weight tickets must satisfy route/customer rules before completion."],
  ], 180);
  notes(s, "The upload validates extension, declared type, file signature, size and malware policy. Original images are preserved; previews are metadata-stripped derivatives.", ["README.md", "docs/product-scope.md"]);
}

// 20 — Operations board
{
  const s = deck.slides.add(); addHeader(s, "Run the shift from the Operations board", "Control tower", 20, "Use live state and filters to find work that is delayed, exceptional or stale—then open the authoritative trip record.");
  await addShot(s, "operations", 510, 175, 700, 444, "LoadIx operations control tower", "contain");
  step(s, 1, "Confirm connection state", "A reconnecting board refetches; disconnected state requires cautious manual refresh.", M, 180, 410);
  step(s, 2, "Filter the workload", "Search plus customer, branch, driver, vehicle, commodity, delayed and exception filters.", M, 280, 410);
  step(s, 3, "Read freshness honestly", "A stale location is shown as stale—not live. Never infer a milestone from map position alone.", M, 380, 410);
  step(s, 4, "Open the trip", "Resolve the issue in its workspace so the action, reason and timeline are recorded.", M, 480, 410, C.green);
  notes(s, "Demonstrate delayed-only and exceptions-only filters. Point out the board is a monitoring surface; the trip remains authoritative.", ["Local LoadIx operations screenshot captured 2026-08-24", "README.md", "docs/product-scope.md"]);
}

// 21 — Driver/offline
{
  const s = deck.slides.add(); addHeader(s, "Drivers progress assigned work with visible sync state", "Driver workflow", 21, "The driver workspace is touch-first and supports intermittent connectivity without silently duplicating actions.");
  const stages = [
    ["1", "Open assigned trip", "Verify route, timing and directions."], ["2", "Record collection", "Arrival, loading, source weights and ticket."],
    ["3", "Travel", "Depart and submit location only after an explicit action."], ["4", "Record delivery", "Arrival, unloading, destination weights and POD."],
    ["5", "Synchronise", "Review queued, failed or conflicted commands before completion."],
  ];
  stages.forEach((v, i) => { const x = M + i * 230; box(s, x, 215, 202, 270, i === 4 ? C.blueSoft : C.panel, i === 4 ? C.blue : C.rule, 7); textBox(s, v[0], x + 15, 234, 45, 42, { size: 29, bold: true, color: C.blue }); textBox(s, v[1], x + 15, 300, 172, 58, { size: 21, bold: true }); textBox(s, v[2], x + 15, 382, 172, 72, { size: 16, color: C.muted }); });
  textBox(s, "Offline rule", M, 525, 170, 30, { size: 24, bold: true });
  textBox(s, "A queued command is not yet confirmed by LoadIx Cloud. Reconnect, review sync state and resolve conflicts—do not repeat actions blindly.", 245, 522, 955, 64, { size: 20, color: C.muted });
  notes(s, "If practical, demonstrate in a narrow viewport with the synthetic driver account. Logout clears the driver's local queue; commands older than seven days require review.", ["README.md", "docs/demo-guide.md", "docs/product-scope.md"]);
}

// 22 — Notifications
{
  const s = deck.slides.add(); addHeader(s, "Treat notifications as an attention queue, not the source of truth", "Notifications", 22, "Open the related record to verify current state; delivery retries and preferences are controlled separately.");
  await addShot(s, "notifications", 540, 175, 670, 444, "LoadIx notification centre", "contain");
  step(s, 1, "Open the bell or Notifications", "Prioritise operational and exception items relevant to your shift.", M, 180, 440);
  step(s, 2, "Read and navigate", "Open the linked customer, instruction, trip or finance record before acting.", M, 280, 440);
  step(s, 3, "Mark read after review", "Read status indicates attention handled, not that the business issue is resolved.", M, 380, 440);
  step(s, 4, "Manage delivery controls", "Users set channel preferences; authorised users review failures and retry queues.", M, 480, 440, C.green);
  notes(s, "Explain that in-app notification remains the default operational channel. Local email/SMS behaviour in the demo is not production delivery.", ["Local LoadIx notifications screenshot captured 2026-08-24", "README.md"]);
}

// 23 — Finance
{
  const s = deck.slides.add(); addHeader(s, "Prepare approved, documented trips for invoicing", "Finance", 23, "LoadIx creates an auditable invoice-preparation batch; it does not issue a SARS tax invoice or post to an accounting ledger.");
  await addShot(s, "finance-queue", 520, 175, 690, 444, "LoadIx invoice-ready queue and batch builder", "contain");
  step(s, 1, "Review tax configuration", "Finance calculations require a reviewed tenant tax setup.", M, 180, 420);
  step(s, 2, "Prepare the trip", "Completed + reconciled + required documents; recalculate from the immutable rate snapshot.", M, 280, 420);
  step(s, 3, "Add and approve costs", "Use the correct cost category; post-approval corrections become explicit adjustments.", M, 380, 420);
  step(s, 4, "Build the batch", "Select approved invoice-ready trips for one customer/contract/currency, review, approve and export.", M, 480, 420, C.green);
  notes(s, "Use the canonical seed's six approved candidates. Show revenue, billable tonnes, tax treatment and costs before batch approval.", ["Local LoadIx invoice-ready queue screenshot captured 2026-08-24", "README.md", "docs/demo-guide.md"]);
}

// 24 — Reports
{
  const s = deck.slides.add(); addHeader(s, "Use reports to explain performance and exceptions", "Reporting", 24, "Every report is tenant-scoped, date-filtered in the tenant time zone and returned with a freshness timestamp.");
  await addShot(s, "reports", 520, 175, 690, 444, "LoadIx operations reporting workspace", "contain");
  step(s, 1, "Set the reporting lens", "Choose report, date range and branch/customer filters before interpreting totals.", M, 180, 420);
  step(s, 2, "Refresh and read freshness", "Confirm the requested period and generation time.", M, 280, 420);
  step(s, 3, "Drill into the right view", "Trip register, variance, contract performance, profitability and invoice status answer different questions.", M, 380, 420);
  step(s, 4, "Export deliberately", "Current pages can export directly; larger extracts use queued export jobs.", M, 480, 420, C.green);
  notes(s, "Demonstrate at least variance and profitability. Explain that South African date selections are resolved to UTC boundaries by the server.", ["Local LoadIx reports screenshot captured 2026-08-24", "README.md", "docs/demo-guide.md"]);
}

// 25 — Daily routine
{
  const s = deck.slides.add(); addHeader(s, "A disciplined shift routine keeps the queue clean", "Daily operating rhythm", 25, "Use repeatable checks at start, during the shift and before handover.");
  const phases = [
    ["START OF SHIFT", C.blue, ["Confirm tenant, branch and API connection", "Review notifications and dashboard counts", "Filter Operations for delayed and exception trips", "Check today's planned/unassigned trips"]],
    ["DURING SHIFT", C.green, ["Dispatch only eligible resources", "Monitor stage changes and stale locations", "Resolve weight/document exceptions in the trip", "Record specific reasons and operational notes"]],
    ["END / HANDOVER", C.navy, ["Review reconciliation-required work", "Confirm completed trips have required evidence", "Hand invoice-ready work to finance", "Record unresolved owner, reason and next action"]],
  ];
  phases.forEach((p, i) => { const x = M + i * 390; box(s, x, 195, 350, 405, C.panel, C.rule, 7); box(s, x, 195, 350, 54, p[1], "none", 7); textBox(s, p[0], x + 15, 212, 320, 25, { size: 17, color: C.white, bold: true, align: "center" }); p[2].forEach((v, j) => step(s, j + 1, v, "", x + 18, 275 + j * 74, 314, p[1])); });
  notes(s, "Adapt the cadence to the operator's shift pattern. The handover standard is an owner plus next action, not a vague status message.", ["docs/product-scope.md", "docs/demo-guide.md"]);
}

// 26 — Troubleshooting
{
  const s = deck.slides.add(); addHeader(s, "Respond to the control that stopped the action", "Exceptions and troubleshooting", 26, "Preserve data and context first; then follow the recovery path that matches the visible symptom.");
  const rows = [
    ["API disconnected", "Pause new mutations; check connection state, refresh once, escalate with time and correlation reference."],
    ["409 conflict / stale version", "Refresh the record, compare the newer change, then reapply only your intended update."],
    ["Permission denied", "Verify role, tenant and record scope. Ask an administrator—do not use another person's account."],
    ["Resource unavailable", "Check active/compliant status and overlapping work; choose another eligible resource or reschedule."],
    ["Weight exception", "Verify units and ticket inputs; review tolerance and evidence; record a reasoned approval/override if authorised."],
    ["Upload rejected", "Check allowed type, extension, size and signature; retry the same valid file without changing the business record."],
    ["Offline command conflict", "Reconnect, review queued status and current trip state; do not repeat lifecycle actions blindly."],
  ];
  textBox(s, "SYMPTOM", M, 180, 285, 28, { size: 15, bold: true, color: C.blue });
  textBox(s, "SAFE RESPONSE", 350, 180, 850, 28, { size: 15, bold: true, color: C.blue });
  rows.forEach((r, i) => { const y = 216 + i * 58; box(s, M, y, 1146, 54, i % 2 ? C.white : C.panel, C.rule); textBox(s, r[0], M + 14, y + 15, 270, 28, { size: 16, bold: true }); textBox(s, r[1], 350, y + 11, 830, 34, { size: 15, color: C.muted }); });
  notes(s, "Support should never request passwords, tokens, credentials, document contents or raw personal-data exports. Capture tenant, workflow, time zone, impact and a safe screenshot/correlation reference.", ["docs/support/customer-support-process.md", "README.md", "docs/product-scope.md"]);
}

// 27 — Practice lab
{
  const s = deck.slides.add(); addHeader(s, "Practice the complete journey in the synthetic tenant", "Guided exercise", 27, "Work in pairs: one learner operates, the other verifies references, state and evidence before each handoff.");
  const actions = [
    ["Create", "Create a synthetic contract for Training Infrastructure and save it as a draft."],
    ["Activate", "Review route, Aggregate commodity, tolerance and reconciled-tonne rate; activate."],
    ["Plan", "Create a 136-tonne instruction with four trips, windows and a fictional PO; plan and generate."],
    ["Dispatch", "Open a planned trip, assign eligible resources and dispatch it."],
    ["Execute", "Advance collection, weights, transit and delivery using the next legal actions."],
    ["Complete", "Attach required evidence, reconcile, complete, prepare finance and find it in a report."],
  ];
  actions.forEach((v, i) => { const col = i % 2; const row = Math.floor(i / 2); const x = col ? 670 : M; const y = 180 + row * 145; step(s, i + 1, v[0], v[1], x, y, 540, col ? C.green : C.blue); });
  tag(s, "SUCCESS = correct references • legal states • complete evidence • visible audit trail", M, 600, 1146, C.navy, C.white);
  notes(s, "Reset the demo after the cohort if required. Learners should narrate why each action is legal before clicking it.", ["docs/demo-guide.md", "docs/workflow-improvements.md"]);
}

// 28 — Knowledge check / close
{
  const s = deck.slides.add(); addHeader(s, "Ready for supervised live use", "Knowledge check", 28, "Before using customer data, each learner should answer these questions and complete the guided exercise without unsafe shortcuts.");
  const qs = [
    ["1", "What makes a contract available for load planning?"],
    ["2", "Where do you assign a driver, truck and trailers?"],
    ["3", "What should you do after a 409 conflict?"],
    ["4", "Does approving a reconciliation exception change captured weights?"],
    ["5", "What does invoice preparation not do?"],
  ];
  qs.forEach((q, i) => { const x = i < 3 ? M : 650; const y = i < 3 ? 185 + i * 122 : 246 + (i - 3) * 150; box(s, x, y, i < 3 ? 535 : 560, 94, i === 4 ? C.blueSoft : C.panel, i === 4 ? C.blue : C.rule, 7); textBox(s, q[0], x + 16, y + 18, 42, 42, { size: 28, bold: true, color: C.blue }); textBox(s, q[1], x + 76, y + 18, (i < 3 ? 440 : 465), 56, { size: 19, bold: true, valign: "middle" }); });
  tag(s, "NEXT STEP  •  complete role-specific coaching and obtain local supervisor sign-off", M, 590, 1146, C.navy, C.white);
  notes(s, "Answer key: 1) Save and activate after all validation. 2) In the Trip workspace. 3) Refresh, compare and reapply the intended change. 4) No; approval records the decision. 5) It does not issue a SARS tax invoice or post to an accounting ledger.\n\nClose by assigning the practice lab and the operator's local SOP/policy module.", ["docs/workflow-improvements.md", "README.md", "docs/product-scope.md"]);
}

await fs.mkdir(RENDER, { recursive: true });
for (const [i, slide] of deck.slides.items.entries()) {
  const png = await deck.export({ slide, format: "png", scale: 1 });
  await fs.writeFile(`${RENDER}/slide-${String(i + 1).padStart(2, "0")}.png`, new Uint8Array(await png.arrayBuffer()));
  const layout = await slide.export({ format: "layout" });
  await fs.writeFile(`${RENDER}/slide-${String(i + 1).padStart(2, "0")}.layout.json`, await layout.text());
}
const montage = await deck.export({ format: "webp", montage: true, scale: 0.55 });
await fs.writeFile(`${RENDER}/deck-montage.webp`, new Uint8Array(await montage.arrayBuffer()));
const pptx = await PresentationFile.exportPptx(deck);
await pptx.save(OUT);
console.log(`Created ${OUT} with ${deck.slides.items.length} slides`);
