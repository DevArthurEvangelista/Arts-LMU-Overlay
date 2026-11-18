# Changelog — Arts LMU OVERLAY

Todas as mudanças documentadas do projeto estão consolidadas abaixo com base no código-fonte auditado e nos relatórios de validação em `docs/`.

> **Nota sobre validação:** diferenciamos explicitamente **testes automatizados** (executados pela suíte `tests/Checks.csproj`) de **validação real no LMU** (executada pelo piloto no Windows com volante físico).

---

## [0.13.4] — 2026-11-18

### Refinamento do `"PROCESSANDO"` Inteligente (Verde vs Amarelo/Laranja/Vermelho)
- **`TIRE O PÉ` somente quando há ganho de tempo / risco real (`src/DeltaTiming.cs`):**
  - Se você espalhar na curva e **perder tempo** (ou sair sem ganhar vantagem no Delta), o aviso já abre processando direto em **Verde (`[ ANÁLISE ] PROCESSANDO • OK`)**, **sem pedir para tirar o pé**.
  - Somente quando há ganho de tempo real no corte ou sinalização preliminar de invalidação é que o processamento abre em **Amarelo, Laranja ou Vermelho (`[ ANÁLISE ] PROCESSANDO • TIRE O PÉ`)**, mudando para `PROCESSANDO • ALIVIANDO` ao soltar o acelerador e caindo ao vivo para **Verde (`PROCESSANDO • OK` → `[ OK ] VOLTA VÁLIDA`)** no exato instante em que o tempo ganho é devolvido.

---

## [0.13.3] — 2026-11-18

### Novidades e Correções de Pista (Modo sem HUD do Jogo)
- **Aviso `"PROCESSANDO"` em Tempo Real (`src/Core.cs`, `src/DeltaTiming.cs`, `src/DeltaDesign.cs`):**
  - Detecta em 100 Hz quando o carro ultrapassa os limites da pista com as 4 rodas (`mPathLateral` vs `mTrackEdge` ou superfície fora de pista) e exibe imediatamente **`[ ANÁLISE ] PROCESSANDO • TIRE O PÉ`** em azul-ciano enquanto o pedal do acelerador segue pressionado, mudando ao vivo para **`PROCESSANDO...`** assim que você alivia o pé até sair o veredito oficial do Comissário do LMU.
  - Quando o Comissário publica `No Further Action` (`Resolution="7"` no `scoringStream`), o aviso muda na mesma hora para **Verde (`[ OK ] VOLTA VÁLIDA` / `SEM PUNIÇÃO`)**.
- **Separação Fiel entre Treino/Qualy e Corrida + Pontuação Exata 1:1:**
  - **Treino Livre, Classificação e Warmup:** suprime completamente qualquer exibição de pontos (`PTS`), mostrando apenas a invalidação de volta — exatamente como nas regras oficiais do LMU.
  - **Corrida:** lê diretamente o fluxo oficial `<TrackLimits WarningPoints="..." CurrentPoints="..." Resolution="...">` do `scoringStream` (e `mTrackLimitsSteps` em passos de `0,25`), exibindo com precisão de duas casas (`0,25`, `0,5`, `0,75`, `1`, `1,25` — sem arredondar `0,25` ou distorcer `1` ponto) tanto o acréscimo do corte quanto o acumulado (`+1 • 1,25/3`), além de anunciar **`DRIVE THROUGH` / `STOP & GO` / `PUNIÇÃO TEMPO`** quando o limite é atingido.

---

## [0.13.2] — 2026-11-17

### Novidades e Correções de Pista
- **Alertas Dinâmicos de Track Limits e Invalidação de Volta (`src/Core.cs`, `src/DeltaTiming.cs`, `src/DeltaDesign.cs`):**
  - Exibe alertas coloridos abaixo da barra de Delta conforme a severidade do LMU (**Amarelo** sob análise inicial, **Laranja** quando há acréscimo de pontos `mTrackLimitsSteps`, e **Vermelho** próximo ao limite de punição ou em corte de fim de volta que invalida `ATUAL + PRÓX.` / `PRÓX. INVÁLIDA`).
  - **Recálculo ao vivo ("tirar o pé"):** se o LMU reavaliar e devolver a validade da volta (`mLapInvalidated` volta para `false`), o aviso muda na mesma hora para **Verde (`[ OK ] VOLTA VÁLIDA`)** por 2,8 s e preserva os setores como válidos.
  - **Linha de chegada em volta invalidada:** quando uma volta termina invalidada, o resumo `[ VOLTA ]` da linha de chegada não é exibido.
- **Correção da Cor dos Setores (Roxo / Verde / Amarelo) e Delta Numérico:**
  - Removida a trava que mantinha a volta marcada como inválida mesmo após o LMU perdoar um *track limit* ao tirar o pé.
  - Corrigida a leitura do setor 2 de oponentes (evitando subtrair `mBestLapSector2` individual de `mBestLapSector1`, o que gerava referência artificialmente baixa e pintava setores verdes/roxos de amarelo).
  - Substituído o texto `"PESSOAL"`/`"MELHOR"` na 1ª volta por delta numérico (`-0,000 s`), mantendo sempre dois valores numéricos alinhados.

---

## [0.13.1] — 2026-11-17

### Correções de Validação em Pista
- **Setores e Volta no Delta muito maiores e com fonte única (`src/DeltaDesign.cs`, `src/WidgetRenderer.cs`):** faixa de setores ampliada (`418 × 50 px`), usando **exatamente a mesma fonte `Consolas` Bold em tamanho grande (`27 px`)** tanto para o tempo de pista (`31.842` / `1:42.318`) quanto para a diferença (`-0.184 s`), além de piso mínimo de legibilidade para cards compactos com `FontPercent` reduzido.
- **Correção do envio de texto no Realtime Chat do LMU (`src/VoiceChat.cs`, `src/WindowsApp.cs`):** `VoiceChatKeys` agora popula simultaneamente `wVk` e `wScan` no `SendInput` (compatível tanto com DirectInput para abrir/enviar o chat quanto com a caixa de texto Coherent GT do LMU via `WM_KEYDOWN`/`WM_CHAR`), e migra configurações antigas que estavam presas em `Ctrl+V` para digitação direta.
- **Correção do congelamento ao abrir *Fonte e tamanho* (`src/PanelAppearanceDialog.cs`, `src/WindowsApp.cs`):** resolvido o deadlock de Z-Order em que o painel principal (`TopMost = true`) encobria o diálogo modal `PanelAppearanceDialog` (`TopMost = false`), fazendo a janela parecer travada.

---

## [0.13.0] — 2026-11-17

### Novidades
- **Avisos Temporários de Setores (`S1`/`S2`/`S3`) e Linha de Chegada no Delta (`src/DeltaTiming.cs`, `src/DeltaDesign.cs`):** ao cruzar cada setor oficial da pista, um selo aparece por 4 s abaixo da barra do Delta com o tempo do setor e o ganho/perda colorido em **Roxo** (melhor da pista na sua classe), **Verde** (seu melhor na sessão) ou **Amarelo** (acima do seu melhor). Na linha de chegada, exibe por 5 s o resumo da volta completa (`MELHOR PISTA` / `SEU MELHOR` / `VOLTA`) + delta + `S3`.
- **Seletor de Arte do Volante 100% Vetorial (`src/WheelStyles.cs`, `src/SettingsDesign.cs`):** 8 modelos desenhados com *paths* subpixel anti-aliased selecionáveis na aba **Pilotagem > Visual do volante no overlay** com prévia giratória em tempo real:
  1. `GT Aberto (Padrão Arts)`
  2. `iRacing Clássico (Minimalista)`
  3. `Estilo G29 (Couro e Azul)`
  4. `Estilo G920 (Dark e Prata)`
  5. `Estilo MOZA / Monza (Fórmula KS/GS)`
  6. `Estilo Fanatec (ClubSport GT3)`
  7. `Estilo Thrustmaster (GT Competição)`
  8. `Estilo PXN (Sport D-Shape)`

### Correções Críticas
- **Delta Central Espelhado 1:1 com o LMU (`src/DeltaTiming.cs`):** o modo padrão *Melhor da sessão (espelho LMU)* passa a espelhar diretamente o campo nativo `TelemInfoV01.mDeltaBest` calculado pela física do LMU e ignora o filtro exponencial de 60 ms nesse modo, eliminando diferenças para mais ou para menos frente ao delta do próprio jogo. Também adicionada proteção contra falso `0.000` em volta de saída (*outlap*) e extrapolação de 100 Hz para os modos *Melhor pessoal* e *Última volta válida*.
- **Correção do Falso `LIMITADOR` em RPM Máximo (`src/Core.cs`):** removido `Telem_mSpeedLimiterActive` da condição de `PitLimiter`, pois o motor do LMU pulsa essa flag durante o corte de giro (*rev limiter*) com acelerador em 100%, o que fazia o indicador de marcha piscar amarelo como se o limitador de box estivesse ligado. Agora apenas `Telem_mSpeedLimiter != 0` aciona o `LIMITADOR`.

---

## [0.12.11] — 2026-11-09 / Consolidado no Git em 2026-11-17

### Novidades (ciclo `0.12.5` → `0.12.11`)
- **Chat por Voz Local com Whisper (`VoiceChat.cs`, `VoiceChatSettings.cs`, `VoiceStatusOverlay.cs`):**
  - Captura local de microfone via `NAudio 2.2.1` (até 12 s por gravação) acionada segurando um **botão do volante** ou uma **tecla vinculada** (`VoiceChatKey` + modificadores, bloqueando conflito com o atalho de visibilidade).
  - Transcrição local em segundo plano via `whisper-cli.exe` (`whisper.cpp`) em Inglês (`en`) ou Português (`pt`), com preenchimento padrão automático para `Whisper\whisper-cli.exe` e `Whisper\ggml-small.en.bin` e timeout de 90 s.
  - **Indicador visual na pista (`VoiceStatusOverlay`):** card arredondado sem roubar foco nem interceptar cliques (`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT`), exibindo estados *Ouvindo*, *Transcrevendo*, *Enviando*, *Concluído* e *Não enviado*.
  - **Dois modos de entrega ao chat (`VoiceChatDelivery`):** digitação automática via *scan codes* nativos do teclado ativo (respeitando `Caps Lock` e aguardando soltar modificadores) ou colagem via `Ctrl+V` (`VoiceChatPaste`), cancelando imediatamente se o jogo perder foco.
  - Botão **Abrir diagnóstico do envio** para inspecionar `erro.txt` diretamente pelo painel.

### Correções e Melhorias
- **Detecção de processo ativo (`GameRunning()`):** oculta imediatamente todos os overlays e encerra capturas de voz quando o processo `Le Mans Ultimate` deixa de existir.
- **Pré-visualização ao vivo de aparência (`controllerForeground` / `appearancePanelIndex`):** mantém os cards visíveis na pista enquanto o painel do Arts ou o diálogo de aparência de um card está em primeiro plano.
- **Estabilidade no `Alt+Tab`:** reforço de topo (`EnsureVisible`) restrito a quando o jogo está em primeiro plano, evitando sobrepor outras janelas do Windows.
- **Suíte de Testes (`tests/Checks.csproj` e `V01211Checks.cs`):** alinhada para `.NET Framework 4.8` (`x64`) no Windows e ampliada para **580 asserções automatizadas**.

---

## [0.12.4] — 2026-10-24
- **Sector (`SectorStatusReader.cs`):** leitura compartilhada de `status.json` com `FileShare.ReadWrite | FileShare.Delete` e tolerância de até 3 s para conflitos atômicos Win32 (`32`/`33`), evitando desconexão intermitente durante troca de status.
- **Testes:** 565 asserções automatizadas + 24 verificações de migração de configuração (`docs/VALIDACAO-0.12.4.md`).

## [0.12.3] — 2026-10-24
- **Botões do Volante no Treino por Trecho (`TrainingWheelButtons.cs`, `SectorButtonCommand.cs`):** captura DirectInput de botões físicos para salvar e retornar trechos via protocolo `command.json` nativo do Sector, restrita exclusivamente ao modo treino offline ativo com o LMU em foco (`docs/VALIDACAO-0.12.3.md`).

## [0.12.2] — 2026-10-23
- **Protocolo Sector:** reconhecimento das fases `paused` e `stopping` publicadas pelo runtime original e retentativas controladas (`3x`) em falhas transitórias de consulta local.
- **Delta Central:** separação entre comparação ao vivo e elegibilidade de gravação (mantendo comparação após pequenas saídas de pista), remoção de cabeçalhos textuais de sessão, ajuste de espessura (`6–28 px`) e ocultação alinhada ao `Alt+Tab` (`docs/VALIDACAO-0.12.2.md`).

## [0.12.1] — 2026-10-23
- **Inicializador Offline (`LmuOfflineLauncher.cs`):** seleção persistente da pasta do LMU e botão **Iniciar LMU sem EAC** executando diretamente `Le Mans Ultimate.exe` (sem alterar arquivos do jogo ou serviços do EAC) com espera supervisionada pela sessão de treino (`docs/VALIDACAO-0.12.1.md`).

## [0.12.0] — 2026-10-23
- **Barra de Delta Central (`DeltaTiming.cs`, `DeltaRepository.cs`, `DeltaDesign.cs`):** comparação em tempo real por distância com persistência atômica por pista/carro/classe.
- **Integração Sector (`SectorIntegration.cs`):** supervisão do runtime oficial `Sector 1.0.0` (`F6`/`F7`) para treino offline (`docs/VALIDACAO-0.12.md`).

## [0.11.0] — 2026-10-09
- Refinamento visual de acabamento comercial, hierarquia tipográfica e consolidação do painel de configurações (`docs/VALIDACAO-0.11.md`).

## [0.10.0] — 2026-10-02
- **Módulo Aprendizado (`Learning*.cs`):** histórico automático de voltas, setores oficiais, microsetores (`100/200/500 m`) e comparação no mapa incluindo tempos setoriais de adversários da sessão (`docs/APRENDIZADO.md`).
- **Pneus e Danos (`VehicleDamage.cs`, `TyreDamageRenderer.cs`):** integração de desgaste, pressão, pneu furado/roda solta e danos de suspensão/aerodinâmica geral via endpoint local do LMU (`docs/DANOS.md`).

## [0.9.0 – 0.9.2] — 2026-09-26
- Migração do atalho padrão de visibilidade para `F8`, histórico de pedais consolidado em `10 s`, redimensionamento e tipografia independentes por card (`PanelAppearanceDialog.cs`) (`docs/VALIDACAO-0.9.md`).

## [0.8.0] — 2026-09-25
- Calibração guiada de volante físico via DirectInput (`WheelInput.cs`, `WheelCalibrationDialog.cs`) para leitura angular real além de 90° (`docs/VALIDACAO-0.8.md`).
