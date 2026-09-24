"""Report original receiver heights by exact native water material identity."""
import argparse
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

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('disc', type=Path)
parser.add_argument('bank')
parser.add_argument('--dump-source', type=Path)
parser.add_argument('--all-flat', action='store_true', help='Include unclassified horizontal source faces for receiver audits.')
parser.add_argument('--example-limit', type=int, default=3)
args = parser.parse_args()
manifest = json.loads((ROOT / 'Textures/Native4x-Test/pack-manifest.json').read_text())
bank = next(b for b in manifest['banks'] if b['source'].split('/')[0] == args.bank)
catalog = json.loads(gzip.decompress((ROOT / f'Lighting/{args.bank}.json.gz').read_bytes()))
kinds = {p[0]: p[1] for m in catalog['meshes'] for p in m['polygons']}
disc = Disc(args.disc)
graph = Graph(disc.read(bank['source'].replace('.TMS', '.DMD')), all_variants=True)
if args.dump_source:
    args.dump_source.write_bytes(graph.b)
graph.walk(next(r for r in graph.roots if graph.sh(r + 2) == 1))
groups = {}
for mesh in graph.meshes:
    for offset, points in mesh['polys']:
        normal = np.cross(points[1]-points[0], points[2]-points[0])
        length = np.linalg.norm(normal)
        horizontal = length > 1e-8 and abs(normal[2])/length > .999
        if kinds.get(offset) not in (2, 4) and not (args.all_flat and horizontal):
            continue
        native = material(graph, offset, bank['textures'])
        key = native[0]['texture_id'] if native else 'untextured'
        group = groups.setdefault(key, dict(faces=0, minimum_z=float('inf'), maximum_z=float('-inf'),
                                          draw_modes={}, lod_ranges={}, examples=[]))
        group['faces'] += 1
        op = graph.b[offset + 19]
        uv = offset + 16 + 4 * graph.b[offset + 1]
        mode = ((graph.sh(uv + 6) & 0xffff) >> 5) & 3 if op & 4 else None
        state = f'kind={kinds.get(offset)},semi={bool(op & 2)},blend={mode}'
        group['draw_modes'][state] = group['draw_modes'].get(state, 0) + 1
        for node in mesh['path']:
            if graph.b[node] != 2 or hex(node) in group['lod_ranges']:
                continue
            ranges = []
            for i in range(graph.u(node + 16)):
                entry = graph.ptr(node + 20 + i * 4)
                ranges.append(dict(minimum_squared=graph.u(entry + 4),
                                   maximum_squared=graph.u(entry),
                                   child=hex(graph.ptr(entry + 8))))
            group['lod_ranges'][hex(node)] = ranges
        group['minimum_z'] = min(group['minimum_z'], float(points[:, 2].min()))
        group['maximum_z'] = max(group['maximum_z'], float(points[:, 2].max()))
        if len(group['examples']) < args.example_limit:
            group['examples'].append(dict(offset=hex(offset), mesh=hex(mesh['offset']),
                                         kind=kinds.get(offset), points=points.tolist()))
print(json.dumps(dict(bank=args.bank, materials=groups), indent=2))
disc.close()
