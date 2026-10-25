# Verificação — Arts LMU OVERLAY 0.9.2

Data: 26/09/2026. Escopo: ocultar automaticamente na pausa/menu/garagem,
manter atalho na pista e preservar o layout escolhido. Nenhuma mudança de
paleta, tabela, telemetria, desenho dos instrumentos ou calibração.

## Alteração e decisões

`OverlayVisibility` centraliza a elegibilidade da sessão, a escolha manual e
a exceção explícita de Posicionar. O controlador aplica a regra aos seis cards
antes dos retornos por foco ou popup. `OverlayWindow.EnsureVisible` só mantém
acima do jogo uma janela cuja solicitação de visibilidade permanece ativa.
Ocultar não modifica a configuração nem chama restauração de posições.

F8 padrão só alterna durante leitura ativa na pista. Fora dela não muda estado;
a ocultação manual sobrevive ao retorno pelo menu. Posicionar permite editar
offline e, ao concluir, restaura a regra automática e a escolha manual anterior.
Carro parado já em modo de pilotagem continua elegível. ApplicationState zero
continua compatível com telemetria realtime válida, conforme o contrato existente.

O schema 9 troca o padrão antigo Ctrl+Alt+H por F8, mantendo atalhos personalizados.
Todos os campos de posição, dimensão, fonte, opacidade e volante são preservados
na migração da 0.9.1. A documentação explica a escolha e a exceção de edição.

## Evidência executada

- `tools/build_release.py`: Microsoft Roslyn 4.8, referências oficiais .NET
  Framework 4.8, C#5/x64, ícone, manifesto e recursos. Compilação bem-sucedida.
- Suite `Checks`, recompilada com Roslyn e executada no runtime Mono:
  **343 asserções aprovadas**, usando fixtures binárias SDK e trabalhadores reais.
  Inclui sessões 0–13, realtime, menu/garagem/carregamento, pausa explícita,
  congelamento de amostras, retomada, leitor interrompido, desconexão, atalho,
  edição offline, preferência manual e card desativado.
- `AppearanceUiChecks --config-only`: **14 verificações aprovadas** no AppSettings
  real. Migração 0.9.1→0.9.2 mantém as posições/dimensões dos seis cards, ativação,
  fonte, opacidades e faixas do volante; F8 e atalhos personalizados persistem.
  Também executadas as verificações anteriores de fonte e gravação/reabertura.
- Auditoria estática de UI em modo strict: zero erros, avisos ou donos não resolvidos.
  `tools/check_design.py`: os 14 tokens de cor coincidem com DESIGN.md e o renderer.
- Instalador NSIS Unicode compilado com `-WX`, extraído com 7-Zip e comparado
  byte a byte: aplicativo, arquivo de runtime e instruções idênticos aos originais.
- Gate `tools/check_windows_package.py` aplicado ao aplicativo e ao payload do
  instalador: PE32+ x64 GUI, ponto de entrada CLR, recursos 3/14/16/24 e manifesto
  embutido idêntico ao fonte, com asInvoker e DPI preservados.

## Limites

Windows, LMU, registro de F8 e composição das janelas nativas não foram executados
neste ambiente Linux. Os testes verificam a política e os dados, não comprovam
a integração com o jogo. Pausa sinalizada oculta na atualização seguinte;
silêncio do produtor segue a detecção existente de até 2 segundos.

As correções de inicialização da 0.9.1 permanecem: compilação Microsoft, gate
do manifesto e captura de erros antes do loop WinForms. A causa da dificuldade
de abertura anteriormente relatada no Windows não foi confirmada por log local.
