using System;
using System.Drawing;
using System.Windows.Forms;
namespace Arthur.LmuOverlay
{
 public sealed partial class Controller
 {
  readonly Label deltaStatus=new Label();
  void BuildDeltaSettings(SettingsPage page)
  {
   var card=new SettingsCard{Height=300,Margin=new Padding(0,0,0,8)};page.Controls.Add(card);
   var rows=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=7,BackColor=Theme.Surface};rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,44));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,56));foreach(int h in new[]{32,43,36,34,34,36,48})rows.RowStyles.Add(new RowStyle(SizeType.Absolute,h));card.Controls.Add(rows);
   var title=Label("Delta central",29);title.Font=new Font(Theme.BodyFont,12,FontStyle.Bold);rows.Controls.Add(title,0,0);
   rows.Controls.Add(Check("Ativado",settings.Widgets[4].Visible,v=>{settings.Widgets[4].Visible=v;Changed();}),1,0);
   var note=Label("Barra transparente. Verde/−: ganhando tempo. Vermelho/+: perdendo tempo.",42);note.Dock=DockStyle.Fill;note.ForeColor=Theme.Muted;rows.Controls.Add(note,0,1);rows.SetColumnSpan(note,2);
   var select=SettingsUi.Select("Referência do delta");select.Items.AddRange(DeltaMode.Names);select.SelectedIndex=settings.DeltaReference;select.SelectedIndexChanged+=delegate{settings.DeltaReference=DeltaMode.Normalize(select.SelectedIndex);Changed();};
   AddDeltaRow(rows,2,"Comparar com",select);
   AddDeltaRow(rows,3,"Fundo (%) • 0 = transparente",Number(settings.DeltaOpacity,0,100,5,v=>{settings.DeltaOpacity=v;Changed();},"Opacidade do delta"));
   AddDeltaRow(rows,4,"Escala da barra (± segundos)",Number(settings.DeltaRange,1,10,1,v=>{settings.DeltaRange=v;Changed();},"Escala da barra do delta"));
   AddDeltaRow(rows,5,"Espessura da barra (px)",Number(settings.DeltaThickness,6,28,2,v=>{settings.DeltaThickness=v;Changed();},"Espessura da barra do delta"));
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};actions.Controls.Add(Button("Fonte e tamanho",()=>EditAppearance(6)));actions.Controls.Add(Button("Tentar novamente",()=>delta.Retry()));rows.Controls.Add(actions,0,6);rows.SetColumnSpan(actions,2);
   deltaStatus.Height=86;deltaStatus.ForeColor=Theme.Muted;deltaStatus.Font=new Font(Theme.BodyFont,9);deltaStatus.Text="A melhor pessoal é salva por pista, carro e categoria. Complete uma volta inteira e válida com o Arts aberto.";page.Controls.Add(deltaStatus);
  }
  void AddDeltaRow(TableLayoutPanel rows,int n,string title,Control field){var label=Label(title,32);label.Dock=DockStyle.Fill;label.ForeColor=Theme.Muted;label.Font=new Font(Theme.BodyFont,9.5f);label.Click+=delegate{field.Focus();};rows.Controls.Add(label,0,n);rows.Controls.Add(field,1,n);}
 }
}
