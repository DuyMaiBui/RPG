#!/usr/bin/env python3
"""Builds a contact sheet from Game View captures (pure Python): one row per scene, one column per capture.

usage: make_sheet.py OUT.png FRAME_DIR SCENE [SCENE ...]   (expects FRAME_DIR/<scene>_1..3.png; frames are halved)
"""
import struct, sys, zlib, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from visual_check import read_png


def half(path):
    w, h, ch, rows = read_png(path)
    out = []
    for y in range(0, h - 1, 2):
        a, b = rows[y], rows[y + 1]
        line = bytearray()
        for x in range(0, w - 1, 2):
            i = x * ch
            j = (x + 1) * ch
            for c in range(3):
                line.append((a[i + c] + a[j + c] + b[i + c] + b[j + c]) >> 2)
        out.append(line)
    return w // 2, h // 2, out


def write_png(path, width, height, rows):
    raw = b''.join(b'\x00' + bytes(r) for r in rows)
    def chunk(kind, data):
        c = struct.pack('>I', len(data)) + kind + data
        return c + struct.pack('>I', zlib.crc32(kind + data) & 0xFFFFFFFF)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 6)) + chunk(b'IEND', b'')
    open(path, 'wb').write(png)


def main(out, frame_dir, scenes):
    sheet_rows = []
    width = 0
    for scene in scenes:
        frames = [half(os.path.join(frame_dir, f'{scene}_{i}.png')) for i in (1, 2, 3)]
        h = frames[0][1]
        for y in range(h):
            line = bytearray()
            for w, _, rows in frames:
                line += rows[y]
                line += bytes([255, 255, 255]) * 4  # separator
            sheet_rows.append(line)
        sheet_rows.append(bytearray([255, 255, 255]) * (len(sheet_rows[-1]) // 3))
        width = len(sheet_rows[-1]) // 3
    write_png(out, width, len(sheet_rows), sheet_rows)
    print(out, width, 'x', len(sheet_rows))


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3:])
