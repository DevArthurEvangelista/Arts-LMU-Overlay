"""Release gate for the actual Windows PE bytes, not just compiler exit status."""
import argparse
from pathlib import Path
import struct
import xml.etree.ElementTree as ET


def verify(path, expected_manifest=None):
    data = Path(path).read_bytes()
    u16 = lambda offset: struct.unpack_from('<H', data, offset)[0]
    u32 = lambda offset: struct.unpack_from('<I', data, offset)[0]
    assert data[:2] == b'MZ', 'Missing DOS header'
    pe = u32(60)
    assert data[pe:pe + 4] == b'PE\0\0', 'Missing PE signature'
    assert u16(pe + 4) == 0x8664, 'Application must target Windows x64'
    optional = pe + 24
    assert u16(optional) == 0x20b and u16(optional + 68) == 2, 'Expected PE32+ GUI application'
    sections = pe + 24 + u16(pe + 20)

    def address(rva):
        for i in range(u16(pe + 6)):
            row = sections + i * 40
            virtual, start, raw_size, raw = struct.unpack_from('<IIII', data, row + 8)
            if start <= rva < start + max(virtual, raw_size):
                return raw + rva - start
        raise AssertionError('RVA outside PE sections')

    clr = address(u32(optional + 112 + 14 * 8))
    assert u32(clr + 16) & 1 and u32(clr + 20), 'Missing managed entry point'
    resources = address(u32(optional + 112 + 2 * 8))

    def entries(offset):
        start = resources + offset
        count = u16(start + 12) + u16(start + 14)
        return [struct.unpack_from('<II', data, start + 16 + i * 8) for i in range(count)]

    types = dict(entries(0))
    assert all(t in types for t in [3, 14, 16, 24]), 'Missing Windows resource (icon, group icon, version or application manifest)'
    node = types[24]
    while node & 0x80000000:
        node = entries(node & 0x7fffffff)[0][1]
    rva, size = struct.unpack_from('<II', data, resources + node)
    manifest = data[address(rva):address(rva) + size]
    xml = ET.fromstring(manifest)
    execution = xml.find('.//{urn:schemas-microsoft-com:asm.v3}requestedExecutionLevel')
    assert execution is not None and execution.get('level') == 'asInvoker', 'Execution level must remain asInvoker'
    dpi = xml.find('.//{http://schemas.microsoft.com/SMI/2005/WindowsSettings}dpiAware')
    assert dpi is not None and dpi.text.strip().lower() == 'true', 'Missing approved DPI configuration'
    if expected_manifest:
        assert manifest == Path(expected_manifest).read_bytes(), 'Embedded manifest differs from source'
    return {'path': str(path), 'machine': 'x64', 'manifest_bytes': len(manifest), 'resource_types': sorted(types)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('exe')
    parser.add_argument('--manifest')
    args = parser.parse_args()
    print(verify(args.exe, args.manifest))
