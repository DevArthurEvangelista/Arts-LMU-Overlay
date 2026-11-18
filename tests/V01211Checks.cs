using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Arthur.LmuOverlay;

partial class Checks
{
    static void Version01211()
    {
        Assert(AppInfo.Version == "0.13.4" && AppInfo.AssemblyVersion == "0.13.4.0", "version metadata matches 0.13.4");

        string dictError = null;
        using (var dictation = new VoiceDictation { ExecutablePath = "ausente-whisper.exe", ModelPath = "ausente-model.bin" })
        {
            dictation.Error += m => dictError = m;
            Assert(!dictation.Start() && dictError != null && dictError.Contains("whisper-cli.exe"), "missing Whisper binary or model fails safely before opening microphone");
        }

        var visibility = new OverlayVisibility();
        var onTrack = DFrame(10, 0, 100);
        onTrack.ReadAt = 10;
        Assert(visibility.ShouldShow(true, onTrack, 10, false, false, true), "controller or appearance editor foreground previews live panels on track");
        visibility.Toggle(onTrack, 10, false);
        Assert(!visibility.ShouldShow(true, onTrack, 10, false, false, true), "manual F8 hide still suppresses controller-foreground preview");

        var events = new List<string>();
        var stages = new List<string>();
        string copied = null;
        bool hasFocus = true;

        bool empty = VoiceChatDelivery.Send("   ", false, () => true, s => copied = s, c => c,
            (code, up, uni) => events.Add(code + ":" + up + ":" + uni), ms => Task.FromResult(0), stages.Add).GetAwaiter().GetResult();
        Assert(!empty && events.Count == 0, "blank voice transcript never opens chat");

        bool tooLong = VoiceChatDelivery.Send(new string('a', 181), false, () => true, s => copied = s, c => c,
            (code, up, uni) => events.Add(code + ":" + up + ":" + uni), ms => Task.FromResult(0), stages.Add).GetAwaiter().GetResult();
        Assert(!tooLong && events.Count == 0, "overlong voice transcript is rejected before opening chat");

        bool controlRejected = false;
        try
        {
            VoiceChatDelivery.Send("Oi\tBox", false, () => true, s => { }, c => c,
                (code, up, uni) => { }, ms => Task.FromResult(0), stages.Add).GetAwaiter().GetResult();
        }
        catch (ArgumentException) { controlRejected = true; }
        Assert(controlRejected, "control characters inside transcript are rejected");

        events.Clear();
        stages.Clear();
        bool typed = VoiceChatDelivery.Send("Oi!", false, () => hasFocus, s => copied = s,
            c => c == 'O' ? (0x100 | 79) : c == 'i' ? 73 : -1,
            (code, up, uni) => events.Add(code + ":" + (up ? "up" : "down") + ":" + (uni ? "u" : "vk")),
            ms => Task.FromResult(0), stages.Add).GetAwaiter().GetResult();
        Assert(typed && copied == null, "typing mode completes without touching clipboard");
        Assert(events[0] == "13:down:vk" && events[1] == "13:up:vk", "typing mode opens chat with Enter");
        Assert(events.Contains("16:down:vk") && events.Contains("79:down:vk") && events.Contains("16:up:vk"), "shifted letter presses and releases Shift around virtual key");
        Assert(events.Contains(((ushort)'!') + ":down:u") && events.Contains(((ushort)'!') + ":up:u"), "unmapped symbol falls back to Unicode key event");
        Assert(events[events.Count - 2] == "13:down:vk" && events[events.Count - 1] == "13:up:vk", "typing mode confirms chat with Enter");

        events.Clear();
        copied = null;
        bool pasted = VoiceChatDelivery.Send("Box nesta volta", true, () => true, s => copied = s, c => 65,
            (code, up, uni) => events.Add(code + ":" + (up ? "up" : "down")),
            ms => Task.FromResult(0), stages.Add).GetAwaiter().GetResult();
        Assert(pasted && copied == "Box nesta volta", "paste mode copies normalized message to clipboard");
        Assert(events.Contains("17:down") && events.Contains("86:down") && events.Contains("86:up") && events.Contains("17:up"), "paste mode presses and releases Ctrl+V");

        events.Clear();
        int focusChecks = 0;
        bool aborted = VoiceChatDelivery.Send("A", false, () => ++focusChecks <= 5, s => { }, c => 0x100 | 65,
            (code, up, uni) => events.Add(code + ":" + (up ? "up" : "down")),
            ms => Task.FromResult(0), stages.Add).GetAwaiter().GetResult();
        Assert(!aborted && events.Contains("16:down") && events.Contains("16:up"),
            "losing game focus mid-typing aborts delivery and still releases held modifiers");
    }
}
