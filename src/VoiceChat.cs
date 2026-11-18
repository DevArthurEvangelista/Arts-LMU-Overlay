using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace Arthur.LmuOverlay
{
    internal sealed class VoiceDictation : IDisposable
    {
        WaveInEvent input;
        WaveFileWriter writer;
        string recordingPath;
        bool disposed, processing;
        public event Action<string> Completed;
        public event Action<string> Error;
        public bool Recording { get { return input != null; } }
        public bool Available { get { return !disposed; } }
        public string ExecutablePath, ModelPath, Language = "en";
        public int Microphone = -1;
        bool sendOnStop;

        public static string[] Microphones()
        {
            var result = new string[WaveIn.DeviceCount + 1];
            result[0] = "Padrão do Windows";
            for (int i = 0; i < WaveIn.DeviceCount; i++)
                result[i + 1] = WaveIn.GetCapabilities(i).ProductName;
            return result;
        }

        public bool Start()
        {
            if (disposed || Recording || processing) return false;
            if (!File.Exists(ExecutablePath) || !File.Exists(ModelPath))
            {
                Fail("Escolha whisper-cli.exe e o arquivo do modelo Whisper.");
                return false;
            }
            if (Microphone < -1 || Microphone >= WaveIn.DeviceCount)
            {
                Fail("Microfone indisponível. Selecione outro.");
                return false;
            }
            try
            {
                recordingPath = Path.Combine(Path.GetTempPath(), "arts-voice-" + Guid.NewGuid().ToString("N") + ".wav");
                var wave = new WaveInEvent { DeviceNumber = Microphone, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
                writer = new WaveFileWriter(recordingPath, wave.WaveFormat);
                wave.DataAvailable += delegate(object s, WaveInEventArgs e)
                {
                    try { if (writer != null) writer.Write(e.Buffer, 0, e.BytesRecorded); }
                    catch (Exception ex) { Fail(ex.Message); }
                };
                wave.RecordingStopped += delegate(object s, StoppedEventArgs e)
                {
                    if (input == wave) input = null;
                    wave.Dispose();
                    if (writer != null) { writer.Dispose(); writer = null; }
                    string path = recordingPath;
                    recordingPath = null;
                    if (e.Exception != null) { Delete(path); Fail(e.Exception.Message); return; }
                    if (!sendOnStop) { Delete(path); return; }
                    processing = true;
                    ThreadPool.QueueUserWorkItem(_ => Transcribe(path));
                };
                input = wave;
                sendOnStop = false;
                wave.StartRecording();
                return true;
            }
            catch (Exception e)
            {
                if (input != null) { input.Dispose(); input = null; }
                if (writer != null) { writer.Dispose(); writer = null; }
                Delete(recordingPath);
                recordingPath = null;
                Fail(e.Message);
                return false;
            }
        }

        public void Stop()
        {
            if (input == null) return;
            sendOnStop = true;
            try { input.StopRecording(); }
            catch (Exception e) { sendOnStop = false; Fail(e.Message); }
        }

        public void Cancel()
        {
            if (input == null) return;
            sendOnStop = false;
            try { input.StopRecording(); }
            catch (Exception e) { Fail(e.Message); }
        }

        static void Delete(string path)
        {
            if (!string.IsNullOrEmpty(path))
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        void Fail(string message)
        {
            var handler = Error;
            if (handler != null) handler(message);
        }

        static string Quote(string v) { return "\"" + v.Replace("\"", "") + "\""; }

        void Transcribe(string path)
        {
            string prefix = Path.ChangeExtension(path, null);
            string output = prefix + ".txt";
            try
            {
                if (new FileInfo(path).Length < 3200) throw new InvalidDataException("Áudio curto demais.");
                var info = new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    Arguments = "-m " + Quote(ModelPath) + " -f " + Quote(path) + " -l " + (Language == "pt" ? "pt" : "en") + " -otxt -of " + Quote(prefix) + " -np",
                    WorkingDirectory = Path.GetDirectoryName(ExecutablePath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(info))
                {
                    if (p == null) throw new IOException("Não foi possível iniciar o Whisper.");
                    if (!p.WaitForExit(90000))
                    {
                        p.Kill();
                        p.WaitForExit();
                        throw new TimeoutException("Transcrição demorou mais de 90 segundos.");
                    }
                    if (p.ExitCode != 0) throw new IOException("Whisper encerrou com código " + p.ExitCode + ".");
                }
                string text = File.ReadAllText(output, Encoding.UTF8).Trim();
                var handler = Completed;
                if (handler != null && !disposed) handler(text);
            }
            catch (Exception e) { if (!disposed) Fail(e.Message); }
            finally { processing = false; Delete(path); Delete(output); }
        }

        public void Dispose() { disposed = true; Cancel(); }
    }

    internal static class VoiceChatDelivery
    {
        public static async Task<bool> Send(string message, bool paste, Func<bool> focused, Action<string> copy, Func<char, int> map, Action<ushort, bool, bool> key, Func<int, Task> delay, Action<string> stage)
        {
            message = (message ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (message.Length == 0 || message.Length > 180)
            {
                stage("Frase vazia ou acima de 180 caracteres");
                return false;
            }
            if (!focused())
            {
                stage("Cancelado antes de abrir: jogo sem foco");
                return false;
            }
            var plan = new List<int>();
            foreach (char c in message)
            {
                if (char.IsControl(c))
                    throw new ArgumentException("A frase contém um caractere de controle.");
                plan.Add(map(c));
            }
            if (paste) copy(message);
            stage("Abrindo chat");
            if (!(await Tap(13, false, focused, key, delay))) return false;
            await delay(450);
            if (!focused())
            {
                stage("Cancelado após abrir: jogo sem foco");
                return false;
            }
            if (paste)
            {
                stage("Colando texto");
                try
                {
                    key(17, false, false);
                    await delay(50);
                    if (!(await Tap(86, false, focused, key, delay))) return false;
                }
                finally { key(17, true, false); }
            }
            else
            {
                stage("Digitando texto");
                for (int j = 0; j < message.Length; j++)
                {
                    if (!focused())
                    {
                        stage("Digitação cancelada: jogo sem foco");
                        return false;
                    }
                    int mapped = plan[j];
                    var modifiers = new List<ushort>();
                    try
                    {
                        if (mapped >= 0)
                        {
                            if ((mapped & 0x100) != 0) modifiers.Add(16);
                            if ((mapped & 0x200) != 0) modifiers.Add(17);
                            if ((mapped & 0x400) != 0) modifiers.Add(18);
                            foreach (ushort item in modifiers) key(item, false, false);
                            if (modifiers.Count > 0) await delay(20);
                        }
                        if (!(await Tap(mapped < 0 ? (ushort)message[j] : (ushort)(mapped & 0xFF), mapped < 0, focused, key, delay)))
                            return false;
                    }
                    finally
                    {
                        for (int num = modifiers.Count - 1; num >= 0; num--)
                            key(modifiers[num], true, false);
                    }
                }
            }
            await delay(220);
            stage("Confirmando com Enter");
            if (!(await Tap(13, false, focused, key, delay))) return false;
            stage("Sequência concluída; recebimento no jogo não verificado");
            return true;
        }

        static async Task<bool> Tap(ushort code, bool unicode, Func<bool> focused, Action<ushort, bool, bool> key, Func<int, Task> delay)
        {
            if (!focused()) return false;
            int holdMs = (!unicode && code == 13) ? 65 : 24;
            int gapMs = (!unicode && code == 13) ? 40 : 16;
            try
            {
                key(code, false, unicode);
                await delay(holdMs);
            }
            finally { key(code, true, unicode); }
            await delay(gapMs);
            return focused();
        }
    }

    internal static class VoiceChatKeys
    {
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public int type; public INPUTUNION u; }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy; public uint data, flags, time; public IntPtr extra; }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public IntPtr extra; }

        static int sending;

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint count, [In] INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKeyEx(uint code, uint kind, IntPtr layout);

        [DllImport("user32.dll")]
        static extern short VkKeyScanEx(char ch, IntPtr layout);

        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint thread);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll")]
        static extern short GetKeyState(int key);

        static void Key(ushort code, bool up, bool unicode, IntPtr layout)
        {
            uint scan = unicode ? code : MapVirtualKeyEx(code, 4u, layout);
            if (!unicode && scan == 0)
                scan = MapVirtualKeyEx(code, 0u, layout);
            // Send BOTH virtual-key (wVk) and hardware scan code (wScan) without KEYEVENTF_SCANCODE
            // when unicode==false, matching AutoHotkey SendInput so DirectInput (Enter) AND LMU's
            // Coherent GT UI text input box (WM_KEYDOWN / WM_CHAR) both process every keystroke.
            uint flags = (uint)((unicode ? 4 : 0) | (up ? 2 : 0));
            if (!unicode && (scan & 0xFF00) == 0xE000) flags |= 1;
            var v = new INPUT
            {
                type = 1,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        vk = unicode ? (ushort)0 : code,
                        scan = (ushort)(unicode ? scan : (scan & 0xFF)),
                        flags = flags
                    }
                }
            };
            if (SendInput(1u, new[] { v }, Marshal.SizeOf(typeof(INPUT))) != 1)
                throw new InvalidOperationException("O Windows não aceitou a tecla. Verifique se jogo e Arts usam o mesmo nível de permissão. Código: " + Marshal.GetLastWin32Error());
        }

        public static async Task<bool> SendAsync(string message, Func<bool> gameFocused, bool paste, Action<string> stage)
        {
            if (Interlocked.CompareExchange(ref sending, 1, 0) != 0)
                throw new InvalidOperationException("Já existe um envio em andamento.");
            try
            {
                IntPtr window = GetForegroundWindow();
                uint process;
                IntPtr layout = GetKeyboardLayout(GetWindowThreadProcessId(window, out process));
                Func<bool> focused = () => GetForegroundWindow() == window && gameFocused();
                for (int i = 0; i < 40; i++)
                {
                    if (!ModifiersHeld()) break;
                    if (!focused()) return false;
                    await Task.Delay(50);
                }
                if (ModifiersHeld())
                    throw new InvalidOperationException("Solte Ctrl, Alt, Shift ou Windows antes do envio.");
                Func<char, int> map = c =>
                {
                    int mapped = VkKeyScanEx(c, layout);
                    if (mapped >= 0 && char.IsLetter(c) && (GetKeyState(20) & 1) != 0)
                        mapped ^= 0x100;
                    return mapped;
                };
                return await VoiceChatDelivery.Send(
                    message,
                    paste,
                    focused,
                    text =>
                    {
                        var data = new DataObject();
                        data.SetData(DataFormats.UnicodeText, text);
                        Clipboard.SetDataObject(data, true, 5, 40);
                    },
                    map,
                    (code, up, unicode) => Key(code, up, unicode, layout),
                    ms => Task.Delay(ms),
                    stage);
            }
            finally { Interlocked.Exchange(ref sending, 0); }
        }

        static bool ModifiersHeld()
        {
            int[] keys = { 16, 17, 18, 91, 92 };
            for (int i = 0; i < keys.Length; i++)
                if ((GetAsyncKeyState(keys[i]) & 0x8000) != 0) return true;
            return false;
        }
    }
}
