"""Build with Microsoft's Roslyn and .NET Framework 4.8 reference assemblies.

The host may be Linux; the output is a native Windows x64 .NET Framework app.
Do not substitute mcs: older versions silently omit /win32manifest.
"""
import argparse
import os
import shutil
from pathlib import Path
import subprocess
from check_windows_package import verify

parser = argparse.ArgumentParser()
parser.add_argument('--dotnet', required=True)
parser.add_argument('--csc', required=True)
parser.add_argument('--references', required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
refs = Path(args.references).resolve()
env = os.environ.copy()
env['DOTNET_ROLL_FORWARD'] = 'Major'
env['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
command = [str(Path(args.dotnet).resolve()), str(Path(args.csc).resolve()),
           '/nologo', '/langversion:5', '/target:winexe', '/platform:x64',
           '/nostdlib+', '/optimize+', '/deterministic+',
           '/win32icon:assets/App.ico', '/win32manifest:src/app.manifest',
           '/out:LMUOverlay.exe', '/resource:assets/Flags.zip,Flags.zip',
           '/resource:assets/BrandLogo.png,BrandLogo.png', '/resource:assets/CarLogos.zip,CarLogos.zip']
for name in ['mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Drawing',
             'System.Windows.Forms', 'System.Runtime.Serialization', 'System.IO.Compression']:
    command.append('/r:' + str(refs / (name + '.dll')))
command.extend(str(p) for p in sorted((root / 'src').glob('*.cs')))
subprocess.run(command, cwd=root, env=env, check=True)
print(verify(root / 'LMUOverlay.exe', root / 'src/app.manifest'))

shutil.copy2(root / "assets/SectorRuntime-1.0.0.zip", root / "SectorRuntime-1.0.0.zip")
