"""Export the verified original TIM font as a source-addressed 4x replacement."""
import argparse
import hashlib
import json
import struct
from pathlib import Path
from PIL import Image
from build_native_pack import Disc, records, upscale


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cue', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    disc = Disc(args.cue)
    data = disc.read('ISLAND1/JMFONT.TIM')
    digest = hashlib.sha256(data).hexdigest()
    if digest != 'ddba37d93e47ad1931f5de0ea4aa07217141c0e388d3874d555b88859e436bfe':
        raise ValueError('Unsupported original font revision')
    bank = struct.pack('<6I', 0x50535854, 0x43, 0, 1, 0, len(data)) + data
    _, original = next(records(bank))
    original.save(args.output / 'original.png')
    enlarged = upscale(original)
    # Reconstruct occupancy separately: PS1 alpha 128 is a material bit, not opacity.
    occupancy = original.getchannel('A').point(lambda a: 255 if a else 0)
    edge = occupancy.resize(enlarged.size, Image.Resampling.BICUBIC)
    categories = enlarged.getchannel('A')
    enlarged.putalpha(Image.frombytes('L', enlarged.size, bytes(
        (a or 255) if e >= 128 else 0
        for a, e in zip(categories.tobytes(), edge.tobytes()))))
    enlarged.save(args.output / 'JMFONT.png')
    (args.output / 'manifest.json').write_text(json.dumps({
        'source': 'ISLAND1/JMFONT.TIM', 'sourceSha256': digest,
        'method': '4x Lanczos color, bicubic occupancy contour, categorical alpha',
        'neural': False,
        'outputSha256': hashlib.sha256((args.output / 'JMFONT.png').read_bytes()).hexdigest()
    }, indent=2) + '\n', encoding='ascii')


if __name__ == '__main__':
    main()
