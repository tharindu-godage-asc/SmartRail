import { HumanMessage, SystemMessage } from "@langchain/core/messages";
import { parseArgs } from "node:util";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { z } from "zod";
import { createChatModel, invokeModel } from "./model.js";

const baseDir = resolve(import.meta.dirname, "..");
export const DISCOVERY_PATH = resolve(
  baseDir,
  "context",
  "smartrail-discovery.md",
);
export const PERSONAS_DIR = resolve(baseDir, "personas");
export const TRANSCRIPT_DIR = resolve(baseDir, "transcripts");

export interface PanelRole {
  name: string;
  slug: string;
  perspective: string;
  discoverySections: readonly string[];
  decisionIds: readonly string[];
}

export const ROLES: readonly PanelRole[] = [
  {
    name: "Passenger",
    slug: "passenger",
    perspective:
      "Passenger decisions and trust in live information, including the daily commuter and passengers with accessibility or language needs.",
    discoverySections: ["11", "13"],
    decisionIds: [
      "D-02", "D-19", "D-20", "D-25", "D-26", "D-31", "D-38", "D-39",
      "D-45", "D-47", "D-50", "D-53", "D-54",
    ],
  },
  {
    name: "Driver",
    slug: "driver",
    perspective:
      "Treat the driver's non-actor and safety boundary as a scope check; do not invent a SmartRail driver workflow.",
    discoverySections: [],
    decisionIds: ["D-03"],
  },
  {
    name: "Station Manager",
    slug: "station-manager",
    perspective:
      "Station-facing information and staff needs, while respecting the design-only status of station staff and controller-owned platform changes.",
    discoverySections: ["11", "12"],
    decisionIds: ["D-12", "D-18", "D-19", "D-22", "D-23", "D-50", "D-51", "D-53"],
  },
  {
    name: "Railway Controller",
    slug: "railway-controller",
    perspective:
      "Operational exceptions, event verification, overrides, auditability, and passenger information responsibilities merged into the controller for the MVP.",
    discoverySections: ["6", "8", "9", "19.4", "21"],
    decisionIds: [
      "D-10", "D-11", "D-12", "D-36", "D-40", "D-42", "D-43", "D-44",
      "D-49", "ADR-05",
    ],
  },
  {
    name: "Customer-Service Representative",
    slug: "customer-service-representative",
    perspective:
      "Passenger enquiries about stale, missing, or incorrect information; recognize that this responsibility is merged into the controller for the MVP.",
    discoverySections: ["7", "11", "18.3"],
    decisionIds: ["D-10", "D-11", "D-20", "D-39", "D-46", "D-52"],
  },
  {
    name: "Catering Provider",
    slug: "catering-provider",
    perspective:
      "External catering integration and ownership boundaries for menu, payment, refunds, fulfilment, handlers, and versioned messages.",
    discoverySections: ["10", "18.5"],
    decisionIds: [
      "D-13", "D-14", "D-15", "D-16", "D-32", "ADR-12", "ADR-13",
    ],
  },
  {
    name: "Security Engineer",
    slug: "security-engineer",
    perspective:
      "Security assurance and risks grounded in the discovery's security decisions, requirements, open questions, and stated threat-model work.",
    discoverySections: ["14", "18.3", "18.5", "23"],
    decisionIds: [
      "D-27", "D-28", "D-31", "D-35", "D-36", "D-55", "ADR-08", "ADR-12",
    ],
  },
  {
    name: "QA Engineer",
    slug: "qa-engineer",
    perspective:
      "Testability of stated business rules, offline and failure cases, accessibility definition of done, and explicitly required quality evidence.",
    discoverySections: ["6", "7", "8", "14", "23"],
    decisionIds: [
      "D-05", "D-06", "D-08", "D-09", "D-35", "D-39", "D-41", "D-43",
      "D-44", "D-48", "D-49", "D-53", "D-54", "ADR-06", "ADR-09", "ADR-12",
    ],
  },
  {
    name: "Accessibility Specialist",
    slug: "accessibility-specialist",
    perspective:
      "Language, colour-independent status, text scaling, screen readers, and station-display legibility grounded in the discovery.",
    discoverySections: ["11.3", "11.4", "14", "20.4"],
    decisionIds: [
      "D-19", "D-20", "D-25", "D-46", "D-47", "D-50", "D-51", "D-53", "D-54",
    ],
  },
  {
    name: "Product Owner",
    slug: "product-owner",
    perspective:
      "MVP outcome, priorities, decision traceability, open assumptions, and scope boundaries recorded in the discovery.",
    discoverySections: ["13", "14", "23"],
    decisionIds: [
      "D-01", "D-02", "D-03", "D-11", "D-12", "D-13", "D-24", "D-25",
      "D-26", "D-29", "D-30", "D-31", "D-33", "D-34", "ADR-05", "ADR-06",
      "ADR-07", "ADR-08", "ADR-09", "ADR-10", "ADR-11", "ADR-12", "ADR-13",
    ],
  },
];

const SHARED_DISCOVERY_SECTIONS = ["1", "2", "4", "5", "15", "24"] as const;

export interface DiscussionMessage {
  role: string;
  phase: "response" | "challenge";
  content: string;
}

export interface Finding {
  category: string;
  finding: string;
  raised_by: string;
  related_decision_or_brief_section: string;
  suggested_outcome: string;
}

export interface PanelResult {
  topic: string;
  roles: readonly PanelRole[];
  messages: DiscussionMessage[];
  findings: Finding[];
}

export async function exportPanelTranscripts(
  result: PanelResult,
  outputDir = TRANSCRIPT_DIR,
  generatedAt = new Date(),
): Promise<string[]> {
  await mkdir(outputDir, { recursive: true });
  const generated = generatedAt.toISOString();
  const responses = result.messages.filter(
    (message) => message.phase === "response",
  );
  const challenges = result.messages.filter(
    (message) => message.phase === "challenge",
  );
  const files: string[] = [];

  for (const [index, role] of result.roles.entries()) {
    const response = responses.find((message) => message.role === role.name);
    const challenge = challenges.find((message) => message.role === role.name);
    if (!response || !challenge) {
      throw new Error(`Cannot export transcript: ${role.name} response or challenge is missing.`);
    }
    const challengedRole = result.roles[(index + 1) % result.roles.length];
    const content = [
      `# ${role.name} — Stakeholder Response`,
      "",
      `- Generated: ${generated}`,
      `- Panel question: ${result.topic}`,
      `- Source of truth: [SmartRail discovery](../context/smartrail-discovery.md)`,
      `- Role perspective: [${role.name} persona](../personas/${role.slug}.md)`,
      "",
      "## Response",
      "",
      response.content,
      "",
      `## Challenge to ${challengedRole.name}`,
      "",
      challenge.content,
      "",
    ].join("\n");
    const path = resolve(outputDir, `${role.slug}.md`);
    await writeFile(path, content, "utf8");
    files.push(path);
  }

  const finding = result.findings[0];
  if (!finding) {
    throw new Error("Cannot export panel summary: synthesized finding is missing.");
  }
  const summary = [
    "# Stakeholder Panel Summary",
    "",
    `- Generated: ${generated}`,
    `- Requested topic: ${result.topic}`,
    "- Source of truth: [SmartRail discovery](../context/smartrail-discovery.md)",
    "",
    "## Synthesized finding",
    "",
    `**Category:** ${finding.category}`,
    "",
    `**Raised by:** ${finding.raised_by}`,
    "",
    finding.finding,
    "",
    `**Related decisions/sections:** ${finding.related_decision_or_brief_section}`,
    "",
    `**Suggested outcome:** ${finding.suggested_outcome}`,
    "",
  ].join("\n");
  const summaryPath = resolve(outputDir, "_panel-summary.md");
  await writeFile(summaryPath, summary, "utf8");
  files.push(summaryPath);
  return files;
}

export type TextGenerator = (
  systemPrompt: string,
  userPrompt: string,
) => Promise<string>;
export type FindingGenerator = (
  systemPrompt: string,
  userPrompt: string,
  decisionIds: readonly string[],
) => Promise<Finding>;

export interface RunPanelOptions {
  roles?: readonly PanelRole[];
  discovery?: string;
  textGenerator?: TextGenerator;
  findingGenerator?: FindingGenerator;
}

export function extractDecisionIds(discovery: string): string[] {
  const section = discovery.split("## 22. Decision Log (summary)")[1]?.split(
    /^## /m,
  )[0];
  if (!section) {
    throw new Error(
      "The discovery document is missing its section 22 decision log.",
    );
  }

  const ids = new Set<string>();
  for (const line of section.split(/\r?\n/)) {
    const range = line.match(/\b(ADR|D)-(\d{2})\s*[–-]\s*(ADR|D)-(\d{2})\b/);
    if (range && range[1] === range[3]) {
      for (let number = Number(range[2]); number <= Number(range[4]); number++) {
        ids.add(`${range[1]}-${String(number).padStart(2, "0")}`);
      }
    }
    const rangePrefix = range?.[1];
    const rangeStart = range ? Number(range[2]) : -1;
    const rangeEnd = range ? Number(range[4]) : -1;
    for (const match of line.matchAll(/\b(?:D|ADR)-\d{2}\b/g)) {
      const id = match[0];
      const [, prefix, number] = id.match(/^(D|ADR)-(\d{2})$/) ?? [];
      const value = Number(number);
      if (
        rangePrefix &&
        prefix === rangePrefix &&
        value >= rangeStart &&
        value <= rangeEnd
      ) {
        continue;
      }
      ids.add(id);
    }
  }

  if (ids.size === 0) {
    throw new Error("No decision IDs were found in the discovery decision log.");
  }
  return [...ids];
}

export function createFindingSchema(decisionIds: readonly string[]) {
  if (decisionIds.length === 0) {
    throw new Error("At least one decision ID is required for findings.");
  }
  const [first, ...rest] = decisionIds;
  return z.object({
    category: z.enum([first, ...rest] as [string, ...string[]]),
    finding: z.string().trim().min(1),
    raised_by: z.string().trim().min(1),
    related_decision_or_brief_section: z.string().trim().min(1),
    suggested_outcome: z.string().trim().min(1),
  });
}

export function validateFinding(
  value: unknown,
  decisionIds: readonly string[],
): Finding {
  return createFindingSchema(decisionIds).parse(value);
}

export async function loadDiscovery(): Promise<string> {
  return readFile(DISCOVERY_PATH, "utf8");
}

async function loadPersona(role: PanelRole): Promise<string> {
  return readFile(resolve(PERSONAS_DIR, `${role.slug}.md`), "utf8");
}

export function extractDiscoverySections(
  discovery: string,
  requestedSections: readonly string[],
): string {
  const sections: string[] = [];
  for (const requested of requestedSections) {
    const level = requested.includes(".") ? 3 : 2;
    const escaped = requested.replaceAll(".", "\\.");
    const heading = new RegExp(`^#{${level}}\\s+${escaped}(?:\\s|\\.|$).*`, "m");
    const match = heading.exec(discovery);
    if (!match || match.index === undefined) {
      throw new Error(`Discovery document is missing section ${requested}.`);
    }
    const start = match.index;
    const nextHeading = new RegExp(`^#{1,${level}}\\s`, "gm");
    nextHeading.lastIndex = heading.lastIndex;
    const next = nextHeading.exec(discovery);
    sections.push(discovery.slice(start, next?.index ?? discovery.length).trim());
  }
  return sections.join("\n\n");
}

export function extractDecisionRows(
  discovery: string,
  requestedIds: readonly string[],
): string {
  const section = discovery.split("## 22. Decision Log (summary)")[1]?.split(
    /^## /m,
  )[0];
  if (!section) {
    throw new Error(
      "The discovery document is missing its section 22 decision log.",
    );
  }
  const ids = new Set(requestedIds);
  const rows = section.split(/\r?\n/).filter((line) => {
    const range = line.match(/^\|\s*(ADR|D)-(\d{2})\s*[–-]\s*(ADR|D)-(\d{2})\b/);
    if (range && range[1] === range[3]) {
      for (let number = Number(range[2]); number <= Number(range[4]); number++) {
        if (ids.has(`${range[1]}-${String(number).padStart(2, "0")}`)) {
          return true;
        }
      }
      return false;
    }
    const id = line.match(/^\|\s*((?:D|ADR)-\d{2})\b/);
    return id ? ids.has(id[1]) : false;
  });
  return rows.join("\n");
}

export function buildDiscoveryExcerpt(
  discovery: string,
  focusSections: readonly string[],
  decisionIds: readonly string[],
): string {
  const sections = extractDiscoverySections(discovery, [
    ...SHARED_DISCOVERY_SECTIONS,
    ...focusSections,
  ]);
  const decisions = extractDecisionRows(discovery, decisionIds);
  return [
    "Extracts below are copied directly from the canonical discovery document. Omitted sections are not evidence.",
    sections,
    decisions ? `## Relevant decision-log entries\n${decisions}` : "",
  ].filter(Boolean).join("\n\n");
}

function sourceMaterial(discoveryExcerpt: string, persona: string): string {
  return [
    "AUTHORITATIVE SOURCE — the excerpts below come from SmartRail's discovery document and are the source of truth. Do not invent facts or treat omitted requirements as settled.",
    "The role persona is only a concise perspective. If it differs from discovery, follow discovery and flag the discrepancy.",
    `\n## Relevant discovery excerpts\n${discoveryExcerpt}`,
    `\n## Role persona\n${persona}`,
  ].join("\n");
}

async function generateText(
  systemPrompt: string,
  userPrompt: string,
): Promise<string> {
  const messages = [
    new SystemMessage(systemPrompt),
    new HumanMessage(userPrompt),
  ];
  const response = await invokeModel(
    `${systemPrompt}\n${userPrompt}`,
    () => createChatModel().invoke(messages),
  );
  const answer = response.text;
  if (typeof answer !== "string" || answer.trim().length === 0) {
    throw new TypeError("Expected a non-empty text response from Gemini.");
  }
  return answer.trim();
}

async function generateFinding(
  systemPrompt: string,
  userPrompt: string,
  decisionIds: readonly string[],
): Promise<Finding> {
  const model = createChatModel().withStructuredOutput(
    createFindingSchema(decisionIds),
  );
  const messages = [
    new SystemMessage(systemPrompt),
    new HumanMessage(userPrompt),
  ];
  const finding = await invokeModel(
    `${systemPrompt}\n${userPrompt}`,
    () => model.invoke(messages),
  );
  return validateFinding(finding, decisionIds);
}

export async function runPanel(
  requestedTopic: string,
  options: RunPanelOptions = {},
): Promise<PanelResult> {
  const roles = options.roles ?? ROLES;
  if (roles.length === 0) throw new Error("A panel must have at least one role.");

  const discovery = options.discovery ?? (await loadDiscovery());
  const decisionIds = extractDecisionIds(discovery);
  const text = options.textGenerator ?? generateText;
  const synthesize = options.findingGenerator ?? generateFinding;
  const facilitatorExcerpt = buildDiscoveryExcerpt(
    discovery,
    ["13", "23"],
    [],
  );

  const facilitatorQuestion = await text(
    "You are the SmartRail stakeholder-panel facilitator. The supplied discovery document is authoritative. Use only it to set one specific, open-ended panel question based on the requested topic. Do not answer the question.",
    `Requested topic: ${requestedTopic}\n\n${sourceMaterial(facilitatorExcerpt, "No role persona applies to the facilitator.")}\n\nDecision IDs available for citations: ${decisionIds.join(", ")}\n\nReturn only the single question.`,
  );
  const topic = facilitatorQuestion.trim();
  if (!topic) throw new Error("The facilitator returned an empty question.");

  const messages: DiscussionMessage[] = [];
  for (const role of roles) {
    const persona = await loadPersona(role);
    const roleExcerpt = buildDiscoveryExcerpt(
      discovery,
      role.discoverySections,
      role.decisionIds,
    );
    const answer = await text(
      `You are the ${role.name}. Focus on ${role.perspective} Use only facts in the authoritative discovery excerpts. Label uncertainties as questions. Cite D-xx or ADR-xx IDs when relevant. Raise concerns, not solutions. Be concise: at most 5 bullets and 180 words.`,
      `Panel question: ${topic}\n\n${sourceMaterial(roleExcerpt, persona)}`,
    );
    messages.push({ role: role.name, phase: "response", content: answer });
  }

  for (let index = 0; index < roles.length; index++) {
    const role = roles[index];
    const target = roles[(index + 1) % roles.length];
    const targetMessage = messages.find(
      (message) => message.phase === "response" && message.role === target.name,
    );
    if (!targetMessage) {
      throw new Error(`Cannot challenge ${target.name}: their response is missing.`);
    }
    const persona = await loadPersona(role);
    const roleExcerpt = buildDiscoveryExcerpt(
      discovery,
      role.discoverySections,
      role.decisionIds,
    );
    const challenge = await text(
      `You are the ${role.name}. Challenge one specific assumption or consequence in the other stakeholder's point. Ground your challenge in the authoritative discovery excerpts and cite decision IDs where relevant. Do not invent facts or offer solutions. Be concise: at most 3 bullets and 100 words.`,
      `Panel question: ${topic}\nYour perspective: ${role.perspective}\nPoint by ${target.name}:\n${targetMessage.content}\n\n${sourceMaterial(roleExcerpt, persona)}`,
    );
    messages.push({ role: role.name, phase: "challenge", content: challenge });
  }

  const discussion = messages
    .map(
      (message) =>
        `[${message.phase.toUpperCase()} — ${message.role}]\n${message.content}`,
    )
    .join("\n\n");
  const discussionDecisionIds = [
    ...new Set(
      [...discussion.matchAll(/\b(?:D|ADR)-\d{2}\b/g)].map((match) => match[0]),
    ),
  ];
  const synthesisExcerpt = [
    extractDiscoverySections(discovery, ["1", "23"]),
    extractDecisionRows(discovery, discussionDecisionIds),
  ].filter(Boolean).join("\n\n");
  const finding = await synthesize(
    `You are the SmartRail panel synthesiser. Consolidate the discussion into one actionable, evidence-grounded finding. The discovery document is authoritative. Do not invent facts. Use the most directly relevant decision ID as category; valid values are: ${decisionIds.join(", ")}. Set raised_by to the role or roles that raised the concern. The related field may cite additional decisions or brief sections.`,
    `Panel question: ${topic}\n\n${sourceMaterial(synthesisExcerpt, "Role-specific perspectives are reflected in the discussion below.")}\n\nDiscussion:\n${discussion}`,
    decisionIds,
  );

  return {
    topic,
    roles,
    messages,
    findings: [validateFinding(finding, decisionIds)],
  };
}

function parseCliArgs(argv: string[]) {
  const { positionals, values } = parseArgs({
    args: argv,
    options: {
      help: { type: "boolean", short: "h" },
    },
    allowPositionals: true,
  });
  if (values.help || positionals.length !== 1) {
    console.log("Usage: npm run panel -- <topic>");
    process.exitCode = values.help ? 0 : 2;
    return undefined;
  }
  return positionals[0];
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  const topic = parseCliArgs(process.argv.slice(2));
  if (topic) {
    try {
      const result = await runPanel(topic);
      for (const finding of result.findings) {
        console.log(JSON.stringify(finding, null, 2));
      }
      const transcriptFiles = await exportPanelTranscripts(result);
      console.log(`\nPersona transcripts saved to: ${TRANSCRIPT_DIR}`);
      console.log(`Wrote ${transcriptFiles.length - 1} persona files and a panel summary.`);
    } catch (error) {
      console.error(error instanceof Error ? error.message : error);
      process.exitCode = 1;
    }
  }
}
