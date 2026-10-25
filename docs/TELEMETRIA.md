# Contrato de telemetria — 0.9.2

O app usa a memória compartilhada nativa `LMU_Data`, em leitura. O mapa vem de
[TinyPedal/pyLMUSharedMemory](https://github.com/TinyPedal/pyLMUSharedMemory),
obtido em 12/09/2026, que acompanha a interface distribuída com o LMU em
`Support\SharedMemoryInterface\SharedMemoryInterface.hpp`. Cópia e licença MIT
estão em third_party. Não é necessário instalar o plugin antigo do rFactor 2.

| Estrutura | Offset / bytes |
|---|---:|
| LMUObjectOut | 324820 bytes |
| Generic | 0 |
| Scoring | 1632 |
| Scoring.vehScoringInfo | 2192 |
| Telemetry | 128464 |
| Telemetry.telemInfo | 128468 |
| Veículo de classificação | 584 bytes |
| Veículo de telemetria | 1888 bytes |
| Limite de carros | 104 |

Layout.Generated.cs contém 259 constantes, geradas com ctypes/pack=4. Ponteiros
são campos de 8 bytes e não são dereferenciados. Os índices dos dois arrays
podem diferir; o vínculo sempre usa mID.

## Correção da leitura

A atualização 0.3.1 corrige uma falha no tratamento de erros dos serviços em
segundo plano. `InvalidDataException` não deriva de `IOException`, conforme a
[documentação do .NET](https://learn.microsoft.com/en-us/dotnet/api/system.io.invaliddataexception?view=netframework-4.8.1).
As validações de pacote e de resposta do lobby lançavam esse tipo de exceção,
mas os filtros da versão 0.3 não o capturavam. Uma exceção não tratada em uma
thread de trabalho pode [encerrar o processo](https://learn.microsoft.com/en-us/dotnet/standard/threading/exceptions-in-managed-threads).

Agora o leitor descarta pacotes inválidos, registra o motivo e espera o próximo
pacote coerente. Depois de três amostras inválidas consecutivas, publica o estado
“Aguardando dados válidos da sessão”. O mapa permanece aberto e uma amostra válida
reinicia a contagem. O timer também captura essa exceção como última barreira,
com reconexão automática. O serviço de nacionalidades captura respostas vazias,
incompletas ou fora do limite e mantém os pilotos com país neutro enquanto tenta
novamente no próximo intervalo. O leitor de setup trata a mesma categoria.
Não foi aplicado um catch genérico que oculte qualquer erro de programação.

`AppDomain.UnhandledException` registra falhas fatais fora da interface, além do
registro já existente para `Application.ThreadException`. Esse registro não
suprime exceções fatais nem reinicia o app à força. A própria Microsoft distingue
[os dois eventos](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception?view=netframework-4.8.1).

A versão 0.1 descartava a classificação quando mOptionsLocation era zero
e vetava a telemetria se playerHasVehicle fosse falso. Esses campos auxiliares
agora não bloqueiam dados válidos de scoring e telemetria. Não há filtro por
modalidade: treino, classificação, aquecimento e corrida usam o mesmo leitor;
controle local, bots e carros remotos continuam na classificação.

Identificação, em ordem: um único mIsPlayer; um único mControl=0; nome local exato
e único de mPlayerName; índice explicitamente informado pelo jogo. Depois, o
leitor procura o mID correspondente na telemetria. Não escolhe o primeiro carro
arbitrariamente. Sem identificação, a tabela continua e os pedais mostram espera.

Se activeVehicles estiver ausente/zero, o leitor procura o ID do jogador nos 104
slots, exigindo limite de RPM válido e mElapsedTime a até 2 s do scoring. Essa
condição impede que slots vazios ou antigos sejam tratados como o carro atual.

Contadores de eventos são comparados antes/depois da cópia, com três tentativas.
Não há seqlock formal documentado: isso reduz, mas não prova ausência de leitura
concorrente. Contagens inválidas, IDs duplicados e entradas não finitas são
rejeitados. A atualização é reconhecida tanto pelos eventos quanto pelos tempos
nativos de scoring/telemetria. O relógio sozinho não mantém pedais congelados
como se estivessem ativos. Após 2 s sem atualização, aparece indicação de pausa;
após 1 s sem amostra coerente, o controlador também marca interrupção.

Na 0.3, `TelemetryPump` adquire as amostras fora do loop de mensagens Windows.
O timer solicita 8 ms e não permite chamadas simultâneas ao leitor; a frequência
real depende do produtor LMU e do agendamento Windows. A interface solicita
16 ms para instrumentos e atualiza a tabela a cada 100 ms. O histórico usa um
buffer circular protegido por lock de 4096 amostras. A publicação troca a
referência do Frame atomicamente; o controlador usa uma cópia para apresentação.
Falhas recuperáveis de acesso ao mapa provocam reconexão, com nova tentativa
após 1 s; o painel de configuração não precisa estar visível para ler os dados.

## Visibilidade automática — 0.9.2

`OverlayVisibility` é a política única para os seis cards e o atalho/menu da bandeja.
Modo normal exige conexão, telemetria do jogador, `mInRealtime`, fase diferente
de 9, ausência de `Stale` e uma amostra coerente publicada há no máximo 1 segundo.
O SDK define `mOptionsLocation` como 0=UI, 1=carregamento, 2=monitor e 3=pista.
Os valores explícitos 1 e 2 ocultam; zero não veta telemetria realtime fresca,
porque ApplicationState pode vir vazio em produtores/sessões suportados.
O parser continua identificando pilotos em todas as modalidades, sem filtro
novo de tipo de sessão. Velocidade, RPM e `mInPits` não são critérios de ocultação:
o jogador parado no box, já em modo de pilotagem, ainda precisa dos instrumentos.

Menu/monitor e fase pausada ocultam na próxima atualização coerente. Interrupção
silenciosa segue os limites de frescor existentes (até 2 segundos). O controlador
avalia a visibilidade antes de retornar por foco em outro aplicativo ou popup.
`OverlayWindow.EnsureVisible` respeita a mesma solicitação, evitando reabrir o
card oculto a cada 250 ms. Abrir/elevar continua suspenso durante o menu da bandeja
e captura de teclas; ocultar não rouba foco.

O atalho padrão é F8 sem modificadores, com `MOD_NOREPEAT`. Só alterna na pista,
com leitura ativa e fora do modo Posicionar. Pressioná-lo no menu não altera
estado nem agenda uma ação. A escolha manual de ocultar permanece durante
pausa/menu e retorno à pista; reiniciar o app inicia a escolha como visível.
Posicionar é uma prévia explícita dos cards ativados, inclusive sem LMU; Concluir
ou Esc volta à regra automática sem alterar a escolha manual anterior.

O schema 9 migra somente Ctrl+Alt+H padrão antigo para F8 e preserva outro atalho
personalizado. Preferências por card (ativação, posição, tamanho, fonte e opacidade)
e calibração permanecem. Visibilidade em tempo de execução não grava configuração.

## Instrumentos e classificação

| Informação | Campo / regra |
|---|---|
| Entrada de pedais | mUnfilteredThrottle / mUnfilteredBrake, 0 a 1; preserva frações analógicas |
| Gráfico | Amostras adquiridas, sem suavização; primeira amostra à esquerda, janela móvel após preencher o período |
| Barras e movimento | Transição exponencial visual de 35 ms, desativável; não altera o histórico nem os dados lidos |
| Giro do motor | mEngineRPM / mEngineMaxRPM; LEDs verde, amarelo, vermelho |
| Aviso de troca | 96% do limite por padrão, ou RPM definido pelo usuário; exige aceleração, velocidade e marcha seguinte disponível |
| Volante físico (padrão) | DirectInput: (eixo − centro) / unidades por grau, com dispositivo/eixo calibrados e validação em 90° e 180°; a escala opcional do hardware pode remapear a rotação total |
| Direção do modo JOGO | mUnfilteredSteering; fallback mFilteredSteering se a bruta for inválida ou zero com a filtrada não nula |
| Faixa total do modo JOGO | Manual > mPhysicalSteeringWheelRange > setup local > mVisualSteeringWheelRange; não comprova ângulo do hardware |
| Sem faixa no modo JOGO | Desenho normalizado em ±135°; valor em %, sem alegar graus físicos |
| Combustível | mFuel / mFuelCapacity, em litros |
| Velocidade | módulo de mLocalVel × 3,6 |
| Marcha | mGear; -1=R, 0=N; mMaxGears evita aviso na última marcha |
| Piloto e classe | mDriverName / mVehicleClass |
| POS / GER | ordem dentro da classe / mPlace geral |
| Última / melhor | mLastLapTime / mBestLapTime |
| GAP treino/classificação/aquecimento | Melhor volta do piloto − melhor volta do jogador; negativo=mais rápido, positivo=mais lento; — sem duas voltas válidas |
| GAP corrida | mTimeBehindLeader / mLapsBehindLeader; referência é o líder geral |
| Boxes | mInPits / mNumPitstops |
| +/− | posição de largada na classe menos posição atual na classe |
| Limitador | mSpeedLimiter ou mSpeedLimiterActive; marcha amarela e rótulo LIMITADOR apenas com telemetria atual |
| Melhor no cabeçalho | Menor melhor volta válida, excluindo DSQ, autor e posição atual; escopo geral ou própria classe com filtro ativo |
| PEN. | mNumPenalties: short de punições pendentes; DT/SG/T detalhados pelo canal local quando disponíveis |
| Carro | mVehicleName, mVehFilename; correspondência explícita de fabricante em CarIdentity |
| Pneu próprio | mFrontTireCompoundName / mRearTireCompoundName do mesmo mID da telemetria local |
| Pneus dos demais | tireCompoundNameFront / tireCompoundNameRear de LiveStandings; ausência permanece — |

Só a classe do jogador é exibida: jogador, três à frente e três atrás; somar
o líder no topo apenas quando não estiver nesse grupo (máximo oito). Nas pontas
do grid, mostrar só os vizinhos existentes. Outras classes ficam ocultas.
O cabeçalho considera sempre a própria classe. Não há paginação.
Altura inicialmente automática; largura e altura escolhidas pelo usuário ficam
salvas separadamente, com limites de leitura/monitor. Letras e volante não são
distorcidos quando a proporção externa do painel muda.

A largada é registrada nas fases 1–4 da sessão de corrida (reconhecimento,
grid, formação e luzes). O último estado antes da bandeira verde é preservado.
Mudança de sessão, contador de início, janela do jogo ou reinício limpa o grid.
Entrar com a corrida já em andamento não permite recuperar a largada: exibe —.
Classificação/treino também exibem — nessa coluna. Não se presume que a posição
da classificação foi necessariamente a posição de largada.

O desenho do volante reproduz em traços nativos a foto enviada pelo usuário,
sem o emblema Porsche. Sua forma não pressupõe um ângulo universal para todos
os carros. A versão 0.5 substitui a correção fixa em 50% pela leitura direta
do dispositivo em DirectInput8W. A migração do schema 4 para 5 restaura ganho
100%, ativa o modo físico e preserva posições/opacidades/atalho. Na primeira
abertura, é necessário calibrar. Não reutiliza o antigo ganho como ângulo real.

O leitor usa acesso background + nonexclusive, conforme
[SetCooperativeLevel](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ee417921(v=vs.85)),
sem efeitos de força, alteração de autocenter ou escrita nas configurações do
jogo/driver. Tenta DIPROPCALIBRATIONMODE_RAW; se indisponível, zera a zona morta
somente no seu próprio objeto DirectInput. Enumera eixos absolutos; o usuário
escolhe o dispositivo e o eixo mais movido na captura identifica a direção.
Na 0.8, enumera por chegada/remoção, falha ou pedido de calibração e lê em
trabalhador dedicado, inclusive com o painel oculto. Não usa XInput.

Calibração: centro, 90° para a direita, 180° para a direita. A terceira captura
precisa ter aproximadamente o dobro do deslocamento (tolerância de ±0,18 na
razão), rejeitando saturação ou não linearidade evidente. O sinal é aprendido
na captura direita. A conversão final não é limitada em 90°/180°: esses são
pontos de calibração, não o limite de giro. O modo físico não recebe a
suavização do sinal do carro, zona morta do jogo nem o antigo ganho 50%.

Calibração é vinculada a GUID de dispositivo, eixo e modo raw. Os limites
inteiros do eixo podem ser atualizados pelo driver sem congelar o ângulo, e uma
mudança da faixa publicada pelo LMU não pede nova calibração: graus do hardware
e graus do carro são escalas independentes. Troca real de dispositivo/eixo ou
leitura inválida produz SEM VOLANTE/RECALIBRAR; enquanto houver telemetria válida,
o desenho usa temporariamente a direção do jogo para continuar se movendo.
Reconexão do mesmo volante recupera a calibração. Em Configurações, “Rotação
máxima do meu volante” (0 = calibração) remapeia outro hardware, e “Rotação
máxima do carro” (0 = LMU) define a escala do fallback/modo JOGO. Após trocar
amplitude no driver, conferir e, se necessário, recalibrar para máxima precisão.
Cancelamento não substitui a calibração anterior. O leitor não força uma faixa
no hardware e não tenta ultrapassar restrições impostas pelo driver/soft lock.

Desativar “Usar meu volante físico” habilita o modo explicitamente JOGO,
com entrada normalizada × faixa total / 2. Ele representa o comando do carro,
que pode ter sensibilidade, saturação e amplitude diferentes do volante real.
O aviso automático de troca é uma referência
próxima do limite, não um mapa de desempenho ideal por carro/marcha.

Quando a faixa física está ausente, `SteeringSetup` consulta por GET
`http://127.0.0.1:6397/rest/garage/getPlayerGarageData`. O campo
`VM_STEER_LOCK.stringValue` contém a faixa total textual. Essa interpretação e
a alternativa ao campo físico são utilizadas pelo projeto
[TinyPedal](https://github.com/TinyPedal/TinyPedal), em
`tinypedal/adapter/lmu_restapi.py` e `tinypedal/adapter/lmu_reader.py`.
O parser aceita valores de 90 a 2880 graus; não interpreta outra unidade como
rotação. A implementação deste app foi escrita para esse contrato, sem incluir
código do TinyPedal no executável ou no pacote de fontes.

Consulta em segundo plano a cada 5 s, timeout de 900 ms, resposta limitada a
1 MiB, sem proxy, redirecionamento ou credenciais. Cache por sessão, jogador e
carro, com validade de 30 s; resposta de sessão antiga é ignorada. A falta do
endpoint não impede o movimento normalizado. Rotação total manual tem prioridade
apenas na representação do modo JOGO; não substitui a calibração física.

## Punições, carros e pneus

O canal opcional é `ws://127.0.0.1:6398/websocket/ui`; o app envia somente a
assinatura `{"messageType":"SUB","topic":"LiveStandings"}`. Contrato verificado
no código do [LMUSessionTracker](https://github.com/mbeader/LMUSessionTracker):
`Common/LMU/WSStanding.cs`, `WSPenalties.cs`, `WSMessage.cs` e
`Common/Http/WebSocketLMUClient.cs`. A interface é local e não documentada como
API estável oficial do jogo; mudanças no LMU podem indisponibilizar os detalhes.

Pacotes UPDATE/LiveStandings são listas com slotID, driverName, vehFilename,
penalties.DT/SG/TIME e os nomes de compostos. Outros tópicos/ACK são ignorados,
inclusive quando o corpo tem formato diferente. Limite 2 MiB, 256 entradas;
timeout de conexão 2 s e de mensagem 5 s, cache 3 s, nova tentativa após 3 s.
Troca de sessão invalida o cache; versões antigas de respostas são descartadas.
Vínculo exige ID+nome exatos, e nome de arquivo do carro quando fornecido;
duplicatas não transferem informações. Sem canal, a contagem nativa permanece.
Se mNumPenalties zerar, a punição some imediatamente mesmo com cache anterior.
T indica punição de tempo; o app não inventa segundos, voltas para cumprir ou
quantidade por tipo. DT+SG combina tipos quando ambos são positivos.

Compostos nativos do jogador têm prioridade; os demais usam o canal acima.
Cache vencido limpa pneus recebidos pelo canal em vez de perpetuar dados antigos.
Letras/cores: Soft=S cinza #AAAAAA, Medium=M amarelo #FFCC00, Hard=H vermelho
#EE2200, Wet=W azul #00AAFF. Esse mapa segue os compostos LMU no
[mapeamento do TinyPedal](https://github.com/TinyPedal/TinyPedal/blob/master/tinypedal/template/setting_compounds.py),
consultado em 13/09/2026; não usa a convenção da Fórmula 1. Dianteiro/traseiro
diferentes mostram as duas letras. Não é inferida a marca do fabricante do pneu.
Índice numérico sem nome não é tratado como composto conhecido.

Logotipos de fabricantes são recursos locais, sem consulta de imagem durante
a corrida. Só nomes/modelos reconhecidos geram correspondência; desconhecidos
usam carro genérico. Fontes e atribuições: docs/ASSETS.md.

## Bandeiras

Nacionalidades são opcionais. O app faz somente GET ao endereço local fixo
`http://127.0.0.1:6397/rest/multiplayer/teams`. A estrutura, incluindo os campos
`nationality`, `drivers`, `teams` e chaves de slot `utid<ID>`, está documentada no
código do [LMUSessionTracker](https://github.com/mbeader/LMUSessionTracker), em
Common/Http/HttpLMUClient.cs, Common/LMU/MultiplayerTeams.cs, MultiplayerDriver.cs,
MultiplayerTeam.cs, MultiplayerTeamMember.cs e Core/Tracking/EntryList.cs.

O vínculo usa slot+nome exato ou nome exato único no grid. Não há consulta a
contas externas nem inferência de país pelo nome. Na ausência de nacionalidade,
com endpoint indisponível ou nome ambíguo, aparece um globo neutro. Isso também
vale para bots e sessões solo: eles aparecem mesmo sem bandeira.

Consulta em segundo plano a cada 10 s, timeout de 900 ms, resposta limitada a
1 MiB, sem proxy, redirecionamento ou credenciais. Cache expira em 30 s e troca
de sessão o limpa. Uma falha não bloqueia a leitura dos pedais/tabela.
Bandeiras PNG estão embutidas no executável, derivadas de
[flag-icons](https://github.com/lipis/flag-icons), licença MIT em third_party.

## Janelas e configurações

Duas janelas independentes, com fundo em alfa e texto/valores preservados.
[UpdateLayeredWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow)
apresenta um bitmap com alfa pré-multiplicado. No modo travado,
WS_EX_TRANSPARENT/WS_EX_NOACTIVATE deixam o mouse e o foco no jogo.
A configuração usa controles nativos do Windows e salvamento local automático.

Na 0.3, `LayeredSurface` mantém um
[DIB de 32 bits](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection)
e um DC por painel, recriados somente quando muda o tamanho ou há falha.
O acesso GDI é sincronizado antes do desenho sobre a memória e da apresentação.
Erros transitórios de apresentação são registrados e tentados novamente após
0,2 s, 0,4 s, 0,8 s, 1,6 s e, no máximo, 2 s. Uma apresentação bem-sucedida
reinicia essa espera. Mudanças de monitor, DPI e composição recriam a superfície.

Enquanto um overlay está habilitado e não foi ocultado pelo atalho, a janela
é colocada no topo sem ativação a cada 250 ms, somente com o LMU ou o próprio
app em primeiro plano, usando SWP_NOACTIVATE e SWP_NOOWNERZORDER em
[SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos).
A restauração por ShowWindow só acontece se a janela estiver minimizada.
A configuração fica no topo quando está ativa e libera essa posição ao entrar
no jogo. O comportamento é voltado ao jogo em janela sem bordas; não depende de
injeção no processo do LMU.

X oculta a configuração e mantém o aplicativo ativo; minimizar deixa o painel
na barra de tarefas. A ação de saída é “Encerrar o app” na bandeja e no cabeçalho.
`PopupInteraction` pausa o timer visual e os eventos de pintura enquanto o menu
está aberto e por 400 ms após fechar. A leitura continua em segundo plano.
O mesmo bloqueio visual protege a captura modal de teclas. Fora do foco do app
ou LMU, a interface não disputa a frente com a bandeja do Windows. O HWND
informado pelo jogo tem prioridade; há fallback pelo processo Le Mans Ultimate.
Somente duplo clique esquerdo no NotifyIcon reabre o painel, conforme a
identificação do botão em
[MouseDoubleClick](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.mousedoubleclick).
Uma segunda
abertura do executável envia uma mensagem registrada à instância existente para
reabrir a configuração. `AppLog` registra abertura, fechamento e falhas, com
limite de tamanho e deduplicação de erros repetidos.

[RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
registra o atalho escolhido com MOD_NOREPEAT. A captura só acontece no diálogo
ativo; não há hook global ou registro das teclas digitadas. Um novo atalho só
substitui o anterior depois de registrado com sucesso. Ctrl+Alt+O e Ctrl+Alt+L
continuam reservados para abrir o painel e posicionar/travar; F12 é reservada
pelo sistema. Conflitos são informados na janela.

## Compilar e verificar

Windows x64 com .NET Framework 4.8: COMPILAR.cmd, sem baixar dependências.
Alternativa: dotnet build LMUOverlay.csproj -c Release (restore NuGet).
Fixture independente do parser: python tools/make_fixture.py.
Testes: dotnet run --project tests/Checks.csproj -- tests/lmu-fixture.bin.
O segundo argumento opcional dos testes é uma pasta para renderizar as prévias.
PreviewData.cs existe apenas no projeto de testes, excluído do executável.

Instalador: após compilar o aplicativo, execute
`makensis installer/Setup.nsi` com NSIS 3 Unicode. O resultado é
`Instalar-Arts-LMU-Overlay-0.8.exe` na raiz do projeto. Ele instala para o usuário em
`%LOCALAPPDATA%\Programs\ArthurLMUOverlay`, verifica Windows x64/Framework 4.8,
cria atalhos na Área de Trabalho/menu Iniciar e registra o desinstalador em HKCU.
O nome visível passa a Arts LMU OVERLAY; pasta, mutex, executável interno e
chave de desinstalação permanecem compatíveis com a instalação anterior.
Os atalhos antigos criados pelo instalador são substituídos pelos novos.
Solicita encerrar a instância ativa antes de atualizar; não força o fechamento
nem altera associações de arquivo do Windows. O antigo INSTALAR.cmd foi retirado.
Desinstalação remove os arquivos instalados e preserva a pasta de preferências
`%LOCALAPPDATA%\ArthurLMUOverlay`. Licença do NSIS acompanha o instalador.

O app não tem modo de demonstração ou argumento para ativar dados fictícios.
A cada 5 s, grava diagnostico.txt na configuração local, com versão, contagens,
fases, motivo da identificação, entradas normalizadas, taxa observada de
amostras e estado de apresentação, sem lista de nomes. `erro.txt` registra
eventos do ciclo de vida e falhas, mesmo quando a configuração está oculta.
Os testes sintéticos não substituem executar o WinForms e o jogo no Windows.

## Revisão 0.8: atualização e painéis adicionais

`WheelPump` possui um trabalhador dedicado. Criação, enumeração, propriedades,
leitura e liberação de DirectInput acontecem nesse trabalhador. O controlador
só lê a última referência publicada. Snapshot físico de mais de 300 ms é
descartado para permitir o fallback da direção. Não há enumeração a cada três
segundos na UI: dispositivos conectados são reavaliados por WM_DEVICECHANGE,
calibração ou falha de leitura; enquanto nenhum está conectado, a tentativa é
a cada cinco segundos no trabalhador. Gravação de diagnóstico também sai da UI.

A tabela renderiza apenas a classe do usuário, com a mesma seleção de vizinhos
e líder. O alcance da melhor volta no cabeçalho passa a ser sempre essa classe.
O gráfico mantém os últimos 15 segundos, com o buffer circular já existente.

| Instrumento adicional | Fonte e regra |
|---|---|
| Volta atual | mElapsedTime − mLapStartET do carro local |
| Última/melhor/voltas | scoring do ID local |
| Previsão | melhor + mDeltaBest, só com melhor válida e volta não invalidada |
| Pneus | mWheels[0..3], dianteiro esquerdo/direito e traseiro esquerdo/direito |
| Temperatura | média mTemperature[0..2] em Kelvin, menos 273,15 → °C |
| Pressão | mPressure em kPa / 100 → bar, duas casas |
| Cor térmica | apenas com mOptimalTemp válido: abaixo de ótimo−15 azul, acima de ótimo+15 vermelho, faixa central verde; sem ótimo: branco |
| Consumo | diferença do combustível entre cruzamentos observados; média móvel de até 5 voltas, excluindo entrada parcial, pit/refuel, lacunas >1 s e saltos |
| Autonomia | combustível / consumo, disponível depois de uma volta completa observada |
| Combustível até o fim | estimativa: voltas restantes × média; corrida por tempo usa floor(restante / melhor volta) + 1; não garante margem nem substitui estratégia |
| Mapa | mPos.x/z e mLapDist de carros da classe; 720 intervalos, atualização a 5 Hz, sem ligar trechos desconhecidos ou incluir pit lane |

Mapa disponível progressivamente na sessão: não vem de texturas/contornos do
jogo, não é persistido entre sessões e não inventa um circuito predefinido.
O marcador mostra a posição na classe; jogador azul com numeral branco.
Os quatro painéis adicionais começam desativados e seguem a opacidade das
informações e os mesmos atalhos/posicionamento. Campos ausentes mostram —.
