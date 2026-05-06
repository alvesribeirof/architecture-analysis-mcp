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

## Segurança

### MCP Inspector

Se o MCP Inspector solicitar um token local de sessão ou acesso, isso faz parte da própria interface web do Inspector e não é uma credencial da aplicação. Não reutilize esse valor em produção.

### Produção

Para expor o backend fora da máquina local, configure `API_KEY` e use o mesmo valor em `BACKEND_API_KEY` no MCP server. Mantenha `AllowedOrigins` restrito, deixe `DEBUG=false` e publique o backend atrás de um proxy confiável.

## Ambientes

### Desenvolvimento local

Use este modo quando estiver iterando no código na sua máquina.

Backend:
```powershell
cd .\backend
dotnet run
```

MCP Server:
```powershell
cd .\mcp-server
npm run build
npm start
```

Nesse cenário, `API_KEY` pode ficar vazio e `AllowedOrigins` pode manter os padrões de localhost.

### Teste / homologação

Use este modo quando quiser validar o fluxo em um ambiente controlado antes de promover para produção.

Backend:
```env
ASPNETCORE_ENVIRONMENT=Staging
AllowedOrigins=https://seu-front-teste.com
API_KEY=uma_chave_teste
```

MCP Server:
```env
BACKEND_URL=https://seu-backend-teste.com
BACKEND_API_KEY=uma_chave_teste
DEBUG=false
```

### Produção

Em produção, mantenha `API_KEY` configurada, `AllowedOrigins` restrito ao front real e `DEBUG=false`. Se o backend estiver atrás de proxy ou gateway, preserve o header `X-Api-Key` até a aplicação.

## Instalação

### 1. Backend ASP.NET Core

Entre na pasta do backend e crie um arquivo `.env` baseado no exemplo:

**PowerShell (Windows):**
```powershell
Copy-Item backend\.env.example backend\.env
```

**Bash (Linux/macOS):**
```bash
cp backend/.env.example backend/.env
```

Edite o arquivo `.env` e informe sua chave do OpenRouter:

```env
OpenRouter__ApiKey=sua_chave_aqui
OpenRouter__Referer=http://localhost
OpenRouter__Title=Architecture Analysis MCP Backend
ASPNETCORE_URLS=http://localhost:5000
ASPNETCORE_ENVIRONMENT=Development
AllowedOrigins=http://localhost:5173,http://localhost:6274,http://localhost:3000
API_KEY=
```

Se quiser simular teste/homologação localmente, troque `ASPNETCORE_ENVIRONMENT` para `Staging`, ajuste `AllowedOrigins` para a origem real e defina `API_KEY` com um valor de teste.

### 2. MCP Server

Instale as dependências do servidor MCP:

```powershell
cd .\mcp-server
npm install
```

Opcionalmente, crie um arquivo `.env` em `mcp-server/` para sobrescrever as configurações padrão:

```env
BACKEND_URL=http://localhost:5000
BACKEND_ENDPOINT=/api/architecture/analyze
DEFAULT_LLM_MODEL=openai/gpt-4o-mini
BACKEND_API_KEY=
DEBUG=false
```

Em teste ou produção, aponte `BACKEND_URL` para o ambiente desejado e, se o backend estiver protegido, configure o mesmo valor em `BACKEND_API_KEY`.

## Como executar

### Backend

```powershell
cd .\backend
dotnet run
```

O backend sobe em `http://localhost:5000`.

### MCP Server

Em outro terminal, compile e inicie o servidor:

```powershell
cd .\mcp-server
npm run build
npm start
```

> **⚠️ Comportamento esperado:** O servidor MCP usa transporte **stdio** e não imprime nada visível no terminal durante a execução normal — isso é correto. Toda a comunicação acontece via stdin/stdout, reservado para o protocolo MCP. Logs de debug são emitidos via `stderr` e só aparecem se `DEBUG=true` estiver no `.env`.

> **ℹ️ PowerShell e stderr:** O PowerShell pode exibir um `NativeCommandError` ou exit code 1 ao capturar a saída stderr do servidor. Isso é um **falso positivo** do PowerShell — o servidor está funcionando normalmente. Para confirmar, ative o debug:
> ```powershell
> $env:DEBUG="true"; node dist/index.js
> # Deve exibir: [architecture-analysis-mcp] MCP server ready { backend: '...' }
> ```

Para desenvolvimento com **recarga automática** ao salvar arquivos:

```powershell
npm run dev:watch
```

> **Nota:** `npm run dev` usa `ts-node` diretamente (sem compilar) e emite avisos de deprecação no Node.js moderno — prefira `npm start` (com build) para execução estável.

### Usando o MCP Inspector (interface web)

Para interagir com o servidor via interface web sem precisar de uma IDE compatível:

```powershell
# Produção (build compilado) — recomendado
npm run start:inspector

# Desenvolvimento (ts-node)
npm run dev:inspector
```

Após executar, acesse o link gerado no terminal (geralmente `http://localhost:5173` ou `http://localhost:6274`) para abrir o MCP Inspector.

## Regras customizadas por projeto (.archrc.json)

Crie um arquivo `.archrc.json` na raiz do projeto que será analisado com um array `"rules"` para aplicar regras arquiteturais específicas do seu time. O servidor MCP carrega essas regras automaticamente e as inclui na análise.

```json
{
  "rules": [
    "Controllers não devem acessar o banco de dados diretamente. Use Repositories ou Services.",
    "O código deve priorizar a Injeção de Dependência via construtor.",
    "Mantenha classes focadas em uma única responsabilidade (SRP)."
  ]
}
```

Se o arquivo não existir, a análise prossegue normalmente sem regras customizadas.

## Como usar a ferramenta MCP

A ferramenta exposta é `check_my_architecture`.

Parâmetros principais:

- `file_path`: caminho do arquivo local a ser analisado (absoluto ou relativo). **Obrigatório se `source_code` não for informado.**
- `source_code`: código-fonte enviado diretamente, sem precisar de um arquivo em disco. **Obrigatório se `file_path` não for informado.**

> **Regra:** informe ao menos um dos dois. Se ambos forem fornecidos, `source_code` tem preferência e o arquivo não é lido do disco (mas `file_path` ainda é usado como nome de referência no relatório e pelo `auto_fix`).

- `llm_model`: modelo do OpenRouter a ser usado na análise. Se omitido, usa o valor de `DEFAULT_LLM_MODEL` (padrão: `openai/gpt-4o-mini`).
- `additional_context`: contexto adicional para enriquecer a análise.
- `auto_fix`: quando `true`, o sistema gera o código refatorado e **sobrescreve automaticamente o arquivo** com a versão corrigida. Requer `file_path`. Use com atenção — a operação não tem desfazer.

### Exemplo 1 — analisar um arquivo no disco

```json
{
  "file_path": "src/OrderService.cs",
  "llm_model": "openrouter/auto",
  "additional_context": "Quero focar em SRP e possíveis melhorias de desacoplamento",
  "auto_fix": false
}
```

### Exemplo 2 — enviar o código diretamente (sem arquivo em disco)

```json
{
  "source_code": "public class OrderService { public void Process() { /* ... */ } }",
  "llm_model": "openrouter/auto",
  "additional_context": "Código colado direto no MCP Inspector"
}
```

> `auto_fix` não tem efeito quando somente `source_code` é informado (não há arquivo para sobrescrever).

Para acionar a refatoração automática com sobrescrita do arquivo:

```json
{
  "file_path": "src/OrderService.cs",
  "llm_model": "openrouter/auto",
  "auto_fix": true
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

### Health check

`GET http://localhost:5000/health` retorna `{"status":"ok"}` quando o backend está no ar.

### OpenAPI (Swagger)

Em modo Development (`ASPNETCORE_ENVIRONMENT=Development`), o backend expõe a especificação OpenAPI em:

`GET http://localhost:5000/openapi/v1.json`

## Resultado esperado

A resposta retorna algo neste formato:

```json
{
  "analysis": "...",
  "violations": [],
  "suggestions": [],
  "patterns": [],
  "confidence": 0.95,
  "refactoredCode": null,
  "architectureDiagram": null,
  "metadata": {
    "provider": "OpenRouter",
    "model": "openrouter/auto",
    "generatedAtUtc": "2025-01-01T00:00:00.0000000Z"
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

1. Testar a ferramenta `check_my_architecture` com arquivos reais do seu projeto via MCP Inspector ou IDE compatível.
2. Criar um `.archrc.json` na raiz do projeto com as regras arquiteturais do seu time.
3. Implementar a etapa de análise no workflow `.github/workflows/architecture-analysis.yml` para postar feedback arquitetural diretamente nos Pull Requests.
