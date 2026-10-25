using Arthur.LmuOverlay;

partial class Checks
{
    static void Version092(byte[] bytes)
    {
        var visibility=new OverlayVisibility();double now=10;
        var frame=LmuParser.Parse(bytes,false);frame.ReadAt=now;
        Assert(!visibility.ShouldShow(true,new Frame(),now,false),"startup without LMU keeps cards hidden");
        for(int session=0;session<=13;session++)
        {
            frame.Session=session;
            Assert(visibility.ShouldShow(true,frame,now,false),"live driving remains visible in session "+session);
        }
        frame.Speed=frame.Rpm=0;
        foreach(var driver in frame.Drivers) if(driver.IsPlayer) driver.InPits=true;
        Assert(visibility.ShouldShow(true,frame,now,false),"driving mode at zero speed in the pit lane is not the garage menu");
        frame.ApplicationLocation=0;
        Assert(visibility.ShouldShow(true,frame,now,false),"optional zero application location does not hide valid realtime telemetry");
        frame.InRealtime=false;
        Assert(!visibility.ShouldShow(true,frame,now,false) && !visibility.Toggle(frame,now,false),"menu with cached car data hides cards and ignores the hotkey");
        frame.InRealtime=true;frame.ApplicationLocation=1;
        Assert(!visibility.ShouldShow(true,frame,now,false),"loading hides even if realtime flag is not yet updated");
        frame.ApplicationLocation=2;
        Assert(!visibility.ShouldShow(true,frame,now,false) && !visibility.Toggle(frame,now,false),"garage hides and cannot be overridden by the hotkey");
        frame.ApplicationLocation=3;
        Assert(visibility.ShouldShow(true,frame,now,false) && !visibility.ManuallyHidden,"return to track restores cards without queued menu hotkeys");
        Assert(visibility.Toggle(frame,now,false) && !visibility.ShouldShow(true,frame,now,false),"track hotkey hides");
        frame.InRealtime=false;
        Assert(!visibility.Toggle(frame,now,false) && visibility.ManuallyHidden,"menu hotkey cannot change manual hidden choice");
        frame.InRealtime=true;
        Assert(!visibility.ShouldShow(true,frame,now,false),"return to track preserves manual hide");
        Assert(visibility.Toggle(frame,now,false) && visibility.ShouldShow(true,frame,now,false),"track hotkey shows again");

        var freshness=new DataFreshness();frame.ScoringEvent=frame.TelemetryEvent=0;freshness.Apply(frame,now);
        var paused=frame.Copy();paused.Phase=9;freshness.Apply(paused,now+.1);paused.ReadAt=now+.1;
        Assert(!visibility.ShouldShow(true,paused,now+.1,false) && !visibility.Toggle(paused,now+.1,false),"SDK paused heartbeat hides immediately despite fresh packet");
        var frozen=frame.Copy();freshness.Apply(frozen,now+2.1);frozen.ReadAt=now+2.1;
        Assert(!visibility.ShouldShow(true,frozen,now+2.1,false),"polling unchanged native samples cannot keep a frozen overlay visible");
        var resumed=frame.Copy();resumed.Elapsed+=3;resumed.TelemetryElapsed+=3;resumed.ReadAt=now+3;freshness.Apply(resumed,now+3);
        Assert(visibility.ShouldShow(true,resumed,now+3,false),"advancing native telemetry restores after pause without restart");
        Assert(!visibility.ShouldShow(true,resumed,now+4.01,false) && !visibility.Toggle(resumed,now+4.01,false),"stopped reader cannot reopen an old frame with the hotkey");
        Assert(!visibility.ShouldShow(true,resumed,double.NaN,false),"invalid sample age cannot show stale cards");
        resumed.HasTelemetry=false;
        Assert(!visibility.ShouldShow(true,resumed,now+3,false),"losing the player telemetry hides cards");
        resumed.HasTelemetry=true;resumed.Connected=false;
        Assert(!visibility.ShouldShow(true,resumed,now+3,false),"disconnect hides even with old telemetry fields");

        var offline=new Frame();
        Assert(visibility.ShouldShow(true,offline,now,true),"explicit positioning is available offline");
        Assert(!visibility.ShouldShow(false,offline,now,true),"positioning never enables an unwanted optional card");
        Assert(!visibility.Toggle(offline,now,true) && !visibility.Toggle(frame,now,true),"hotkey does not interrupt positioning on or off track");
        Assert(!visibility.ShouldShow(true,offline,now,false),"finishing positioning returns to automatic hiding");
        visibility.Toggle(frame,now,false);
        Assert(visibility.ShouldShow(true,frame,now,true) && !visibility.ShouldShow(true,frame,now,false),"editing does not erase manual visibility choice");
        Assert(!new OverlayVisibility().ShouldShow(false,frame,now,false),"return to track never enables a disabled card");
    }
}
