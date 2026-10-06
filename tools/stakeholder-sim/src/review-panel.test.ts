import { mkdtemp, readFile, rm, stat } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ROLES, type Finding, type TextGenerator } from "./panel.js";
import { runReviewedPanel, selectRoles } from "./review-panel.js";

const discovery = `## 1. Problem Statement
The problem is fragmented live information.

## 2. Context and Assumptions
SmartRail is a design exercise.

## 4. Actors and MVP Status
Passengers and staff are stakeholders.

## 5. Core Design Principles
Use one authoritative source.

## 11. Station Experience
Station information is visible.

## 13. POV Statements and HMW Questions
Help passengers make informed decisions.

## 15. MoSCoW Summary
Scope is prioritized.

## 22. Decision Log (summary)
| D-01 | Problem |
| ADR-05 | Overrides |

## 23. Open Questions and Edge Cases
Validate assumptions.

## 24. Out of Scope / Deferred
Unrelated scope is deferred.
`;
const finding: Finding = {
  category: "D-01",
  finding: "Offline reports may be duplicated.",
  raised_by: "QA Engineer",
  related_decision_or_brief_section: "D-35",
  suggested_outcome: "Test idempotent event handling.",
};
const textGenerator: TextGenerator = async (system, prompt) => {
  if (prompt.includes("Return only the single question.")) return "How to test sync?";
  if (prompt.includes("Point by ")) return "The event ID should persist through retries.";
  return `${system.match(/You are the ([^.]+)\./)?.[1] ?? "Role"} concern.`;
};

let outputDir: string;
afterEach(async () => {
  if (outputDir) await rm(outputDir, { recursive: true, force: true });
});

async function run(reviewAction: "accept" | "edit" | "reject") {
  outputDir = await mkdtemp(join(tmpdir(), "smartrail-review-"));
  return runReviewedPanel("Offline sync", ROLES.slice(0, 2), {
    outputDir,
    discovery,
    textGenerator,
    findingGenerator: async () => finding,
    reviewCallback: async () =>
      reviewAction === "edit"
        ? { action: "edit", edited_fields: { finding: "Edited, reviewed finding." } }
        : { action: reviewAction },
  });
}

describe("review and export", () => {
  it("accepts a finding, appends CSV, and saves the discovery-grounded transcript", async () => {
    const { result, csvPath, transcriptPath } = await run("accept");

    expect(result.accepted_findings).toEqual([finding]);
    expect((await readFile(csvPath, "utf8")).split("\n")).toHaveLength(3);
    const transcript = JSON.parse(await readFile(transcriptPath, "utf8"));
    expect(transcript.context).toBe(discovery);
    expect(transcript.review_decisions[0].action).toBe("accept");
  });

  it("exports edited findings", async () => {
    const { result, csvPath } = await run("edit");

    expect(result.accepted_findings[0].finding).toBe("Edited, reviewed finding.");
    expect(await readFile(csvPath, "utf8")).toContain('"Edited, reviewed finding."');
  });

  it("does not create a CSV when the finding is rejected", async () => {
    const { result, csvPath, transcriptPath } = await run("reject");

    expect(result.accepted_findings).toEqual([]);
    await expect(stat(csvPath)).rejects.toMatchObject({ code: "ENOENT" });
    expect(JSON.parse(await readFile(transcriptPath, "utf8")).review_decisions[0].action)
      .toBe("reject");
  });

  it("validates selected roles", () => {
    expect(selectRoles(["passenger", "Railway Controller"]).map((role) => role.name))
      .toEqual(["Passenger", "Railway Controller"]);
    expect(() => selectRoles(["Passenger"])).toThrow("at least two");
    expect(() => selectRoles(["Passenger", "passenger"])).toThrow("more than once");
    expect(() => selectRoles(["Unknown", "Passenger"])).toThrow("Unknown role");
  });
});
