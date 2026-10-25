# Verificação — Arts LMU OVERLAY 0.11.0

Data: 09/10/2026. Escopo: acabamento visual do produto, navegação da configuração,
novo cockpit com arco de RPM e histórico de dez segundos.

## Resultado observado

- Compilação Microsoft Roslyn, C# 5, .NET Framework 4.8, WinExe x64: passou.
- Gate do executável: GUI x64, entry point CLR, ícone, versão e manifesto
  embutidos. Manifesto corresponde ao fonte e preserva asInvoker e DPI.
- `tools/run_checks.py`: 429 asserções de núcleo; 432 incluindo renderização
  e confirmação da janela de dez segundos. Todos passaram.
- Mais 16 verificações do AppSettings real: migração, salvar/reabrir,
  dimensões, fontes, calibração, preferências e política de visibilidade.
- `audit_project.py . --mode strict --output docs/audit-v011.json`:
  zero erros, avisos ou violações. Auditoria estática, sem execução de interface.
- `python3 tools/check_design.py`: os 14 tokens do documento conferem com Theme.
- `npx --yes -p @google/design.md designmd lint DESIGN.md`: zero erros,
  13 avisos. Os avisos indicam ausência de primary e referências de componentes
  no frontmatter. Theme C# continua sendo a fonte de verdade; os mesmos 14 tokens
  são verificados pelo comando anterior, sem uma segunda paleta gerada.
- Busca de alert/confirm/prompt JavaScript e handlers não semânticos: zero.
  Controles desta aplicação são nativos WinForms; requisitos específicos de
  HTML/ARIA/scrollbars CSS não se aplicam. Botões, números, caixas de seleção e
  combos reutilizam os componentes nativos e seus nomes acessíveis em português.

## Conferência visual

Renders gerados pelo Painter real em GDI/Mono, inspecionados após as alterações:

- Cockpit 760×280; dois pedais em 100%; arco segmentado com o volante centralizado.
- Card largo e alto: volante e arco mantêm proporção uniforme. Ajustada a
  transformação de dois fixtures de teste que antes cortavam a borda do render.
- Limite de boxes: marcha amarela e rótulo LIMITADOR preservados.
- Espera/pausa, início do histórico, direção física e escalas individuais de fonte.
- Classificação: mesmas colunas e grupo da classe própria; novo cabeçalho e contraste.
- Lap timer/combustível: métrica principal destacada; pneus/mapa: tema consistente.
- Cabeçalho da configuração e prévia 1280×660 com dados ilustrativos.

A prévia não é uma captura dentro do jogo. Fixtures artificiais pertencem apenas
aos testes; não são compilados no aplicativo nem adicionam modo de demonstração.

## Comportamento preservado

O schema 10 adota dez segundos e conserva a geometria, opacidade, fonte, atalhos
e calibração existentes. Classificação continua somente na classe do jogador.
F8 e ocultação automática seguem a política já validada de pista/menu/garagem;
Posicionar continua permitindo edição fora da pista. O combustível foi removido
somente do card de pedais: seu card separado permanece opcional.

As páginas Painéis, Pilotagem e Atalhos mantêm seus controles e valores montados
ao alternar entre elas. SettingsPage é o único dono da rolagem de cada página;
status e salvamento ficam no rodapé. Aprendizado abre a janela existente.
As ações reutilizam os mesmos handlers de posicionamento, dimensões, calibração,
visibilidade, gravação e encerramento. Não há novo canal de coleta de telemetria.

## Reprodução e pacote

`tools/build_release.py` recebe --dotnet, --csc e --references para o compilador
Roslyn e referências net48. `tools/run_checks.py` usa os mesmos caminhos mais
--mono-root, isolando as configurações em tests/arts-v09-testdata.

O instalador é compilado com `makensis -V2 -WX installer/Setup.nsi`.
`tools/package_release.py --seven-zip <7z> --output <pasta>` extrai seu payload,
compara executável, configuração e LEIA-ME byte a byte, verifica o manifesto,
gera SHA256.txt e confere a integridade dos ZIPs. Dados de teste e renders
temporários não entram no aplicativo distribuído.

## Limites da validação

Este ambiente é Linux. A instalação e a nova navegação WinForms não foram
executadas interativamente no Windows, nem houve uma sessão real de LMU.
A tentativa de iniciar Xvfb falhou por indisponibilidade dos sockets exigidos.
Os renders verificam composição/legibilidade, mas não comprovam foco, teclado,
rolagem, DPI, registro de atalhos, DirectInput físico ou DWM/topmost no Windows.

Na máquina de destino, conferir alternância entre as três páginas, rolagem na
menor janela, Fonte e tamanho, Posicionar/Esc, F8 e retorno menu/pista.
Para o novo cockpit, conferir freio à esquerda, acelerador à direita, arco
progressivo/alerta de troca, limitador amarelo e gráfico de dez segundos.
As limitações de telemetria de aprendizado e danos da versão 0.10 continuam
documentadas em APRENDIZADO.md e DANOS.md.
