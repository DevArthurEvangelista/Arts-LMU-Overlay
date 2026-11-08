using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Arthur.LmuOverlay
{
 public sealed partial class Controller
 {
  readonly VoiceDictation voiceDictation=new VoiceDictation();
  Label voiceState;ComboBox voiceMicrophone;TextBox whisperExecutable,whisperModel;ComboBox voiceLanguage;bool voiceReleased,voiceCapturing,voicePreviouslyDown,voiceRecordingWasValid;double voiceCaptureUntil,voiceStarted;
  void BuildVoiceChatSettings(SettingsPage page)
  {
   var card=new SettingsCard{Height=540,Margin=new Padding(0,0,0,16)};page.Controls.Add(card);
   var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Theme.Surface};card.Controls.Add(flow);
   var title=Label("Falar no chat do LMU",30);title.Font=new Font(Theme.BodyFont,13,FontStyle.Bold);flow.Controls.Add(title);
   var hint=Label("Segure o botão do volante, fale e solte: o texto reconhecido será enviado automaticamente. Usa um modelo Whisper local. Configure Realtime Chat do LMU para Enter.",68);flow.Controls.Add(hint);
   flow.Controls.Add(Check("Ativar envio automático",settings.VoiceChatEnabled,v=>{settings.VoiceChatEnabled=v;if(!v)voiceDictation.Cancel();Changed();}));
   var micLabel=Label("Microfone",25);flow.Controls.Add(micLabel);
   voiceMicrophone=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=470};flow.Controls.Add(voiceMicrophone);
   try{foreach(string item in VoiceDictation.Microphones())voiceMicrophone.Items.Add(item);int index=settings.VoiceMicrophone+1;voiceMicrophone.SelectedIndex=index>=0&&index<voiceMicrophone.Items.Count?index:0;}
   catch(Exception e){voiceMicrophone.Items.Add("Dispositivo indisponível: "+e.Message);voiceMicrophone.SelectedIndex=0;}
   voiceMicrophone.SelectedIndexChanged+=delegate{settings.VoiceMicrophone=voiceMicrophone.SelectedIndex-1;QueueSave();};
   flow.Controls.Add(Label("whisper-cli.exe",25));whisperExecutable=new TextBox{Width=470,Text=settings.VoiceWhisperExecutable??""};flow.Controls.Add(whisperExecutable);
   flow.Controls.Add(Button("Selecionar whisper-cli",()=>ChooseVoiceFile(whisperExecutable,"Executável do Whisper|whisper-cli.exe",true)));
   flow.Controls.Add(Label("Modelo Whisper (.bin)",25));whisperModel=new TextBox{Width=470,Text=settings.VoiceWhisperModel??""};flow.Controls.Add(whisperModel);
   flow.Controls.Add(Button("Selecionar modelo",()=>ChooseVoiceFile(whisperModel,"Modelo Whisper|*.bin",false)));
   voiceLanguage=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=220};voiceLanguage.Items.AddRange(new object[]{"Inglês (en)","Português (pt)"});voiceLanguage.SelectedIndex=settings.VoiceLanguage=="pt"?1:0;voiceLanguage.SelectedIndexChanged+=delegate{settings.VoiceLanguage=voiceLanguage.SelectedIndex==1?"pt":"en";QueueSave();};flow.Controls.Add(voiceLanguage);
   var actions=new FlowLayoutPanel{Height=38};flow.Controls.Add(actions);
   actions.Controls.Add(Button("Vincular botão",()=>{voiceCapturing=true;voiceReleased=false;voiceCaptureUntil=Now+20;wheel.Refresh();voiceState.Text="Solte todos os botões e pressione o escolhido (20 s).";}));
   actions.Controls.Add(Button("Limpar",()=>{voiceCapturing=false;settings.VoiceChatButton=null;QueueSave();UpdateVoiceState("Vínculo removido.");}));
   voiceState=Label("",48);flow.Controls.Add(voiceState);UpdateVoiceState("Selecione o microfone, o executável e um modelo local.");
   flow.SizeChanged+=delegate{title.Width=hint.Width=actions.Width=voiceState.Width=whisperExecutable.Width=whisperModel.Width=Math.Max(180,flow.ClientSize.Width-8);};
   voiceDictation.Completed+=text=>{if(IsDisposed||!IsHandleCreated)return;BeginInvoke((Action)(()=>{
    if(!voiceRecordingWasValid||!settings.VoiceChatEnabled||!IsGameForeground(Win32.GetForegroundWindow(),Now)){UpdateVoiceState("Foco mudou; mensagem descartada.");return;}
    if(string.IsNullOrWhiteSpace(text)){UpdateVoiceState("Não entendi a fala. Nada foi enviado.");return;}
    try{if(VoiceChatKeys.Send(text,()=>IsGameForeground(Win32.GetForegroundWindow(),Now)))UpdateVoiceState("Comando de envio: "+text);else UpdateVoiceState("Envio cancelado: jogo fora de foco ou frase longa.");}catch(Exception ex){UpdateVoiceState("Falha no envio: "+ex.Message);AppLog.Write("Chat por voz",ex);}
   }));};
   voiceDictation.Error+=message=>{if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)(()=>UpdateVoiceState("Falha no microfone: "+message)));};
  }
  void ChooseVoiceFile(TextBox target,string filter,bool executable)
  {
   using(var dialog=new OpenFileDialog{Filter=filter,CheckFileExists=true})if(dialog.ShowDialog(this)==DialogResult.OK){target.Text=dialog.FileName;if(executable)settings.VoiceWhisperExecutable=dialog.FileName;else settings.VoiceWhisperModel=dialog.FileName;QueueSave();UpdateVoiceState("Arquivo selecionado.");}
  }
  void UpdateVoiceState(string message){if(voiceState!=null)voiceState.Text=(settings.VoiceChatButton==null?"Botão não vinculado":settings.VoiceChatButton.ToString())+" • "+message;}
  void PollVoiceChat(double now)
  {
   var buttons=wheel.ReadButtons(now);
   if(voiceCapturing){
    if(now>voiceCaptureUntil){voiceCapturing=false;UpdateVoiceState("Tempo esgotado.");}
    else if(Win32.GetForegroundWindow()==Handle){var pressed=buttons.Where(b=>b.Pressed).ToList();if(pressed.Count==0&&buttons.Count>0)voiceReleased=true;
     if(voiceReleased&&pressed.Count==1){var binding=TrainingWheelBinding.From(pressed[0]);if(binding.Same(settings.TrainingSaveButton)||binding.Same(settings.TrainingRestoreButton))UpdateVoiceState("Este botão já controla o treino por trecho. Escolha outro.");else{settings.VoiceChatButton=binding;voiceCapturing=false;QueueSave();UpdateVoiceState("Vinculado.");}}}
   }
   if(whisperExecutable!=null&&whisperExecutable.Text!=settings.VoiceWhisperExecutable){settings.VoiceWhisperExecutable=whisperExecutable.Text;QueueSave();}
   if(whisperModel!=null&&whisperModel.Text!=settings.VoiceWhisperModel){settings.VoiceWhisperModel=whisperModel.Text;QueueSave();}
   bool active=!voiceCapturing&&settings.VoiceChatEnabled&&settings.VoiceChatButton!=null&&voiceDictation.Available&&
      !WindowInteractionPaused&&OverlayVisibility.OnTrack(Current,now)&&IsGameForeground(Win32.GetForegroundWindow(),now);
   bool found=buttons.Any(b=>settings.VoiceChatButton!=null&&settings.VoiceChatButton.Matches(b));
   bool down=found&&buttons.Any(b=>settings.VoiceChatButton.Matches(b)&&b.Pressed);
   if(voiceDictation.Recording){
    if(!active||!found||now-voiceStarted>12){voiceRecordingWasValid=false;voiceDictation.Cancel();UpdateVoiceState("Fala cancelada (foco, dispositivo ou limite de 12 s).");}
    else if(!down){voiceRecordingWasValid=true;voiceDictation.Stop();UpdateVoiceState("Transcrevendo…");}
   }else if(active&&down&&!voicePreviouslyDown){
    voiceDictation.ExecutablePath=settings.VoiceWhisperExecutable;voiceDictation.ModelPath=settings.VoiceWhisperModel;voiceDictation.Microphone=settings.VoiceMicrophone;voiceDictation.Language=settings.VoiceLanguage;
    voiceRecordingWasValid=false;if(voiceDictation.Start()){voiceStarted=now;UpdateVoiceState("Ouvindo… solte para enviar.");}
   }
   voicePreviouslyDown=active&&down;
  }
 }
}
