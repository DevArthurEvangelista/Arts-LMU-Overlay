using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arthur.LmuOverlay
{
    // The controller and every card editor share native controls and tokens.
    internal static class SettingsUi
    {
        public static Label Label(string text,int height=27)
        { return new Label { Text=text,AutoSize=false,Height=height,ForeColor=Theme.Text,TextAlign=ContentAlignment.MiddleLeft }; }
        public static Button Button(string text,Action action)
        {
            var b=new Button { Text=text,AutoSize=true,MinimumSize=new Size(142,34),Padding=new Padding(6,2,6,2),FlatStyle=FlatStyle.Flat,BackColor=Theme.Surface,ForeColor=Theme.Text,Cursor=Cursors.Hand,Margin=new Padding(0,0,10,8) };
            b.FlatAppearance.BorderColor=Theme.Grid;b.FlatAppearance.MouseOverBackColor=Theme.Player;b.FlatAppearance.MouseDownBackColor=Theme.Background;b.AccessibleName=text;b.Click+=delegate { action(); };return b;
        }
        public static NumericUpDown Number(int value,int min,int max,int step,Action<int> action,string accessible)
        {
            var n=new NumericUpDown { Minimum=min,Maximum=max,Increment=step,Value=Math.Max(min,Math.Min(max,value)),Width=92,BackColor=Theme.Surface,ForeColor=Theme.Text,AccessibleName=accessible,BorderStyle=BorderStyle.FixedSingle,TextAlign=HorizontalAlignment.Right };
            n.ValueChanged+=delegate { action((int)n.Value); };return n;
        }
        public static ComboBox Select(string accessible)
        { return new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Surface,ForeColor=Theme.Text,Dock=DockStyle.Fill,AccessibleName=accessible,IntegralHeight=false,DropDownHeight=360 }; }
    }
    public sealed class PanelAppearanceDialog : Form
    {
        readonly PanelAppearance appearance;
        readonly Func<Size> readSize,designSize;
        readonly Action<Size> resize;
        readonly Action changed;
        readonly ComboBox family=SettingsUi.Select("Fonte dos textos");
        readonly NumericUpDown fontSize,width,height;
        readonly Label dimensions=SettingsUi.Label("",30);
        readonly Label saveState=SettingsUi.Label("As alterações são salvas automaticamente.",28);
        bool syncing;
        public PanelAppearanceDialog(string name,PanelAppearance value,Func<Size> currentSize,Func<Size> originalSize,Action<Size> setSize,Action appearanceChanged)
        {
            appearance=value;readSize=currentSize;designSize=originalSize;resize=setSize;changed=appearanceChanged;
            Text="Fonte e tamanho • "+name;AccessibleName=Text;StartPosition=FormStartPosition.CenterParent;
            Font=new Font(Theme.BodyFont,10);BackColor=Theme.Background;ForeColor=Theme.Text;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;ShowInTaskbar=false;
            AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(600,450);
            MaximumSize=Screen.FromControl(this).WorkingArea.Size;
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22),ColumnCount=2,RowCount=0 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));Controls.Add(root);
            var intro=SettingsUi.Label(name,32);intro.Font=new Font(Theme.BodyFont,13,FontStyle.Bold);AddWide(root,intro,38);
            var help=SettingsUi.Label("Ajustes exclusivos deste painel, aplicados na hora.",28);help.ForeColor=Theme.Muted;AddWide(root,help,28);
            saveState.ForeColor=Theme.Muted;AddWide(root,saveState,28);
            family.DropDownStyle=ComboBoxStyle.DropDownList;family.Items.AddRange(PanelAppearance.Families);family.SelectedItem=appearance.FontFamily;
            family.BackColor=Theme.Surface;family.ForeColor=Theme.Text;family.AccessibleName="Fonte dos textos de "+name;family.Dock=DockStyle.Fill;
            family.SelectedIndexChanged+=delegate { if(!syncing) { appearance.FontFamily=(string)family.SelectedItem;changed(); } };
            AddRow(root,"Fonte dos textos",family);
            fontSize=SettingsUi.Number(appearance.FontPercent,PanelAppearance.MinimumFont,PanelAppearance.MaximumFont,5,v=>
            {
                if(syncing) return;int old=appearance.FontPercent;appearance.FontPercent=v;changed();
                if(v>old) { var need=appearance.SuggestedSize(designSize());var current=readSize();resize(new Size(Math.Max(current.Width,need.Width),Math.Max(current.Height,need.Height))); }
                Sync();
            },"Tamanho das letras de "+name+" em porcentagem");
            AddRow(root,"Tamanho das letras (%)",fontSize);
            var size=readSize();
            width=SettingsUi.Number(size.Width,1,16384,10,v=>{if(!syncing) ChangeSize(v,readSize().Height);},"Largura de "+name+" em pixels");
            height=SettingsUi.Number(size.Height,1,16384,10,v=>{if(!syncing) ChangeSize(readSize().Width,v);},"Altura de "+name+" em pixels");
            AddRow(root,"Largura do painel (px)",width);AddRow(root,"Altura do painel (px)",height);
            dimensions.ForeColor=Theme.Muted;AddWide(root,dimensions,32);
            var note=SettingsUi.Label("Números mantêm a fonte de largura fixa. Letras maiores ampliam o painel\nquando há espaço. Ao diminuir, o texto se ajusta para caber.",44);note.ForeColor=Theme.Muted;note.Font=new Font(Theme.BodyFont,9);AddWide(root,note,50);
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
            actions.Controls.Add(SettingsUi.Button("Ajustar painel às letras",()=>{resize(appearance.SuggestedSize(designSize()));Sync();}));
            actions.Controls.Add(SettingsUi.Button("Restaurar padrão",()=>{appearance.Reset();changed();resize(Size.Empty);Sync();}));
            var close=SettingsUi.Button("Concluir",Close);close.MinimumSize=new Size(92,34);actions.Controls.Add(close);AddWide(root,actions,44);
            AcceptButton=close;CancelButton=close;Sync();
        }
        void AddWide(TableLayoutPanel root,Control item,int height)
        { int row=root.RowCount++;root.RowStyles.Add(new RowStyle(SizeType.Absolute,height));item.Dock=DockStyle.Fill;root.Controls.Add(item,0,row);root.SetColumnSpan(item,2); }
        void AddRow(TableLayoutPanel root,string title,Control item)
        {
            int row=root.RowCount++;root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            var label=SettingsUi.Label(title,32);label.Dock=DockStyle.Fill;label.Click+=delegate { item.Focus(); };
            root.Controls.Add(label,0,row);item.Margin=new Padding(0,5,0,4);root.Controls.Add(item,1,row);
        }
        void ChangeSize(int w,int h) { resize(new Size(w,h));Sync(); }
        public void SetSaveState(string text) { saveState.Text=text; }
        void Sync()
        {
            syncing=true;
            try
            {
                var actual=readSize();width.Value=Math.Max(1,Math.Min(16384,actual.Width));height.Value=Math.Max(1,Math.Min(16384,actual.Height));
                fontSize.Value=appearance.FontPercent;family.SelectedItem=appearance.FontFamily;
                dimensions.Text="Tamanho atual: "+actual.Width+" × "+actual.Height+" px • limite: área do monitor";
            }
            finally { syncing=false; }
        }
    }
}
