namespace Arthur.LmuOverlay
{
    // One policy for all seven cards, the tray action and the visibility hotkey.
    // Runtime hiding never modifies enabled cards or their saved placement.
    public sealed class OverlayVisibility
    {
        public bool ManuallyHidden { get; private set; }

        public static bool OnTrack(Frame frame,double now)
        {
            if(frame==null || !frame.Connected || !frame.HasTelemetry || frame.Stale ||
                !frame.InRealtime || frame.Phase==9) return false;
            double age=now-frame.ReadAt;
            if(!Format.Finite(age) || age<0 || age>1) return false;
            // SDK: 1 = loading, 2 = monitor/garage, 3 = on track.
            // Zero may also mean an unpopulated optional ApplicationState:
            // fresh telemetry plus mInRealtime remains authoritative then.
            return frame.ApplicationLocation!=1 && frame.ApplicationLocation!=2;
        }

        public bool CanToggle(Frame frame,double now,bool positioning)
        { return !positioning && OnTrack(frame,now); }

        public bool Toggle(Frame frame,double now,bool positioning)
        {
            if(!CanToggle(frame,now,positioning)) return false;
            ManuallyHidden=!ManuallyHidden;return true;
        }

        public bool ShouldShow(bool enabled,Frame frame,double now,bool positioning,bool gameForeground=true)
        { return enabled && (positioning || (gameForeground && !ManuallyHidden && OnTrack(frame,now))); }
    }
}
