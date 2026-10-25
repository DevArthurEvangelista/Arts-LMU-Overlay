using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Arthur.LmuOverlay
{
 public sealed partial class Controller
 {
  readonly TrainingButtonEdges trainingEdges=new TrainingButtonEdges();
  Label saveButtonText,restoreButtonText,buttonCaptureStatus;
  int captureTrainingButton;bool captureReleased;double captureUntil;
  void BuildTrainingButtons(SettingsPage page)
  {
   var card=new SettingsCard{Height=290,Margin=new Padding(0,0,0,16)};page.Controls.Add(card);
   var rows=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=5,BackColor=Theme.Surface};
   rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
   foreach(int height in new[]{32,50,58,58,62})rows.RowStyles.Add(new RowStyle(SizeType.Absolute,height));card.Controls.Add(rows);
   var title=Label("Botões do volante",30);title.Font=new Font(Theme.BodyFont,13,FontStyle.Bold);rows.Controls.Add(title,0,0);rows.SetColumnSpan(title,3);
   var hint=Label("Clique em Vincular e pressione um botão livre do volante. Os vínculos ficam salvos. F6/F7 continuam disponíveis no treino.",48);hint.ForeColor=Theme.Muted;hint.Dock=DockStyle.Fill;rows.Controls.Add(hint,0,1);rows.SetColumnSpan(hint,3);
   saveButtonText=AddTrainingBindingRow(rows,2,1);
   restoreButtonText=AddTrainingBindingRow(rows,3,2);
   buttonCaptureStatus=Label("Inativos fora do treino por trecho conectado. Um toque executa uma ação.",60);buttonCaptureStatus.ForeColor=Theme.Muted;buttonCaptureStatus.Dock=DockStyle.Fill;rows.Controls.Add(buttonCaptureStatus,0,4);rows.SetColumnSpan(buttonCaptureStatus,3);
   UpdateTrainingButtonLabels();
  }
  Label AddTrainingBindingRow(TableLayoutPanel rows,int row,int action)
  {
   var label=Label("",56);label.Dock=DockStyle.Fill;label.AutoEllipsis=true;rows.Controls.Add(label,0,row);
   var bind=Button("Vincular",()=>{captureTrainingButton=action;captureReleased=false;captureUntil=Now+20;wheel.Refresh();buttonCaptureStatus.Text="Solte os botões. Depois pressione apenas o botão desejado. (20 segundos)";});
   bind.AutoSize=false;bind.MinimumSize=new Size(90,34);bind.Size=new Size(100,34);bind.AccessibleName="Vincular botão para "+(action==1?"salvar ponto":"retornar");rows.Controls.Add(bind,1,row);
   var clear=Button("Limpar",()=>{captureTrainingButton=0;if(action==1)settings.TrainingSaveButton=null;else settings.TrainingRestoreButton=null;QueueSave();UpdateTrainingButtonLabels();buttonCaptureStatus.Text="Vínculo removido. F6/F7 continuam disponíveis durante o treino.";});
   clear.AutoSize=false;clear.MinimumSize=new Size(85,34);clear.Size=new Size(90,34);clear.AccessibleName="Limpar botão para "+(action==1?"salvar ponto":"retornar");rows.Controls.Add(clear,2,row);return label;
  }
  void UpdateTrainingButtonLabels()
  {
   saveButtonText.Text="Salvar ponto\n"+(settings.TrainingSaveButton==null?"Não vinculado":settings.TrainingSaveButton.ToString());
   restoreButtonText.Text="Retornar\n"+(settings.TrainingRestoreButton==null?"Não vinculado":settings.TrainingRestoreButton.ToString());
  }
  void PollTrainingButtons(double now)
  {
   var buttons=wheel.ReadButtons(now);
   bool capturing=captureTrainingButton!=0;
   if(capturing)
   {
    if(now>captureUntil||!saveButtonText.Visible){captureTrainingButton=0;buttonCaptureStatus.Text="Vinculação cancelada. Clique em Vincular para tentar novamente.";}
    else if(Win32.GetForegroundWindow()==Handle)
    {
     var pressed=buttons.Where(b=>b.Pressed).ToList();
     if(pressed.Count==0&&buttons.Count>0)captureReleased=true;
     if(captureReleased&&pressed.Count>0)
     {
      captureReleased=false;
      if(pressed.Count!=1)buttonCaptureStatus.Text="Pressione apenas um botão por vez.";
      else
      {
       var binding=TrainingWheelBinding.From(pressed[0]);var other=captureTrainingButton==1?settings.TrainingRestoreButton:settings.TrainingSaveButton;
       if(binding.Same(other))buttonCaptureStatus.Text="Este botão já está vinculado à outra ação. Escolha outro.";
       else{if(captureTrainingButton==1)settings.TrainingSaveButton=binding;else settings.TrainingRestoreButton=binding;captureTrainingButton=0;QueueSave();UpdateTrainingButtonLabels();buttonCaptureStatus.Text="Botão vinculado. Só atua com treino por trecho conectado e LMU em foco.";}
      }
     }
    }
   }
   bool enabled=!capturing&&!WindowInteractionPaused&&sector.Busy&&sector.State.Phase=="active"&&
    Current.Session>=1&&Current.Session<=4&&OverlayVisibility.OnTrack(Current,now)&&IsGameForeground(Win32.GetForegroundWindow(),now);
   string action=trainingEdges.Step(buttons,settings.TrainingSaveButton,settings.TrainingRestoreButton,enabled);
   if(action.Length>0)sector.RequestWheelAction(action);
  }
 }
}
