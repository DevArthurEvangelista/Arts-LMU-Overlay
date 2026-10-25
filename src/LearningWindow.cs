using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Arthur.LmuOverlay
{
    internal sealed class LearningCanvas : Control
    {
        public LearningComparison Comparison;
        public int Selected=-1;
        public bool IsMap;
        public Action<int> SelectionChanged;
        public LearningCanvas(bool map)
        {
            IsMap=map;Dock=DockStyle.Fill;Margin=new Padding(0,0,0,8);TabStop=map;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
            AccessibleName=map ? "Mapa dos trechos. Use as setas para selecionar." : "Gráficos de delta e velocidade por distância";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if(IsMap) LearningPainter.Map(e.Graphics,ClientRectangle,Comparison,Selected);
            else LearningPainter.Charts(e.Graphics,ClientRectangle,Comparison,Selected);
            if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-3,-3),Theme.Text,Theme.Surface);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        { base.OnMouseDown(e);if(IsMap) { Focus();int at=LearningPainter.HitMap(Comparison,ClientRectangle,e.Location);if(at>=0 && SelectionChanged!=null) SelectionChanged(at); } }
        protected override bool IsInputKey(Keys keyData)
        { return keyData==Keys.Left || keyData==Keys.Right || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if(IsMap && Comparison!=null && (e.KeyCode==Keys.Left || e.KeyCode==Keys.Right))
            { if(SelectionChanged!=null) SelectionChanged(Math.Max(0,Math.Min(Comparison.Segments.Length-1,Selected+(e.KeyCode==Keys.Left ? -1 : 1))));e.Handled=true; }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e);Invalidate(); }
    }
    public sealed class LearningWindow : Form
    {
        sealed class Pilot
        {
            public string Key,Name,Class;public bool Own;
            public override string ToString() { return Name+(Own ? " (você)" : "")+" • "+Class; }
        }
        sealed class Catalog
        { public LearningSession[] Sessions;public string Warning; }
        sealed class LapList
        { public LearningLapInfo[] Laps;public string Warning; }
        readonly LearningStore store;
        readonly LearningPump recorder;
        readonly ComboBox sessions=Select("Sessão gravada"),pilotA=Select("Piloto da volta A"),lapA=Select("Volta A"),pilotB=Select("Piloto da referência B"),lapB=Select("Volta de referência B"),segmentSize=Select("Tamanho estimado dos microsetores em metros");
        readonly Label status=SettingsUi.Label("",28),notice=SettingsUi.Label("",46),historyNotice=SettingsUi.Label("",42);
        readonly DataGridView history=Grid("Histórico das voltas"),sectors=Grid("Comparação dos três setores oficiais"),segments=Grid("Microsetores estimados: A menos B");
        readonly LearningCanvas map=new LearningCanvas(true),charts=new LearningCanvas(false);
        readonly TabControl tabs=new TabControl { Dock=DockStyle.Fill,Margin=new Padding(0) };
        readonly System.Windows.Forms.Timer refreshTimer=new System.Windows.Forms.Timer { Interval=2000 },analysisTimer=new System.Windows.Forms.Timer { Interval=180 };
        LearningLapInfo[] laps=new LearningLapInfo[0];
        bool syncing,loading,analysisBusy;
        int revision=-1,loadRequest,analysisRequest;
        string diskWarning="";
        string displayedKey="";
        string loadedSession="";
        Button refreshButton;
        public Action<bool> PopupStateChanged;
        public LearningWindow(LearningStore storage,LearningPump recording)
        {
            store=storage;recorder=recording;
            Text="Aprendizado • "+AppInfo.Name;AccessibleName=Text;Font=new Font(Theme.BodyFont,10.5f);BackColor=Theme.Background;ForeColor=Theme.Text;
            AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterScreen;MinimumSize=new Size(1060,700);
            var area=Screen.PrimaryScreen.WorkingArea;ClientSize=new Size(Math.Min(1280,Math.Max(1040,area.Width-60)),Math.Min(860,Math.Max(660,area.Height-100)));
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(18,10,18,14) };
            foreach(float size in new[]{40f,30f,61f,64f}) root.RowStyles.Add(new RowStyle(SizeType.Absolute,size));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
            var title=SettingsUi.Label("Aprendizado",36);title.Font=new Font(Theme.DisplayFont,23,FontStyle.Bold);title.Dock=DockStyle.Fill;root.Controls.Add(title,0,0);
            status.Dock=DockStyle.Fill;status.ForeColor=Theme.Muted;root.Controls.Add(status,0,1);
            var sessionRow=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0) };
            sessionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));sessionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,157));
            sessionRow.Controls.Add(Field("SESSÃO SALVA NESTE COMPUTADOR",sessions),0,0);
            var refresh=refreshButton=SettingsUi.Button("Atualizar",RefreshCatalog);refresh.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;refresh.Margin=new Padding(8,0,0,6);sessionRow.Controls.Add(refresh,1,0);root.Controls.Add(sessionRow,0,2);
            var aRow=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0) };
            aRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));aRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
            aRow.Controls.Add(Field("PILOTO A • VOLTA PARA ANALISAR",pilotA),0,0);aRow.Controls.Add(Field("VOLTA A",lapA),1,0);root.Controls.Add(aRow,0,3);root.Controls.Add(tabs,0,4);
            BuildHistory();BuildComparison();
            foreach(var combo in new[]{sessions,pilotA,lapA,pilotB,lapB,segmentSize})
            {
                combo.DropDown+=delegate { if(PopupStateChanged!=null) PopupStateChanged(true); };
                combo.DropDownClosed+=delegate { if(PopupStateChanged!=null) PopupStateChanged(false); };
            }
            sessions.SelectedIndexChanged+=delegate { if(!syncing) LoadLaps(); };
            pilotA.SelectedIndexChanged+=delegate { if(!syncing) FillA(null); };
            pilotB.SelectedIndexChanged+=delegate { if(!syncing) FillB(null); };
            lapA.SelectedIndexChanged+=delegate { if(!syncing) { SelectHistory();QueueComparison(); } };
            lapB.SelectedIndexChanged+=delegate { if(!syncing) QueueComparison(); };
            segmentSize.Items.AddRange(new object[]{100,200,500});segmentSize.SelectedItem=200;
            segmentSize.SelectedIndexChanged+=delegate { if(!syncing) QueueComparison(); };
            history.SelectionChanged+=delegate
            {
                if(syncing || history.CurrentRow==null) return;
                var info=history.CurrentRow.Tag as LearningLapInfo;
                if(info!=null && lapA.SelectedItem!=info) lapA.SelectedItem=info;
            };
            history.CellDoubleClick+=delegate(object sender,DataGridViewCellEventArgs e) { if(e.RowIndex>=0) tabs.SelectedIndex=1; };
            map.SelectionChanged=SelectSegment;
            segments.SelectionChanged+=delegate { if(!syncing && segments.CurrentRow!=null) Highlight(segments.CurrentRow.Index); };
            refreshTimer.Tick+=delegate { UpdateStatus();if(!loading && revision!=recorder.Revision) RefreshCatalog(); };
            analysisTimer.Tick+=delegate { analysisTimer.Stop();Analyze(); };
            Shown+=delegate { RefreshCatalog();refreshTimer.Start(); };
        }
        static ComboBox Select(string accessible)
        {
            var combo=SettingsUi.Select(accessible);
            combo.DropDown+=delegate
            {
                int width=combo.Width;
                foreach(var item in combo.Items) width=Math.Max(width,TextRenderer.MeasureText(item.ToString(),combo.Font).Width+32);
                combo.DropDownWidth=Math.Min(Screen.FromControl(combo).WorkingArea.Width-32,width);
            };
            return combo;
        }
        static DataGridView Grid(string name)
        {
            var g=new DataGridView { Dock=DockStyle.Fill,AccessibleName=name,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AllowUserToOrderColumns=false,
                RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=Theme.Background,BorderStyle=BorderStyle.None,
                GridColor=Theme.Grid,EnableHeadersVisualStyles=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,ColumnHeadersHeight=32,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing };
            g.DefaultCellStyle=new DataGridViewCellStyle { BackColor=Theme.Surface,ForeColor=Theme.Text,SelectionBackColor=Theme.Player,SelectionForeColor=Theme.Text,Font=new Font(Theme.DataFont,10.5f),Padding=new Padding(5,3,5,3) };
            g.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle { BackColor=Theme.Background,ForeColor=Theme.Muted,SelectionBackColor=Theme.Background,Font=new Font(Theme.BodyFont,9),Padding=new Padding(5) };
            g.AlternatingRowsDefaultCellStyle.BackColor=Theme.Background;g.RowTemplate.Height=33;return g;
        }
        static void Column(DataGridView grid,string text,float weight,bool numeric=true)
        {
            int i=grid.Columns.Add("c"+grid.Columns.Count,text);var column=grid.Columns[i];column.FillWeight=weight;column.MinimumWidth=numeric ? 58 : 90;
            column.SortMode=DataGridViewColumnSortMode.NotSortable;
            if(numeric) column.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;
            else column.DefaultCellStyle.Font=new Font(Theme.BodyFont,10.5f);
        }
        static Control Field(string name,Control control)
        {
            var field=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0,0,10,4) };
            field.RowStyles.Add(new RowStyle(SizeType.Absolute,23));field.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var label=SettingsUi.Label(name,23);label.Font=new Font(Theme.BodyFont,9);label.ForeColor=Theme.Muted;label.Dock=DockStyle.Fill;label.Click+=delegate { control.Focus(); };
            control.Margin=new Padding(0,0,0,4);field.Controls.Add(label,0,0);field.Controls.Add(control,0,1);return field;
        }
        void BuildHistory()
        {
            var page=new TabPage("  Histórico de voltas  ") { BackColor=Theme.Background,Padding=new Padding(12) };tabs.TabPages.Add(page);
            var body=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3 };body.RowStyles.Add(new RowStyle(SizeType.Absolute,46));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Absolute,48));page.Controls.Add(body);
            historyNotice.Dock=DockStyle.Fill;historyNotice.ForeColor=Theme.Muted;body.Controls.Add(historyNotice,0,0);body.Controls.Add(history,0,1);
            Column(history,"VOLTA",55);Column(history,"TEMPO",120);Column(history,"SETOR 1",95);Column(history,"SETOR 2",95);Column(history,"SETOR 3",95);Column(history,"ESTADO DA VOLTA",240,false);
            var action=SettingsUi.Button("Comparar volta selecionada",()=>tabs.SelectedIndex=1);action.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;action.Margin=new Padding(0,10,0,0);body.Controls.Add(action,0,2);
        }
        void BuildComparison()
        {
            var page=new TabPage("  Comparar no mapa  ") { BackColor=Theme.Background,Padding=new Padding(12) };tabs.TabPages.Add(page);
            var body=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(0) };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute,64));body.RowStyles.Add(new RowStyle(SizeType.Absolute,48));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));page.Controls.Add(body);
            var row=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=4,Margin=new Padding(0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,153));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
            row.Controls.Add(Field("PILOTO B • REFERÊNCIA",pilotB),0,0);row.Controls.Add(Field("VOLTA B",lapB),1,0);
            var best=SettingsUi.Button("Melhor válida",()=>
            {
                var selected=pilotB.SelectedItem as Pilot;if(selected==null) return;
                lapB.SelectedItem=LearningComparison.Best(laps.Where(l=>l.DriverKey==selected.Key));
                if(lapB.SelectedItem==null) notice.Text="Este piloto ainda não tem volta válida gravada. Você pode selecionar outra volta manualmente.";
            });best.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;best.Margin=new Padding(0,0,8,6);row.Controls.Add(best,2,0);row.Controls.Add(Field("TRECHO (m)",segmentSize),3,0);body.Controls.Add(row,0,0);
            notice.Dock=DockStyle.Fill;notice.ForeColor=Theme.Muted;body.Controls.Add(notice,0,1);
            var columns=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0) };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,57));body.Controls.Add(columns,0,2);
            var left=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0,0,12,0) };left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,169));left.Controls.Add(map,0,0);left.Controls.Add(sectors,0,1);columns.Controls.Add(left,0,0);
            Column(sectors,"OFICIAL",78,false);Column(sectors,"A",95);Column(sectors,"B",95);Column(sectors,"A − B",100);
            var right=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0) };right.RowStyles.Add(new RowStyle(SizeType.Percent,57));right.RowStyles.Add(new RowStyle(SizeType.Percent,43));right.Controls.Add(charts,0,0);right.Controls.Add(segments,0,1);columns.Controls.Add(right,1,0);
            Column(segments,"TRECHO (m)",115,false);Column(segments,"A (s)",78);Column(segments,"B (s)",78);Column(segments,"A − B",91);Column(segments,"LEITURA",115,false);
        }
        void Post(Action action)
        {
            if(IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)(()=>{if(!IsDisposed) action();})); } catch(InvalidOperationException) { }
        }
        void Background<T>(Func<T> work,Action<T> success,Action<Exception> failed)
        {
            Task.Factory.StartNew(work).ContinueWith(t=>
            {
                if(t.IsFaulted) { var error=t.Exception.GetBaseException();Post(()=>failed(error)); }
                else { var result=t.Result;Post(()=>success(result)); }
            });
        }
        void UpdateStatus()
        {
            status.Text=recorder.Error.Length>0 ? recorder.Error : recorder.Status;
            if(recorder.UnsavedLaps>0) status.Text+=" • "+recorder.UnsavedLaps+" volta(s) não salvas";
            if(recorder.DroppedFrames>0) status.Text+=" • houve intervalos sem amostras";
            status.ForeColor=recorder.Error.Length>0 ? Theme.Fuel : Theme.Muted;
        }
        void RefreshCatalog()
        {
            if(loading) return;loading=true;refreshButton.Enabled=false;int seenRevision=recorder.Revision;UpdateStatus();
            if(sessions.Items.Count==0) historyNotice.Text="Lendo sessões salvas…";
            Background(()=> { string warning;var values=store.Sessions(out warning);return new Catalog { Sessions=values,Warning=warning }; },result=>
            {
                var selected=sessions.SelectedItem as LearningSession;string id=selected==null ? null : selected.Id;
                revision=seenRevision;diskWarning=result.Warning;syncing=true;
                sessions.Items.Clear();sessions.Items.AddRange(result.Sessions);
                sessions.SelectedItem=result.Sessions.FirstOrDefault(s=>s.Id==id)??result.Sessions.FirstOrDefault();syncing=false;loading=false;refreshButton.Enabled=true;LoadLaps();
            },e=> { loading=false;refreshButton.Enabled=true;revision=seenRevision;notice.Text="Não foi possível ler as sessões: "+e.Message+" Use Atualizar para tentar novamente.";historyNotice.Text=notice.Text; });
        }
        void LoadLaps()
        {
            var session=sessions.SelectedItem as LearningSession;int request=++loadRequest;
            if(session==null)
            {
                loadedSession="";
                laps=new LearningLapInfo[0];FillPilots();historyNotice.Text="Ainda não há voltas gravadas. Deixe o Arts aberto e entre na pista; o histórico é salvo automaticamente.";return;
            }
            if(loadedSession!=session.Id) { laps=new LearningLapInfo[0];FillPilots();ClearComparison("Lendo voltas desta sessão…");loadedSession=session.Id; }
            historyNotice.Text="Lendo voltas salvas…";
            Background(()=> { string warning;var values=store.Laps(session.Id,out warning);return new LapList { Laps=values,Warning=warning }; },result=>
            {
                if(request!=loadRequest) return;laps=result.Laps;FillPilots();
                if(result.Warning.Length>0) historyNotice.Text=result.Warning;else if(diskWarning.Length>0) historyNotice.Text=diskWarning;
            },e=>
            { if(request!=loadRequest) return;laps=new LearningLapInfo[0];FillPilots();historyNotice.Text="Não foi possível ler as voltas: "+e.Message; });
        }
        void FillPilots()
        {
            var oldA=pilotA.SelectedItem as Pilot;var oldB=pilotB.SelectedItem as Pilot;
            var oldLapA=lapA.SelectedItem as LearningLapInfo;var oldLapB=lapB.SelectedItem as LearningLapInfo;
            var pilots=laps.GroupBy(l=>l.DriverKey).Select(g=>new Pilot { Key=g.Key,Name=g.First().Driver,Class=g.First().Class,Own=g.Any(l=>l.IsPlayer) }).OrderByDescending(p=>p.Own).ThenBy(p=>p.Name).ToArray();
            syncing=true;pilotA.Items.Clear();pilotB.Items.Clear();pilotA.Items.AddRange(pilots);pilotB.Items.AddRange(pilots);
            pilotA.SelectedItem=pilots.FirstOrDefault(p=>oldA!=null && p.Key==oldA.Key)??pilots.FirstOrDefault();
            pilotB.SelectedItem=pilots.FirstOrDefault(p=>oldB!=null && p.Key==oldB.Key)??pilotA.SelectedItem;syncing=false;
            FillA(oldLapA==null ? null : oldLapA.Id);FillB(oldLapB==null ? null : oldLapB.Id);
        }
        void FillA(string keep)
        {
            var pilot=pilotA.SelectedItem as Pilot;var selected=laps.Where(l=>pilot!=null && l.DriverKey==pilot.Key).OrderByDescending(l=>l.Number).ToArray();
            syncing=true;lapA.Items.Clear();lapA.Items.AddRange(selected);lapA.SelectedItem=selected.FirstOrDefault(l=>l.Id==keep)??selected.FirstOrDefault(l=>l.Completed)??selected.FirstOrDefault();
            int scroll=history.FirstDisplayedScrollingRowIndex;history.Rows.Clear();
            foreach(var l in selected)
            {
                int row=history.Rows.Add(l.Number,LearningFormat.Time(l),LearningFormat.Seconds(l.Sector1),LearningFormat.Seconds(l.Sector2),LearningFormat.Seconds(l.Sector3),l.State);
                history.Rows[row].Tag=l;if(l.Invalid || l.Unscored) history.Rows[row].Cells[5].Style.ForeColor=Theme.Fuel;
            }
            syncing=false;SelectHistory();if(scroll>=0 && scroll<history.Rows.Count) history.FirstDisplayedScrollingRowIndex=scroll;
            historyNotice.Text=selected.Length==0 ? "As voltas aparecem ao cruzar a linha ou encerrar a saída. Só há histórico do período em que o Arts estava aberto." :
                selected.Length+" volta(s) • "+selected[0].Car+" • Tempos de setor em segundos. Selecione uma volta e abra Comparar no mapa.";
            QueueComparison();
        }
        void FillB(string keep)
        {
            var pilot=pilotB.SelectedItem as Pilot;var selected=laps.Where(l=>pilot!=null && l.DriverKey==pilot.Key).OrderByDescending(l=>l.Number).ToArray();
            syncing=true;lapB.Items.Clear();lapB.Items.AddRange(selected);lapB.SelectedItem=selected.FirstOrDefault(l=>l.Id==keep)??LearningComparison.Best(selected);syncing=false;QueueComparison();
        }
        void SelectHistory()
        {
            var lap=lapA.SelectedItem as LearningLapInfo;syncing=true;history.ClearSelection();
            if(lap!=null) foreach(DataGridViewRow row in history.Rows) if(((LearningLapInfo)row.Tag).Id==lap.Id) { row.Selected=true;history.CurrentCell=row.Cells[0];break; }
            syncing=false;
        }
        void QueueComparison()
        { analysisRequest++;analysisTimer.Stop();analysisTimer.Start(); }
        void ClearComparison(string message)
        {
            displayedKey="";
            map.Comparison=charts.Comparison=null;map.Selected=charts.Selected=-1;map.Invalidate();charts.Invalidate();sectors.Rows.Clear();segments.Rows.Clear();notice.Text=message;
        }
        void Analyze()
        {
            if(analysisBusy) return;var a=lapA.SelectedItem as LearningLapInfo;var b=lapB.SelectedItem as LearningLapInfo;
            if(a==null || b==null)
            { ClearComparison("Escolha uma volta A e uma referência B. Se ainda não houver melhor volta válida, selecione B manualmente.");return; }
            int size=(int)segmentSize.SelectedItem;string key=a.Id+"/"+b.Id+"/"+size;
            if(key==displayedKey) return;
            analysisBusy=true;int request=analysisRequest;
            ClearComparison("Carregando as passagens e calculando a comparação…");
            Background(()=>
            {
                var first=store.Load(a.SessionId,a.Id);var second=a.Id==b.Id ? first : store.Load(b.SessionId,b.Id);return LearningComparison.Build(first,second,size);
            },result=>
            {
                analysisBusy=false;if(request!=analysisRequest) { analysisTimer.Start();return; }Display(result);displayedKey=key;
            },e=>
            {
                analysisBusy=false;if(request!=analysisRequest) { analysisTimer.Start();return; }ClearComparison("Não foi possível comparar estas voltas: "+e.Message);
            });
        }
        void Display(LearningComparison comparison)
        {
            syncing=true;map.Comparison=charts.Comparison=comparison;sectors.Rows.Clear();segments.Rows.Clear();
            var a=comparison.A.Info;var b=comparison.B.Info;
            AddSector("Setor 1",a.Sector1,b.Sector1);AddSector("Setor 2",a.Sector2,b.Sector2);AddSector("Setor 3",a.Sector3,b.Sector3);
            AddSector("Volta",a.OfficialTime ? a.Time : -1,b.OfficialTime ? b.Time : -1);
            foreach(var s in comparison.Segments)
            {
                int index=segments.Rows.Add(s.Start.ToString("0")+"–"+s.End.ToString("0"),LearningFormat.Seconds(s.TimeA),LearningFormat.Seconds(s.TimeB),LearningFormat.Delta(s.Delta),s.Available ? "Estimado" : "Sem dados");
                if(s.Available) segments.Rows[index].Cells[3].Style.ForeColor=LearningPainter.DeltaColor(s.Delta);
            }
            syncing=false;
            notice.Text="Microsetores estimados: "+comparison.Available+"/"+comparison.Segments.Length+" trechos comparáveis. Verde/− = ganho de A; vermelho/+ = perda de A. Cinza = sem dados.";
            if(!a.Valid || !b.Valid) notice.Text+=" Atenção: há volta invalidada, de boxes ou incompleta.";
            else if(a.Id==b.Id) notice.Text+=" A e B são a mesma volta.";
            SelectSegment(0);
        }
        void AddSector(string title,double a,double b)
        {
            double delta=a>0 && b>0 ? a-b : double.NaN;int row=sectors.Rows.Add(title,LearningFormat.Seconds(a),LearningFormat.Seconds(b),LearningFormat.Delta(delta));
            if(Format.Finite(delta)) sectors.Rows[row].Cells[3].Style.ForeColor=LearningPainter.DeltaColor(delta);
        }
        void Highlight(int index)
        { map.Selected=charts.Selected=index;map.Invalidate();charts.Invalidate(); }
        void SelectSegment(int index)
        {
            if(index<0 || index>=segments.Rows.Count) return;syncing=true;segments.ClearSelection();segments.CurrentCell=segments.Rows[index].Cells[0];segments.Rows[index].Selected=true;syncing=false;Highlight(index);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        { if(PopupStateChanged!=null) PopupStateChanged(false);refreshTimer.Stop();analysisTimer.Stop();refreshTimer.Dispose();analysisTimer.Dispose();base.OnFormClosed(e); }
    }
}
