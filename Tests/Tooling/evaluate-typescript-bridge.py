import json, pathlib, subprocess, time, statistics, base64, gzip
root = pathlib.Path("UI/Fuse.Web")
version = "6.0.3-bridge.19.tsgo.7.0.2"
def run(args, **kwargs):
    return subprocess.run(args, cwd=root, text=True, check=True, **kwargs)
def check(label):
    durations = []
    for i in range(3):
        started = time.monotonic()
        result = run(["node_modules/.bin/vue-tsc", "--noEmit", "-p", "tsconfig.app.json"], capture_output=True)
        print(result.stdout, result.stderr, flush=True)
        durations.append(round(time.monotonic() - started, 3))
    run(["npm", "run", "test:typecheck"])
    print("BENCHMARK " + json.dumps({"compiler": label, "app_seconds": durations, "median": statistics.median(durations)}), flush=True)
    probe_ts = root / "src/__compiler_probe.ts"
    probe_vue = root / "src/__CompilerProbe.vue"
    try:
        probe_ts.write_text('export const invalidAssignment: number = "must fail";\n')
        probe_vue.write_text('<script setup lang="ts">\nconst acceptNumber = (value: number) => value;\n</script>\n<template>{{ acceptNumber("must fail") }}</template>\n')
        result = subprocess.run(["node_modules/.bin/vue-tsc", "--noEmit", "-p", "tsconfig.app.json"], cwd=root, text=True, capture_output=True)
        output = result.stdout + result.stderr
        print("DIAGNOSTIC PROBE " + label + "\n" + output, flush=True)
        assert result.returncode != 0, "Compiler accepted deliberate type errors"
        assert "__compiler_probe.ts" in output and "TS2322" in output, "TypeScript diagnostic missing"
        assert "__CompilerProbe.vue" in output and "TS2345" in output, "Vue template diagnostic missing"
    finally:
        probe_ts.unlink(missing_ok=True)
        probe_vue.unlink(missing_ok=True)
run(["npm", "ci"])
check("stock-6.0.3")
package = root / "package.json"
data = json.loads(package.read_text())
data["devDependencies"]["typescript"] = "npm:typescript-native-bridge@" + version
data.setdefault("overrides", {})["typescript"] = "$typescript"
package.write_text(json.dumps(data, indent=2) + "\n")
run(["npm", "install", "--package-lock-only", "--ignore-scripts"])
run(["npm", "ci"])
run(["node", "-e", "console.log(require('typescript/package.json').name, require('typescript/package.json').version); console.log(require.resolve('typescript'))"])
check("bridge-" + version)
run(["npm", "test"])
run(["npm", "run", "build"])
for filename in ["package.json", "package-lock.json"]:
    payload = base64.b64encode(gzip.compress((root / filename).read_bytes())).decode()
    print("GENERATED_FILE " + filename + " " + payload, flush=True)
