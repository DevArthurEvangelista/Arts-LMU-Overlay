using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Arthur.LmuOverlay
{
 public sealed partial class Controller
 {
  readonly VoiceDictation voiceDictation=new VoiceDictation();
  Label voiceState;ComboBox voiceMicrophone;TextBox whisperExecutable,whisperModel;ComboBox voiceLanguage;bool voiceReleased,voiceCapturing,voicePreviouslyDown,voiceRecordingWasValid,voiceStartedWithKeyboard,voiceStopRequested,voiceStopStalled;double voiceCaptureUntil,voiceStarted,voiceStopAt;
  [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
  static bool Held(int key){return key>0&&(GetAsyncKeyState(key)&0x8000)!=0;}
  bool VoiceKeyboardDown()
  {
   int key=settings.VoiceChatKey,mods=settings.VoiceChatModifiers;
   if(!Held(key))return false;
   bool ctrl=Held((int)Keys.ControlKey),alt=Held((int)Keys.Menu),shift=Held((int)Keys.ShiftKey);
   return ctrl==((mods&2)!=0)&&alt==((mods&1)!=0)&&shift==((mods&4)!=0);
  }
  void ShowVoice(string title,string detail,Color color,double seconds)
  {voiceOverlay.Display(title,detail,color,seconds);UpdateVoiceState(title+" • "+detail);}
  void VoiceOverlayExpired(string previous)
  {
   voiceRecordingWasValid=false;
   if(previous=="Ouvindo"&&voiceDictation.Recording){voiceStopRequested=true;voiceStopAt=Now;voiceDictation.Cancel();}
   UpdateVoiceState(previous=="Ouvindo"?"Gravação encerrada: jogo sem foco ou tempo esgotado.":"Transcrição interrompida: jogo sem foco ou limite de tempo.");
  }
  void BuildVoiceChatSettings(SettingsPage page)
  {
   var card=new SettingsCard{Height=590,Margin=new Padding(0,0,0,16)};page.Controls.Add(card);
   var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Theme.Surface};card.Controls.Add(flow);
   var title=Label("Falar no chat do LMU",30);title.Font=new Font(Theme.BodyFont,13,FontStyle.Bold);flow.Controls.Add(title);
   var hint=Label("Segure o botão do volante ou a tecla vinculada, fale e solte. O indicador aparece na pista. Configure Realtime Chat do LMU para Enter.",68);flow.Controls.Add(hint);
   flow.Controls.Add(Check("Ativar envio automático",settings.VoiceChatEnabled,v=>{settings.VoiceChatEnabled=v;if(!v){voiceRecordingWasValid=false;voiceDictation.Cancel();voiceOverlay.Clear();}Changed();}));
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
   actions.Controls.Add(Button("Vincular tecla",BindVoiceKey));
   actions.Controls.Add(Button("Limpar",()=>{voiceCapturing=false;settings.VoiceChatButton=null;settings.VoiceChatKey=0;settings.VoiceChatModifiers=0;QueueSave();UpdateVoiceState("Vínculos removidos.");}));
   voiceState=Label("",64);flow.Controls.Add(voiceState);UpdateVoiceState("Pronto para gravar na pista.");
   flow.SizeChanged+=delegate{title.Width=hint.Width=actions.Width=voiceState.Width=whisperExecutable.Width=whisperModel.Width=Math.Max(180,flow.ClientSize.Width-8);};
   voiceDictation.Completed+=text=>{if(IsDisposed||!IsHandleCreated)return;BeginInvoke((Action)(()=>{
    if(!voiceRecordingWasValid||!settings.VoiceChatEnabled||!IsGameForeground(Win32.GetForegroundWindow(),Now)){ShowVoice("Não enviado","O jogo perdeu o foco durante a transcrição.",Color.OrangeRed,5);return;}
    if(string.IsNullOrWhiteSpace(text)){ShowVoice("Não enviado","Não foi possível entender a fala.",Color.OrangeRed,5);return;}
    try{if(VoiceChatKeys.Send(text,()=>IsGameForeground(Win32.GetForegroundWindow(),Now)))ShowVoice("Concluído","Comando enviado ao jogo: "+text,Color.FromArgb(73,220,155),5);else ShowVoice("Não enviado","Jogo fora de foco ou frase longa.",Color.OrangeRed,5);}catch(Exception ex){ShowVoice("Falha no envio",ex.Message,Color.OrangeRed,5);AppLog.Write("Chat por voz",ex);}
   }));};
   voiceDictation.Error+=message=>{if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)(()=>ShowVoice("Falha na voz",message,Color.OrangeRed,5)));};
  }
  void BindVoiceKey()
  {
   capturing=true;
   try{using(var dialog=new KeyCapture())if(dialog.ShowDialog(this)==DialogResult.OK){
    if(dialog.SelectedKey==settings.HotkeyKey&&dialog.SelectedModifiers==settings.HotkeyModifiers){UpdateVoiceState("Essa tecla já oculta os painéis. Escolha outra.");return;}
    settings.VoiceChatKey=dialog.SelectedKey;settings.VoiceChatModifiers=dialog.SelectedModifiers;QueueSave();UpdateVoiceState("Tecla vinculada. Segure para falar na pista.");
   }}finally{capturing=false;}
  }
  void ChooseVoiceFile(TextBox target,string filter,bool executable)
  {
   using(var dialog=new OpenFileDialog{Filter=filter,CheckFileExists=true})if(dialog.ShowDialog(this)==DialogResult.OK){target.Text=dialog.FileName;if(executable)settings.VoiceWhisperExecutable=dialog.FileName;else settings.VoiceWhisperModel=dialog.FileName;QueueSave();UpdateVoiceState("Arquivo selecionado.");}
  }
  void UpdateVoiceState(string message){if(voiceState!=null)voiceState.Text=(settings.VoiceChatButton==null?"Sem botão":settings.VoiceChatButton.ToString())+" / "+(settings.VoiceChatKey==0?"sem tecla":KeyCapture.Display(settings.VoiceChatKey,settings.VoiceChatModifiers))+" • "+message;}
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
   bool active=!voiceCapturing&&settings.VoiceChatEnabled&&(settings.VoiceChatButton!=null||settings.VoiceChatKey!=0)&&voiceDictation.Available&&
      !WindowInteractionPaused&&OverlayVisibility.OnTrack(Current,now)&&IsGameForeground(Win32.GetForegroundWindow(),now);
   bool found=buttons.Any(b=>settings.VoiceChatButton!=null&&settings.VoiceChatButton.Matches(b));
   bool wheelDown=found&&buttons.Any(b=>settings.VoiceChatButton.Matches(b)&&b.Pressed);
   bool keyDown=VoiceKeyboardDown(),down=wheelDown||keyDown;
   if(voiceDictation.Recording){
    if(voiceStopRequested){
     if(!voiceStopStalled&&now-voiceStopAt>3){voiceStopStalled=true;voiceRecordingWasValid=false;voiceDictation.Cancel();
      if(IsGameForeground(Win32.GetForegroundWindow(),now))ShowVoice("Falha na captura","O microfone não encerrou a gravação.",Color.OrangeRed,5);}
    }
    else if(!active||(!voiceStartedWithKeyboard&&!found)||now-voiceStarted>12){
     voiceRecordingWasValid=false;voiceStopRequested=true;voiceStopAt=now;voiceDictation.Cancel();
     if(IsGameForeground(Win32.GetForegroundWindow(),now))ShowVoice("Gravação cancelada","Foco, dispositivo ou limite de 12 segundos.",Color.OrangeRed,5);else voiceOverlay.Clear();
    }
    else if(!(voiceStartedWithKeyboard?keyDown:wheelDown)){
     voiceRecordingWasValid=true;voiceStopRequested=true;voiceStopAt=now;voiceDictation.Stop();ShowVoice("Transcrevendo","Aguarde o reconhecimento da fala.",Color.DeepSkyBlue,0);
    }
   }else if(active&&down&&!voicePreviouslyDown){
    voiceDictation.ExecutablePath=settings.VoiceWhisperExecutable;voiceDictation.ModelPath=settings.VoiceWhisperModel;voiceDictation.Microphone=settings.VoiceMicrophone;voiceDictation.Language=settings.VoiceLanguage;
    voiceRecordingWasValid=false;voiceStopRequested=voiceStopStalled=false;voiceStartedWithKeyboard=keyDown;
    if(voiceDictation.Start()){voiceStarted=now;ShowVoice("Ouvindo",voiceStartedWithKeyboard?"Segure "+KeyCapture.Display(settings.VoiceChatKey,settings.VoiceChatModifiers)+"; solte para enviar.":"Segure o botão do volante; solte para enviar.",Color.FromArgb(94,201,255),0);}
   }
   voicePreviouslyDown=active&&down;
  }
 }
}
