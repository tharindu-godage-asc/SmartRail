import { mkdir, readFile, writeFile } from "node:fs/promises";
import { parseArgs } from "node:util";
import { resolve } from "node:path";
import {
  ROLES,
  TRANSCRIPT_DIR,
  buildDiscoveryExcerpt,
  generateText,
  loadDiscovery,
  loadPersona,
  sourceMaterial,
  type PanelRole,
} from "./panel.js";
import { activeProvider, currentKeyNumber, loadApiKeys, setStartingKey } from "./model.js";

/**
 * Resumable, one-persona-at-a-time transcript generation.
 * State lives in transcripts/_state.json, so each run only spends requests
 * on work that is not done yet (important on the free Gemini tier).
 * Each role responds, then challenges the previous role in panel order.
 */
interface RoleState {
  response?: string;
  challenge?: string;
  challengedRole?: string;
  /** Set in --pain mode: the one pain point this persona was asked about. */
  pain?: string;
}
interface State {
  topic: string;
  question?: string;
  roles: Record<string, RoleState>;
}

const statePath = resolve(TRANSCRIPT_DIR, "_state.json");

async function loadState(topic: string | undefined, keepAny = false): Promise<State> {
  try {
    const state = JSON.parse(await readFile(statePath, "utf8")) as State;
    if (!topic || keepAny || topic === state.topic) return state;
  } catch {
    // no state yet
  }
  if (!topic) throw new Error("No existing run; pass a topic (or use --pain).");
  return { topic, roles: {} };
}

async function save(state: State): Promise<void> {
  await mkdir(TRANSCRIPT_DIR, { recursive: true });
  await writeFile(statePath, JSON.stringify(state, null, 2), "utf8");
}

async function writeRoleFile(role: PanelRole, state: State): Promise<void> {
  const entry = state.roles[role.slug];
  const lines = [
    `# ${role.name} — Stakeholder Transcript`,
    "",
    entry.pain ? `- Pain point: ${entry.pain}` : `- Panel question: ${state.question}`,
    "- Source of truth: [SmartRail discovery](../context/smartrail-discovery.md)",
    `- Role perspective: [${role.name} persona](../personas/${role.slug}.md)`,
    "",
    "## Response",
    "",
    entry.response ?? "_pending_",
    "",
  ];
  if (entry.challenge) {
    lines.push(`## Challenge to ${entry.challengedRole}`, "", entry.challenge, "");
  }
  await writeFile(resolve(TRANSCRIPT_DIR, `${role.slug}.md`), lines.join("\n"), "utf8");
}

/** One request: the persona responds to a single pain point (no panel question, no challenge). */
async function runPain(role: PanelRole, state: State, discovery: string, pain: string): Promise<void> {
  let entry = (state.roles[role.slug] ??= {});
  if (entry.pain !== pain) entry = state.roles[role.slug] = { pain };
  if (!entry.response) {
    const persona = await loadPersona(role);
    const excerpt = buildDiscoveryExcerpt(discovery, role.discoverySections, role.decisionIds);
    console.log(`[${role.name}] pain-point response (key #${currentKeyNumber()})...`);
    entry.response = await generateText(
      `You are the ${role.name}. Focus on ${role.perspective} Use only facts in the authoritative discovery excerpts. Label uncertainties as questions. Cite D-xx or ADR-xx IDs when relevant. Say whether the discovery addresses this pain point and what is still open. Raise concerns, not solutions. Be concise: at most 5 bullets and 180 words.`,
      `Pain point from your role: ${pain}

${sourceMaterial(excerpt, persona)}`,
    );
    await save(state);
  }
  await writeRoleFile(role, state);
  console.log(`[${role.name}] saved transcripts/${role.slug}.md`);
}

async function runRole(role: PanelRole, state: State, discovery: string): Promise<void> {
  const entry = (state.roles[role.slug] ??= {});
  const persona = await loadPersona(role);
  const excerpt = buildDiscoveryExcerpt(discovery, role.discoverySections, role.decisionIds);

  if (!state.question) {
    console.log("Generating panel question...");
    const facilitatorExcerpt = buildDiscoveryExcerpt(discovery, ["13", "23"], []);
    state.question = (
      await generateText(
        "You are the SmartRail stakeholder-panel facilitator. The supplied discovery document is authoritative. Use only it to set one specific, open-ended panel question based on the requested topic. Do not answer the question.",
        `Requested topic: ${state.topic}\n\n${sourceMaterial(facilitatorExcerpt, "No role persona applies to the facilitator.")}\n\nReturn only the single question.`,
      )
    ).trim();
    await save(state);
  }

  if (!entry.response) {
    console.log(`[${role.name}] response (key #${currentKeyNumber()})...`);
    entry.response = await generateText(
      `You are the ${role.name}. Focus on ${role.perspective} Use only facts in the authoritative discovery excerpts. Label uncertainties as questions. Cite D-xx or ADR-xx IDs when relevant. Raise concerns, not solutions. Be concise: at most 5 bullets and 180 words.`,
      `Panel question: ${state.question}\n\n${sourceMaterial(excerpt, persona)}`,
    );
    await save(state);
  }

  const index = ROLES.findIndex((r) => r.slug === role.slug);
  const target = index > 0 ? ROLES[index - 1] : undefined;
  const targetResponse = target && state.roles[target.slug]?.response;
  if (!entry.challenge && target && targetResponse) {
    console.log(`[${role.name}] challenge to ${target.name} (key #${currentKeyNumber()})...`);
    entry.challenge = await generateText(
      `You are the ${role.name}. Challenge one specific assumption or consequence in the other stakeholder's point. Ground your challenge in the authoritative discovery excerpts and cite decision IDs where relevant. Do not invent facts or offer solutions. Be concise: at most 3 bullets and 100 words.`,
      `Panel question: ${state.question}\nYour perspective: ${role.perspective}\nPoint by ${target.name}:\n${targetResponse}\n\n${sourceMaterial(excerpt, persona)}`,
    );
    entry.challengedRole = target.name;
    await save(state);
  }
  await writeRoleFile(role, state);
  console.log(`[${role.name}] saved transcripts/${role.slug}.md`);
}

const { positionals, values } = parseArgs({
  allowPositionals: true,
  options: {
    role: { type: "string", multiple: true },
    next: { type: "string" },
    key: { type: "string" },
    provider: { type: "string" },
    list: { type: "boolean" },
    pain: { type: "string" },
  },
});

try {
  const topicArg = positionals.join(" ") || (values.pain ? "per-persona pain points" : undefined);
  const state = await loadState(topicArg, Boolean(values.pain));
  if (values.list) {
    for (const r of ROLES) {
      const e = state.roles[r.slug];
      console.log(
        `${r.slug.padEnd(34)} response=${e?.response ? "done" : "-"} challenge=${e?.challenge ? "done" : "-"}`,
      );
    }
    process.exit(0);
  }
  if (values.provider) process.env.LLM_PROVIDER = values.provider;
  const keys = loadApiKeys();
  if (keys.length === 0) {
    throw new Error(`No ${activeProvider()} keys found. Set ${activeProvider() === "groq" ? "GROQ" : "GEMINI"}_API_KEYS=key1,key2,... in .env`);
  }
  if (values.key) setStartingKey(Number(values.key) - 1);
  console.log(`Provider ${activeProvider()}: using ${keys.length} key(s), starting at #${currentKeyNumber()}. Topic: ${state.topic}`);

  const discovery = await loadDiscovery();
  let selected: PanelRole[];
  if (values.role?.length) {
    selected = values.role.map((slug) => {
      const found = ROLES.find((r) => r.slug === slug);
      if (!found) {
        throw new Error(`Unknown role "${slug}". Use: ${ROLES.map((r) => r.slug).join(", ")}`);
      }
      return found;
    });
  } else {
    const pending = ROLES.filter(
      (r) => !state.roles[r.slug]?.response || (r !== ROLES[0] && !state.roles[r.slug]?.challenge),
    );
    selected = pending.slice(0, Number(values.next ?? pending.length));
  }
  if (selected.length === 0) console.log("Everything is already generated.");
  if (values.pain) {
    if (selected.length !== 1) throw new Error("--pain needs exactly one --role.");
    await runPain(selected[0], state, discovery, values.pain);
  } else {
    for (const role of selected) await runRole(role, state, discovery);
  }
} catch (error) {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
}
