import { appendFile, mkdir, stat, writeFile } from "node:fs/promises";
import { createInterface } from "node:readline/promises";
import { stdin, stdout } from "node:process";
import { randomUUID } from "node:crypto";
import { parseArgs } from "node:util";
import { resolve } from "node:path";
import {
  extractDecisionIds,
  loadDiscovery,
  ROLES,
  runPanel,
  validateFinding,
  type Finding,
  type PanelRole,
  type PanelResult,
  type RunPanelOptions,
} from "./panel.js";

const baseDir = resolve(import.meta.dirname, "..");
const outputDir = resolve(baseDir, "output");
const csvFields = [
  "category",
  "finding",
  "raised_by",
  "related_decision_or_brief_section",
  "suggested_outcome",
] as const;

export type ReviewAction = "accept" | "edit" | "reject";
export interface ReviewDecision {
  action: ReviewAction;
  original_finding: Finding;
  accepted_finding: Finding | null;
}

export interface ReviewedPanelResult extends PanelResult {
  review_decisions: ReviewDecision[];
  accepted_findings: Finding[];
}

export type ReviewCallback = (finding: Finding) => Promise<{
  action: ReviewAction;
  edited_fields?: Partial<Finding>;
}>;

export function selectRoles(names?: string[]): PanelRole[] {
  if (!names || names.length === 0) return [...ROLES];
  const roleByName = new Map(ROLES.map((role) => [role.name.toLowerCase(), role]));
  const selected: PanelRole[] = [];
  for (const inputName of names) {
    const role = roleByName.get(inputName.trim().toLowerCase());
    if (!role) {
      throw new Error(
        `Unknown role ${JSON.stringify(inputName)}. Choose from: ${ROLES.map((item) => item.name).join(", ")}`,
      );
    }
    if (selected.includes(role)) {
      throw new Error(`Role ${JSON.stringify(role.name)} was selected more than once.`);
    }
    selected.push(role);
  }
  if (selected.length < 2) {
    throw new Error("A reviewed panel needs at least two stakeholder roles.");
  }
  return selected;
}

async function askAction(finding: Finding): Promise<{
  action: ReviewAction;
  edited_fields?: Partial<Finding>;
}> {
  const terminal = createInterface({ input: stdin, output: stdout });
  try {
    while (true) {
      const action = (await terminal.question("Review finding [accept/edit/reject]: "))
        .trim()
        .toLowerCase();
      if (!["accept", "edit", "reject"].includes(action)) {
        console.log("Enter accept, edit, or reject.");
        continue;
      }
      if (action !== "edit") return { action: action as ReviewAction };

      const labels: Record<keyof Finding, string> = {
        category: "Category decision ID",
        finding: "Finding",
        raised_by: "Raised by",
        related_decision_or_brief_section: "Related decision/brief section",
        suggested_outcome: "Suggested outcome",
      };
      const editedFields: Partial<Finding> = {};
      for (const field of csvFields) {
        const value = (
          await terminal.question(`${labels[field]} [${finding[field]}] (blank keeps current): `)
        ).trim();
        if (value) Object.assign(editedFields, { [field]: value });
      }
      return { action: "edit", edited_fields: editedFields };
    }
  } finally {
    terminal.close();
  }
}

function csvCell(value: string): string {
  return `"${value.replaceAll('"', '""')}"`;
}

async function appendAcceptedFindings(path: string, findings: Finding[]) {
  if (findings.length === 0) return;
  await mkdir(resolve(path, ".."), { recursive: true });
  let hasHeader = false;
  try {
    hasHeader = (await stat(path)).size > 0;
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error;
  }
  const lines = findings.map((finding) =>
    csvFields.map((field) => csvCell(finding[field])).join(","),
  );
  if (!hasHeader) lines.unshift(csvFields.map(csvCell).join(","));
  await appendFile(path, `${lines.join("\n")}\n`, "utf8");
}

export async function runReviewedPanel(
  topic: string,
  roles: readonly PanelRole[],
  options: {
    reviewCallback?: ReviewCallback;
    outputDir?: string;
    discovery?: string;
    textGenerator?: RunPanelOptions["textGenerator"];
    findingGenerator?: RunPanelOptions["findingGenerator"];
  } = {},
): Promise<{ result: ReviewedPanelResult; csvPath: string; transcriptPath: string }> {
  const discovery = options.discovery ?? (await loadDiscovery());
  const result = await runPanel(topic, {
    roles,
    discovery,
    textGenerator: options.textGenerator,
    findingGenerator: options.findingGenerator,
  });
  const decisionIds = extractDecisionIds(discovery);
  const reviewCallback = options.reviewCallback ?? askAction;
  const reviewDecisions: ReviewDecision[] = [];
  const acceptedFindings: Finding[] = [];

  for (const originalFinding of result.findings) {
    console.log("\nFinding for review:");
    console.log(JSON.stringify(originalFinding, null, 2));
    let review = await reviewCallback(originalFinding);
    while (true) {
      try {
        const acceptedFinding =
          review.action === "reject"
            ? null
            : validateFinding(
                {
                  ...originalFinding,
                  ...(review.action === "edit" ? review.edited_fields : {}),
                },
                decisionIds,
              );
        if (acceptedFinding) acceptedFindings.push(acceptedFinding);
        reviewDecisions.push({
          action: review.action,
          original_finding: originalFinding,
          accepted_finding: acceptedFinding,
        });
        break;
      } catch (error) {
        if (options.reviewCallback) throw error;
        console.error(`Invalid edit: ${error instanceof Error ? error.message : error}`);
        review = await reviewCallback(originalFinding);
      }
    }
  }

  const runId = `${new Date().toISOString().replaceAll(/[-:.]/g, "")}-${randomUUID().slice(0, 8)}`;
  const resolvedOutput = options.outputDir ?? outputDir;
  const csvPath = resolve(resolvedOutput, "findings.csv");
  const transcriptPath = resolve(resolvedOutput, "transcripts", `${runId}.json`);
  await mkdir(resolve(transcriptPath, ".."), { recursive: true });
  const reviewedResult: ReviewedPanelResult = {
    ...result,
    review_decisions: reviewDecisions,
    accepted_findings: acceptedFindings,
  };
  await writeFile(
    transcriptPath,
    `${JSON.stringify(
      {
        run_id: runId,
        created_at: new Date().toISOString(),
        topic,
        context: discovery,
        roles: roles.map((role) => role.name),
        panel_question: result.topic,
        messages: reviewedResult.messages,
        findings: reviewedResult.findings,
        review_decisions: reviewedResult.review_decisions,
        accepted_findings: reviewedResult.accepted_findings,
      },
      null,
      2,
    )}\n`,
    "utf8",
  );
  await appendAcceptedFindings(csvPath, acceptedFindings);
  return { result: reviewedResult, csvPath, transcriptPath };
}

function parseCliArgs(argv: string[]) {
  const { positionals, values } = parseArgs({
    args: argv,
    options: {
      roles: { type: "string" },
      help: { type: "boolean", short: "h" },
    },
    allowPositionals: true,
  });
  if (values.help || positionals.length !== 1) {
    console.log(
      `Usage: npm run review -- <topic> [--roles "${ROLES.slice(0, 2).map((role) => role.name).join(",")}"]`,
    );
    console.log(`Available roles: ${ROLES.map((role) => role.name).join(", ")}`);
    process.exitCode = values.help ? 0 : 2;
    return undefined;
  }
  const roleNames = values.roles ? values.roles.split(",") : undefined;
  return { topic: positionals[0], roles: selectRoles(roleNames) };
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  const args = parseCliArgs(process.argv.slice(2));
  if (args) {
    try {
      const { result, csvPath, transcriptPath } = await runReviewedPanel(
        args.topic,
        args.roles,
      );
      console.log(`\nTranscript saved to: ${transcriptPath}`);
      console.log(
        result.accepted_findings.length
          ? `Accepted findings exported to: ${csvPath}`
          : "No findings accepted; the CSV was not changed.",
      );
    } catch (error) {
      console.error(error instanceof Error ? error.message : error);
      process.exitCode = 1;
    }
  }
}
