# Architecture Analysis MCP

Servidor MCP para análise arquitetural de código com foco em SOLID, Design Patterns e feedback imediato ao desenvolvedor.

O projeto funciona em duas camadas:

- Um backend ASP.NET Core expõe um endpoint HTTP que recebe `sourceCode`, `filePath` e `llmModel`.
- Um servidor MCP em Node.js expõe a ferramenta `check_my_architecture`, lê o arquivo local e envia o conteúdo para o backend.

O backend atua como ponte agnóstica para o OpenRouter. O modelo é escolhido pelo cliente/MCP e o backend apenas encaminha a análise para o provedor.

## O que este projeto entrega

- Análise de violações de SOLID.
- Sugestões objetivas de refatoração.
- Indicação de Design Patterns aplicáveis.
- Resposta em JSON estruturado para consumo por LLMs e ferramentas de desenvolvimento.

## Estrutura

- `backend/`: API ASP.NET Core que consulta o OpenRouter.
- `mcp-server/`: servidor MCP em Node.js.
- `docs/`: documentação complementar.

## Pré-requisitos

- Node.js 18 ou superior.
- .NET SDK 10.0.
- Uma API Key do OpenRouter.
- VS Code com suporte a MCP, se for integrar o servidor ao editor.

## Instalação

### 1. Backend ASP.NET Core

Entre na pasta do backend e crie um arquivo `.env` baseado no exemplo:

```powershell
Copy-Item .\backend\.env.example .\backend\.env
```

Edite o arquivo `.env` e informe sua chave do OpenRouter:

```env
OpenRouter__ApiKey=sua_chave_aqui
OpenRouter__Referer=http://localhost
OpenRouter__Title=Architecture Analysis MCP Backend
ASPNETCORE_URLS=http://localhost:5000
ASPNETCORE_ENVIRONMENT=Development
```

### 2. MCP Server

Instale as dependências do servidor MCP:

```powershell
cd .\mcp-server
npm install
```

## Como executar

### Backend

```powershell
cd .\backend
dotnet run
```

O backend sobe em `http://localhost:5000`.

### MCP Server

Em outro terminal:

```powershell
cd .\mcp-server
npm run build
npm run dev
```

Se preferir usar o build compilado:

```powershell
npm start
```

## Como usar a ferramenta MCP

A ferramenta exposta é `check_my_architecture`.

Parâmetros principais:

- `file_path`: caminho do arquivo local a ser analisado.
- `source_code`: opcional, caso você queira enviar o código direto sem ler do disco.
- `llm_model`: modelo do OpenRouter a ser usado na análise.
- `additional_context`: contexto adicional sobre o código.

### Exemplo de uso

Se você estiver integrando o MCP ao VS Code ou a outro cliente MCP, chame a ferramenta assim:

```json
{
  "file_path": "src/OrderService.cs",
  "llm_model": "openrouter/auto",
  "additional_context": "Quero focar em SRP e possíveis melhorias de desacoplamento"
}
```

## Teste rápido com HTTP

Você pode testar o backend diretamente com um POST para o endpoint:

`POST http://localhost:5000/api/architecture/analyze`

Exemplo de payload:

```json
{
  "sourceCode": "public class SampleService { }",
  "filePath": "SampleService.cs",
  "llmModel": "openrouter/auto",
  "additionalContext": "Teste local"
}
```

## Resultado esperado

A resposta retorna algo neste formato:

```json
{
  "analysis": "...",
  "violations": [],
  "suggestions": [],
  "patterns": [],
  "confidence": 0.95,
  "metadata": {
    "provider": "OpenRouter",
    "model": "openrouter/auto"
  }
}
```

## Como interpretar o resultado

O agente consumidor deve priorizar:

- violações de SOLID com impacto direto;
- riscos de acoplamento e baixa coesão;
- padrões de projeto aplicáveis com ganho real;
- recomendações imediatas e acionáveis para o desenvolvedor.

## Validação que já foi feita

O fluxo completo foi validado localmente com Playwright: o backend recebeu o POST, consultou o OpenRouter e retornou uma análise em JSON com status 200.

## Observações importantes

- O arquivo `backend/.env` não é versionado; use o `.env.example` como base.
- O backend carrega automaticamente o `.env` na inicialização.
- O servidor MCP também usa `.env` via `dotenv`.

## Próximos passos

1. Conectar o servidor MCP ao VS Code como servidor local.
2. Testar a ferramenta `check_my_architecture` com arquivos reais do seu projeto.
3. Ajustar o prompt do backend se você quiser uma abordagem mais rígida em relação a SOLID ou mais orientada a patterns.
