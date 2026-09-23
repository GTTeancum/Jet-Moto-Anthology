"""Inspect original horizontal source faces omitted from the receiver catalog."""
import gzip
import json
import sys
from pathlib import Path
import numpy as np
from native_graph import Graph
from native_material import material

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / 'TextureTools'))
from build_native_pack import Disc

disc = Disc(Path(sys.argv[1]))
bank_name = sys.argv[2] if len(sys.argv) > 2 else 'ISLAND1'
manifest = json.loads((ROOT / 'Textures/Native4x-Test/pack-manifest.json').read_text())
bank = next(b for b in manifest['banks'] if b['source'].split('/')[0] == bank_name)
original = disc.read(bank['source'].replace('.TMS', '.DMD'))
graph = Graph(original, all_variants=True)
root = next(r for r in graph.roots if graph.sh(r+2) == 1)
graph.walk(root)
catalog = json.loads(gzip.decompress((ROOT / f'Lighting/{bank_name}.json.gz').read_bytes()))
known = {p[0]: p[1] for m in catalog['meshes'] for p in m['polygons']}
rows = []
water_levels = {}
for mesh in graph.meshes:
    for offset, points in mesh['polys']:
        if known.get(offset) == 2 and float(points[:,2].max()-points[:,2].min()) < .001:
            level = str(round(float(points[0,2]), 4))
            water_levels[level] = water_levels.get(level, 0) + 1
        normal = np.cross(points[1]-points[0], points[2]-points[0])
        area = float(np.linalg.norm(normal))
        if area < 1e-8 or abs(normal[2])/area < .88 or offset in known:
            continue
        mat = material(graph, offset, bank['textures'])
        rows.append(dict(mesh=hex(mesh['offset']), polygon=hex(offset), area=round(area,3),
            material=mat[0]['texture_id'] if mat else None, opcode=hex(original[offset+19]),
            minimum=points.min(0).tolist(), maximum=points.max(0).tolist(),
            path=[hex(p) for p in mesh['path']]))
rows.sort(key=lambda r:r['area'], reverse=True)
groups = {}
for row in rows:
    key = str(row['material'])
    group = groups.setdefault(key, dict(count=0, area=0))
    group['count'] += 1
    group['area'] += row['area']
print(json.dumps(dict(bank=bank_name,water_levels=water_levels,omitted_count=len(rows),materials=groups,
    faces=[r for r in rows if r['material'] is None][:35]),indent=2))
disc.close()
