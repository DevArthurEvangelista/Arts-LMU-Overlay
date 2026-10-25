using System;
using System.IO;
using System.Threading;
using Arthur.LmuOverlay;

partial class Checks
{
    sealed class FakeLmuGame : ILmuLaunchedGame
    {
        public bool Closed, Released;
        public string Identity { get { return "12/34"; } }
        public bool Running { get { return !Closed; } }
        public void Dispose() { Released = true; }
    }
    static void Version0121()
    {
        string root = Path.Combine(Path.GetTempPath(), "arts launcher espaço " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string exe = Path.Combine(root, LmuLaunchPath.Executable);
            Reject(() => LmuLaunchPath.Resolve(""), "launcher rejects an unset location");
            Reject(() => LmuLaunchPath.Resolve(Path.Combine(root, "start_protected_game.exe")), "launcher rejects protected bootstrapper");
            bool missing = false;
            try { LmuLaunchPath.Resolve(root); } catch (FileNotFoundException) { missing = true; }
            Assert(missing, "missing game is reported without launching");
            File.WriteAllText(exe, "invalid executable");
            Reject(() => LmuLaunchPath.Resolve(root), "launcher rejects text disguised as exe");
            var bytes = new byte[128];
            bytes[0] = 0x4d; bytes[1] = 0x5a;
            BitConverter.GetBytes(64).CopyTo(bytes,60);
            bytes[64] = 0x50; bytes[65] = 0x45; bytes[68] = 0x64; bytes[69] = 0x86;
            bytes[88] = 0x0b; bytes[89] = 2;
            File.WriteAllBytes(exe,bytes);
            Assert(LmuLaunchPath.Resolve(root) == exe && LmuLaunchPath.Resolve(exe) == exe, "folder and explicit exe resolve with spaces and unicode");
            var info = LmuLaunchPath.StartInfo(exe);
            Assert(info.FileName == exe && info.WorkingDirectory == root && !info.UseShellExecute && info.Arguments == "", "launch uses exact executable and working directory with no shell or injected arguments");
            bytes[69] = 1; File.WriteAllBytes(exe,bytes);
            Reject(() => LmuLaunchPath.Resolve(root), "wrong architecture rejected");

            int launches = 0;
            using (var launcher = new LmuOfflineLauncher(p => p, () => "LMU já aberto", p => { launches++; return new FakeLmuGame(); }, () => new SectorAdmission(), 10))
            {
                launcher.Start(exe);
                Assert(SpinWait.SpinUntil(() => !launcher.Busy,2000) && launcher.State.Phase == "error" && launches == 0, "running game blocks a second launch");
            }
            var game = new FakeLmuGame();
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var launcher = new LmuOfflineLauncher(p => p, () => "", p => game, () => { entered.Set(); release.WaitOne(2000); return new SectorAdmission { Allowed = true, Identity = game.Identity }; }, 10))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                launcher.Start(exe);
                Assert(watch.ElapsedMilliseconds < 500 && entered.WaitOne(2000), "launch/admission work does not block UI caller");
                Assert(!launcher.Start(exe), "double click cannot spawn twice");
                launcher.Cancel(); release.Set();
                Assert(SpinWait.SpinUntil(() => !launcher.Busy,2000) && launcher.State.Phase == "off" && game.Released && !game.Closed, "cancel wins over pending readiness and leaves game running");
            }
            game = new FakeLmuGame();
            int admit = 0;
            using (var launcher = new LmuOfflineLauncher(p => p, () => "", p => game,
                () => new SectorAdmission { Allowed = Volatile.Read(ref admit) == 1, Identity = game.Identity, Reason = "Exige treino offline / build compatível" }, 10))
            {
                launcher.Start(exe);
                Assert(SpinWait.SpinUntil(() => launcher.State.Phase == "waiting",2000) && launcher.Busy, "menu/online/incompatible admission remains waiting without Sector activation");
                Interlocked.Exchange(ref admit,1);
                Assert(SpinWait.SpinUntil(() => !launcher.Busy,2000) && launcher.State.Phase == "ready" && launcher.State.Identity == game.Identity, "verified session produces readiness for exact launched process");
                Assert(!launcher.Start(exe), "unconsumed readiness cannot relaunch");
                launcher.ConsumeReady(); Assert(launcher.State.Phase == "off", "readiness consumed once");
            }
            game = new FakeLmuGame();
            using (var launcher = new LmuOfflineLauncher(p => p, () => "", p => game, () => new SectorAdmission { Allowed = true, Identity = "99/88" }, 10))
            {
                launcher.Start(exe);
                Assert(SpinWait.SpinUntil(() => !launcher.Busy,2000) && launcher.State.Phase == "error", "process replacement never auto-connects");
            }
            game = new FakeLmuGame { Closed = true };
            using (var launcher = new LmuOfflineLauncher(p => p, () => "", p => game, () => new SectorAdmission(),10))
            {
                launcher.Start(exe);
                Assert(SpinWait.SpinUntil(() => !launcher.Busy,2000) && launcher.State.Phase == "error" && game.Released, "early game exit is reported and handle released");
            }
            using (var launcher = new LmuOfflineLauncher(p => p, () => "", p => { throw new System.ComponentModel.Win32Exception("access denied"); }, () => new SectorAdmission(),10))
            {
                launcher.Start(exe);
                Assert(SpinWait.SpinUntil(() => !launcher.Busy,2000) && launcher.State.Phase == "error", "Windows launch failure is visible");
            }
            int backendStarts = 0;
            using (var engine = new SectorEngine(() => new SectorAdmission { Allowed=true, Identity="99/88", Pid=99 }, () => root,
                (r,w,p) => { backendStarts++; return null; }, root, (m,e) => { }))
            {
                engine.Start("12/34");
                Assert(SpinWait.SpinUntil(() => !engine.Busy,2000) && engine.State.Phase == "error" && backendStarts == 0,
                    "Sector revalidates launcher identity before preparing or attaching runtime");
            }
        }
        finally { Directory.Delete(root,true); }
    }
}
