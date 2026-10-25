using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Arthur.LmuOverlay
{
    sealed class WheelCalibrationDialog : Form
    {
        readonly Func<List<WheelAxis>> read;
        readonly double gameRange;
        readonly ComboBox device=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=540,AccessibleName="Seu volante" };
        readonly Label prompt=new Label { Width=540,Height=110 },live=new Label { Width=540,Height=50 };
        readonly Button capture=new Button { Width=250,Height=40,Text="Capturar centro" };
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer { Interval=100 };
        List<WheelAxis> center;
        WheelAxis at90;
        int step;
        string error="";
        public WheelCalibration Calibration;
        public WheelCalibrationDialog(Func<List<WheelAxis>> provider,double range)
        {
            read=provider;gameRange=range;
            Text="Calibrar meu volante • "+AppInfo.Name;BackColor=Theme.Background;ForeColor=Theme.Text;
            Font=new Font(Theme.BodyFont,11);ClientSize=new Size(610,500);FormBorderStyle=FormBorderStyle.FixedDialog;
            StartPosition=FormStartPosition.CenterParent;MaximizeBox=MinimizeBox=false;ShowInTaskbar=false;TopMost=true;AutoScaleMode=AutoScaleMode.Dpi;
            var root=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown,WrapContents=false };Controls.Add(root);
            Shown+=delegate { var area=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,area.Width-24),Math.Min(Height,area.Height-24)); };
            root.Controls.Add(new Label { Text="Selecione seu volante. Não toque nos pedais durante as capturas.",Width=540,Height=45 });
            var choices=read().GroupBy(x=>x.DeviceId).Select(x=>new Choice { Id=x.Key,Name=x.First().DeviceName }).ToList();
            foreach(var c in choices) device.Items.Add(c);if(choices.Count>0) device.SelectedIndex=0;root.Controls.Add(device);
            prompt.Margin=new Padding(0,20,0,0);root.Controls.Add(prompt);root.Controls.Add(live);
            capture.Click+=delegate { CaptureStep(); };root.Controls.Add(capture);
            var cancel=new Button { Text="Cancelar",Width=130,Height=35,DialogResult=DialogResult.Cancel };root.Controls.Add(cancel);CancelButton=cancel;
            UpdatePrompt();timer.Tick+=delegate { UpdateLive(); };timer.Start();FormClosed+=delegate { timer.Stop();timer.Dispose(); };
        }
        sealed class Choice { public string Id,Name;public override string ToString() { return Name; } }
        List<WheelAxis> Selected()
        { var selected=device.SelectedItem as Choice;return selected==null ? new List<WheelAxis>() : read().Where(x=>x.DeviceId==selected.Id).ToList(); }
        void UpdatePrompt()
        {
            prompt.Text=step==0 ? "1. Deixe o volante reto, no centro.\nSegure parado e clique em Capturar centro." : step==1 ?
                "2. Gire exatamente 90° para a DIREITA.\nÉ um quarto de volta a partir do centro.\nSegure e clique em Capturar 90°." :
                "3. Continue até 180° para a DIREITA.\nÉ meia volta a partir do centro.\nVamos conferir se a leitura continua proporcional além de 90°.";
            capture.Text=step==0 ? "Capturar centro" : step==1 ? "Capturar 90°" : "Validar 180° e salvar";
        }
        void UpdateLive()
        {
            if(step==0)
            {
                foreach(var axesByDevice in read().GroupBy(x=>x.DeviceId))
                    if(!device.Items.Cast<Choice>().Any(c=>c.Id==axesByDevice.Key))
                        device.Items.Add(new Choice { Id=axesByDevice.Key,Name=axesByDevice.First().DeviceName });
                if(device.SelectedIndex<0 && device.Items.Count>0) device.SelectedIndex=0;
            }
            var axes=Selected();capture.Enabled=axes.Count>0;
            if(axes.Count==0) { live.Text="Volante desconectado ou indisponível. Reconecte e reabra a calibração.";return; }
            if(error.Length>0) { live.Text=error;return; }
            if(step==2 && at90!=null)
            {
                var c=center.First(x=>x.Id==at90.Id);var current=axes.FirstOrDefault(x=>x.Id==at90.Id);
                live.Text=current==null ? "Eixo não disponível." : "Leitura aproximada: "+((current.Value-c.Value)/(at90.Value-c.Value)*90).ToString("0.0")+"° • confira 180° físicos.";
            }
            else live.Text="Leitura disponível. Faça a captura com o volante parado.";
        }
        void CaptureStep()
        {
            var axes=Selected();if(axes.Count==0) return;error="";
            if(step==0) { center=axes;device.Enabled=false;step=1;UpdatePrompt();return; }
            if(step==1)
            {
                var moved=axes.Select(a=>new { Axis=a,Start=center.FirstOrDefault(c=>c.Id==a.Id) })
                    .Where(x=>x.Start!=null).OrderByDescending(x=>Math.Abs(x.Axis.Value-x.Start.Value)/Math.Max(1,x.Start.Maximum-x.Start.Minimum)).FirstOrDefault();
                if(moved==null || Math.Abs(moved.Axis.Value-moved.Start.Value)<Math.Max(2,Math.Abs(moved.Start.Maximum-moved.Start.Minimum)*.01))
                { error=live.Text="Não houve giro suficiente. Confirme o dispositivo e gire 90° à direita.";return; }
                at90=moved.Axis;step=2;UpdatePrompt();return;
            }
            try
            {
                Calibration=WheelCalibration.Create(center.FirstOrDefault(x=>x.Id==at90.Id),at90,axes.FirstOrDefault(x=>x.Id==at90.Id),gameRange);
                DialogResult=DialogResult.OK;Close();
            }
            catch(InvalidOperationException ex) { step=0;device.Enabled=true;UpdatePrompt();error=live.Text=ex.Message; }
        }
    }
}
