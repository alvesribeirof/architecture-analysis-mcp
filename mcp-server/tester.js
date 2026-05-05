import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

async function run() {
  console.log("Iniciando MCP Client...");
  const transport = new StdioClientTransport({
    command: "node",
    args: ["dist/index.js"]
  });

  const client = new Client(
    { name: "test-client", version: "1.0.0" },
    { capabilities: {} }
  );

  await client.connect(transport);
  console.log("Conectado ao MCP Server!");

  console.log("Chamando a ferramenta check_my_architecture com auto_fix = true...");
  const result = await client.callTool({
    name: "check_my_architecture",
    arguments: {
      file_path: "../examples/SampleCode.cs",
      auto_fix: true
    }
  });

  console.log("\n================ RESULTADO DA ANÁLISE ================\n");
  const textContent = result.content[0];
  if ('text' in textContent) {
    console.log(textContent.text);
  }
  console.log("\n======================================================\n");

  console.log("Teste finalizado com sucesso!");
  process.exit(0);
}

run().catch(err => {
  console.error("Erro:", err);
  process.exit(1);
});
