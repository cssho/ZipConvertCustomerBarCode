// Write / Edit の後に実行するフック (.claude/settings.json の PostToolUse)
// - .cs: UTF-8 の BOM がなければ付ける (CLAUDE.md のコード規約)
// - .github/workflows/*.yml: gh actions-lock --verify-local でロックファイルの漏れを確認する
import { readFileSync, writeFileSync } from "node:fs";
import { spawnSync } from "node:child_process";

const input = JSON.parse(readFileSync(0, "utf8"));
const file = input.tool_input?.file_path ?? input.tool_response?.filePath;
if (!file) process.exit(0);
const path = file.replaceAll("\\", "/");

if (path.endsWith(".cs")) {
  const bytes = readFileSync(file);
  if (!(bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf)) {
    writeFileSync(file, Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), bytes]));
  }
} else if (/\/\.github\/workflows\/[^/]+\.ya?ml$/.test(path)) {
  const result = spawnSync("gh", ["actions-lock", "--verify-local"], {
    cwd: process.env.CLAUDE_PROJECT_DIR || process.cwd(),
    encoding: "utf8",
  });
  // gh や拡張機能がない環境では何もしない
  if (result.error) process.exit(0);
  if (result.status !== 0) {
    console.log(JSON.stringify({
      decision: "block",
      reason: "actions.lock がワークフローと一致していません。gh actions-lock でロックし直し、gh actions-lock --verify で確認してください。\n"
        + (result.stdout + result.stderr).trim(),
    }));
  }
}
