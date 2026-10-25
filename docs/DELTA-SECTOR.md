# Arts 0.12 — Delta e Sector

## Delta
Capture independente de disco, no TelemetryPump. Usa somente dados do próprio
piloto. LearningRecorder reconhece a passagem e a validade; referência exige
início observado, volta oficial, sem boxes/invalidação/lacunas. Delta é tempo
atual menos tempo interpolado da referência na mesma distância. É estimativa,
não split oficial do jogo. Sem referência ou sem continuidade mostra uma razão.
Última válida e melhor da sessão reiniciam ao mudar de sessão; pessoal permanece
em XML local por pista, classe, carro, piloto e comprimento de traçado.
A escrita usa arquivo temporário + substituição atômica. XML limita tamanho e
proíbe DTD. Arquivo corrompido é preservado e o erro é visível. Tentar novamente
repete carga ou gravação, sem bloquear aquisição/UI. Volta inválida pode mostrar
delta para orientação, com rótulo de invalidada; nunca substitui referência.

## Sector
Componente original: https://github.com/sector-lmu/Sector/tree/prod
Release: https://github.com/sector-lmu/Sector/releases/tag/v1.0.0
ZIP oficial SHA256: f5637386a730d1a8b9445c831e7a150e5b6ff5786a35c7b21b3fb46044c68a96
O assets/SectorRuntime-1.0.0.zip contém somente resources/runtime do release,
sem alteração de conteúdo. Todos os 266 hashes do release-manifest.json foram
verificados. Inclui Python embutido; não inclui a GUI Electron/overlays.
Licença: third_party/Sector-MIT.txt e licença Python dentro do pacote.

Inicialização explícita. Antes de executar: processo único identificado por PID
+ criação, ausência de EAC detectável, navigation/state offline Practice1–4,
appBuild 14200, hash do pacote e dos arquivos extraídos. Revalida contexto e
identidade depois da extração. Reutiliza diretório por digest sem sobrescrever
DLL carregada; o runtime original mantém seus próprios hashes e controles.
Python repete verificações de modo, versão, EAC, telemetria e estado do carro;
o host nativo controla restauração. Não altera nem remove essas proteções.
Não tenta resolver incompatibilidade por patch, bypass ou offsets adivinhados.

Um worker supervisiona status.json, heartbeat e stop. Status de checkpoint
somente após confirmação. F6/F7 são lidos pelo componente original. Ao perceber
restore_count maior, marca o próximo Frame como TrainingReset; delta e
LearningFrame tratam como quebra. Não promete reparar a cronometragem do LMU.
Troca para online/contexto desconhecido pede parada. Encerrar Arts aguarda
saída; nunca usa Kill no jogo ou no listener. Em encerramento forçado, o
watchdog de heartbeat original encerra a admissão. Não reinicia automaticamente
após erro. Atualizar exige encerrar Arts; arquivos nativos ficam em diretórios
por versão/digest separados da pasta de instalação.

## Verificação e limites
Testes usam SDK fixture, voltas determinísticas, disco real, worker de disco
bloqueado, backend supervisionado substituto e renderer GDI real. Verificam
interpolação, modos, invalidade, parcialidade, lacunas, reset, persistência,
contexto, hash, extração, parada e migração. Backend substituto não comprova
integração com LMU. Não houve execução nativa Windows, EAC, hotkeys Sector,
DirectInput, DWM ou instalação interativa. Validar no treino compatível antes
de depender do retorno. Delta é independente e não depende de ativar Sector.

## Atualização 0.12.1 — abrir o LMU para treino

`LmuExecutablePath` é um campo aditivo na configuração existente (sem recriar
posições, dimensões, fontes, opacidades, calibração ou atalhos). A pasta escolhida
resolve Le Mans Ultimate.exe, valida o cabeçalho PE x64 e é persistida pelo
salvamento já existente. O worker revalida o arquivo ao iniciar. Rejeita bootstrapper,
atalhos, arquivo ausente/inválido e instância do jogo/inicializador já aberta.

O botão inicia diretamente o executável, com WorkingDirectory na pasta do jogo,
UseShellExecute=false e sem argumentos. Não usa shell, URI Steam, comando para
encerrar EAC, edição do jogo nem alteração persistente de opções. Esse caminho
está documentado pelo suporte oficial:
https://guide.lemansultimate.com/hc/en-gb/articles/14590657733903-How-do-I-run-the-game-without-Easy-Anti-Cheat-EAC
https://guide.lemansultimate.com/hc/en-gb/articles/13192678574095-Connection-Issues

Um worker acompanha somente a instância lançada (PID + horário de criação).
Menu, modo online, build incompatível e EAC não autorizam a conexão; a aba
mostra a razão e aguarda o treino. Ao obter admissão, a UI encaminha a identidade
esperada ao SectorEngine, que revalida antes e depois da preparação. Cancelar a
espera não fecha o jogo e impede consumir uma admissão tardia. A conexão é
consumida uma vez; não se arma automaticamente após erro ou nova abertura do Arts.

Process.Start aceito não prova que o jogo terminou de abrir: saída precoce,
instância substituída e erros do Windows são informados. A entrada em uma sessão
(carros/pista) continua sendo uma ação do usuário dentro do LMU. Os bloqueios
originais de compatibilidade do Sector não foram alterados nesta atualização.

## Atualização 0.12.2 — protocolo, continuidade e foco

O runtime original publica paused e stopping além de waiting, starting, active,
stopped e error (reset_host_runner.py e start_reset_hotkeys.py). O parser agora
aceita esses estados e preserva checkpoint durante paused. Uma restauração
confirmada em paused também interrompe a captura, exatamente uma vez.

A consulta navigation/state agora usa 1,6 s por tentativa, até três tentativas
apenas em erros transitórios de rede/leitura, com intervalo de 150 ms. EAC,
modo incompatível e identidade errada não são admitidos nem recebem essa
carência. Falha persistente continua encerrando o módulo. O runtime original
continua aplicando seus próprios controles de modo e segurança.

Delta separa a elegibilidade de gravação da comparação ao vivo. Recuos normais
e lacunas de amostragem não eliminam a comparação disponível pelo resto da
volta; saltos incompatíveis com movimento, F7 e relógio regressivo ainda a
suspendem. Pequeno ruído de até 2 m na distância é normalizado somente na cópia
para gravação; a distância original permanece usada na comparação. CountLap=0
no primeiro segundo da volta é tratado como inicialização, se a telemetria não
indicar invalidação. Os demais bloqueios de referência válida permanecem.
CountLap desconhecido deixa de representar invalidação no estado do delta.

O card remove os cabeçalhos de referência/invalidade e mostra valor/barra ou
um traço quando indisponível. Detalhes e seleção de referência ficam no painel.
DeltaThickness é aditivo, padrão 16 px, limites 6–28 px, salvo na configuração.
A política de visibilidade recebe foco do jogo para todos os sete cards;
Alt-Tab não altera posições, habilitação ou estado manual F8. Posicionamento
continua visível fora do jogo.

## Atualização 0.12.3 — botões do volante

WheelInput enumera DIDFT_BUTTON junto dos eixos absolutos, preservando a ordem
anterior dos eixos e seus offsets. O formato personalizado acrescenta bytes de
botões e alinha DataSize a 4 bytes; o identificador do vínculo é GUID de instância
mais dwType do objeto. WheelPump publica snapshots novos por leitura; falha e
idade >300 ms retiram botões. Não escreve comandos de force feedback.

Os campos aditivos TrainingSaveButton/TrainingRestoreButton são opcionais e
persistidos pelo AppSettings. Captura exige soltar os botões, pressionar somente
um, janela de configuração em foco e prazo de 20 segundos. Rejeita duplicidade
entre salvar e retornar. Nenhuma captura produz comando para o jogo.

TrainingButtonEdges exige transição solto→pressionado observada no mesmo modo
ativo. Sessão desconectada, pausa, foco externo, USB ausente e captura limpam o
histórico; reativar/reconectar com botão segurado não dispara. O Controller
confirma treino por trecho active, sessão Practice1–4, carro na pista e foco.

SectorEngine recebe um único pedido por vez sem IO na UI. O worker revalida
admissão e PID/criação e usa o protocolo command.json original do Sector:
schema_version=1, id único, action save/restore, host=log_prefix do status e
created_at_ms UTC. Requer host atual, fase active, reset_action=0, checkpoint
para restore e idade <=1 s. Verifica foco pelo PID imediatamente antes da
escrita atômica. Não substitui pedido ainda não consumido. RequestStop limpa
pendências e impede novos pedidos. Não simula teclado e não altera o runtime
original, que continua validando o contexto e o comando recebido.
