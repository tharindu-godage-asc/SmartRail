import type { BaseChatModel } from "@langchain/core/language_models/chat_models";
import { ChatGoogleGenerativeAI } from "@langchain/google-genai";
import { ChatGroq } from "@langchain/groq";
import { config } from "dotenv";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const baseDir = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultGeminiModel = "gemini-2.5-flash";
const defaultGroqModel = "qwen/qwen3.8-27b";
const maxAttempts = 4;

config({ path: resolve(baseDir, ".env"), quiet: true });

export type Provider = "gemini" | "groq";

/** LLM_PROVIDER wins; otherwise Groq only when it is the only configured provider. */
export function activeProvider(): Provider {
  const chosen = process.env.LLM_PROVIDER?.toLowerCase();
  if (chosen === "gemini" || chosen === "groq") return chosen;
  return keysFor("gemini").length === 0 && keysFor("groq").length > 0
    ? "groq"
    : "gemini";
}

function keysFor(provider: Provider): string[] {
  const raw =
    provider === "groq"
      ? (process.env.GROQ_API_KEYS ?? process.env.GROQ_API_KEY ?? "")
      : (process.env.GEMINI_API_KEYS ??
        process.env.GEMINI_API_KEY ??
        process.env.GOOGLE_API_KEY ??
        "");
  return raw.split(",").map((key) => key.trim()).filter(Boolean);
}

/** Keys for the active provider (comma-separated *_API_KEYS, or a single *_API_KEY). */
export function loadApiKeys(): string[] {
  return keysFor(activeProvider());
}

let keyIndex = 0;
const exhaustedKeys = new Set<number>();

export function setStartingKey(index: number): void {
  keyIndex = index;
}

export function currentKeyNumber(): number {
  return keyIndex + 1;
}

/** Moves to the next key that has not hit its daily quota; false if none remain. */
function rotateKey(): boolean {
  exhaustedKeys.add(keyIndex);
  const total = loadApiKeys().length;
  for (let step = 1; step <= total; step++) {
    const next = (keyIndex + step) % total;
    if (!exhaustedKeys.has(next)) {
      keyIndex = next;
      return true;
    }
  }
  return false;
}

export function createChatModel(): BaseChatModel {
  const provider = activeProvider();
  const keys = keysFor(provider);
  if (keys.length === 0) {
    throw new Error(
      provider === "groq"
        ? "No Groq key found. Set GROQ_API_KEYS=key1,key2,... in tools/stakeholder-sim/.env."
        : "No Gemini key found. Set GEMINI_API_KEYS=key1,key2,... in tools/stakeholder-sim/.env.",
    );
  }
  const apiKey = keys[keyIndex % keys.length];

  if (provider === "groq") {
    return new ChatGroq({
      apiKey,
      model: process.env.GROQ_MODEL ?? defaultGroqModel,
      temperature: 0.4,
      maxTokens: 1024,
    });
  }
  return new ChatGoogleGenerativeAI({
    apiKey,
    model: process.env.GEMINI_MODEL ?? defaultGeminiModel,
    temperature: 0.4,
    maxOutputTokens: 1024,
  });
}

function sleep(milliseconds: number): Promise<void> {
  return new Promise((resolveSleep) => setTimeout(resolveSleep, milliseconds));
}

function isRetryable(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error);
  return /429|503|RESOURCE_EXHAUSTED|UNAVAILABLE|overloaded|rate.?limit/i.test(
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

/**
 * Runs a model call. On a daily-quota error it switches to the next API key;
 * on other rate-limit/overload errors it retries with backoff.
 */
export async function invokeModel<T>(operation: () => Promise<T>): Promise<T> {
  for (let attempt = 0; ; attempt++) {
    try {
      return await operation();
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (/PerDay|per day|TPD|RPD/i.test(message)) {
        const exhausted = currentKeyNumber();
        if (!rotateKey()) {
          throw new Error(
            `All ${loadApiKeys().length} ${activeProvider()} key(s) hit their daily quota. Add another key to the *_API_KEYS list.`,
          );
        }
        console.error(
          `Key #${exhausted} hit its daily quota; switching to key #${currentKeyNumber()}.`,
        );
        attempt--;
        continue;
      }
      if (!isRetryable(error) || attempt >= maxAttempts - 1) throw error;
      const delay = retryDelay(error, attempt);
      console.error(
        `Rate limit or overload; retrying in ${Math.ceil(delay / 1000)}s.`,
      );
      await sleep(delay);
    }
  }
}
