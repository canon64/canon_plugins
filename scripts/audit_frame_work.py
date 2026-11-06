"""Inventory per-frame call chains; findings require source review, not automatic blame."""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '_diagnostics' / 'perf-reduction-20261027' / 'frame-audit.json'
SKIP = {'bin', 'obj', 'Tests', 'RuntimeSmoke', 'RegressionHarness', 'Characterization', 'ShaderBuild', 'SmokeProject', 'node_modules', '.git'}
METHOD = re.compile(r'\b(?:public|private|protected|internal)\s+(?:(?:static|override|virtual|sealed|new|async)\s+)*[\w.<>,?\[\]]+\s+(\w+)\s*\([^;{}]*\)\s*\{')
ENTRY = re.compile(r'^(?:Update|LateUpdate|FixedUpdate|OnGUI|On\w*(?:PreCull|PreRender|PostRender|RenderImage)|\w*(?:Prefix|Postfix|Finalizer))$')
FLAGS = {
    'hierarchy': r'GetComponentsInChildren|GetComponentInChildren',
    'scene_search': r'FindObjects?OfType(?:All)?|GameObject\.Find\(',
    'allocation': r'new (?:List|Dictionary|HashSet|StackTrace)|\.ToArray\(|\.ToList\(|Camera\.allCameras\b|\.OrderBy\(',
    'reflection': r'GetAssemblies\(|\.GetTypes\(|\.GetMethods\(|\.GetFields\(|\.GetProperties\(',
    'io': r'(?:File|Directory)\.(?:Exists|GetFiles|EnumerateFiles|Read|Write|Append|GetLastWrite)',
    'gpu_readback': r'ReadPixels\(|GetPixels(?:32)?\(|EncodeToPNG\(',
    'render': r'\.Render\(|\.BakeMesh\(|RecalculateNormals\(|\.materials\b',
}


def masked(source):
    # Preserve offsets/newlines while excluding comments and ordinary/verbatim strings.
    pattern = r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    return re.sub(pattern, lambda m: ''.join('\n' if c == '\n' else ' ' for c in m.group()), source)


def inventory(directory):
    methods = []
    files = 0
    for path in directory.rglob('*.cs'):
        if any(part in SKIP or part.endswith('.Tests') for part in path.relative_to(directory).parts):
            continue
        if path.name.startswith('_tmp'): continue
        source = path.read_text(encoding='utf-8-sig')
        clean = masked(source)
        files += 1
        for match in METHOD.finditer(clean):
            depth, end = 1, match.end()
            while depth and end < len(clean):
                depth += (clean[end] == '{') - (clean[end] == '}')
                end += 1
            body = clean[match.end():end]
            name = match[1]
            coroutine = ('IEnumerator' in clean[match.start():match.end()] and
                         'yield return' in body and ('while' in body or 'for (' in body))
            methods.append(dict(name=name, path=str(path.relative_to(ROOT)),
                                line=source.count('\n', 0, match.start()) + 1,
                                entry=bool(ENTRY.fullmatch(name)) or coroutine,
                                flags=[k for k, p in FLAGS.items() if re.search(p, body)],
                                calls=set(re.findall(r'\b(\w+)\s*\(', body))))
    by_name = {}
    for i, method in enumerate(methods): by_name.setdefault(method['name'], []).append(i)
    chains = []
    for root, method in enumerate(methods):
        if not method['entry']: continue
        visited, pending = set(), [(root, [method['name']])]
        while pending:
            index, chain = pending.pop()
            if index in visited: continue
            visited.add(index)
            called = methods[index]
            if called['flags']:
                chains.append({k: called[k] for k in ('name', 'path', 'line', 'flags')} | {'chain': chain})
            for call in called['calls']:
                for child in by_name.get(call, []): pending.append((child, chain + [call]))
    return {'project': directory.name, 'files': files, 'entrypoints': sum(m['entry'] for m in methods), 'candidates': chains}


def main():
    print('Starting source call-chain inventory (candidates are not measured costs)', flush=True)
    results = [inventory(p) for p in sorted(ROOT.iterdir())
               if p.is_dir() and not p.name.startswith('_') and list(p.glob('*.csproj'))
               and not p.name.endswith(('.Tests', '.LoaderTests'))]
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'projects={len(results)} files={sum(p["files"] for p in results)} entrypoints={sum(p["entrypoints"] for p in results)}')
    for project in results:
        unique = {(c['path'], c['line']) for c in project['candidates']}
        print(f'{project["project"]}: entrypoints={project["entrypoints"]} candidate_methods={len(unique)}')
    print(f'Inventory saved: {OUT}', flush=True)


if __name__ == '__main__': main()
