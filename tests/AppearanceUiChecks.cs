using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Arthur.LmuOverlay;

static class AppearanceUiChecks
{
    static int checks;
    static void Assert(bool value,string reason) { if(!value) throw new Exception(reason);checks++; }
    static System.Collections.Generic.IEnumerable<Control> All(Control root)
    { foreach(Control c in root.Controls) { yield return c;foreach(var child in All(c)) yield return child; } }
    [STAThread] static void Main(string[] args)
    {
        if(!AppSettings.DirectoryPath.Contains("arts-v09-testdata")) throw new Exception("Use an isolated XDG_DATA_HOME or LOCALAPPDATA for this test.");
        string directory=args[0];Directory.CreateDirectory(directory);
        var defaults=new AppSettings();
        Assert(defaults.Seconds==10,"new cockpit starts with ten seconds of pedal history");
        Assert(defaults.HotkeyKey==(int)Keys.F8 && defaults.HotkeyModifiers==0,"new installs use F8 without modifiers");
        var oldDefault=new AppSettings { SchemaVersion=8,PositionsSet=true,HotkeyKey=(int)Keys.H,HotkeyModifiers=3,
            PedalsX=314,PedalsY=159,PedalsWidth=512,PedalsHeight=190,TableX=77,TableY=90,TableWidth=850,TableHeight=338,
            PedalOpacity=67,TableOpacity=89,WheelMaximumRotation=900,CarMaximumRotation=500,
            Appearance=PanelAppearance.NormalizeAll(null),Widgets=new WidgetPlacement[4] };
        for(int i=0;i<4;i++) oldDefault.Widgets[i]=new WidgetPlacement { X=100+i*80,Y=210+i*50,Width=330+i*20,Height=260+i*10,Visible=i%2==0 };
        oldDefault.Appearance[0].FontFamily="Tahoma";oldDefault.Appearance[0].FontPercent=150;
        Assert(oldDefault.Save()=="","0.9.1 preferences save as upgrade fixture");
        string upgradeWarning;var upgraded=AppSettings.Load(out upgradeWarning);
        Assert(upgraded.Seconds==10,"legacy history preference migrates to ten seconds");
        Assert(upgradeWarning=="" && upgraded.SchemaVersion==11 && upgraded.HotkeyKey==(int)Keys.F8 && upgraded.HotkeyModifiers==0,"old standard shortcut migrates once to F8");
        Assert(upgraded.PositionsSet && upgraded.PedalsX==314 && upgraded.PedalsY==159 && upgraded.PedalsWidth==512 && upgraded.PedalsHeight==190 &&
            upgraded.TableX==77 && upgraded.TableY==90 && upgraded.TableWidth==850 && upgraded.TableHeight==338,"0.9.2 keeps exact saved main-card positions and sizes");
        Assert(upgraded.Widgets.Select((w,i)=>w.X==100+i*80 && w.Y==210+i*50 && w.Width==330+i*20 && w.Height==260+i*10 && w.Visible==(i%2==0)).All(x=>x),"0.9.2 keeps exact optional-card positions, sizes and activation");
        Assert(upgraded.Appearance[0].FontFamily=="Tahoma" && upgraded.Appearance[0].FontPercent==150 && upgraded.PedalOpacity==67 && upgraded.TableOpacity==89 &&
            upgraded.WheelMaximumRotation==900 && upgraded.CarMaximumRotation==500,"automatic visibility upgrade preserves fonts, opacity and wheel ranges");
        upgraded.HotkeyKey=(int)Keys.H;upgraded.HotkeyModifiers=3;upgraded.Save();upgraded=AppSettings.Load(out upgradeWarning);
        Assert(upgraded.HotkeyKey==(int)Keys.H && upgraded.HotkeyModifiers==3,"a shortcut chosen after upgrade is not reset on next launch");
        var settings=new AppSettings { SchemaVersion=7,PedalsX=314,PedalsY=159,PedalsWidth=512,PedalsHeight=190,HotkeyKey=(int)Keys.J,WheelMaximumRotation=900,PedalOpacity=67 };
        Assert(settings.Save()=="","legacy settings can be saved");string warning;
        settings=AppSettings.Load(out warning);
        Assert(warning=="" && settings.Appearance.Length==7,"0.8 configuration migrates to all seven independent appearances");
        Assert(settings.PedalsX==314 && settings.PedalsY==159 && settings.PedalsWidth==512 && settings.PedalsHeight==190 && settings.PedalOpacity==67 && settings.HotkeyKey==(int)Keys.J && settings.WheelMaximumRotation==900,"migration preserves geometry, opacity, hotkey and calibration settings");
        settings.Appearance[0].FontPercent=150;settings.Appearance[0].FontFamily="Tahoma";
        settings.PedalsWidth=1140;settings.PedalsHeight=420;
        Assert(settings.Save()=="","font and dimensions save through the application settings writer");
        settings=AppSettings.Load(out warning);
        Assert(settings.Appearance[0].FontPercent==150 && settings.Appearance[0].FontFamily=="Tahoma" && settings.PedalsWidth==1140 && settings.PedalsHeight==420,"new appearance and geometry reopen exactly");
        Assert(settings.Appearance.Skip(1).All(x=>x.FontPercent==100),"reopening preserves independent sibling settings");
        settings.Appearance[0].Reset();settings.Save();settings=AppSettings.Load(out warning);
        Assert(settings.Appearance[0].FontPercent==100 && settings.Appearance[0].FontFamily=="Segoe UI" && settings.PedalOpacity==67 && settings.PedalsX==314,"reset typography survives reopening without resetting opacity or position");
        Assert(string.IsNullOrEmpty(settings.LmuExecutablePath),"old preferences have no launcher path and never auto-launch");
        Assert(settings.DeltaThickness==16,"existing preferences receive readable delta thickness default");
        settings.DeltaThickness=26;
        settings.TrainingSaveButton=new TrainingWheelBinding{DeviceId="wheel-1",DeviceName="Volante USB",ObjectId=257,Number=2};
        settings.LmuExecutablePath=@"D:\SteamLibrary\pasta com espaço\Le Mans Ultimate.exe";
        Assert(settings.Save()=="","launcher location saves with existing preferences");
        var reopened=AppSettings.Load(out warning);
        Assert(reopened.TrainingSaveButton!=null && reopened.TrainingSaveButton.DeviceId=="wheel-1" && reopened.TrainingSaveButton.ObjectId==257 && reopened.TrainingRestoreButton==null && reopened.DeltaThickness==26 && reopened.LmuExecutablePath==settings.LmuExecutablePath && reopened.PedalsX==314 && reopened.PedalOpacity==67 && reopened.HotkeyKey==(int)Keys.J,"launcher path round-trips without altering cards or shortcuts");
        var expanded=WidgetPlacement.Expand(upgraded.Widgets,new Rectangle(0,0,1920,1080));
        Assert(expanded.Length==5 && upgraded.Widgets.Select((w,i)=>object.ReferenceEquals(w,expanded[i])).All(x=>x),"delta addition preserves all four existing placements");
        Assert(expanded[4].X==730 && expanded[4].Y==360 && expanded[4].Visible,"delta has independent centered initial placement");
        settings.Widgets=expanded;settings.DeltaReference=2;settings.DeltaOpacity=0;settings.DeltaRange=5;settings.Appearance[6].FontPercent=140;settings.Save();settings=AppSettings.Load(out warning);
        Assert(settings.DeltaReference==2 && settings.DeltaOpacity==0 && settings.DeltaRange==5 && settings.Appearance[6].FontPercent==140 && settings.Widgets[4].X==730,"delta reference, opacity, font, range and position reopen exactly");
        settings.DeltaReference=99;settings.DeltaOpacity=-20;settings.DeltaRange=999;settings.Save();settings=AppSettings.Load(out warning);
        Assert(settings.DeltaReference==2 && settings.DeltaOpacity==0 && settings.DeltaRange==10,"delta invalid settings are bounded");
        if(args.Length>1 && args[1]=="--config-only") { Console.WriteLine("PASS: "+checks+" real application migration and save/reopen checks.");return; }
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        Size actual=new Size(760,280);int writes=0;
        using(var dialog=new PanelAppearanceDialog("Pedais, motor e volante",settings.Appearance[0],()=>actual,()=>new Size(760,280),size=>
        {
            actual=size.IsEmpty ? new Size(760,280) : PanelSizing.Fit(size.Width,size.Height,266,98,1920,1080);
            settings.PedalsWidth=actual.Width;settings.PedalsHeight=actual.Height;writes++;
        },()=>writes++))
        {
            dialog.Show();Application.DoEvents();
            var fields=All(dialog).OfType<NumericUpDown>().ToArray();var family=All(dialog).OfType<ComboBox>().Single();
            var font=fields.Single(c=>c.AccessibleName.StartsWith("Tamanho"));
            var width=fields.Single(c=>c.AccessibleName.StartsWith("Largura"));
            var height=fields.Single(c=>c.AccessibleName.StartsWith("Altura"));
            width.Value=520;height.Value=180;
            Assert(actual==new Size(520,180),"real native number controls allow a smaller independent size");
            font.Value=150;family.SelectedItem="Tahoma";Application.DoEvents();
            Assert(actual==new Size(1140,420) && settings.Appearance[0].FontPercent==150 && settings.Appearance[0].FontFamily=="Tahoma","native font change expands the selected card and changes family");
            Assert(settings.Appearance.Skip(1).All(x=>x.FontPercent==100),"UI edits keep other panels unchanged");
            height.Value=500;Assert(actual.Width==1140 && actual.Height==500,"height edit does not change width");
            Assert(settings.Save()=="","UI-edited settings save successfully");
            var loaded=AppSettings.Load(out warning);
            Assert(loaded.PedalsWidth==1140 && loaded.PedalsHeight==500 && loaded.Appearance[0].FontPercent==150 && loaded.Appearance[0].FontFamily=="Tahoma","full application configuration reopens with exact geometry and font");
            dialog.SetSaveState("Configurações salvas neste computador.");Application.DoEvents();
            using(var bitmap=new Bitmap(dialog.Width,dialog.Height)) { dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(Path.Combine(directory,"ajustes-por-painel.png"),ImageFormat.Png); }
            family.Focus();family.DroppedDown=true;Application.DoEvents();
            using(var bitmap=new Bitmap(dialog.Width,dialog.Height)) using(var g=Graphics.FromImage(bitmap))
            { g.CopyFromScreen(dialog.Location,Point.Empty,dialog.Size);bitmap.Save(Path.Combine(directory,"fontes-abertas.png"),ImageFormat.Png); }
            family.DroppedDown=false;
            All(dialog).OfType<Button>().Single(b=>b.Text=="Ajustar painel às letras").PerformClick();
            Assert(actual==new Size(1140,420),"fit to text restores the suggested geometry");
            All(dialog).OfType<Button>().Single(b=>b.Text=="Restaurar padrão").PerformClick();
            Assert(actual==new Size(760,280) && settings.Appearance[0].FontPercent==100 && settings.Appearance[0].FontFamily=="Segoe UI","per-card reset restores typography and size");
            Assert(settings.PedalsX==314 && settings.HotkeyKey==(int)Keys.J && settings.PedalOpacity==67,"appearance reset preserves unrelated preferences");
            All(dialog).OfType<Button>().Single(b=>b.Text=="Concluir").PerformClick();Assert(!dialog.Visible,"complete closes the editor");
        }
        Console.WriteLine("PASS: "+checks+" application migration, persistence and native editor checks.");
    }
}
