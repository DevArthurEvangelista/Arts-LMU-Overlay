"""Regenerate C# offsets from the vendored SDK mapping (MIT)."""
from pathlib import Path
import ctypes
import sys

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root / 'third_party'))
import lmu_data as l

values = {
    'Size': ctypes.sizeof(l.LMUObjectOut), 'MaxCars': l.LMUConstants.MAX_MAPPED_VEHICLES,
    'Scoring': l.LMUObjectOut.scoring.offset, 'Telemetry': l.LMUObjectOut.telemetry.offset,
    'ScoringCars': l.LMUObjectOut.scoring.offset + l.LMUScoringData.vehScoringInfo.offset,
    'TelemetryCars': l.LMUObjectOut.telemetry.offset + l.LMUTelemetryData.telemInfo.offset,
    'ScoringCarSize': ctypes.sizeof(l.LMUVehicleScoring),
    'TelemetryCarSize': ctypes.sizeof(l.LMUVehicleTelemetry),
    'WheelSize': ctypes.sizeof(l.LMUWheel),
    'EventTelemetry': l.LMUEvent.SME_UPDATE_TELEMETRY.offset,
    'EventScoring': l.LMUEvent.SME_UPDATE_SCORING.offset,
    'EventSessionStart': l.LMUEvent.SME_START_SESSION.offset,
    'AppWindow': l.LMUGeneric.appInfo.offset + l.LMUApplicationState.mAppWindow.offset,
    'AppLocation': l.LMUGeneric.appInfo.offset + l.LMUApplicationState.mOptionsLocation.offset,
}
for prefix, cls in [('Generic', l.LMUGeneric), ('ScoringInfo', l.LMUScoringInfo),
                    ('Telemetry', l.LMUTelemetryData), ('Car', l.LMUVehicleScoring),
                    ('Telem', l.LMUVehicleTelemetry), ('Wheel', l.LMUWheel)]:
    for name, _ in cls._fields_:
        values[prefix + '_' + name] = getattr(cls, name).offset
text = '// Generated from TinyPedal/pyLMUSharedMemory (MIT); see docs/TELEMETRIA.md.\n'
text += 'namespace Arthur.LmuOverlay { public static class Layout {\n'
text += ''.join('    public const int %s = %d;\n' % item for item in values.items()) + '}}\n'
(root / 'src' / 'Layout.Generated.cs').write_text(text)
print('Generated', len(values), 'SDK offsets.')
