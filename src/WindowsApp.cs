using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Arthur.LmuOverlay
{
    [DataContract]
    public sealed class WidgetPlacement
    {
        [DataMember] public int X,Y,Width,Height;
        [DataMember] public bool Visible;
        public static WidgetPlacement[] Expand(WidgetPlacement[] old,Rectangle r)
        {
            var result=new WidgetPlacement[5];
            for(int i=0;i<5;i++) result[i]=old!=null&&i<old.Length&&old[i]!=null?old[i]:new WidgetPlacement { X=i==4?r.Left+(r.Width-460)/2:r.Right-(i<2?344:690),Y=i==4?r.Top+r.Height/3:r.Top+70+(i%2)*300,Visible=i==4 };
            return result;
        }

    }
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public int SchemaVersion=11;
        [DataMember] public PanelAppearance[] Appearance;
        [DataMember] public int PedalsX=700,PedalsY=700,TableX=24,TableY=100;
        [DataMember] public int PedalsScale=100,TableScale=100,Seconds=PedalDesign.HistorySeconds;
        [DataMember] public int PedalsWidth,PedalsHeight,TableWidth,TableHeight;
        [DataMember] public bool OnlyMyClass=true,UsePhysicalWheel=true;
        [DataMember] public WidgetPlacement[] Widgets;
        [DataMember] public WheelCalibration WheelCalibration;
        [DataMember] public int PedalOpacity=92,TableOpacity=92;
        [DataMember] public int DeltaReference,DeltaOpacity,DeltaRange=2;
        [DataMember] public int DeltaThickness=16;
        [DataMember] public TrainingWheelBinding TrainingSaveButton,TrainingRestoreButton;
        [DataMember] public string LmuExecutablePath="";
        [DataMember(EmitDefaultValue=false)] public int Opacity;
        [DataMember] public bool ShowPedals=true,ShowTable=true,PositionsSet,LinkOpacity,Flags=true,PulseShift=true,SmoothMotion=true;
        [DataMember] public int ShiftRpm,SteeringRange;
        [DataMember] public int SteeringGain=100;
        // Zero on the physical field keeps the guided calibration's measured
        // scale. A value such as 900 lets a replacement wheel use its full
        // configured rotation without changing the overlay layout.
        [DataMember] public int WheelMaximumRotation,CarMaximumRotation;
        [DataMember] public int HotkeyKey=(int)Keys.F8,HotkeyModifiers=0;
        public static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArthurLMUOverlay"); } }
        public static string FilePath { get { return Path.Combine(DirectoryPath,"config.xml"); } }
        [OnDeserializing] void Defaults(StreamingContext c)
        {
            SchemaVersion=0;DeltaRange=2;DeltaThickness=16; PedalsScale=TableScale=100; Seconds=PedalDesign.HistorySeconds; PedalOpacity=92; TableOpacity=92;SteeringGain=100;WheelMaximumRotation=0;CarMaximumRotation=0;UsePhysicalWheel=true;
            ShowPedals=ShowTable=Flags=PulseShift=SmoothMotion=true; HotkeyKey=(int)Keys.F8; HotkeyModifiers=0;
        }
        public static AppSettings Load(out string warning)
        {
            warning="";
            try
            {
                if(!File.Exists(FilePath)) return new AppSettings();
                using(var stream=File.OpenRead(FilePath))
                {
                    var s=(AppSettings)new DataContractSerializer(typeof(AppSettings)).ReadObject(stream);
                    if(s.SchemaVersion<2)
                    {
                        if(s.Opacity>=20 && s.Opacity<=100 && s.Opacity!=94) s.PedalOpacity=s.TableOpacity=s.Opacity;
                        s.Flags=s.PulseShift=true; s.SchemaVersion=3; s.Opacity=0;
                    }
                    if(s.SchemaVersion<3) { s.SmoothMotion=true;s.SchemaVersion=3; }
                    if(s.SchemaVersion<4) { s.SteeringGain=50;s.SchemaVersion=4; }
                    if(s.SchemaVersion<5) { s.SteeringGain=100;s.UsePhysicalWheel=true;s.SchemaVersion=5; }
                    if(s.SchemaVersion<6) { s.WheelMaximumRotation=0;s.CarMaximumRotation=0;s.SchemaVersion=6; }
                    if(s.SchemaVersion<7)
                    { s.Seconds=PedalDesign.HistorySeconds;s.OnlyMyClass=true;s.TableHeight=0;s.PedalsHeight=0;s.PedalOpacity=Math.Max(88,s.PedalOpacity);s.TableOpacity=Math.Max(88,s.TableOpacity);s.SchemaVersion=7; }
                    // Move only the old default. Keep a shortcut the user chose.
                    if(s.SchemaVersion<9 && s.HotkeyKey==(int)Keys.H && s.HotkeyModifiers==3)
                    { s.HotkeyKey=(int)Keys.F8;s.HotkeyModifiers=0; }
                    s.SchemaVersion=11;s.Appearance=PanelAppearance.NormalizeAll(s.Appearance);
                    s.DeltaThickness=(int)Format.Clamp(s.DeltaThickness,6,28);s.DeltaReference=DeltaMode.Normalize(s.DeltaReference);s.DeltaOpacity=(int)Format.Clamp(s.DeltaOpacity,0,100);s.DeltaRange=(int)Format.Clamp(s.DeltaRange,1,10);
                    s.OnlyMyClass=true;s.Seconds=PedalDesign.HistorySeconds;
                    s.PedalsScale=(int)Format.Clamp(s.PedalsScale,40,200); s.TableScale=(int)Format.Clamp(s.TableScale,40,200);
                    s.PedalOpacity=(int)Format.Clamp(s.PedalOpacity,20,100); s.TableOpacity=(int)Format.Clamp(s.TableOpacity,20,100);
                    s.Seconds=(int)Format.Clamp(s.Seconds,3,10); s.ShiftRpm=(int)Format.Clamp(s.ShiftRpm,0,20000);
                    s.SteeringRange=(int)Format.Clamp(s.SteeringRange,0,2880);
                    s.SteeringGain=(int)Format.Clamp(s.SteeringGain,10,200);
                    s.WheelMaximumRotation=(int)Format.Clamp(s.WheelMaximumRotation,0,2880);
                    s.CarMaximumRotation=(int)Format.Clamp(s.CarMaximumRotation,0,2880);
                    if(s.HotkeyKey<8 || s.HotkeyKey>254 || s.HotkeyKey==(int)Keys.F12) { s.HotkeyKey=(int)Keys.F8;s.HotkeyModifiers=0; }
                    s.HotkeyModifiers&=7;
                    return s;
                }
            }
            catch(Exception ex)
            {
                if(!(ex is IOException) && !(ex is SerializationException) && !(ex is System.Xml.XmlException) && !(ex is UnauthorizedAccessException)) throw;
                warning="Não foi possível ler a configuração salva. Foram usados os valores iniciais.";
                try { File.Copy(FilePath,FilePath+".recuperacao-"+DateTime.Now.ToString("yyyyMMddHHmmss"),false); } catch(IOException) { } catch(UnauthorizedAccessException) { }
                return new AppSettings();
            }
        }
        public string Save()
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath); string tmp=FilePath+".tmp";
                using(var stream=File.Create(tmp)) new DataContractSerializer(typeof(AppSettings)).WriteObject(stream,this);
                if(File.Exists(FilePath)) File.Replace(tmp,FilePath,FilePath+".bak"); else File.Move(tmp,FilePath);
                return "";
            }
            catch(Exception ex)
            { if(!(ex is IOException) && !(ex is UnauthorizedAccessException)) throw; return "Não foi possível salvar as configurações na sua pasta de usuário."; }
        }
    }
    sealed class KeyCapture : Form
    {
        public int SelectedKey,SelectedModifiers;
        readonly Label prompt;
        public KeyCapture()
        {
            Text="Definir atalho"; FormBorderStyle=FormBorderStyle.FixedDialog; StartPosition=FormStartPosition.CenterParent;
            MaximizeBox=MinimizeBox=false; ShowInTaskbar=false; TopMost=true; ClientSize=new Size(445,148);
            Font=new Font(Theme.BodyFont,11); BackColor=Theme.Background; ForeColor=Theme.Text;
            prompt=new Label { Text="Pressione uma tecla ou combinação.\nVocê pode usar Ctrl, Alt e Shift.\nEsc cancela.",Dock=DockStyle.Fill,Padding=new Padding(20),TextAlign=ContentAlignment.MiddleLeft };
            Controls.Add(prompt);
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys data)
        {
            Keys key=data&Keys.KeyCode;
            if(key==Keys.Escape) { DialogResult=DialogResult.Cancel;Close();return true; }
            if(key==Keys.ControlKey || key==Keys.ShiftKey || key==Keys.Menu || key==Keys.LWin || key==Keys.RWin) return true;
            if(key==Keys.F12) { prompt.Text="F12 é reservada pelo Windows.\nEscolha outra tecla.\nEsc cancela.";return true; }
            SelectedKey=(int)key;SelectedModifiers=((data&Keys.Control)!=0 ? 2 : 0)|((data&Keys.Alt)!=0 ? 1 : 0)|((data&Keys.Shift)!=0 ? 4 : 0);
            DialogResult=DialogResult.OK;Close();return true;
        }
        public static string Display(int key,int mods)
        {
            return ((mods&2)!=0 ? "Ctrl + " : "")+((mods&1)!=0 ? "Alt + " : "")+((mods&4)!=0 ? "Shift + " : "")+new KeysConverter().ConvertToString((Keys)key);
        }
    }
    sealed class BrandHeader : Control
    {
        readonly Bitmap logo=BrandArtwork.LoadLogo();
        public BrandHeader()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            Width=684;Height=108;AccessibleName=AppInfo.Name;TabStop=false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var state=e.Graphics.Save();float scale=e.Graphics.DpiX/96f;e.Graphics.ScaleTransform(scale,scale);
            BrandArtwork.Header(e.Graphics,logo,Width/scale,Height/scale);e.Graphics.Restore(state);
        }
        protected override void Dispose(bool disposing) { if(disposing) logo.Dispose();base.Dispose(disposing); }
    }
    public sealed partial class Controller : Form
    {
        public Frame Current=new Frame();
        public InputHistory History { get { return pump.History; } }
        public Frame Display=new Frame();
        readonly InputMotion motion=new InputMotion();
        readonly DeltaMotion deltaMotion=new DeltaMotion();
        readonly DeltaRepository deltaRepository;
        readonly DeltaTracker delta;
        public readonly ViewOptions Options=new ViewOptions();
        public double Now { get { return clock.Elapsed.TotalSeconds; } }
        public bool WindowInteractionPaused { get { return capturing || popup.Blocks(Now); } }
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly TelemetryPump pump;
        readonly LearningStore learningStore;
        readonly LearningPump learning;
        readonly VehicleDamageService vehicleDamage;
        LearningWindow learningWindow;
        readonly SteeringSetup steeringSetup=new SteeringSetup();
        readonly StartPositions grid=new StartPositions();
        readonly CountryService countries=new CountryService(AppLog.Write);
        readonly PenaltyService penalties=new PenaltyService(AppLog.Write);
        readonly WheelPump wheel;
        System.Collections.Generic.List<WheelAxis> wheelAxes=new System.Collections.Generic.List<WheelAxis>();
        readonly AppSettings settings;
        readonly OverlayWindow pedals,table;
        readonly OverlayWindow[] extras;
        readonly OverlayWindow[] panels;
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer { Interval=16 };
        readonly System.Windows.Forms.Timer saveTimer=new System.Windows.Forms.Timer { Interval=600 };
        readonly NotifyIcon tray=new NotifyIcon();
        readonly ContextMenuStrip trayMenu=new ContextMenuStrip();
        readonly ToolStripMenuItem trayVisibility=new ToolStripMenuItem("Mostrar / ocultar na pista");
        readonly PopupInteraction popup=new PopupInteraction();
        readonly OverlayVisibility visibility=new OverlayVisibility();
        readonly Label status=new Label(),saved=new Label(),hotkey=new Label(),flagStatus=new Label();
        readonly CheckBox positioning=new CheckBox();
        CheckBox physicalWheelCheck;
        NumericUpDown pedalAlpha,tableAlpha;
        PanelAppearanceDialog appearanceDialog;
        bool suppress,closing,capturing,exitRequested,notifiedClose;
        int visibilityHotkey,displayRefresh;
        double lastTable=-1,lastMaintenance=-1,lastDiagnostic=-30;
        int diagnosticBusy;
        double processChecked=-1;
        uint checkedProcess;
        bool checkedIsGame;
        string startupWarning="";
        readonly Label steeringStatus=new Label();
        public Controller()
        {
            settings=AppSettings.Load(out startupWarning);
            settings.Appearance=PanelAppearance.NormalizeAll(settings.Appearance);
            AppLog.Write("Configurações carregadas",null);
            deltaRepository=new DeltaRepository(new DeltaFiles(Path.Combine(AppSettings.DirectoryPath,"ReferenciasDelta")),AppLog.Write);delta=new DeltaTracker(deltaRepository);
            pump=new TelemetryPump(()=>Now,AppLog.Write) { Delta=delta };
            sector=new SectorEngine(AppDomain.CurrentDomain.BaseDirectory,AppSettings.DirectoryPath,AppLog.Write);sector.Restored+=pump.BreakTraining;wheel=new WheelPump(()=>Now,AppLog.Write);
            learningStore=new LearningStore(Path.Combine(AppSettings.DirectoryPath,"Aprendizado"));
            learning=new LearningPump(learningStore,AppLog.Write);pump.LearningSample=learning.Accept;
            vehicleDamage=new VehicleDamageService(()=>Now,AppLog.Write);
            InitializeWidgets();
            AppLog.Write("Aplicativo iniciado",null);
            Text=AppInfo.Name+" • "+AppInfo.Version; AccessibleName="Configurações do "+AppInfo.Name;TopMost=false;
            Font=new Font(Theme.BodyFont,10,FontStyle.Regular,GraphicsUnit.Point); BackColor=Theme.Background;ForeColor=Theme.Text;
            StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
            var workArea=Screen.FromPoint(Cursor.Position).WorkingArea;
            MinimumSize=new Size(Math.Min(900,workArea.Width-16),Math.Min(600,workArea.Height-16));
            ClientSize=new Size(Math.Min(1020,workArea.Width-48),Math.Min(820,workArea.Height-72));
            Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            pedals=new OverlayWindow(this,PanelKind.Pedals);table=new OverlayWindow(this,PanelKind.Standings);
            extras=new[]{new OverlayWindow(this,PanelKind.LapTimer),new OverlayWindow(this,PanelKind.Tyres),new OverlayWindow(this,PanelKind.Fuel),new OverlayWindow(this,PanelKind.TrackMap),new OverlayWindow(this,PanelKind.Delta)};
            panels=new[]{pedals,table}.Concat(extras).ToArray();
            foreach(var panel in panels) panel.PositionCommitted+=PositionsChanged;
            AppLog.Write("Montando a configuração",null);
            BuildControls();if(!settings.PositionsSet) ResetPositions(false);ApplySettings();
            AppLog.Write("Configuração pronta",null);
            saveTimer.Tick+=delegate { saveTimer.Stop(); string error=settings.Save(); saved.Text=error.Length==0 ? "Alterações salvas." : error;if(appearanceDialog!=null) appearanceDialog.SetSaveState(saved.Text); };
            timer.Tick+=Tick;
            trayMenu.Items.Add("Abrir painel",null,delegate { ShowController(); });
            trayMenu.Items.Add("Aprendizado",null,delegate { OpenLearning(); });
            trayMenu.Items.Add("Posicionar / travar",null,delegate { positioning.Checked=!positioning.Checked; });
            trayVisibility.Click+=delegate { ToggleVisible(); };trayMenu.Items.Add(trayVisibility);
            trayMenu.Items.Add(new ToolStripSeparator());trayMenu.Items.Add("Encerrar o app",null,delegate { ExitApp(); });
            trayMenu.Opening+=delegate { popup.Open();ApplyVisibility(); };
            trayMenu.Closed+=delegate { popup.Close(Now); };
            tray.Text=AppInfo.Name;tray.Icon=Icon;tray.ContextMenuStrip=trayMenu;
            tray.MouseDoubleClick+=delegate(object sender,MouseEventArgs e) { if(e.Button==MouseButtons.Left) ShowController(); };tray.Visible=true;
            SystemEvents.DisplaySettingsChanged+=DisplaySettingsChanged;
            Shown+=delegate
            {
                ApplyVisibility();pump.Start();wheel.Start(Handle);timer.Start();ShowController();
                bool a=Win32.RegisterHotKey(Handle,1,0x4003,(uint)Keys.O),b=Win32.RegisterHotKey(Handle,2,0x4003,(uint)Keys.L);
                bool c=SetVisibilityKey(settings.HotkeyKey,settings.HotkeyModifiers);
                saved.Text=startupWarning.Length>0 ? startupWarning : "As alterações são salvas automaticamente.";
                if(!a || !b || !c) saved.Text="Um atalho está em uso. Defina outro ou use o ícone junto ao relógio do Windows.";
            };
        }
        Label Label(string text,int height=27)
        { return SettingsUi.Label(text,height); }
        Button Button(string text,Action action)
        { return SettingsUi.Button(text,action); }
        CheckBox Check(string text,bool value,Action<bool> action)
        {
            var c=new CheckBox { Text=text,Checked=value,AutoSize=true,Padding=new Padding(0,3,0,3),ForeColor=Theme.Text,Cursor=Cursors.Hand };
            c.CheckedChanged+=delegate { if(!suppress) action(c.Checked); };return c;
        }
        NumericUpDown Number(int value,int min,int max,int step,Action<int> action,string accessible)
        { return SettingsUi.Number(value,min,max,step,v=>{if(!suppress) action(v);},accessible); }
        void Row(TableLayoutPanel p,string title,Control control)
        {
            int row=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var l=Label(title,30);l.Dock=DockStyle.Fill;l.Margin=new Padding(0,0,5,3);
            p.Controls.Add(l,0,row);control.Margin=new Padding(0,4,0,3);p.Controls.Add(control,1,row);
        }
        void OpenLearning()
        {
            if(learningWindow==null || learningWindow.IsDisposed)
            {
                learningWindow=new LearningWindow(learningStore,learning);learningWindow.Icon=Icon;
                learningWindow.PopupStateChanged=open=>{if(open) popup.Open();else popup.Close(Now);};
            }
            learningWindow.Show();learningWindow.WindowState=FormWindowState.Normal;learningWindow.Activate();
        }
        void EditAppearance(int index)
        {
            var panel=panels[index];
            string[] names={"Pedais, motor e volante","Informações dos pilotos","Lap timer","Pneus e danos","Combustível","Mapa da minha classe","Delta central"};
            using(var dialog=new PanelAppearanceDialog(names[index],settings.Appearance[index],()=>panel.ClientSize,
                ()=>panel.Kind==PanelKind.Standings && Current.Drivers.Count==0 ? new Size(Painter.TableWidth,118+8*Painter.StandingsRowHeight) : panel.DesignSize,
                size=>panel.SetDimensions(size),Changed))
            {
                appearanceDialog=dialog;
                try { dialog.ShowDialog(this); }
                finally { appearanceDialog=null; }
            }
        }
        void OpacityChanged(bool isPedal,int value)
        {
            if(isPedal) settings.PedalOpacity=value;else settings.TableOpacity=value;
            if(settings.LinkOpacity) settings.PedalOpacity=settings.TableOpacity=value;
            suppress=true;pedalAlpha.Value=settings.PedalOpacity;tableAlpha.Value=settings.TableOpacity;suppress=false;Changed();
        }
        void QueueSave()
        { saved.Text="Alterações aplicadas. Salvando…";if(appearanceDialog!=null) appearanceDialog.SetSaveState(saved.Text);saveTimer.Stop();saveTimer.Start(); }
        void Changed() { settings.PositionsSet=true;ApplySettings();QueueSave(); }
        void ApplySettings()
        {
            Options.HistorySeconds=PedalDesign.HistorySeconds;Options.PedalOpacity=settings.PedalOpacity;Options.TableOpacity=settings.TableOpacity;
            Options.OnlyMyClass=true;Options.DeltaReference=settings.DeltaReference;Options.DeltaOpacity=settings.DeltaOpacity;Options.DeltaRange=settings.DeltaRange;Options.DeltaThickness=settings.DeltaThickness;
            Options.ShiftRpm=settings.ShiftRpm;Options.SteeringRange=settings.SteeringRange;Options.WheelMaximumRotation=settings.WheelMaximumRotation;Options.CarMaximumRotation=settings.CarMaximumRotation;Options.SteeringGain=settings.SteeringGain;Options.PulseShift=settings.PulseShift;
            pedals.ScaleValue=settings.PedalsScale/100.0;table.ScaleValue=settings.TableScale/100.0;
            pedals.PreferredWidth=settings.PedalsWidth;pedals.PreferredHeight=settings.PedalsHeight;table.PreferredWidth=settings.TableWidth;table.PreferredHeight=settings.TableHeight;
            pedals.Location=new Point(settings.PedalsX,settings.PedalsY);table.Location=new Point(settings.TableX,settings.TableY);
            for(int i=0;i<extras.Length;i++)
            {
                var p=settings.Widgets[i];extras[i].Location=new Point(p.X,p.Y);extras[i].PreferredWidth=p.Width;extras[i].PreferredHeight=p.Height;
            }
            for(int i=0;i<panels.Length;i++) { panels[i].Appearance=settings.Appearance[i];panels[i].FitSize(); }
            hotkey.Text=KeyCapture.Display(settings.HotkeyKey,settings.HotkeyModifiers);
            if(IsHandleCreated) ApplyVisibility();foreach(var panel in panels) panel.DrawFrame();
        }
        void ApplyVisibility()
        {
            double now=Now;bool editing=positioning.Checked;
            bool gameFocused=IsGameForeground(Win32.GetForegroundWindow(),now);
            pedals.SetRequestedVisibility(visibility.ShouldShow(settings.ShowPedals,Current,now,editing,gameFocused));
            table.SetRequestedVisibility(visibility.ShouldShow(settings.ShowTable,Current,now,editing,gameFocused));
            for(int i=0;i<extras.Length;i++)
                extras[i].SetRequestedVisibility(visibility.ShouldShow(settings.Widgets[i].Visible,Current,now,editing,gameFocused));
            trayVisibility.Enabled=visibility.CanToggle(Current,now,editing);
            trayVisibility.Text=editing ? "Conclua o posicionamento para ocultar" :
                !trayVisibility.Enabled ? "Mostrar / ocultar: disponível na pista" :
                visibility.ManuallyHidden ? "Mostrar overlays na pista" : "Ocultar overlays na pista";
        }
        void ToggleVisible() { if(visibility.Toggle(Current,Now,positioning.Checked)) ApplyVisibility(); }
        public void LockPanels() { positioning.Checked=false; }
        void PositionsChanged()
        {
            settings.PedalsX=pedals.Left;settings.PedalsY=pedals.Top;settings.TableX=table.Left;settings.TableY=table.Top;
            settings.PedalsScale=(int)Math.Round(pedals.ScaleValue*100);settings.TableScale=(int)Math.Round(table.ScaleValue*100);
            settings.PedalsWidth=pedals.PreferredWidth;settings.PedalsHeight=pedals.PreferredHeight;settings.TableWidth=table.PreferredWidth;settings.TableHeight=table.PreferredHeight;
            for(int i=0;i<extras.Length;i++)
            {
                var p=settings.Widgets[i];p.X=extras[i].Left;p.Y=extras[i].Top;p.Width=extras[i].PreferredWidth;p.Height=extras[i].PreferredHeight;
            }
            settings.PositionsSet=true;QueueSave();
        }
        void InitializeWidgets()
        {
            var r=Screen.PrimaryScreen.WorkingArea;
            settings.Widgets=WidgetPlacement.Expand(settings.Widgets,r);
        }
        void ResetPositions(bool commit)
        {
            var r=Screen.PrimaryScreen.WorkingArea;settings.PedalsScale=settings.TableScale=100;
            settings.PedalsWidth=settings.PedalsHeight=settings.TableWidth=settings.TableHeight=0;
            settings.PedalsX=r.Left+Math.Max(0,(r.Width-Painter.PedalsWidth)/2);settings.PedalsY=r.Bottom-Painter.PedalsHeight-24;
            settings.TableX=r.Left+22;settings.TableY=r.Top+70;settings.PositionsSet=true;
            for(int i=0;i<settings.Widgets.Length;i++)
            { var p=settings.Widgets[i];p.X=i==4?r.Left+(r.Width-460)/2:r.Right-(i<2 ? 344 : 690);p.Y=i==4?r.Top+r.Height/3:r.Top+70+(i%2)*300;p.Width=p.Height=0; }
            if(commit) Changed();
        }
        bool SetVisibilityKey(int key,int mods)
        {
            if(sector.Busy && mods==0 && (key==(int)Keys.F6 || key==(int)Keys.F7)) return false;
            if(mods==3 && (key==(int)Keys.O || key==(int)Keys.L)) return false;
            if(visibilityHotkey!=0 && settings.HotkeyKey==key && settings.HotkeyModifiers==mods) return true;
            int newId=visibilityHotkey==3 ? 4 : 3;
            if(!Win32.RegisterHotKey(Handle,newId,(uint)mods|0x4000,(uint)key)) return false;
            if(visibilityHotkey!=0) Win32.UnregisterHotKey(Handle,visibilityHotkey);
            visibilityHotkey=newId;settings.HotkeyKey=key;settings.HotkeyModifiers=mods;hotkey.Text=KeyCapture.Display(key,mods);return true;
        }
        void BindKey()
        {
            capturing=true;
            try
            {
                using(var dialog=new KeyCapture()) if(dialog.ShowDialog(this)==DialogResult.OK)
                {
                    if(SetVisibilityKey(dialog.SelectedKey,dialog.SelectedModifiers)) Changed();
                    else saved.Text="Essa combinação está em uso. Seu atalho anterior foi mantido; escolha outro.";
                }
            }
            finally { capturing=false; }
        }
        void CalibrateWheel()
        {
            capturing=true;
            try
            {
                wheel.Refresh();wheelAxes=wheel.Read(Now);
                using(var dialog=new WheelCalibrationDialog(()=>wheel.Read(Now),Current.SteeringRange>0 ? Current.SteeringRange : settings.CarMaximumRotation))
                    if(dialog.ShowDialog(this)==DialogResult.OK)
                    { settings.WheelCalibration=dialog.Calibration;settings.UsePhysicalWheel=true;physicalWheelCheck.Checked=true;Changed();saved.Text="Volante calibrado. A leitura física foi validada além de 90°."; }
            }
            finally { capturing=false; }
        }
        public void ReportRecoverable(string context,Exception error)
        { AppLog.Write(context,error); }
        public void RequestDisplayRefresh() { Interlocked.Exchange(ref displayRefresh,1); }
        void DisplaySettingsChanged(object sender,EventArgs e) { RequestDisplayRefresh(); }
        void Diagnostic()
        {
            if(Now-lastDiagnostic<5 || Interlocked.CompareExchange(ref diagnosticBusy,1,0)!=0) return;lastDiagnostic=Now;
            try
            {
                var culture=System.Globalization.CultureInfo.InvariantCulture;
                var samples=History.Window(Now,1);
                string report=AppInfo.Name+" "+AppInfo.Version+"\r\n"+DateTime.Now.ToString("s")+
                    "\r\nStatus: "+Current.Status+"\r\nVersão jogo: "+Current.GameVersion+"\r\nSessão/fase: "+Current.Session+"/"+Current.Phase+
                    "\r\nCarros classificação/telemetria: "+Current.ScoringCount+"/"+Current.ActiveTelemetryCount+"\r\nIdentificação: "+Current.PlayerIdentification+
                    "\r\nID local: "+Current.PlayerId+"\r\nContadores: "+Current.ScoringEvent+"/"+Current.TelemetryEvent+
                    "\r\nET scoring/telemetria: "+Current.Elapsed.ToString(culture)+"/"+Current.TelemetryElapsed.ToString(culture)+
                    "\r\nAmostras recebidas no último segundo: "+samples.Count+
                    "\r\nAcelerador/freio: "+Current.Throttle.ToString("0.000",culture)+"/"+Current.Brake.ToString("0.000",culture)+
                    "\r\nDireção normalizada: "+Current.Steering.ToString("0.000",culture)+" ("+Current.SteeringInputSource+")"+
                    "\r\nFaixa física/setup/visual: "+Current.SteeringRange+"/"+Current.GarageSteeringRange+"/"+Current.VisualSteeringRange+
                    "\r\nFaixa usada: "+Instruments.SteeringRange(Current,Options.SteeringRange,Options.CarMaximumRotation)+" ("+Instruments.SteeringSource(Current,Options.SteeringRange)+")"+
                    "\r\nModo de direção: "+(settings.UsePhysicalWheel ? "FÍSICO" : "JOGO")+"\r\nEstado físico: "+Current.WheelStatus+
                    "\r\nDispositivo calibrado: "+(settings.WheelCalibration==null ? "nenhum" : settings.WheelCalibration.DeviceName)+
                    "\r\nÂngulo exibido: "+Instruments.SteeringAngle(Current,Options.SteeringRange,Options.SteeringGain,Options.CarMaximumRotation).ToString("0.0",culture)+
                    "\r\nLimitador: "+Current.PitLimiter+"\r\n"+penalties.Status+
                    "\r\nNa pista/local da aplicação: "+Current.InRealtime+"/"+Current.ApplicationLocation+
                    "\r\nVisibilidade: "+(OverlayVisibility.OnTrack(Current,Now) ? "leitura ativa na pista" : "oculto por menu/pausa")+
                    " • oculto pelo atalho: "+visibility.ManuallyHidden+" • posicionando: "+positioning.Checked+
                    "\r\nPainéis visíveis: "+pedals.Visible+"/"+table.Visible+"\r\nRender: "+pedals.RenderStatus+" / "+table.RenderStatus+"\r\n";
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { Directory.CreateDirectory(AppSettings.DirectoryPath);File.WriteAllText(Path.Combine(AppSettings.DirectoryPath,"diagnostico.txt"),report); }
                    catch(IOException) { } catch(UnauthorizedAccessException) { }
                    finally { Interlocked.Exchange(ref diagnosticBusy,0); }
                });
            }
            catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
        void Tick(object sender,EventArgs args)
        {
            if(exitAfterSector && !sector.Busy) { exitRequested=true;Close();return; }
            if(Now-lastSectorUi>=.25) { lastSectorUi=Now;UpdateSector(); }
            double now=Now;
            Frame next=pump.Latest;
            if(next!=null)
            {
                next=next.Copy();
                if(now-next.ReadAt>1) { next.Stale=true;next.HasTelemetry=false;next.Status="Sem atualização de telemetria"; }
                grid.Apply(next);countries.Apply(next,now,settings.Flags);steeringSetup.Apply(next,now);penalties.Apply(next);vehicleDamage.Apply(next,now,settings.Widgets[1].Visible);Current=next;
            }
            wheelAxes=wheel.Read(now);
            PollTrainingButtons(now);
            PhysicalWheelState.Apply(Current,settings.UsePhysicalWheel,settings.WheelCalibration,wheelAxes,settings.WheelMaximumRotation,settings.CarMaximumRotation);
            Display=motion.Step(Current,now,settings.SmoothMotion);Display.Delta=deltaMotion.Step(Current.Delta,settings.DeltaReference,now,settings.SmoothMotion);
            // Hide on state changes even while another app or a tray popup has focus.
            // Showing waits for popup/key capture to end inside OverlayWindow.
            ApplyVisibility();
            // Shell popups and the key dialog must retain focus. Acquisition runs independently.
            if(WindowInteractionPaused) return;
            IntPtr foreground=Win32.GetForegroundWindow();
            bool editorActive=appearanceDialog!=null && foreground==appearanceDialog.Handle;
            bool learningActive=learningWindow!=null && !learningWindow.IsDisposed && foreground==learningWindow.Handle;
            if(learningWindow!=null && !learningWindow.IsDisposed && !learningActive && learningWindow.TopMost) learningWindow.TopMost=false;
            bool panelActive=foreground==Handle || editorActive || learningActive;
            if(!panelActive && !panels.Any(p=>foreground==p.Handle) && !IsGameForeground(foreground,now))
            { Diagnostic();return; }
            pedals.DrawFrame();extras[4].DrawFrame();
            if(now-lastTable>=.1)
            {
                lastTable=now;table.FitSize();table.DrawFrame();foreach(var panel in extras) if(panel.Kind!=PanelKind.Delta)panel.DrawFrame();
            }
            if(now-lastMaintenance>=.25)
            {
                lastMaintenance=now;
                if(Interlocked.Exchange(ref displayRefresh,0)!=0)
                { foreach(var panel in panels) { panel.ResetSurface();panel.FitSize(); }AppLog.Write("Mudança de vídeo: painéis reajustados",null); }
                foreach(var panel in panels) panel.EnsureVisible();
                TopMost=panelActive && !learningActive;
                if(learningWindow!=null && !learningWindow.IsDisposed) learningWindow.TopMost=learningActive;
                if(panelActive) Win32.SetWindowPos(learningActive ? learningWindow.Handle : editorActive ? appearanceDialog.Handle : Handle,new IntPtr(-1),0,0,0,0,0x213);
                status.Text=pedals.RenderStatus.Length>0 ? pedals.RenderStatus : table.RenderStatus.Length>0 ? table.RenderStatus : Current.Status;
                status.ForeColor=Current.HasTelemetry ? Theme.Throttle : Theme.Muted;
                flagStatus.Text=countries.Status;deltaStatus.Text=Current.Delta.Context+"\n"+(Format.Finite(Current.Delta.Values[settings.DeltaReference])?"Comparando: "+DeltaMode.Names[settings.DeltaReference]:Current.Delta.Reason)+"\n"+Current.Delta.Storage;
                steeringStatus.Text=(settings.UsePhysicalWheel ? "Volante físico: "+Current.WheelStatus+" • "+(settings.WheelCalibration==null ? "clique em Calibrar volante" : settings.WheelCalibration.DeviceName) :
                    "Modo JOGO: "+Instruments.SteeringSource(Current,Options.SteeringRange)+" • não mede o volante físico")+"\n"+penalties.Status;
                Diagnostic();
            }
        }
        bool IsGameForeground(IntPtr foreground,double now)
        {
            if(foreground==IntPtr.Zero) return false;
            if(Current.GameWindow!=0 && unchecked((ulong)foreground.ToInt64())==Current.GameWindow) return true;
            uint id;Win32.GetWindowThreadProcessId(foreground,out id);
            if(id==0) return false;
            if(id!=checkedProcess || now-processChecked>1)
            {
                checkedProcess=id;processChecked=now;checkedIsGame=false;
                try { using(var process=Process.GetProcessById((int)id)) checkedIsGame=string.Equals(process.ProcessName,"Le Mans Ultimate",StringComparison.OrdinalIgnoreCase); }
                catch(ArgumentException) { } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { }
            }
            return checkedIsGame;
        }
        void ExitApp() { if(sector.Busy) { exitAfterSector=true;sector.RequestStop();saved.Text="Desconectando o treino antes de encerrar…";ShowController();return; }exitRequested=true;Close(); }
        void ShowController() { Show();WindowState=FormWindowState.Normal;TopMost=true;BringToFront();Activate(); }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(!exitRequested && e.CloseReason==CloseReason.UserClosing)
            {
                e.Cancel=true;Hide();TopMost=false;AppLog.Write("Painel fechado; overlays continuam ativos",null);
                if(!notifiedClose) { notifiedClose=true;tray.ShowBalloonTip(3500,AppInfo.Name+" continua aberto","Use o atalho para mostrar/ocultar. Para sair, escolha Encerrar o app no painel ou no ícone junto ao relógio.",ToolTipIcon.Info); }
                return;
            }
            base.OnFormClosing(e);
        }
        protected override void WndProc(ref Message m)
        {
            if((uint)m.Msg==Win32.OpenMessage) { ShowController();return; }
            if(m.Msg==0x219) wheel.Refresh(); // Device arrival/removal: never enumerate on the UI thread.
            if(m.Msg==0x312)
            {
                if(!capturing)
                {
                    int id=m.WParam.ToInt32();if(id==1) ShowController();else if(id==2) positioning.Checked=!positioning.Checked;else if(id==visibilityHotkey) ToggleVisible();
                }
                return;
            }
            base.WndProc(ref m);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if(!closing)
            {
                closing=true;lmuLauncher.Dispose();sector.Dispose();timer.Stop();saveTimer.Stop();settings.Save();
                SystemEvents.DisplaySettingsChanged-=DisplaySettingsChanged;AppLog.Write("Aplicativo encerrado",null);
                for(int i=1;i<=4;i++) Win32.UnregisterHotKey(Handle,i);
                foreach(var panel in panels) panel.Close();pump.LearningSample=null;pump.Dispose();deltaRepository.Dispose();learning.Dispose();
                if(learningWindow!=null && !learningWindow.IsDisposed) learningWindow.Close();
                countries.Dispose();steeringSetup.Dispose();vehicleDamage.Dispose();penalties.Dispose();wheel.Dispose();tray.Visible=false;tray.Dispose();trayMenu.Dispose();timer.Dispose();saveTimer.Dispose();
            }
            base.OnFormClosed(e);
        }
    }
    internal static class Program
    {
        [STAThread] static void Main()
        {
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT) return;
            // Constructor and single-instance failures happen before the
            // WinForms message loop and are not covered by ThreadException.
            try { Run(); }
            catch(Exception ex) { ShowFailure("Não foi possível abrir o aplicativo.",ex); }
        }
        static void ShowFailure(string message,Exception error)
        {
            AppLog.Write(message,error);
            string path=Path.Combine(AppSettings.DirectoryPath,"erro.txt");
            MessageBox.Show(message+"\n\n"+error.GetType().Name+": "+error.Message+
                "\n\nEnvie uma foto desta mensagem ou o arquivo:\n"+path,
                AppInfo.Name+" • "+AppInfo.Version,MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        static void Run()
        {
            AppLog.Write("Abertura solicitada",null);
            // ThreadException only covers the UI thread. Record fatal failures from other threads too.
            AppDomain.CurrentDomain.UnhandledException+=delegate(object sender,UnhandledExceptionEventArgs e)
            { AppLog.Write(e.IsTerminating ? "Encerramento por exceção não tratada fora da interface" : "Exceção fora da interface",e.ExceptionObject as Exception); };
            using(var mutex=new Mutex(false,"Local\\ArthurLMUOverlay"))
            {
                bool acquired;try { acquired=mutex.WaitOne(0); } catch(AbandonedMutexException) { acquired=true; }
                if(!acquired)
                {
                    AppLog.Write("Outra instância já está aberta; solicitando o painel",null);
                    if(!Win32.PostMessage(new IntPtr(0xffff),Win32.OpenMessage,IntPtr.Zero,IntPtr.Zero))
                        MessageBox.Show("O Arts LMU OVERLAY já está aberto. Abra o painel pelo ícone junto ao relógio do Windows.",AppInfo.Name,MessageBoxButtons.OK,MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e)
                    {
                        ShowFailure("O aplicativo encontrou um erro e será fechado.",e.Exception);Application.Exit();
                    };
                    Application.Run(new Controller());
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}
