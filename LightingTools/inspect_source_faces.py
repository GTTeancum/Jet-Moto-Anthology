"""Report exact native geometry and ancestry for supplied polygon offsets."""
import json
import argparse
import sys
from pathlib import Path
from native_graph import Graph

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / 'TextureTools'))
from build_native_pack import Disc

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('disc', type=Path)
parser.add_argument('scene')
parser.add_argument('offsets', nargs='+', type=lambda value: int(value, 16))
parser.add_argument('--all-roots', action='store_true')
args = parser.parse_args()
disc = Disc(args.disc)
name = args.scene
offsets = set(args.offsets)
graph = Graph(disc.read(f'{name}/{name}.DMD'), all_variants=True)
if args.all_roots:
    graph.all()
else:
    graph.walk(next(r for r in graph.roots if graph.sh(r + 2) == 1))
rows = []
for mesh in graph.meshes:
    for offset, points in mesh['polys']:
        if offset in offsets:
            rows.append(dict(polygon=hex(offset), mesh=hex(mesh['offset']),
                shift=mesh['shift'], points=points.tolist(),
                ancestry=[dict(offset=hex(p), type=graph.b[p], tag=graph.sh(p+2))
                          for p in mesh['path']],
                record=graph.b[offset:offset+graph.b[offset+2]*4].hex()))
print(json.dumps(rows, indent=2))
disc.close()
