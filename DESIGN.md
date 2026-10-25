---
version: alpha
name: Arts LMU OVERLAY
description: Instrumentos compactos de corrida para leitura periférica durante uma sessão de LMU.
colors:
  background: "#0C121B"
  surface: "#182230"
  grid: "#2C3D50"
  text: "#FFFFFF"
  muted: "#C5CFDD"
  throttle: "#38E58C"
  brake: "#FF5368"
  rpm: "#89B9FF"
  fuel: "#F4C56B"
  player: "#1D3E6C"
  tyresoft: "#AAAAAA"
  tyremedium: "#FFCC00"
  tyrehard: "#EE2200"
  tyrewet: "#00AAFF"
typography:
  display:
    fontFamily: "Bahnschrift, Segoe UI, sans-serif"
  body:
    fontFamily: "Segoe UI, sans-serif"
  data:
    fontFamily: "Consolas, monospace"
rounded:
  DEFAULT: "22px"
spacing:
  panel-padding: "16px"
  scoring-row: "32px"
components:
  pedal-chart:
    height: "166px"
  standings:
    width: "900px"
---

# Arts LMU OVERLAY

## Overview

North Star: os recortes enviados por Arthur, com as revisões solicitadas em
12–13/09/2026. App local Windows, pt-BR, simples e legível durante sim racing.
A tarefa é enxergar frenagem, giro, direção e posição com leitura periférica.
Não há superfície de marketing nem escopo Japão.

Assinatura atual (0.12): gráfico com traços verde/vermelho e arco segmentado de RPM,
volante personalizado rotativo, classificação compacta com bandeiras e ganhos/perdas.
Identidade aprovada: nome Arts LMU OVERLAY e logo original enviada pelo usuário
no cabeçalho da configuração. O volante usa desenho nativo inspirado na foto:
fibra escura, empunhaduras abertas, botões vermelhos e seletor vermelho, sem
emblema Porsche. A arte permanece transparente e nítida ao redimensionar.
A cor dos pedais não muda de significado. As imagens de bandeiras representam
apenas nacionalidades informadas pelo jogo; ausência usa símbolo neutro. Na
revisão 0.6, os ícones de fabricante preservam uma cor de marca de alto
contraste. Na revisão 0.7, a tabela também ganhou hierarquia tipográfica sem
mudar colunas: dados críticos brancos/tabulares, cabeçalhos espaçados e linhas
mais arejadas. Os compostos de pneu usam badges circulares compactos: contorno,
preenchimento sutil e letra usam a mesma cor S/M/H/W; quando dianteiro e
traseiro divergem, os dois círculos aparecem em ordem esquerda/direita.

`src/Renderer.cs:Theme` possui os quatorze tokens de cor; este documento os espelha,
verificados por tools/check_design.py. Painter adapta os mesmos tokens aos
painéis e WindowsApp aos controles nativos. Não há carregamento de fonte por rede.

## Colors

Fundo carvão, separadores discretos. Verde=acelerador/ganho; vermelho=
freio/perda/limite do motor; âmbar=combustível/atenção; azul=seleção do jogador.
O arco de giro passa de verde a amarelo e vermelho; o aviso traz texto TROQUE.
Estados têm rótulos, símbolos e valores, sem depender somente de cor.
Fundo dos pedais e tabela começa em 92% de opacidade. Valores independentes
ou vinculados pelo botão, de 20 a 100%. Texto e traços preservam seu próprio alfa.
Transparência alta naturalmente faz o cenário atrás influenciar o contraste.

## Typography

Segoe UI para nomes/rótulos; Consolas para voltas, RPM e marcha. Dados críticos
brancos e fortes: piloto/gap 20 px, POS 22 px e tempos 18 px. Cabeçalhos têm
11,5 px e +0,055em. Linhas de 32 px; opacidade dos glifos independente do fundo.
Sombra preta curta mantém a separação sobre o cenário do jogo. Percentuais dos
pedais têm 26 px, velocidade 35 px e RPM 32 px. Bahnschrift dá identidade aos
títulos da configuração, com fallback local.
Texto e volante mantêm a proporção própria ao mudar a forma do painel. Na 0.9, a escala das letras é independente das dimensões do card e ajustada para caber quando o espaço fica pequeno.
Campos têm largura estável; nomes longos recebem reticências. Fontes usam fallback
local quando a família não estiver disponível no ambiente de renderização.

## Layout

Painéis principais: pedais 760×280; tabela 900 px de largura com altura automática
até o usuário redimensionar. As quatro bordas mudam largura/altura
separadamente; os quatro cantos mudam ambas, sem travar a proporção. As dimensões escolhidas ficam salvas.
Somente a classe do usuário: jogador + três pilotos à frente + três atrás,
somando o líder no topo somente se estiver fora desse grupo. Máximo de oito;
nas extremidades, não completar o grupo com vizinhos mais distantes.
As demais classes ficam ocultas. A opção antiga foi substituída por essa regra
fixa a pedido do usuário; classe única não cria grupos fictícios.
Não há paginação. Largura e altura respeitam limites pequenos de operação e o monitor; o tamanho de origem não é um mínimo.
Cabeçalho junto à pista mostra melhor volta válida, autor e posição atual,
considerando somente a classe própria; sem volta válida usa —.

Configuração nativa com rolagem quando necessária. Cada painel tem Fonte e tamanho, com família dos textos, escala das letras, largura e altura. Sem campos X/Y,
demonstração, páginas, botão Como usar ou botão Minimizar para jogar.
Posicionar permite arrastar/redimensionar, com alternativa por teclado: setas,
Shift+setas e Esc. Visibilidade por painel e atalho global configurável.
O cabeçalho de 108 px reúne avatar da logo, nome, versão e Encerrar o app.
A configuração tem navegação lateral: Painéis, Pilotagem, Atalhos e Aprendizado.
Painéis reúne os seis cards em pares, cada um com Ativado e Fonte e tamanho.
A faixa inferior mantém estado do LMU e salvamento visíveis durante a rolagem.
Na 0.5, a correção fixa de 50% foi substituída por leitura direta do dispositivo
e calibração guiada: centro, 90° e validação a 180°. A identificação fica
vinculada ao volante/eixo; ausência ou desconexão cai temporariamente para a
direção do jogo quando há telemetria, evitando que o desenho congele. O campo
Rotação máxima do meu volante (0 = calibração) permite remapear um hardware
substituído; Rotação máxima do carro (0 = LMU) controla o fallback/modo JOGO.
A rotação física não é invalidada quando o LMU troca a faixa de esterço do carro:
graus do hardware e graus do carro são escalas independentes. A direção por
telemetria continua disponível como modo explicitamente JOGO.

## Elevation & Depth

Janelas topmost com superfície DIB persistente, alfa pré-multiplicado e
UpdateLayeredWindow, sem blur ou sombra decorativa. A cada 250 ms, a posição
acima do jogo é reafirmada sem ativar as janelas, respeitando a visibilidade
escolhida pelo usuário. Falhas transitórias de apresentação recriam a superfície
com espera progressiva, sem encerrar o aplicativo. Modo travado usa click-through
e não toma foco do jogo.
Modo de posicionamento aceita foco e teclado. Sem SetLayeredWindowAttributes
ou Form.Opacity, que conflitam com a apresentação por pixel.

## Shapes

Painéis com cantos de 22 px; linhas de 1 px; LEDs e barras com raio discreto.
Volante vetorial sem marca, com centro e geometria preservados mesmo quando
altura e largura do painel diferem. Modo físico usa o eixo calibrado; modo jogo
usa a entrada e faixa total conhecida, ou porcentagem quando a faixa falta.
Marcha amarela e rótulo LIMITADOR indicam o limitador de velocidade dos boxes.
Gráfico de segmentos contínuos sem preenchimento nem spline que distorça os
valores. A primeira amostra começa à esquerda; depois de preencher o período,
a janela desliza para preservar os últimos segundos.
Controles Windows preservam foco, clique, seleção e rolagem nativos.

## Components

### Canonical UI Map

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
|---|---|---|---|---|
| Form | SettingsUi; SettingsDesign; WindowsApp.Controller; PanelAppearanceDialog; LearningWindow | DESIGN.md; AppSettings; docs/APRENDIZADO.md | native: números limitados, opções rotuladas, vínculo, captura modal de atalho; Aprendizado modeless | Compilação; execução Windows pendente |
| Select/Listbox | SettingsUi.Select; PanelAppearanceDialog.family; LearningWindow | DESIGN.md | native: ComboBox DropDownList; fonte, sessão/pilotos/voltas/trechos; geometria e teclado Windows | Compilação; execução Windows pendente |
| Scrollbar | SettingsPage.AutoScroll; LearningWindow.Grid | DESIGN.md | native: rolagem na configuração; tabelas de aprendizado com rolagem própria | Execução Windows pendente |
| Tabela | RaceView; Painter.Standings | docs/TELEMETRIA.md | Somente classe própria, líder fixo, grupo do usuário | tests/Checks.cs; renders |
| Status | Frame.Status; DataFreshness | docs/TELEMETRIA.md | Espera, pausa, leitura parcial; nunca substitui por demonstração | tests/Checks.cs |
| Overlay | OverlayWindow; OverlayVisibility | DESIGN.md; docs/TELEMETRIA.md | Só na pista com leitura ativa; Posicionar permite edição fora da pista; travado passa cliques | tests/V092Checks.cs; composição nativa pendente |
| Atalho | Controller.SetVisibilityKey; KeyCapture; OverlayVisibility | docs/TELEMETRIA.md | F8 padrão, configurável; só alterna na pista; conflito mantém vínculo anterior | tests/V092Checks.cs; registro Windows pendente |
| Instalação | installer/Setup.nsi | LEIA-ME.txt | Instalador Windows por usuário, atalhos e desinstalador; preserva configurações | Compilação NSIS; execução Windows pendente |
| Movimento | InputMotion; InputCapture; PedalDesign; Painter.Pedals | docs/TELEMETRIA.md | Transição visual curta e desativável para barras/volante; histórico conserva entradas | tests/Checks.cs; renders |
| Identidade | AppInfo; BrandArtwork; BrandHeader | assets/BrandLogo.png; DESIGN.md | Logo original no cabeçalho, ícone derivado e nome Arts LMU OVERLAY | Recurso embutido; render do cabeçalho |
| Giro calibrado | WheelCalibration; WheelInput; Instruments | AppSettings.WheelCalibration; AppSettings.WheelMaximumRotation; AppSettings.CarMaximumRotation | Dispositivo/eixo, centro e graus físicos; validação além de 90°; campos de rotação e fallback de telemetria; modo jogo separado | tests/Checks.cs; integração DirectInput Windows pendente |
| Calibração | WheelCalibrationDialog | AppSettings.WheelCalibration; docs/TELEMETRIA.md | Modal nativo com Cancelar; erro persistente, desconexão explícita e validação centro/90°/180° | Testes da conversão; operação nativa pendente |
| Detalhes de pilotos | PenaltyMessage; CarIdentity; TyreCompound | docs/TELEMETRIA.md; docs/ASSETS.md | Fabricante reconhecido, composto informado e punição pendente; sem inferência de dados faltantes | tests/V05Checks.cs; renders |
| Delta | DeltaTracker; DeltaRepository; DeltaMotion; Painter.DeltaBar; DeltaSettings | docs/DELTA-SECTOR.md; AppSettings | Transparente; três referências; sinal e cor; mesma distância; sete aparências | tests/V012Checks.cs; preview; Windows pendente |
| Treino por trecho | SectorEngine; SectorProtocol; SectorRuntime; SectorSettings | docs/DELTA-SECTOR.md; runtime original 1.0.0 | Ativação explícita; treino offline; F6/F7; status confirmado; parada cooperativa | tests/V012Checks.cs; LMU nativo pendente |
| Painéis adicionais | Painter.Widget; RaceWidgets | docs/TELEMETRIA.md | Lap timer, pneus, combustível e mapa; opcionais, dados ausentes explícitos | tests/V08Checks.cs; renders 1× |
| Menu de saída | Controller.trayMenu; PopupInteraction | DESIGN.md | Botão direito abre menu; duplo clique esquerdo abre painel; pausa visual durante popup | Testes da política; interação nativa Windows pendente |
| Aprendizado | LearningWindow; LearningPainter; LearningComparison | docs/APRENDIZADO.md | Histórico local, mesma sessão, setores oficiais e microsetores estimados, lacunas explícitas | tests/V010Checks.cs; renders; Windows/LMU pendente |
| Pneus e danos | Painter.TyresAndDamage; VehicleDamageService | docs/DANOS.md | Temperatura/pressão, pneu restante, dano por suspensão e aero total; desconhecido explícito | tests/V010DamageChecks.cs; renders; Windows/LMU pendente |

Dados ausentes viram travessões. Em treino, classificação e aquecimento, GAP
compara a melhor volta de cada piloto à melhor volta do usuário: negativo é
mais rápido; positivo, mais lento. Sem duas melhores voltas válidas, exibe —.
Na corrida, mantém diferença para o líder geral. PEN. informa punições pendentes:
DT/SG/T quando o canal local detalha o tipo; quantidade quando só há contagem.
Punição cumprida some assim que a contagem nativa fica zerada.
Posições ganhas/perdidas precisam de captura pré-largada; não se presume o grid
com o usuário entrando depois. Bandeiras desconhecidas usam globo neutro.
Pneus têm ícone e letra: S cinza, M amarelo, H vermelho, W azul, conforme o mapa
LMU documentado em docs/TELEMETRIA.md, e não o padrão de cores da Fórmula 1.
Compostos dianteiro/traseiro diferentes aparecem em par; ausentes usam —.
Logotipos indicam apenas fabricantes reconhecidos pelo nome/modelo publicado;
fabricante sem correspondência usa carro genérico, sem uma marca inventada.
Os PNGs embutidos são rasterizações locais de Simple Icons (CC0, com marcas
preservadas) e do conjunto car-logos-dataset para Alpine/Lexus; não são texturas
extraídas do LMU. A telemetria fornece nome/modelo, mas não oferece uma API pública
estável para baixar logotipos de equipes dentro do jogo. Extrair texturas do
instalador seria frágil a atualizações e não é necessário para o overlay local.
O gráfico conserva as amostras adquiridas sem suavização. As barras e o volante
do modo JOGO usam uma transição exponencial curta (35 ms), desativável nos ajustes,
sem ultrapassar a entrada. O volante físico usa diretamente o eixo calibrado.
Telemetria e DirectInput têm trabalhadores independentes da interface; intervalos
solicitados de 8 ms para leitura e 16 ms para desenho, sujeitos ao jogo/Windows.
O aviso de troca pode pulsar a 2 Hz e esse pulso pode ser desligado.
Não há som ou animação decorativa.

Botões e campos usam nomes acessíveis. X oculta apenas a configuração e mantém
os overlays ativos; minimizar mantém o painel na barra de tarefas. O menu da
bandeja e o cabeçalho possuem a ação explícita Encerrar o app. Abrir o atalho novamente traz
o painel da instância existente. A configuração fica acima dos overlays quando
está ativa e deixa essa posição quando o jogo recebe foco.
O menu da bandeja mantém o foco: o timer não desenha nem reposiciona janelas
enquanto o menu está aberto e nos 400 ms seguintes ao fechamento. Somente
duplo clique esquerdo abre a configuração. O app não disputa a frente com
o menu de ícones ocultos do Windows ou outros aplicativos. A aquisição de
telemetria continua em segundo plano durante essas interações.
O erro de salvar/atalho fica na configuração; erro não recuperável exibe diálogo
nativo e fecha o app. Falhas recuperáveis de leitura/desenho tentam novamente.
Na 0.3.1, pacotes inválidos durante a entrada na sessão e lobby sem nacionalidades
são tratados pelos serviços em segundo plano. O painel indica espera por dados
válidos e pilotos sem nacionalidade ficam com símbolo neutro. Uma resposta de
bandeiras ausente não encerra os instrumentos. Erros fora da interface também
ficam registrados no diagnóstico local.
A migração preserva posições, atalho e calibração; adota 10 segundos na revisão 0.11, classe
própria, fundos de pelo menos 88% e altura compacta dos painéis.

## Visibilidade automática — revisão 0.9.2

Decisão de Arthur em 26/09/2026: ocultar overlays durante leitura pausada, menu
e garagem; manter o F8 na pista e todas as posições e dimensões escolhidas.
Os seis cards usam OverlayVisibility. Carro parado em modo de pilotagem continua
visível. A política é aplicada também à manutenção de topmost; ela não pode
reabrir cards ocultos. Menu, pausa, perda de telemetria ou desconexão ocultam;
retornar à pista com leitura ativa restaura somente os cards ativados.

F8 é o padrão; apenas o padrão anterior Ctrl+Alt+H é migrado. Outro atalho
personalizado permanece. Fora da pista ou durante Posicionar, o atalho é ignorado
sem alterar a escolha manual. Ocultar manualmente permanece após visitar o menu;
um novo atalho na pista mostra. A edição Posicionar exibe cards ativados mesmo
offline, preservando a escolha manual quando a edição termina. As dimensões,
fontes, opacidades e posições não são regravadas pela visibilidade automática.

Configuração e bandeja usam o mesmo rótulo Mostrar / ocultar na pista. A ação
da bandeja fica desabilitada com o motivo no texto quando indisponível. As opções
por painel passam a Ativado / Mostrar na pista para distinguir a preferência
salva da ocultação temporária. Paleta, colunas, tipografia e layout permanecem.

## Do's and Don'ts

- Preservar significado de acelerador/freio e as amostras no histórico.
- Limpar o gráfico ao vivo ao mudar de sessão/carro ou disponibilidade de telemetria; conservar as voltas salvas no Aprendizado.
- Exibir o mesmo leitor em treino, classificação, aquecimento e corrida.
- Ajustar ambos os fundos juntos somente quando o vínculo estiver ativo.
- Usar dados artificiais apenas no projeto de testes, excluído do app.
- Não afirmar teste dentro do LMU a partir de fixture ou render.

## Revisão 0.8 — decisão de Arthur em 20/09/2026

As novas referências de iRacing orientam uma apresentação compacta, com números
maiores, fundo carvão uniforme e cantos de 18 px. Somente a classe do jogador
é exibida: líder mais três à frente, jogador e três atrás. Essa regra substitui
a apresentação multiclasse das revisões anteriores. Colunas e sua ordem são
preservadas; o espaço antes gasto com explicações passa para a leitura dos dados.
A tabela tem 900 px, linhas de 32 px, nomes e gaps de 20 px, tempos tabulares
de 18 px e posição de 22 px. Nomes longos podem abreviar o primeiro nome.
Pedais 760×280: percentuais de 26 px, velocidade de 36 px, RPM de 32 px;
histórico de 15 segundos. Os mínimos de redimensionamento preservam essas letras.
A migração eleva fundos antigos a pelo menos 88%; continuam configuráveis.

Lap timer, pneus, combustível e mapa são opcionais e usam o mesmo OverlayWindow,
a mesma ação Posicionar, o mesmo atalho e a opacidade do painel de informações.
O mapa aprende o traçado real da classe na sessão; lacunas nunca são conectadas
por linhas inventadas. Temperatura é a média dos três sensores superficiais,
convertida de Kelvin para Celsius; pressão em bar. Consumo usa voltas completas
observadas e descarta entrada parcial, parada/refuel, saltos e reinícios. Valores
indisponíveis são travessões, previsões são rotuladas como estimativas.

WheelPump é o único proprietário de DirectInput: enumeração por conexão ou
reconexão, fora da interface e fora do leitor de telemetria. Diagnósticos são
gravados em segundo plano. Um driver lento não pode bloquear pedais nem a UI.
Verificação: fixtures SDK, regressão de driver bloqueado, renders 1× e compilação.
Integração nativa LMU/Windows depende de teste na máquina do usuário.


## Revisão 0.9 — ajuste por painel solicitado em 26/09/2026

Cada um dos seis cards possui Fonte e tamanho. SettingsUi mantém números, botões
e rótulos consistentes com as opções já existentes. PanelAppearance é a fonte
única dos ajustes: Segoe UI, Arial, Tahoma, Verdana, Trebuchet MS ou Consolas nos
textos, 60–200% nas letras. Dados tabulares continuam Consolas. O ajuste é em
pixels físicos, independente da largura/altura, e não altera os demais cards.
Aumentar as letras amplia o card quando necessário e quando o monitor permite;
Ajustar painel às letras define as dimensões sugeridas. Diminuir um painel não
pode esconder dígitos de um tempo: o renderer reduz o tamanho para caber e limita
o desenho à célula. Nomes extensos continuam abreviados. Não há nova hierarquia,
coluna ou alteração da paleta.

A trava da 0.8 (mínimo igual ao tamanho original) foi removida. Os mínimos de
operação são 35% do tamanho original, respeitando pelo menos 128×80 px e o monitor.
As oito alças de posicionamento são visíveis; bordas e cantos, roda do mouse e
Shift+setas usam as mesmas dimensões persistentes. Perder a captura do mouse
conclui o arraste. Largura e altura também podem ser digitadas no editor.

O editor nativo aplica e salva automaticamente, fecha por Concluir/Esc e oferece
Restaurar padrão apenas para fonte e dimensões daquele card; posição, visibilidade,
atalhos, opacidade, calibração e telemetria são preservados. Schema 8 migra a 0.8
sem reposicionar ou mudar dimensões, criando aparências independentes em 100%.


## Revisão 0.9.1 — diagnóstico de abertura

O pacote volta à compilação Microsoft Roslyn/.NET Framework 4.8 da 0.8. O gate
verifica também o manifesto dentro do PE e dentro do instalador. Falhas anteriores
ao loop de mensagens exibem mensagem nativa com erro e caminho do diagnóstico;
sem alteração de layout ou das preferências do usuário. A causa no Windows
do usuário só pode ser confirmada com execução local ou o erro enviado por ele.

## Revisão 0.10 — aprendizado local

Decisão de Arthur em 27/09/2026: comparar voltas próprias e tempos dos adversários
sem precisar dos pedais deles. LearningWindow é uma janela nativa de análise,
aberta pelo botão Aprendizado ou pela bandeja. Usa os mesmos tokens e SettingsUi,
com histórico, setores oficiais e comparação por distância. Verde/− é ganho de A;
vermelho/+ é perda de A; cinza é trecho sem dados. Delta acumulado e velocidade
complementam o mapa, com seleção sincronizada por clique, tabela ou teclado.

Microsetores são identificados como estimados, nunca como splits oficiais.
Voltas inválidas, parciais e interrompidas permanecem no histórico. Referência
automática exclui invalidada e boxes. Gravação e comparação trabalham fora do
leitor e da interface. Histórico é local e reabre após reiniciar; decisões e
limites estão em docs/APRENDIZADO.md. O módulo de aprendizado não muda o layout,
as preferências ou a visibilidade dos overlays existentes. Schema permanece 9.

## Revisão 0.10 — pneus e danos

Decisão de Arthur em 28/09/2026: integrar danos junto à temperatura dos pneus.
O card opcional Pneus passa a Pneus e danos, com tamanho de desenho 420×402:
quatro blocos de roda e uma silhueta central para o dano aerodinâmico total.
Temperaturas de 27 px; pneu restante de 18 px; danos de 17/21 px. Mantém os
mesmos tokens, fonte configurável, posicionamento, opacidade e visibilidade.
Dimensões manuais antigas são preservadas; Ajustar painel às letras oferece
o novo tamanho sugerido. Nenhum outro card é redimensionado ou reposicionado.

PNEU é restante, com 100% novo; SUSP. e AERO são dano, com 0% sem dano informado.
Ausência é travessão, com rótulo de espera. Furo e roda/peça solta têm texto.
Não se infere dano pela temperatura ou curso da suspensão. A fonte confirmada
publica aero geral, sem valores independentes de asa dianteira/traseira; a
limitação permanece explícita no card. A leitura local opera em segundo plano,
com uma requisição por vez, expiração e espera após erro, sem bloquear pedais.


## Revisão 0.11 — produto de corrida

Decisão de Arthur em 08/10/2026: liberdade para uma apresentação mais profissional;
RPM em arco no volante, retirar combustível e rótulos de acelerador/freio do
card principal, freio à esquerda, acelerador à direita, últimos 10 segundos.
Esta revisão prevalece sobre as descrições históricas de 15 segundos e LEDs
horizontais. A tabela continua com todas as colunas e a seleção da classe própria.

Direção: instrumentos de um cockpit, com azul frio da identidade Arts, carvão
azulado e contraste alto. O elemento principal é o arco de 260° com 28 segmentos
que circunda o desenho do volante original. Ao redimensionar livremente,
arco e volante usam a mesma escala uniforme. A velocidade e a marcha formam
um bloco à direita; o gráfico de 337×166 px ocupa a esquerda. O combustível
é exibido apenas em seu card dedicado. Os percentuais continuam presentes.
Não há legenda de acelerador/freio no gráfico nem nas barras; vermelho e verde,
ordem física e percentuais identificam as duas entradas, conforme pedido.

Tema C# permanece a fonte canônica; DESIGN.md espelha os 14 tokens, conferidos
por tools/check_design.py. Bahnschrift é usado com moderação na marca e títulos
nativos; textos configuráveis seguem Segoe UI e números tabulares Consolas.
Falha da fonte usa o fallback do sistema. Não há dependência de fonte por rede.
Valores críticos continuam brancos; o azul claro é reservado à identidade,
foco e edição. As cores semânticas de pedais, composto e classe são preservadas.

Configuração: SettingsDesign compõe os mesmos controles nativos via SettingsUi.
SettingsCard é apenas superfície visual; não redefine a interação dos campos.
Cada SettingsPage possui sua própria rolagem. Estado e salvamento ficam fora
na faixa inferior. Troca de seção mantém valores/rolagem; Aprendizado abre a
janela existente. Seletor e campos numéricos mantêm popup e teclado Windows.
Não há alteração de captura de atalho, fechamento na bandeja, calibração,
leitura de telemetria ou dados de Aprendizado. Não existe demonstração no app.

Cada card preserva posição, dimensões, opacidade e fonte salvas. Schema 10
muda somente o período exibido para 10 s na migração da 0.10; o atalho padrão
anterior continua migrando somente para arquivos anteriores ao schema 9.
Os casos de uso e contratos de gravação/validação continuam nos documentos
APRENDIZADO.md, TELEMETRIA.md e DANOS.md.

Mudanças duráveis conferidas: cabeçalho e agrupamento da configuração,
fundos e tipografia compartilhados, cabeçalho de classificação, métricas
principais de lap timer/combustível e instrumento de RPM. Não foi substituída
a camada WinForms/DWM, que já funcionava na máquina do usuário.


## Revisão 0.12 — delta central e treino por trecho

Solicitação de Arthur em outubro de 2026: barra transparente com referência
selecionável, Sector embutido sem os overlays dele. Mantém os seis cards
aprovados; adiciona Delta de 460×90, fundo 0%, número tabular de 36 px,
rótulos de 12/13 px e barra com centro branco. Sinal e texto complementam cor.
O verde usa Theme.Throttle; o vermelho usa Theme.Brake. Não cria paleta nova.
DeltaSettings usa SettingsUi.Select e números nativos. Aparência e colocação
seguem o mesmo proprietário dos demais cards. Migração schema 11 amplia
arrays preservando todos os índices antigos; delta é sempre o último.

Treino por trecho é a quarta seção nativa da configuração. Há estados de
verificação, espera, conexão, ponto salvo, falha e desconexão. Conectar fica
desabilitado durante operação; não há repetição automática de ação de retorno.
F6/F7 atuam através do motor original do Sector com LMU em foco. Sair aguarda
parada cooperativa. O módulo inicia desligado, independentemente dos cards.
Limitação material exposta na própria tela: versão suportada, offline e
checkpoint somente da sessão atual. Sem promessas de compatibilidade nativa
não testada. Consulte docs/DELTA-SECTOR.md para contratos e recuperação.

Reconciliação: expansão intencional de seis para sete cards e de três para
quatro seções; nenhuma mudança na semântica/posição das seis superfícies
anteriores. Tokens e proprietários nativos preservados. A compilação e os
renders cobrem desenho; foco, DPI, DWM e LMU seguem pendentes no Windows.
