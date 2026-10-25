"""Package an already compiled and checked Windows installer and source tree."""
import argparse
import hashlib
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import zipfile
from check_windows_package import verify

parser = argparse.ArgumentParser()
parser.add_argument('--seven-zip', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
out = Path(args.output).resolve()
out.mkdir(parents=True, exist_ok=True)
version = re.search(r'Version="([0-9.]+)"', (root / 'src/Branding.cs').read_text()).group(1)
installer = root / ('Instalar-Arts-LMU-Overlay-' + version + '.exe')
assert installer.is_file(), 'Build installer/Setup.nsi first'
verify(root / 'LMUOverlay.exe', root / 'src/app.manifest')

with tempfile.TemporaryDirectory(prefix='arts-installer-check-') as temp:
    subprocess.run([args.seven_zip, 'x', '-y', '-o' + temp, str(installer)],
                   check=True, stdout=subprocess.DEVNULL)
    payload = Path(temp)
    for name in ['LMUOverlay.exe', 'LMUOverlay.exe.config', 'LEIA-ME.txt']:
        assert (payload / name).read_bytes() == (root / name).read_bytes(), name + ' differs in installer'
    verify(payload / 'LMUOverlay.exe', root / 'src/app.manifest')
    assert (payload / 'SectorRuntime-1.0.0.zip').read_bytes() == (root / 'assets/SectorRuntime-1.0.0.zip').read_bytes()
    assert (payload / 'Licencas/Sector-MIT.txt').read_bytes() == (root / 'third_party/Sector-MIT.txt').read_bytes()

top = {'LMUOverlay.csproj', 'LMUOverlay.exe', 'LMUOverlay.exe.config',
       'COMPILAR.cmd', 'LEIA-ME.txt', 'DESIGN.md', 'premium-ui.json', 'SectorRuntime-1.0.0.zip'}
directories = {'src', 'assets', 'third_party', 'tools', 'tests', 'docs', 'installer'}
files = []
for path in sorted(root.rglob('*')):
    if not path.is_file():
        continue
    relative = path.relative_to(root)
    if len(relative.parts) == 1:
        if path.name in top:
            files.append(path)
        continue
    if relative.parts[0] not in directories:
        continue
    if any(part in {'__pycache__', 'bin', 'obj', 'arts-v09-testdata'} or
           part.startswith(('render-', 'installer-')) for part in relative.parts):
        continue
    if path.suffix in {'.pyc', '.exe', '.pdb', '.mdb', '.tmp'}:
        continue
    files.append(path)

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

checksums = root / 'SHA256.txt'
checksums.write_text(''.join(sha(path) + '  ' + path.relative_to(root).as_posix() + '\n'
                             for path in files), encoding='utf-8')
files.append(checksums)
source_zip = out / ('Arts-LMU-Overlay-Fontes-v' + version + '.zip')
with zipfile.ZipFile(source_zip, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for path in files:
        archive.write(path, 'Arts-LMU-Overlay/' + path.relative_to(root).as_posix())

installed_copy = out / installer.name
shutil.copy2(installer, installed_copy)
installer_zip = out / ('Arts-LMU-Overlay-Instalador-v' + version + '.zip')
with zipfile.ZipFile(installer_zip, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    archive.write(installed_copy, installed_copy.name)
    archive.write(root / 'LEIA-ME.txt', 'LEIA-PRIMEIRO-Arts-v' + version + '.txt')

for path in [source_zip, installer_zip]:
    with zipfile.ZipFile(path) as archive:
        assert archive.testzip() is None, 'Corrupt archive: ' + path.name
        if path == source_zip:
            assert archive.read('Arts-LMU-Overlay/LMUOverlay.exe') == (root / 'LMUOverlay.exe').read_bytes()
            for item in files:
                assert archive.read('Arts-LMU-Overlay/' + item.relative_to(root).as_posix()) == item.read_bytes()
        else:
            assert archive.read(installer.name) == installed_copy.read_bytes()
print('PASS: extracted NSIS payload, embedded manifest, source inventory, SHA256 list and both ZIP archives.')
for path in [installed_copy, installer_zip, source_zip]:
    print(path.name + ': ' + str(path.stat().st_size) + ' bytes • SHA256 ' + sha(path))
