using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Arthur.LmuOverlay
{
    // A shared surface, not a new input implementation. Controls retain their
    // native focus, keyboard, accessibility and validation behaviour.
    internal sealed class SettingsCard : Panel
    {
        public SettingsCard()
        {
            BackColor=Theme.Background;Padding=new Padding(18,14,18,14);Margin=new Padding(0);
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float d=24;var r=new RectangleF(.5f,.5f,Width-1,Height-1);
            if(r.Width<d || r.Height<d) return;
            using(var path=new GraphicsPath())
            {
                path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);
                path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();
                using(var brush=new SolidBrush(Theme.Surface)) e.Graphics.FillPath(brush,path);
                using(var pen=new Pen(Theme.Grid)) e.Graphics.DrawPath(pen,path);
            }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e);Invalidate(); }
    }
    internal sealed class SettingsPage : FlowLayoutPanel
    {
        public SettingsPage()
        {
            Dock=DockStyle.Fill;AutoScroll=true;FlowDirection=FlowDirection.TopDown;WrapContents=false;
            Padding=new Padding(24,12,24,18);BackColor=Theme.Background;Margin=new Padding(0);
            SizeChanged+=delegate { FitChildren(); };ControlAdded+=delegate { FitChildren(); };
        }
        void FitChildren()
        {
            int width=Math.Max(220,ClientSize.Width-Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-2);
            foreach(Control c in Controls) c.Width=width;
        }
    }
    public sealed partial class Controller
    {
        void PageHeading(SettingsPage page,string title,string description)
        {
            var heading=Label(title,39);heading.Font=new Font(Theme.DisplayFont,23,FontStyle.Bold);heading.Margin=new Padding(0,0,0,3);page.Controls.Add(heading);
            var note=Label(description,36);note.ForeColor=Theme.Muted;note.Margin=new Padding(0,0,0,12);page.Controls.Add(note);
        }
        Control PanelTile(int index,string title,string description)
        {
            var tile=new SettingsCard { Height=index<2 ? 178 : 142 };
            var body=new TableLayoutPanel { Dock=DockStyle.Fill,BackColor=Theme.Surface,ColumnCount=2,RowCount=index<2 ? 4 : 3,Margin=new Padding(0) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute,30));body.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            if(index<2) body.RowStyles.Add(new RowStyle(SizeType.Absolute,34));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            tile.Controls.Add(body);
            var label=Label(title,30);label.Font=new Font(Theme.BodyFont,12,FontStyle.Bold);label.Dock=DockStyle.Fill;label.Margin=new Padding(0);body.Controls.Add(label,0,0);
            bool active=index==0 ? settings.ShowPedals : index==1 ? settings.ShowTable : settings.Widgets[index-2].Visible;
            var toggle=Check("Ativado",active,value=>
            { if(index==0) settings.ShowPedals=value;else if(index==1) settings.ShowTable=value;else settings.Widgets[index-2].Visible=value;Changed(); });
            toggle.AccessibleName="Mostrar na pista: "+title;toggle.Anchor=AnchorStyles.Right|AnchorStyles.Top;toggle.Margin=new Padding(4,2,0,0);body.Controls.Add(toggle,1,0);
            var note=Label(description,38);note.ForeColor=Theme.Muted;note.Font=new Font(Theme.BodyFont,9.5f);note.Dock=DockStyle.Fill;note.Margin=new Padding(0);body.Controls.Add(note,0,1);body.SetColumnSpan(note,2);
            if(index<2)
            {
                var opacity=Number(index==0 ? settings.PedalOpacity : settings.TableOpacity,20,100,5,value=>OpacityChanged(index==0,value),"Opacidade do fundo de "+title+" em porcentagem");
                if(index==0) pedalAlpha=opacity;else tableAlpha=opacity;
                var opacityLabel=Label("Opacidade do fundo (%)",30);opacityLabel.ForeColor=Theme.Muted;opacityLabel.Font=new Font(Theme.BodyFont,9.5f);opacityLabel.Dock=DockStyle.Fill;opacityLabel.Margin=new Padding(0);
                body.Controls.Add(opacityLabel,0,2);opacity.Anchor=AnchorStyles.Right|AnchorStyles.Top;opacity.Margin=new Padding(0,2,0,0);body.Controls.Add(opacity,1,2);
            }
            var edit=Button("Fonte e tamanho",()=>EditAppearance(index));edit.AccessibleName="Fonte e tamanho de "+title;edit.Margin=new Padding(0,5,0,0);edit.MinimumSize=new Size(146,32);edit.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
            body.Controls.Add(edit,0,index<2 ? 3 : 2);body.SetColumnSpan(edit,2);return tile;
        }
        void TilePair(SettingsPage page,Control left,Control right,int height)
        {
            var row=new TableLayoutPanel { Height=height,ColumnCount=3,RowCount=1,Margin=new Padding(0,0,0,14) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,14));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            left.Dock=right.Dock=DockStyle.Fill;row.Controls.Add(left,0,0);row.Controls.Add(right,2,0);page.Controls.Add(row);
        }
        void BuildControls()
        {
            var shell=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Margin=new Padding(0) };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,178));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute,108));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,58));Controls.Add(shell);
            var header=new BrandHeader { Dock=DockStyle.Fill,Margin=new Padding(24,0,24,0) };
            var exit=Button("Encerrar o app",ExitApp);exit.AutoSize=false;exit.Size=new Size(144,35);exit.AccessibleDescription="Encerra o aplicativo e todos os painéis.";
            header.Controls.Add(exit);header.Resize+=delegate { exit.Location=new Point(header.ClientSize.Width-exit.Width,30); };shell.Controls.Add(header,0,0);shell.SetColumnSpan(header,2);
            var nav=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(16,17,10,10),Margin=new Padding(0) };shell.Controls.Add(nav,0,1);
            var navLabel=Label("SEU ARTS",24);navLabel.Font=new Font(Theme.BodyFont,8.5f,FontStyle.Bold);navLabel.ForeColor=Theme.Muted;navLabel.Width=145;navLabel.Margin=new Padding(10,0,0,12);nav.Controls.Add(navLabel);
            var host=new Panel { Dock=DockStyle.Fill,Margin=new Padding(0) };shell.Controls.Add(host,1,1);
            var pages=new[]{new SettingsPage(),new SettingsPage(),new SettingsPage(),new SettingsPage()};
            string[] titles={"Painéis","Pilotagem","Atalhos","Treino por trecho"};var navigation=new Button[4];
            for(int i=0;i<pages.Length;i++)
            {
                int selected=i;host.Controls.Add(pages[i]);pages[i].Visible=i==0;
                var button=Button(titles[i],delegate
                {
                    for(int j=0;j<pages.Length;j++)
                    {
                        pages[j].Visible=j==selected;navigation[j].BackColor=j==selected ? Theme.Player : Theme.Background;
                        navigation[j].FlatAppearance.BorderSize=j==selected ? 1 : 0;navigation[j].AccessibleDescription=j==selected ? "Seção selecionada" : "Abrir seção";
                    }
                });button.AutoSize=false;button.Size=new Size(146,44);button.TextAlign=ContentAlignment.MiddleLeft;button.Padding=new Padding(12,2,0,2);
                button.BackColor=i==0 ? Theme.Player : Theme.Background;button.FlatAppearance.BorderSize=i==0 ? 1 : 0;button.Margin=new Padding(0,0,0,8);navigation[i]=button;nav.Controls.Add(button);
            }
            var learn=Button("Aprendizado",OpenLearning);learn.AutoSize=false;learn.Size=new Size(146,44);learn.TextAlign=ContentAlignment.MiddleLeft;learn.Padding=new Padding(12,2,0,2);learn.Margin=new Padding(0,24,0,8);nav.Controls.Add(learn);
            var learnHelp=Label("Compare suas voltas\ne encontre tempo\nem cada trecho.",67);learnHelp.Width=144;learnHelp.ForeColor=Theme.Muted;learnHelp.Font=new Font(Theme.BodyFont,9);learnHelp.Margin=new Padding(10,0,0,0);nav.Controls.Add(learnHelp);
            BuildPanelPage(pages[0]);BuildDrivingPage(pages[1]);BuildShortcutPage(pages[2]);BuildSectorPage(pages[3]);
            var footer=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(24,0,24,0),BackColor=Theme.Surface,Margin=new Padding(0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,54));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,46));shell.Controls.Add(footer,0,2);shell.SetColumnSpan(footer,2);
            status.Dock=saved.Dock=DockStyle.Fill;status.Text="Aguardando o LMU";status.ForeColor=Theme.Muted;saved.ForeColor=Theme.Muted;status.TextAlign=ContentAlignment.MiddleLeft;saved.TextAlign=ContentAlignment.MiddleRight;
            status.Font=saved.Font=new Font(Theme.BodyFont,9);footer.Controls.Add(status,0,0);footer.Controls.Add(saved,1,0);
        }
        void BuildPanelPage(SettingsPage page)
        {
            PageHeading(page,"Seu cockpit","Escolha os painéis e deixe cada informação no lugar certo.");
            var actions=new FlowLayoutPanel { Height=48,WrapContents=false,Margin=new Padding(0,0,0,5) };
            positioning.Text="Posicionar painéis";positioning.Appearance=Appearance.Button;positioning.AutoSize=true;positioning.MinimumSize=new Size(198,38);positioning.FlatStyle=FlatStyle.Flat;positioning.BackColor=Theme.Player;positioning.ForeColor=Theme.Text;positioning.Cursor=Cursors.Hand;positioning.Padding=new Padding(9,3,9,3);positioning.Margin=new Padding(0,0,12,0);positioning.FlatAppearance.BorderColor=Theme.Rpm;positioning.FlatAppearance.CheckedBackColor=Theme.Player;
            positioning.CheckedChanged+=delegate
            { foreach(var panel in panels) panel.Locked=!positioning.Checked;positioning.Text=positioning.Checked ? "Concluir posicionamento" : "Posicionar painéis";ApplyVisibility(); };
            actions.Controls.Add(positioning);actions.Controls.Add(Button("Restaurar posições",()=>ResetPositions(true)));page.Controls.Add(actions);
            var hint=Label("Arraste os cards e suas bordas. Esc conclui. Eles aparecem na pista e se ocultam no menu.",40);hint.ForeColor=Theme.Muted;hint.Font=new Font(Theme.BodyFont,9);hint.Margin=new Padding(0,0,0,12);page.Controls.Add(hint);
            BuildDeltaSettings(page);
            TilePair(page,PanelTile(0,"Pedais e motor","Gráfico de 10 s • RPM ao redor do volante"),PanelTile(1,"Pilotos da sua classe","Você, três à frente, três atrás e o líder"),178);
            var link=Check("Ajustar as duas opacidades juntas",settings.LinkOpacity,value=>{settings.LinkOpacity=value;if(value) OpacityChanged(true,settings.PedalOpacity);else Changed();});link.Margin=new Padding(1,0,0,15);page.Controls.Add(link);
            TilePair(page,PanelTile(2,"Lap timer","Volta atual, melhor tempo e previsão"),PanelTile(3,"Pneus e danos","Temperatura, desgaste e condição do carro"),142);
            TilePair(page,PanelTile(4,"Combustível","Tanque, consumo por volta e autonomia"),PanelTile(5,"Mapa da pista","Posição dos carros da sua classe"),142);
            var help=Label("Painéis adicionais usam a opacidade dos pilotos. Fonte e tamanho ajusta cada card separadamente.",38);help.ForeColor=Theme.Muted;help.Font=new Font(Theme.BodyFont,9);page.Controls.Add(help);
        }
        void BuildDrivingPage(SettingsPage page)
        {
            PageHeading(page,"Volante e motor","Ajuste a direção e o ponto de troca para o seu equipamento.");
            var wheelBox=new SettingsCard { Height=182,Margin=new Padding(0,0,0,16) };page.Controls.Add(wheelBox);
            var wheelFlow=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Theme.Surface };
            wheelBox.Controls.Add(wheelFlow);physicalWheelCheck=Check("Usar meu volante físico",settings.UsePhysicalWheel,value=>{settings.UsePhysicalWheel=value;Changed();});wheelFlow.Controls.Add(physicalWheelCheck);
            wheelFlow.Controls.Add(Button("Calibrar volante",CalibrateWheel));steeringStatus.Height=60;steeringStatus.ForeColor=Theme.Muted;steeringStatus.Font=new Font(Theme.BodyFont,9);wheelFlow.Controls.Add(steeringStatus);
            wheelFlow.SizeChanged+=delegate { steeringStatus.Width=Math.Max(180,wheelFlow.ClientSize.Width-8); };
            var tuning=new SettingsCard { Height=223,Margin=new Padding(0,0,0,16) };page.Controls.Add(tuning);
            var rows=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=0,BackColor=Theme.Surface };rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,106));tuning.Controls.Add(rows);
            Row(rows,"Aviso de troca (RPM) • 0 = automático",Number(settings.ShiftRpm,0,20000,100,v=>{settings.ShiftRpm=v;Changed();},"RPM para trocar de marcha; zero usa 96% do limite"));
            Row(rows,"Meu volante: rotação total (°) • 0 = calibração",Number(settings.WheelMaximumRotation,0,2880,10,v=>{settings.WheelMaximumRotation=v;Changed();},"Rotação total física; zero usa a calibração"));
            Row(rows,"Carro: rotação total (°) • 0 = LMU",Number(settings.CarMaximumRotation,0,2880,10,v=>{settings.CarMaximumRotation=v;Changed();},"Rotação total do carro; zero usa o jogo"));
            Row(rows,"Modo JOGO: faixa manual (°) • 0 = automática",Number(settings.SteeringRange,0,2880,10,v=>{settings.SteeringRange=v;Changed();},"Faixa manual do modo jogo"));
            var note=Label("A rotação física é independente da do carro. Se trocar o volante ou alterar a amplitude no driver, calibre novamente.",59);note.ForeColor=Theme.Muted;note.Font=new Font(Theme.BodyFont,9);int r=rows.RowCount++;rows.RowStyles.Add(new RowStyle(SizeType.Absolute,59));note.Dock=DockStyle.Fill;rows.Controls.Add(note,0,r);rows.SetColumnSpan(note,2);
            page.Controls.Add(Check("Suavizar as barras e o volante do modo JOGO",settings.SmoothMotion,v=>{settings.SmoothMotion=v;Changed();}));
            page.Controls.Add(Check("Pulsar o arco de RPM na hora de trocar",settings.PulseShift,v=>{settings.PulseShift=v;Changed();}));
            var tip=Label("A curva do gráfico preserva as entradas capturadas. O volante físico acompanha o eixo calibrado.",45);tip.ForeColor=Theme.Muted;tip.Font=new Font(Theme.BodyFont,9);page.Controls.Add(tip);
        }
        void BuildShortcutPage(SettingsPage page)
        {
            PageHeading(page,"Controle rápido","Mostre, oculte e posicione o cockpit sem sair da corrida.");
            var box=new SettingsCard { Height=156,Margin=new Padding(0,0,0,16) };page.Controls.Add(box);
            var table=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,BackColor=Theme.Surface };table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,162));box.Controls.Add(table);
            var title=Label("Mostrar / ocultar na pista",35);title.Font=new Font(Theme.BodyFont,12,FontStyle.Bold);title.Dock=DockStyle.Fill;table.Controls.Add(title,0,0);
            hotkey.Dock=DockStyle.Fill;hotkey.ForeColor=Theme.Rpm;hotkey.Font=new Font(Theme.DataFont,16,FontStyle.Bold);hotkey.TextAlign=ContentAlignment.MiddleRight;table.Controls.Add(hotkey,1,0);
            var description=Label("Funciona durante a pilotagem. No menu ou na garagem, os cards ficam ocultos automaticamente.",44);description.ForeColor=Theme.Muted;description.Dock=DockStyle.Fill;table.Controls.Add(description,0,1);table.SetColumnSpan(description,2);
            var bind=Button("Definir atalho",BindKey);bind.Margin=new Padding(0,6,0,0);table.Controls.Add(bind,0,2);
            var fixedKeys=new SettingsCard { Height=113,Margin=new Padding(0,0,0,20) };page.Controls.Add(fixedKeys);
            var keys=Label("Ctrl + Alt + O     Abrir configuração\nCtrl + Alt + L     Posicionar / travar\nEsc                       Concluir posicionamento",82);keys.Dock=DockStyle.Fill;keys.BackColor=Theme.Surface;fixedKeys.Controls.Add(keys);
            page.Controls.Add(Check("Consultar bandeiras do lobby",settings.Flags,v=>{settings.Flags=v;Changed();}));flagStatus.Height=32;flagStatus.ForeColor=Theme.Muted;flagStatus.Font=new Font(Theme.BodyFont,9);page.Controls.Add(flagStatus);
            var explain=Label("Use o LMU em janela sem bordas. O X fecha a configuração e mantém o Arts ativo. Para sair completamente, use Encerrar o app.",74);explain.ForeColor=Theme.Muted;explain.Margin=new Padding(0,12,0,0);page.Controls.Add(explain);
        }
    }
}
