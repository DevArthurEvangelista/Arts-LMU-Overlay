"""Run the Windows-targeted core checks on an isolated Linux Mono runtime.

This does not claim native Windows UI coverage; see docs/VALIDACAO-0.11.md.
"""
import argparse
import os
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--dotnet', required=True)
parser.add_argument('--csc', required=True)
parser.add_argument('--references', required=True)
parser.add_argument('--mono-root', required=True)
parser.add_argument('--render', default='tests/render-v012')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
refs = Path(args.references).resolve()
mono = Path(args.mono_root).resolve()
env = os.environ.copy()
env.update(DOTNET_ROLL_FORWARD='Major', DOTNET_CLI_TELEMETRY_OPTOUT='1')
compiler = [str(Path(args.dotnet).resolve()), str(Path(args.csc).resolve()),
            '/nologo', '/langversion:5', '/target:exe', '/platform:x64',
            '/nostdlib+', '/optimize+']
compiler += ['/r:' + str(refs / (name + '.dll')) for name in
             ['mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Drawing',
              'System.Windows.Forms', 'System.Runtime.Serialization', 'System.IO.Compression']]
resources = ['/resource:assets/Flags.zip,Flags.zip',
             '/resource:assets/BrandLogo.png,BrandLogo.png',
             '/resource:assets/CarLogos.zip,CarLogos.zip']
sources = [str(p) for p in sorted((root / 'src').glob('*.cs'))]
test_sources = ['Checks.cs', 'V05Checks.cs', 'V08Checks.cs', 'V09Checks.cs',
                'V092Checks.cs', 'V010Checks.cs', 'V010DamageChecks.cs', 'V011DesignChecks.cs', 'V012Checks.cs', 'V0121Checks.cs', 'V0122Checks.cs', 'V0123Checks.cs', 'PreviewData.cs']
subprocess.run(compiler + ['/main:Checks', '/out:tests/Checks.exe'] + resources + sources
               + [str(root / 'tests' / p) for p in test_sources], cwd=root, env=env, check=True)
subprocess.run(compiler + ['/main:AppearanceUiChecks', '/out:tests/AppearanceUiChecks.exe']
               + resources + sources + ['tests/AppearanceUiChecks.cs'], cwd=root, env=env, check=True)
env.update(MONO_PATH=str(root) + ':' + str(mono / 'usr/lib/mono/4.5'),
           MONO_CFG_DIR=str(mono / 'etc'),
           LD_LIBRARY_PATH=str(mono / 'usr/lib') + ':' + str(mono / 'usr/lib/x86_64-linux-gnu'),
           XDG_DATA_HOME=str(root / 'tests/arts-v09-testdata'))
runtime = str(mono / 'usr/bin/mono-sgen')
subprocess.run([runtime, 'tests/Checks.exe', 'tests/lmu-fixture.bin', args.render],
               cwd=root, env=env, check=True)
subprocess.run([runtime, 'tests/AppearanceUiChecks.exe', args.render, '--config-only'],
               cwd=root, env=env, check=True)
