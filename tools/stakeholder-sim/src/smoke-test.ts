import { HumanMessage, SystemMessage } from "@langchain/core/messages";
import { parseArgs } from "node:util";
import { createChatModel, invokeModel } from "./model.js";
import { buildDiscoveryExcerpt, loadDiscovery } from "./panel.js";

export async function runSmokeTest(
  question = "What is one high-value edge case to test for offline operator event sync, and which decision IDs make it important?",
): Promise<string> {
  const discovery = await loadDiscovery();
  const excerpt = buildDiscoveryExcerpt(
    discovery,
    ["6", "7", "8", "14", "23"],
    [
      "D-05", "D-06", "D-07", "D-08", "D-09", "D-35", "D-39", "D-41",
      "D-43", "D-44", "ADR-06", "ADR-09",
    ],
  );
  const systemPrompt =
    "You are the SmartRail QA engineer. Treat the supplied excerpts from the authoritative discovery document as the source of truth. Do not invent facts, infer unstated system behavior, or cite decision IDs whose entries are not supplied. Raise concerns and questions, not solutions. Cite only the supplied decision IDs.";
  const userPrompt = `Discovery excerpts:\n${excerpt}\n\nQuestion:\n${question}`;
  const response = await invokeModel(
    `${systemPrompt}\n${userPrompt}`,
    () =>
      createChatModel().invoke([
        new SystemMessage(systemPrompt),
        new HumanMessage(userPrompt),
      ]),
  );
  if (typeof response.text !== "string" || !response.text.trim()) {
    throw new TypeError("Expected a non-empty text response from Gemini.");
  }
  return response.text;
}

if (process.argv[1] && process.argv[1] === import.meta.filename) {
  const { positionals } = parseArgs({ allowPositionals: true });
  try {
    console.log(await runSmokeTest(positionals.join(" ") || undefined));
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}
