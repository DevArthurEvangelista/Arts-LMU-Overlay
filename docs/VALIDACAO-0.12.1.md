# Validação — Arts LMU OVERLAY 0.12.1

## Alteração

- Seleção da pasta que contém Le Mans Ultimate.exe, salva em LmuExecutablePath.
- Botão Iniciar LMU sem EAC abre o executável principal pelo caminho documentado
  no suporte do LMU. Não termina processos, altera serviços ou modifica arquivos.
- Worker de abertura/espera separado da interface e da aquisição de telemetria.
- Espera por treino compatível e encaminha a identidade exata do processo ao
  Sector, que repete a verificação antes e depois da preparação do runtime.
- Conectar treino por trecho preserva conexão manual para quem abriu o jogo fora
  do Arts. Desconectar cancela espera/conexão e deixa o jogo aberto.
- Layout dos overlays, leitura de telemetria, referências de delta e motor
  original do Sector permanecem sem alterações.

## Verificação automatizada

- Compilação de todos os fontes com Roslyn, C# 5, referências .NET Framework 4.8,
  alvo Windows x64, recursos de versão, ícone e manifesto DPI embutidos.
- 501 verificações de lógica, workers e renderer GDI passaram no Mono/Linux.
  Incluem validação de caminho e PE, diretório com espaços/acentos, arquivo
  inexistente, bootstrapper incorreto, processo já aberto, clique duplicado,
  cancelamento durante admissão, sessão incompatível, identidade substituída,
  saída precoce do jogo, falha do Windows e nova validação pelo SectorEngine.
- 23 verificações de migração e salvar/reabrir passaram. Caminho novo persiste
  sem mudar posições, dimensões, fontes, opacidades, calibração ou atalhos.
- NSIS compila com -WX. A ferramenta package_release.py extrai o instalador,
  compara executável/configuração/LEIA-ME/runtime/licença com o projeto e
  verifica manifesto, inventário, SHA256 e integridade dos dois ZIPs.

## Limites

Não foi executado LMU ou Windows nativo neste ambiente. Os testes do launcher
usam processos substitutos controlados, não comprovam Steam/DRM/EAC nem a
abertura real do jogo. O diálogo de pasta e a disposição dos controles nativos
não tiveram captura visual em Windows. Testar na máquina do usuário:

1. Atualizar mantendo os ajustes existentes; escolher a pasta e reabrir o Arts.
2. Abrir LMU pelo novo botão; selecionar treino e confirmar conexão automática.
3. Salvar F6/retornar F7 com LMU em foco na versão suportada.
4. Desconectar enquanto aguarda e enquanto conectado; jogo permanece aberto.
5. Clicar para iniciar com LMU já aberto; uma segunda instância não deve iniciar.
6. Fechar LMU durante espera; mostrar mensagem de saída, sem conectar a outro PID.
7. Testar mudança de sessão/online; os bloqueios existentes devem permanecer.

O Sector continua restrito a LMU build 14200 / Steam 25661166 e treino offline
sem EAC. Esta atualização não expande compatibilidade de versões do Sector.
