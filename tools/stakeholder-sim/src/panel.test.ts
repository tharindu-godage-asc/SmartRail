import { describe, expect, it, vi } from "vitest";
import { readFile } from "node:fs/promises";
import {
  extractDecisionIds,
  extractDecisionRows,
  buildDiscoveryExcerpt,
  exportPanelTranscripts,
  loadDiscovery,
  PERSONAS_DIR,
  ROLES,
  runPanel,
  validateFinding,
  type DiscussionMessage,
  type Finding,
} from "./panel.js";
import { resolve } from "node:path";

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

## 12. Component Scope
Displays are in scope.

## 13. POV Statements and HMW Questions
Help passengers make informed decisions.

## 15. MoSCoW Summary
Scope is prioritized.

## 22. Decision Log (summary)
| D-01 | Problem |
| ADR-05 | Overrides |
| ADR-07 – ADR-09 | Architecture |

## 23. Open Questions and Edge Cases
Validate assumptions.

## 24. Out of Scope / Deferred
Unrelated scope is deferred.
`;

const finding: Finding = {
  category: "D-01",
  finding: "Offline reports can conflict with manual event entry.",
  raised_by: "QA Engineer",
  related_decision_or_brief_section: "A-04; D-35",
  suggested_outcome: "Clarify duplicate handling for phone fallback.",
};

describe("stakeholder panel", () => {
  it("uses all requested role responses and sequential challenges", async () => {
    const roles = ROLES.slice(0, 3);
    const textGenerator = vi.fn(async (system: string, prompt: string) => {
      if (prompt.includes("Return only the single question.")) {
        return "How should the status stay trustworthy?";
      }
      if (prompt.includes("Point by ")) return "Challenge grounded in the discovery.";
      const role = system.match(/You are the ([^.]+)\./)?.[1] ?? "Role";
      return `${role} concern.`;
    });
    const findingGenerator = vi.fn(async () => finding);

    const result = await runPanel("Passenger information", {
      roles,
      discovery,
      textGenerator,
      findingGenerator,
    });

    expect(result.topic).toBe("How should the status stay trustworthy?");
    expect(result.messages.filter((item) => item.phase === "response").map((m) => m.role))
      .toEqual(roles.map((role) => role.name));
    expect(result.messages.filter((item) => item.phase === "challenge").map((m) => m.role))
      .toEqual(roles.map((role) => role.name));
    const challengePrompts = textGenerator.mock.calls.filter(([, prompt]) =>
      prompt.includes("Point by "),
    );
    challengePrompts.forEach(([, prompt], index) => {
      expect(prompt).toContain(`Point by ${roles[(index + 1) % roles.length].name}:`);
    });
    expect(findingGenerator).toHaveBeenCalledOnce();
    expect(result.findings).toEqual([finding]);
  });

  it("expands decision-log ranges and rejects categories absent from discovery", () => {
    expect(extractDecisionIds(discovery)).toEqual([
      "D-01",
      "ADR-05",
      "ADR-07",
      "ADR-08",
      "ADR-09",
    ]);
    expect(extractDecisionRows(discovery, ["ADR-08"])).toContain(
      "| ADR-07 – ADR-09 | Architecture |",
    );
    expect(() => validateFinding({ ...finding, category: "D-56" }, ["D-01"])).toThrow();
  });

  it("rejects a discovery document without the decision log", () => {
    expect(() => extractDecisionIds("No decision table")).toThrow(
      "missing its section 22 decision log",
    );
  });

  it("uses the complete discovery decision log and grounds every role persona in it", async () => {
    const source = await loadDiscovery();
    const decisionIds = extractDecisionIds(source);

    expect(decisionIds).toHaveLength(64);
    expect(decisionIds).toContain("D-55");
    expect(decisionIds).toContain("ADR-13");
    for (const role of ROLES) {
      const persona = await readFile(
        resolve(PERSONAS_DIR, `${role.slug}.md`),
        "utf8",
      );
      expect(persona, role.name).toContain("../context/smartrail-discovery.md");
      const excerpt = buildDiscoveryExcerpt(
        source,
        role.discoverySections,
        role.decisionIds,
      );
    }
  });

  it("exports a separate Markdown response and challenge for each role", async () => {
    const { mkdtemp, readFile, rm } = await import("node:fs/promises");
    const { tmpdir } = await import("node:os");
    const { resolve } = await import("node:path");
    const outputDir = await mkdtemp(resolve(tmpdir(), "smartrail-transcript-"));
    try {
      const messages: DiscussionMessage[] = [];
      for (const role of ROLES) {
        messages.push({
          role: role.name,
          phase: "response",
          content: `${role.name} response text.`,
        });
        messages.push({
          role: role.name,
          phase: "challenge",
          content: `${role.name} challenge text.`,
        });
      }
      const files = await exportPanelTranscripts(
        {
          topic: "How should the discovery guide this role?",
          roles: ROLES,
          messages,
          findings: [finding],
        },
        outputDir,
        new Date("2026-10-05T00:00:00.000Z"),
      );

      expect(files).toHaveLength(ROLES.length + 1);
      for (const role of ROLES) {
        const markdown = await readFile(
          resolve(outputDir, `${role.slug}.md`),
          "utf8",
        );
        expect(markdown).toContain(`${role.name} response text.`);
        expect(markdown).toContain(`${role.name} challenge text.`);
        expect(markdown).toContain("../context/smartrail-discovery.md");
      }
      expect(await readFile(resolve(outputDir, "_panel-summary.md"), "utf8"))
        .toContain("Synthesized finding");
    } finally {
      await rm(outputDir, { recursive: true, force: true });
    }
  });
});
