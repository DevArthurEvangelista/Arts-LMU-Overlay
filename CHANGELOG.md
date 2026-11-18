# Changelog — Arts LMU OVERLAY

Todas as mudanças documentadas do projeto estão consolidadas abaixo com base no código-fonte auditado e nos relatórios de validação em `docs/`.

> **Nota sobre validação:** diferenciamos explicitamente **testes automatizados** (executados pela suíte `tests/Checks.csproj`) de **validação real no LMU** (executada pelo piloto no Windows com volante físico).

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
