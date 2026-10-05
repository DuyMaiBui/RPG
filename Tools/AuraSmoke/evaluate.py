#!/usr/bin/env python3
"""Evaluate per-scene smoke expectations against sampled probe metrics.

usage: evaluate.py SAMPLES.jsonl EXPECTATIONS.json SCENE
Each rule: {"metric": name, "stat": min|max|range|first|last, "op": "<"|">"|"=="|"<=", ">=", "value": number}
or {"ratio": [metricA, metricB], "min": x, "max": y} comparing unwrapped angle deltas (degrees).
Exit code 0 when every rule passes.
"""
import json, sys

def unwrap(values):
    out, offset, prev = [], 0.0, None
    for v in values:
        if prev is not None:
            d = v - prev
            if d > 180: offset -= 360
            elif d < -180: offset += 360
        out.append(v + offset); prev = v
    return out

def main(samples_path, exp_path, scene):
    samples = []
    for line in open(samples_path):
        line = line.strip()
        if line.startswith('{'):
            try: samples.append(json.loads(line)['m'])
            except Exception: pass
    exp = json.load(open(exp_path))
    rules = exp.get('*', []) + exp.get(scene, [])
    failures = []
    if len(samples) < 3: failures.append(f'only {len(samples)} usable samples')
    def series(name):
        vals = [s[name] for s in samples if name in s]
        if not vals: raise KeyError(name)
        return vals
    ops = {'<': lambda a, b: a < b, '>': lambda a, b: a > b, '<=': lambda a, b: a <= b, '>=': lambda a, b: a >= b, '==': lambda a, b: a == b}
    for r in rules:
        try:
            if 'ratio' in r:
                a = unwrap(series(r['ratio'][0])); b = unwrap(series(r['ratio'][1]))
                da, db = a[-1] - a[0], b[-1] - b[0]
                ratio = db / da if abs(da) > 1e-3 else float('nan')
                ok = r['min'] <= ratio <= r['max']
                if not ok: failures.append(f"ratio {r['ratio'][1]}/{r['ratio'][0]} = {ratio:.3f}, expected [{r['min']}, {r['max']}] (delta {da:.1f}, {db:.1f})")
                continue
            v = series(r['metric'])
            if r['metric'].startswith(('door',)) and r.get('fold'):
                v = [min(x % 360, 360 - x % 360) for x in v]
            stat = {'min': min(v), 'max': max(v), 'range': max(v) - min(v), 'first': v[0], 'last': v[-1]}[r['stat']]
            if not ops[r['op']](stat, r['value']):
                failures.append(f"{r['metric']} {r['stat']} = {stat:.3f}, expected {r['op']} {r['value']}")
        except KeyError as e:
            failures.append(f'metric {e} missing')
    print(f"{scene}: {len(samples)} samples, {len(rules)} rules, {'PASS' if not failures else 'FAIL'}")
    for f in failures: print('   -', f)
    return 1 if failures else 0

if __name__ == '__main__':
    sys.exit(main(*sys.argv[1:4]))
