#!/usr/bin/env node

import { readFile, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";
import axios from "axios";
import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import "dotenv/config";

interface ArchitectureAnalysisRequest {
  sourceCode: string;
  filePath: string;
  llmModel: string;
  additionalContext?: string;
  customRules?: string[];
  generateRefactoring?: boolean;
}

interface ArchitectureAnalysisResponse {
  analysis: string;
  violations: string[];
  suggestions: string[];
  patterns: string[];
  confidence: number;
  refactoredCode?: string;
  architectureDiagram?: string;
  metadata?: Record<string, unknown>;
}

const BACKEND_URL = process.env.BACKEND_URL || "http://localhost:5000";
const BACKEND_ENDPOINT =
  process.env.BACKEND_ENDPOINT || "/api/architecture/analyze";
const DEFAULT_LLM_MODEL =
  process.env.DEFAULT_LLM_MODEL || "openai/gpt-4o-mini";
const DEBUG = process.env.DEBUG === "true";

const AGENT_SYSTEM_INSTRUCTIONS =
  "Use a ferramenta check_my_architecture para obter diagnostico arquitetural. " +
  "Ao receber a analise, priorize feedback imediato e acionavel para o desenvolvedor, " +
  "com foco em violacoes de SOLID, impacto tecnico e sugestoes praticas de Design Patterns.";

const server = new McpServer(
  {
    name: "architecture-analysis-mcp",
    version: "1.0.0",
  },
  {
    instructions: AGENT_SYSTEM_INSTRUCTIONS,
  }
);

function log(message: string, data?: unknown): void {
  if (DEBUG) {
    console.error(`[architecture-analysis-mcp] ${message}`, data ?? "");
  }
}

async function readFileContent(filePath: string): Promise<string> {
  const absolutePath = path.isAbsolute(filePath)
    ? filePath
    : path.resolve(process.cwd(), filePath);
  return readFile(absolutePath, "utf-8");
}

async function getCustomRules(): Promise<string[] | undefined> {
  try {
    const archrcPath = path.resolve(process.cwd(), ".archrc.json");
    if (existsSync(archrcPath)) {
      const content = await readFile(archrcPath, "utf-8");
      const parsed = JSON.parse(content);
      if (Array.isArray(parsed.rules)) {
        log("Loaded custom rules from .archrc.json");
        return parsed.rules;
      }
    }
  } catch (err) {
    log("Failed to parse .archrc.json", err);
  }
  return undefined;
}

async function analyzeArchitecture(
  payload: ArchitectureAnalysisRequest
): Promise<ArchitectureAnalysisResponse> {
  const backendFullUrl = `${BACKEND_URL}${BACKEND_ENDPOINT}`;

  const response = await axios.post<ArchitectureAnalysisResponse>(
    backendFullUrl,
    payload,
    {
      headers: { "Content-Type": "application/json" },
      timeout: 30000,
    }
  );

  return response.data;
}

function formatAnalysisResponse(
  analysis: ArchitectureAnalysisResponse,
  filePath: string
): string {
  const lines: string[] = [];

  lines.push("Architecture Analysis Report");
  lines.push(`File: ${filePath}`);
  lines.push(`Confidence: ${(analysis.confidence * 100).toFixed(0)}%`);
  lines.push("");
  lines.push("Analysis:");
  lines.push(analysis.analysis);
  lines.push("");

  if (analysis.violations.length > 0) {
    lines.push("SOLID Violations:");
    for (const violation of analysis.violations) {
      lines.push(`- ${violation}`);
    }
    lines.push("");
  }

  if (analysis.suggestions.length > 0) {
    lines.push("Suggestions:");
    for (const suggestion of analysis.suggestions) {
      lines.push(`- ${suggestion}`);
    }
    lines.push("");
  }

  if (analysis.patterns.length > 0) {
    lines.push("Design Patterns:");
    for (const pattern of analysis.patterns) {
      lines.push(`- ${pattern}`);
    }
    lines.push("");
  }

  if (analysis.architectureDiagram) {
    lines.push("Architecture Diagram:");
    lines.push(analysis.architectureDiagram);
    lines.push("");
  }

  if (analysis.refactoredCode) {
    lines.push("Refactored Code Generated.");
    lines.push("");
  }

  if (analysis.metadata) {
    lines.push("Metadata:");
    lines.push(JSON.stringify(analysis.metadata, null, 2));
  }

  return lines.join("\n");
}

(server as unknown as {
  registerTool: (
    name: string,
    config: { description: string; inputSchema: Record<string, unknown> },
    cb: (args: Record<string, unknown>) => Promise<{
      content: Array<{ type: string; text: string }>;
      isError?: boolean;
    }>
  ) => void;
}).registerTool(
  "check_my_architecture",
  {
    description:
      "Le um arquivo local ou usa source_code, envia para backend ASP.NET Core e retorna analise de arquitetura.",
    inputSchema: {
      file_path: z
        .string()
        .describe("Caminho do arquivo local para analise (absoluto ou relativo)."),
      source_code: z
        .string()
        .optional()
        .describe("Codigo opcional. Se informado, nao le o arquivo do disco."),
      llm_model: z
        .string()
        .optional()
        .describe("Modelo no OpenRouter (ex: openai/gpt-4o-mini)."),
      additional_context: z
        .string()
        .optional()
        .describe("Contexto adicional para enriquecer a analise."),
      auto_fix: z
        .boolean()
        .optional()
        .describe("Se true, aplica a refatoracao sugerida diretamente no arquivo."),
    },
  },
  async (args) => {
    try {
      const file_path = String(args.file_path ?? "");
      const source_code =
        typeof args.source_code === "string" ? args.source_code : undefined;
      const llm_model =
        typeof args.llm_model === "string" ? args.llm_model : undefined;
      const additional_context =
        typeof args.additional_context === "string"
          ? args.additional_context
          : undefined;

      const auto_fix = Boolean(args.auto_fix);

      if (!file_path) {
        return {
          content: [
            {
              type: "text",
              text: "Architecture analysis failed: file_path is required.",
            },
          ],
          isError: true,
        };
      }

      const finalSourceCode = source_code ?? (await readFileContent(file_path));
      const llmModel = llm_model ?? DEFAULT_LLM_MODEL;
      const customRules = await getCustomRules();

      const requestPayload: ArchitectureAnalysisRequest = {
        sourceCode: finalSourceCode,
        filePath: file_path,
        llmModel,
        additionalContext: additional_context,
        customRules,
        generateRefactoring: auto_fix,
      };

      log("Sending architecture request", {
        file_path,
        llmModel,
        auto_fix,
        bytes: finalSourceCode.length,
      });

      const result = await analyzeArchitecture(requestPayload);

      let successMessage = "";
      if (auto_fix && result.refactoredCode) {
        const absolutePath = path.isAbsolute(file_path)
          ? file_path
          : path.resolve(process.cwd(), file_path);
        
        let codeToWrite = result.refactoredCode;
        if (codeToWrite.startsWith("```")) {
          const match = codeToWrite.match(/```[a-z]*\n([\s\S]*?)\n```/);
          if (match) {
            codeToWrite = match[1];
          }
        }
        await writeFile(absolutePath, codeToWrite, "utf-8");
        successMessage = `\n\n[AUTO-FIX] Arquivo sobrescrito com sucesso com o novo código refatorado!`;
      }

      return {
        content: [
          {
            type: "text",
            text: formatAnalysisResponse(result, file_path) + successMessage,
          },
        ],
      };
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);

      return {
        content: [
          {
            type: "text",
            text: `Architecture analysis failed: ${message}`,
          },
        ],
        isError: true,
      };
    }
  }
);

async function main(): Promise<void> {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  log("MCP server ready", { backend: `${BACKEND_URL}${BACKEND_ENDPOINT}` });
}

main().catch((error) => {
  console.error("Failed to start MCP server:", error);
  process.exit(1);
});
