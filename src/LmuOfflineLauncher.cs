using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Arthur.LmuOverlay
{
    // Launch the unmodified executable using LMU's documented offline route.
    // Never stop EAC, kill the game, change Steam options or patch game files.
    public static class LmuLaunchPath
    {
        public const string Executable = "Le Mans Ultimate.exe";

        public static string Resolve(string selected)
        {
            if (string.IsNullOrWhiteSpace(selected))
                throw new InvalidDataException("Selecione a pasta do Le Mans Ultimate primeiro.");
            string path = Path.GetFullPath(selected.Trim());
            if (Directory.Exists(path)) path = Path.Combine(path, Executable);
            if (!string.Equals(Path.GetFileName(path), Executable, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Selecione Le Mans Ultimate.exe, o executável principal do jogo.");
            if (!File.Exists(path))
                throw new FileNotFoundException("Le Mans Ultimate.exe não encontrado. Selecione novamente a pasta do jogo.", path);
            // Reject shortcuts, text files and 32-bit binaries before Process.Start.
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                if (stream.Length < 64 || reader.ReadUInt16() != 0x5a4d)
                    throw new InvalidDataException("O arquivo selecionado não é um executável Windows válido.");
                stream.Position = 60;
                int offset = reader.ReadInt32();
                if (offset < 64 || offset > stream.Length - 26)
                    throw new InvalidDataException("Executável incompleto. Verifique os arquivos do LMU na Steam.");
                stream.Position = offset;
                if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664)
                    throw new InvalidDataException("Selecione o executável de 64 bits do Le Mans Ultimate.");
                stream.Position = offset + 24;
                if (reader.ReadUInt16() != 0x20b)
                    throw new InvalidDataException("Formato do executável do LMU não reconhecido.");
            }
            return path;
        }

        public static ProcessStartInfo StartInfo(string executable)
        {
            return new ProcessStartInfo {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable),
                Arguments = "", UseShellExecute = false
            };
        }

        public static string RunningGameReason()
        {
            foreach (string name in new[] { "Le Mans Ultimate", "start_protected_game" })
            {
                var processes = Process.GetProcessesByName(name);
                bool found = processes.Length > 0;
                foreach (var process in processes) process.Dispose();
                if (found) return "O LMU ou seu inicializador já está aberto. Feche o jogo antes de iniciar sem EAC.";
            }
            return "";
        }
    }

    public interface ILmuLaunchedGame : IDisposable
    {
        string Identity { get; }
        bool Running { get; }
    }

    sealed class LmuLaunchedGame : ILmuLaunchedGame
    {
        readonly Process process;
        public string Identity { get; private set; }
        LmuLaunchedGame(Process value)
        {
            process = value;
            Identity = value.Id + "/" + value.StartTime.ToUniversalTime().Ticks;
        }
        public bool Running { get { return !process.HasExited; } }
        public static ILmuLaunchedGame Start(string path)
        {
            var value = Process.Start(LmuLaunchPath.StartInfo(path));
            if (value == null) throw new InvalidOperationException("O Windows não confirmou a abertura do LMU.");
            try { return new LmuLaunchedGame(value); }
            catch { value.Dispose(); throw; }
        }
        // Releasing the handle leaves the game running.
        public void Dispose() { process.Dispose(); }
    }

    public sealed class LmuLaunchState
    {
        public string Phase = "off", Message = "", Identity = "";
    }

    public sealed class LmuOfflineLauncher : IDisposable
    {
        readonly Func<string, string> resolve;
        readonly Func<string> preflight;
        readonly Func<string, ILmuLaunchedGame> launch;
        readonly Func<SectorAdmission> admission;
        readonly object gate = new object();
        readonly int pollMilliseconds;
        LmuLaunchState state = new LmuLaunchState();
        int busy, stop, disposed;
        public bool Busy { get { return Volatile.Read(ref busy) != 0; } }
        public LmuLaunchState State { get { return Interlocked.CompareExchange(ref state, null, null); } }
        public LmuOfflineLauncher() : this(LmuLaunchPath.Resolve, LmuLaunchPath.RunningGameReason,
            LmuLaunchedGame.Start, SectorAccess.Read, 1000) { }
        internal LmuOfflineLauncher(Func<string,string> r, Func<string> p,
            Func<string,ILmuLaunchedGame> l, Func<SectorAdmission> a, int poll)
        { resolve = r; preflight = p; launch = l; admission = a; pollMilliseconds = poll; }

        void Publish(string phase, string message, string identity = "")
        { Interlocked.Exchange(ref state, new LmuLaunchState { Phase = phase, Message = message, Identity = identity }); }
        public bool Start(string selected)
        {
            lock (gate)
            {
                if (busy != 0 || disposed != 0 || state.Phase == "ready") return false;
                busy = 1; stop = 0;
                Publish("starting", "Verificando o executável e abrindo o LMU…");
                new Thread(() => Run(selected)) { IsBackground = true, Name = "Arts offline launcher" }.Start();
                return true;
            }
        }
        public void Cancel()
        {
            lock (gate)
            {
                Interlocked.Exchange(ref stop, 1);
                Publish("off", "Conexão automática cancelada. O jogo permanece aberto.");
            }
        }
        public void ConsumeReady()
        { lock (gate) { if (busy == 0 && state.Phase == "ready") Publish("off", ""); } }
        void Run(string selected)
        {
            ILmuLaunchedGame game = null;
            try
            {
                string path = resolve(selected);
                string blocked = preflight();
                if (!string.IsNullOrEmpty(blocked)) throw new InvalidOperationException(blocked);
                if (Volatile.Read(ref stop) != 0) return;
                game = launch(path);
                while (Volatile.Read(ref stop) == 0)
                {
                    if (!game.Running) throw new InvalidOperationException("O LMU foi fechado ou não concluiu a abertura. Inicie novamente; se necessário, abra a Steam primeiro.");
                    var check = admission();
                    if (!string.IsNullOrEmpty(check.Identity) && check.Identity != game.Identity)
                        throw new InvalidOperationException("A instância do jogo mudou. Conecte o treino novamente.");
                    if (check.Allowed && check.Identity == game.Identity)
                    { Publish("ready", "Treino compatível encontrado. Conectando…", game.Identity); return; }
                    Publish("waiting", "Escolha carro e pista e entre no treino offline.\n" + check.Reason);
                    for (int elapsed = 0; elapsed < pollMilliseconds && Volatile.Read(ref stop) == 0; elapsed += 50)
                        Thread.Sleep(Math.Min(50, pollMilliseconds - elapsed));
                }
            }
            catch (Exception error) { Publish("error", error.Message); }
            finally
            {
                if (game != null) game.Dispose();
                lock (gate)
                {
                    if (stop != 0) Publish("off", "Conexão automática cancelada. O jogo permanece aberto.");
                    Interlocked.Exchange(ref busy, 0);
                }
            }
        }
        public void Dispose() { lock (gate) { disposed = 1; Cancel(); } }
    }
}
