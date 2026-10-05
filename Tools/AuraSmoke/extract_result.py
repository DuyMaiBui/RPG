#!/usr/bin/env python3
"""Reads `unity command run_script` output on stdin and prints the script's string result (one line)."""
import json, sys
t = sys.stdin.read()
i = t.find('{"diagnostics')
if i >= 0:
    try:
        d = json.JSONDecoder().raw_decode(t[i:])[0]
        r = d.get('result')
        if isinstance(r, str):
            print(r)
    except Exception:
        pass
