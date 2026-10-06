import { ChatGoogleGenerativeAI } from "@langchain/google-genai";
import { config } from "dotenv";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const baseDir = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultModel = "gemini-2.5-flash";
const maxAttempts = 4;

config({ path: resolve(baseDir, ".env"), quiet: true });

export function createChatModel(): ChatGoogleGenerativeAI {
  const apiKey = process.env.GEMINI_API_KEY;
  if (!apiKey) {
    throw new Error(
      "GEMINI_API_KEY is missing. Set it in tools/stakeholder-sim/.env.",
    );
  }

  return new ChatGoogleGenerativeAI({
    apiKey,
    model: process.env.GEMINI_MODEL ?? defaultModel,
    temperature: 0.4,
    maxOutputTokens: 1024,
  });
}

function sleep(milliseconds: number): Promise<void> {
  return new Promise((resolveSleep) => setTimeout(resolveSleep, milliseconds));
}

function isRetryable(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error);
  return /429|503|RESOURCE_EXHAUSTED|UNAVAILABLE|overloaded|rate limit/i.test(
    message,
  );
}

function retryDelay(error: unknown, attempt: number): number {
  const message = error instanceof Error ? error.message : String(error);
  const seconds = message.match(/retry in ([\d.]+)s/i);
  return seconds
    ? Math.ceil(Number(seconds[1]) * 1000) + 1000
    : 5000 * 2 ** attempt;
}

/** Runs a Gemini call, retrying with backoff on rate-limit/overload errors. */
export async function invokeModel<T>(operation: () => Promise<T>): Promise<T> {
  for (let attempt = 0; ; attempt++) {
    try {
      return await operation();
    } catch (error) {
      if (!isRetryable(error) || attempt >= maxAttempts - 1) throw error;
      const delay = retryDelay(error, attempt);
      console.error(
        `Gemini rate limit or overload; retrying in ${Math.ceil(delay / 1000)}s.`,
      );
      await sleep(delay);
    }
  }
}
