"""Build a binary fixture from upstream SDK structures, independently of the C# parser."""
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'third_party'))
import lmu_data as l

out = l.LMUObjectOut()
out.generic.gameVersion = 10050
out.generic.events.SME_UPDATE_SCORING = 60
out.generic.events.SME_UPDATE_TELEMETRY = 1800
out.generic.appInfo.mOptionsLocation = 3
sc = out.scoring.scoringInfo
sc.mTrackName = b'Spa-Francorchamps'
sc.mPlayerName = b'Arthur Gabriel'
sc.mSession = 10
sc.mCurrentET = 580
sc.mEndET = 1000
sc.mSessionTimeRemaining = 420
sc.mNumVehicles = 3
sc.mLapDist = 7004.8
sc.mMaxLaps = 40
sc.mInRealtime = True
sc.mGamePhase = 5
for i, (car_id, pos, name, cl) in enumerate([
    (303, 3, 'Nicolás Silva', 'LMGT3'),
    (101, 1, 'Piloto Hypercar', 'HYPERCAR'),
    (202, 2, 'Arthur Gabriel', 'LMGT3'),
]):
    d = out.scoring.vehScoringInfo[i]
    d.mID, d.mPlace, d.mDriverName, d.mVehicleClass = car_id, pos, name.encode(), cl.encode()
    d.mLapDist = 1000 + i * 500
    d.mPos.x, d.mPos.z = 100 + i * 100, 20 + i * 20
    d.mIsPlayer = car_id == 202
    d.mControl = 0 if car_id == 202 else 1
    d.mVehFilename = str(car_id).encode()
    d.mLastLapTime, d.mBestLapTime = 138.527, 138.102
    d.mTimeBehindLeader = (pos - 1) * 2.524
    d.mInPits, d.mNumPitstops = car_id == 303, 2
out.telemetry.activeVehicles = 3
out.telemetry.playerHasVehicle = True
out.telemetry.playerVehicleIdx = 0  # deliberately disagrees with the player ID
for i, car_id in enumerate([101, 303, 202]):
    t = out.telemetry.telemInfo[i]
    t.mID = car_id
    t.mElapsedTime = 580
    t.mLapNumber = 5
    t.mLapStartET = 540
    t.mDeltaBest = -.3
    for wi in range(4):
        wheel = t.mWheels[wi]
        for sensor in range(3):
            wheel.mTemperature[sensor] = 353.15 + wi + sensor
        wheel.mPressure = 180 + wi * 10
        wheel.mOptimalTemp = 85
    t.mMaxGears = 6
    t.mUnfilteredSteering = -.2
    t.mPhysicalSteeringWheelRange = 540
    t.mVisualSteeringWheelRange = 480
    t.mEngineRPM = 8055 if car_id == 202 else 4100
    t.mEngineMaxRPM = 9000
    t.mUnfilteredThrottle, t.mUnfilteredBrake = .73, .19
    t.mFilteredThrottle, t.mFilteredBrake = .65, .15
    t.mFuel, t.mFuelCapacity, t.mGear = 42, 100, 4
    t.mLocalVel.x, t.mLocalVel.y, t.mLocalVel.z = 30, 40, 0
path = Path(__file__).resolve().parents[1] / 'tests' / 'lmu-fixture.bin'
path.write_bytes(bytes(out))
print(path.name, path.stat().st_size, 'bytes')
