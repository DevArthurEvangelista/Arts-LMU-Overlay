using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace Arthur.LmuOverlay
{
 public sealed partial class Controller
 {
  readonly SectorEngine sector;
  readonly LmuOfflineLauncher lmuLauncher=new LmuOfflineLauncher();
  readonly Label sectorStatus=new Label(),sectorDetail=new Label(),launchStatus=new Label();
  Button sectorConnect,sectorStop,launchGame,selectGame;
  TextBox gamePath;
  bool exitAfterSector;double lastSectorUi=-1;string sectorUiError="";

  bool TrainingKeysAvailable()
  {
   if(settings.HotkeyModifiers==0&&(settings.HotkeyKey==(int)Keys.F6||settings.HotkeyKey==(int)Keys.F7))
   {sectorUiError="Altere o atalho de visibilidade: F6/F7 estão reservados ao treino enquanto ele estiver ativo.";return false;}
   return true;
  }
  void SelectGameFolder()
  {
   using(var dialog=new FolderBrowserDialog())
   {
    dialog.Description="Selecione a pasta que contém Le Mans Ultimate.exe (Steam > Gerenciar > Explorar arquivos locais).";
    dialog.ShowNewFolderButton=false;
    if(!string.IsNullOrEmpty(settings.LmuExecutablePath))
    {try{string dir=Path.GetDirectoryName(settings.LmuExecutablePath);if(Directory.Exists(dir))dialog.SelectedPath=dir;}catch(ArgumentException){}}
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    try
    {
     string path=LmuLaunchPath.Resolve(dialog.SelectedPath);
     settings.LmuExecutablePath=path;gamePath.Text=path;sectorUiError="";
     QueueSave();
    }
    catch(Exception error){sectorUiError=error.Message;}
   }
   UpdateSector();
  }
  void BuildSectorPage(SettingsPage page)
  {
   PageHeading(page,"Treino por trecho","Abra o LMU e repita curvas com o Sector integrado ao Arts.");
   var launchCard=new SettingsCard{Height=290,Margin=new Padding(0,0,0,16)};page.Controls.Add(launchCard);
   var launchFlow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Theme.Surface};launchCard.Controls.Add(launchFlow);
   var launchTitle=Label("1. Abrir o jogo para treinar",32);launchTitle.Font=new Font(Theme.BodyFont,13,FontStyle.Bold);launchFlow.Controls.Add(launchTitle);
   gamePath=new TextBox{ReadOnly=true,Text=settings.LmuExecutablePath??"",BackColor=Theme.Background,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,AccessibleName="Executável do Le Mans Ultimate",Margin=new Padding(3,3,3,8)};launchFlow.Controls.Add(gamePath);
   var launchActions=new FlowLayoutPanel{Height=46,WrapContents=false};launchFlow.Controls.Add(launchActions);
   selectGame=Button("Selecionar pasta do LMU",SelectGameFolder);launchActions.Controls.Add(selectGame);
   launchGame=Button("Iniciar LMU sem EAC",delegate
   {
    if(!TrainingKeysAvailable()){UpdateSector();return;}
    sectorUiError="";lmuLauncher.Start(settings.LmuExecutablePath);UpdateSector();
   });launchGame.BackColor=Theme.Player;launchActions.Controls.Add(launchGame);
   var launchHint=Label("Selecione a pasta uma vez. Depois de abrir o jogo, escolha carro e pista e entre no treino offline. A conexão será automática quando o treino for compatível.",54);launchHint.ForeColor=Theme.Muted;launchFlow.Controls.Add(launchHint);
   launchStatus.Height=56;launchStatus.Font=new Font(Theme.BodyFont,10,FontStyle.Bold);launchFlow.Controls.Add(launchStatus);
   launchFlow.SizeChanged+=delegate{launchTitle.Width=gamePath.Width=launchActions.Width=launchHint.Width=launchStatus.Width=Math.Max(180,launchFlow.ClientSize.Width-8);};

   var card=new SettingsCard{Height=270,Margin=new Padding(0,0,0,16)};page.Controls.Add(card);var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Theme.Surface};card.Controls.Add(flow);
   var title=Label("2. Salvar ponto e retornar",36);title.Font=new Font(Theme.BodyFont,15,FontStyle.Bold);flow.Controls.Add(title);
   var note=Label("Com o carro na pista, salve um ponto antes da curva e retorne para praticar. Se abriu o jogo por fora do Arts, use Conectar treino por trecho.",60);note.ForeColor=Theme.Muted;flow.Controls.Add(note);
   var actions=new FlowLayoutPanel{Height=50,WrapContents=false};flow.Controls.Add(actions);
   sectorConnect=Button("Conectar treino por trecho",delegate{if(!TrainingKeysAvailable()){UpdateSector();return;}sectorUiError="";lmuLauncher.Cancel();sector.Start();UpdateSector();});actions.Controls.Add(sectorConnect);
   sectorStop=Button("Desconectar",delegate{lmuLauncher.Cancel();sector.RequestStop();UpdateSector();});actions.Controls.Add(sectorStop);sectorStatus.Height=64;sectorStatus.Font=new Font(Theme.BodyFont,11,FontStyle.Bold);flow.Controls.Add(sectorStatus);flow.SizeChanged+=delegate{title.Width=note.Width=actions.Width=sectorStatus.Width=Math.Max(180,flow.ClientSize.Width-8);};
   BuildTrainingButtons(page);
   var info=Label("Desconectar cancela a conexão automática e o treino por trecho; o jogo permanece aberto. O ponto salvo vale para o carro e a sessão atuais. F6 e F7 só atuam com o LMU em foco.",85);info.ForeColor=Theme.Text;page.Controls.Add(info);
   var compatibility=Label("Integração experimental • Sector 1.0.0\nLMU build 14200 / Steam 25661166. Apenas treino offline sem Easy Anti-Cheat. Outras versões e modos são bloqueados. O retorno pode afetar a cronometragem e os track limits do jogo.",115);compatibility.ForeColor=Theme.Muted;page.Controls.Add(compatibility);
   var online=Label("Para jogar online: desconecte o treino, feche o LMU e abra pela Steam normalmente, com EAC.",48);online.ForeColor=Theme.Muted;page.Controls.Add(online);
   sectorDetail.Height=95;sectorDetail.ForeColor=Theme.Muted;page.Controls.Add(sectorDetail);
   page.Controls.Add(Button("Abrir diagnósticos",delegate{try{string dir=Path.Combine(AppSettings.DirectoryPath,"TreinoSector");Directory.CreateDirectory(dir);Process.Start("explorer.exe","\""+dir+"\"");}catch(Exception e){sectorUiError=e.Message;}}));UpdateSector();
  }
  void UpdateSector()
  {
   if(sectorConnect==null)return;
   var launch=lmuLauncher.State;
   if(!lmuLauncher.Busy&&launch.Phase=="ready")
   {
    lmuLauncher.ConsumeReady();
    if(!exitAfterSector&&TrainingKeysAvailable()){sectorUiError="";sector.Start(launch.Identity);}
   }
   bool pending=lmuLauncher.Busy;
   var state=sector.State;
   sectorConnect.Enabled=!sector.Busy&&!pending&&!exitAfterSector;
   sectorStop.Enabled=sector.Busy||pending;
   selectGame.Enabled=!pending&&!sector.Busy&&!exitAfterSector;
   launchGame.Enabled=!pending&&!sector.Busy&&!exitAfterSector&&!string.IsNullOrWhiteSpace(settings.LmuExecutablePath);
   launchStatus.Text=sectorUiError.Length>0?sectorUiError:lmuLauncher.State.Message;
   launchStatus.ForeColor=sectorUiError.Length>0||lmuLauncher.State.Phase=="error"?Theme.Brake:Theme.Muted;
   sectorStatus.Text=pending?"Aguardando o treino offline. Desconectar cancela a espera.":SectorProtocol.Describe(state);
   sectorStatus.ForeColor=state.Phase=="error"?Theme.Brake:state.Checkpoint?Theme.Throttle:Theme.Text;
   sectorDetail.Text=sectorUiError.Length>0?sectorUiError:state.Phase=="active"?state.Message:state.Phase=="error"?"Os demais painéis continuam funcionando. Consulte o diagnóstico antes de tentar novamente.":"O delta possui referências próprias, salvas por pista, carro e categoria.";
  }
 }
}
