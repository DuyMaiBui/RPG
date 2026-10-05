#!/usr/bin/env python3
"""Coarse visual guard for a Game View capture (pure Python, no imaging library).

usage: visual_check.py IMAGE.png EXPECTATIONS.json SCENE
Measures the fraction of pixels that look like the orange demo actor material and compares it with the scene's
"<scene>:visual" rule {"actor_min": fraction}. It catches invisible (culled, off-screen, destroyed) actors, not subtle
shading changes. Exit code 0 when the rule passes or the scene has no rule.
"""
import json, struct, sys, zlib


def read_png(path):
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', 'not a PNG'
    pos, idat, width, height, depth, ctype = 8, [], 0, 0, 8, 2
    while pos < len(data):
        length, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b'IHDR':
            width, height, depth, ctype = struct.unpack('>IIBB', body[:10])
        elif kind == b'IDAT':
            idat.append(body)
        pos += 12 + length
    assert depth == 8 and ctype in (2, 6), f'unsupported PNG (depth {depth}, type {ctype})'
    channels = 3 if ctype == 2 else 4
    raw = zlib.decompress(b''.join(idat))
    stride = width * channels
    rows, previous = [], bytearray(stride)
    offset = 0
    for _ in range(height):
        filt = raw[offset]
        line = bytearray(raw[offset + 1:offset + 1 + stride])
        offset += 1 + stride
        if filt == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 255
        elif filt == 2:
            for i in range(stride):
                line[i] = (line[i] + previous[i]) & 255
        elif filt == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + previous[i]) >> 1)) & 255
        elif filt == 4:
            for i in range(stride):
                a = line[i - channels] if i >= channels else 0
                b = previous[i]
                c = previous[i - channels] if i >= channels else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pred = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                line[i] = (line[i] + pred) & 255
        rows.append(line)
        previous = line
    return width, height, channels, rows


def actor_fraction(path):
    width, height, channels, rows = read_png(path)
    count = 0
    for line in rows:
        for x in range(0, width * channels, channels * 2):  # every second pixel is plenty
            r, g, b = line[x], line[x + 1], line[x + 2]
            if r > 90 and r > g + 25 and g > b and r - b > 60:
                count += 1
    return count / (width * height / 2)


def main(image, exp_path, scene):
    fraction = actor_fraction(image)
    rule = json.load(open(exp_path)).get(scene + ':visual')
    if not rule:
        print(f'{scene}: visual actor fraction {fraction:.4f} (no rule)')
        return 0
    ok = fraction >= rule['actor_min']
    print(f"{scene}: visual actor fraction {fraction:.4f}, expected >= {rule['actor_min']} {'PASS' if ok else 'FAIL'}")
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main(*sys.argv[1:4]))
