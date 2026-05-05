import axios from "axios";
import * as fs from "fs";

async function runTest() {
  try {
    const code = fs.readFileSync("../examples/SampleCode.cs", "utf-8");
    const response = await axios.post("http://localhost:5000/api/architecture/analyze", {
      sourceCode: code,
      filePath: "SampleCode.cs",
      llmModel: "openrouter/auto",
      additionalContext: "Analise o código, por favor.",
      customRules: ["Test rule"],
      generateRefactoring: true
    });
    console.log(JSON.stringify(response.data, null, 2));
  } catch (err) {
    if (err.response) {
      console.error(err.response.status, err.response.data);
    } else {
      console.error(err);
    }
  }
}

runTest();
