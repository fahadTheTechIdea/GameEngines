"""Independent triangle/threshold area check for the terrain clamp fixture."""
import json
import math
from pathlib import Path
import sys

root = Path(sys.argv[1])

def polygon_area(points):
    return abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:] + points[:1]))) / 2 if len(points) >= 3 else 0

def clip_above_water(points):
    result = []
    for a, b in zip(points, points[1:] + points[:1]):
        av, bv = a[2] - 2, b[2] - 2
        if av >= 0:
            result.append(a)
        if (av >= 0) != (bv >= 0):
            t = av / (av - bv)
            result.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, 2))
    return result

for name in ("low", "middle", "high"):
    world = json.loads((root / (name + "-world.json")).read_text(encoding="utf-8"))
    field = world["Surface"]["HeightField"]
    z, width = field["Heights"], field["Columns"]
    dx, dy = field["StepX"], field["StepY"]
    total = 0.0
    for y in range(field["Rows"] - 1):
        for x in range(width - 1):
            at = lambda ox, oy: z[(y + oy) * width + x + ox]
            a, b, c, d = at(0, 0), at(1, 0), at(0, 1), at(1, 1)
            px, py = field["Origin"]["X"] + x * dx, field["Origin"]["Y"] + y * dy
            ll, lr, ul, ur = (px, py, a), (px + dx, py, b), (px, py + dy, c), (px + dx, py + dy, d)
            if math.hypot((b - a) / dx, (d - b) / dy) <= .3:
                total += polygon_area(clip_above_water([ll, lr, ur]))
            if math.hypot((d - c) / dx, (c - a) / dy) <= .3:
                total += polygon_area(clip_above_water([ll, ur, ul]))
    actual = world["RegionPlan"]["SuitableArea"]
    assert abs(total - actual) < 1e-7, (name, total, actual)
    print(f"{name}: independent suitable area {total:.12f}; engine {actual:.12f}")
