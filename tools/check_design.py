from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
design = (root / 'DESIGN.md').read_text()
source = (root / 'src' / 'Renderer.cs').read_text()
tokens = dict(re.findall(r'public static readonly Color (\w+) = ColorTranslator.FromHtml\("(#[A-F0-9]{6})"\)', source))
for name, color in tokens.items():
    assert re.search(r'^  ' + name.lower() + ': "' + color + '"$', design, re.M), (name, color)
assert len(tokens) == 14
print('PASS: all 14 palette tokens match DESIGN.md and the native renderer.')
