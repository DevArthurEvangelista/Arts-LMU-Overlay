# Registro histórico de verificação — versão 0.3.1

Data: 13/09/2026. Executável GUI x64, compatibilidade C# 5, referências oficiais
do .NET Framework 4.8 e compilador Roslyn do SDK .NET 8.0.425.
O aplicativo Windows requer somente .NET Framework 4.8; flags embutidas.

SHA-256 do aplicativo: `325e0b720a78845bbc8e34543ae55836d9cf236721cd21af92ff2d7a1fc07eab`

SHA-256 do instalador: `c94b2ebe480b4c266196d15f15637c1d941bc270473c46baac72cab541c69b6d`

## Regressão do encerramento ao entrar na pista

Foram executados os serviços reais de timer/ThreadPool com fontes de dados de
teste. Antes da correção dos filtros, os dois cenários abaixo encerraram o
processo de teste com exceção não tratada (`InvalidDataException`, SIGABRT no
harness Linux). Os construtores de injeção permitem substituir apenas a origem
dos dados; o tratamento de erros anterior permaneceu intacto nessa reprodução.

| Cenário | Tratamento 0.3 | Tratamento 0.3.1 |
|---|---|---|
| Pacote binário inválido, seguido de um válido | Processo encerrado no timer de telemetria | Erro registrado, estado de reconexão e leitura retomada automaticamente |
| Lobby responde `{}`, depois informa os países | Processo encerrado no worker de nacionalidades | País neutro, pilotos e telemetria preservados; país restaurado na próxima consulta |

Os testes verificam também a identidade do jogador, os valores analógicos após
a recuperação, a liberação do leitor, o intervalo de consulta e a liberação do
controle de execução única do serviço. Esses casos eram uma lacuna na suíte 0.3:
o parser rejeitava dados inválidos corretamente, mas a suíte não executava essa
rejeição através dos workers que permanecem ativos durante o jogo.

Reprodução da suíte corrigida: `dotnet run --project tests/Checks.csproj --
tests/lmu-fixture.bin`. Para executar apenas os novos casos, acrescente
`--worker-telemetry` ou `--worker-countries` depois do caminho da fixture.

## Resultado

- Compilação Windows: passou sem avisos.
- Instalador NSIS 3.08 Unicode: compilou com avisos tratados como erros (-WX),
  sem avisos. O bootstrap é PE32 Windows e o aplicativo instalado é PE32+ x64.
- Arquivo NSIS passou no teste de integridade do 7-Zip e foi extraído. Aplicativo,
  configuração, LEIA-ME e as três licenças conferem byte a byte com os arquivos
  finais de origem; o pacote também contém o desinstalador nativo.
- 129 verificações de dados, regras e recuperação nos workers: passaram.
  São as 117 anteriores mais 12 verificações da regressão. Os dois testes de
  alfa e as prévias visuais já aprovados na 0.3 não foram repetidos neste patch;
  não houve alteração no renderer.
- Leitura em todos os códigos de sessão 0–13, com campos opcionais de aplicação
  e playerHasVehicle zerados; cenários solo, bots e controle remoto/local.
- Identificação pelo ID mesmo com arrays e índice em ordem diferente; fallback
  por controle local/nome exato; espectador não recebe carro inventado.
- Contagem opcional ausente: busca de slot fresco; slot antigo rejeitado.
- Atualização por tempo nativo mesmo com contadores ausentes, pausa explícita,
  relógio ativo com telemetria congelada e reinício da detecção.
- Classe própria primeiro; dez carros com líder fixo e jogador em seis posições
  diferentes; cinco de cada outra classe; grids pequenos e classe única.
- Ganho/perda desde pré-largada, bandeira amarela, reinício, troca de sessão e
  entrada tardia com posição inicial desconhecida.
- Schema de nacionalidades do lobby, vínculo por slot/nome, nome ambíguo,
  aliases explícitos e ausência de país sem inferência.
- Giro do volante pelo alcance físico, setup local e faixa visual; prioridade
  manual; rotação normalizada sem alegar graus quando a faixa é desconhecida;
  fallback da entrada filtrada quando a bruta é ausente/inválida.
- Parser do setup: valores 540 deg e 900°, unidade inválida rejeitada e campos
  não relacionados ignorados. A resposta de teste é sintética.
- Alerta de troca, marcha neutra, última marcha e dados pausados.
- Gráfico começa à esquerda e depois desliza; histórico reinicia com sessão,
  identificação, perda de telemetria ou retrocesso do tempo. Buffer conserva
  15 s na taxa simulada de 125 amostras/s; pacotes repetidos não são duplicados.
- Frações analógicas preservadas no histórico; transição de barras/volante é
  monotônica e passa de 98% do alvo em 160 ms, sem alterar a entrada original;
  opção desativada usa os valores recebidos diretamente.
- Política de recuperação do desenho: tentativas com espera crescente limitada
  a 2 s; uma apresentação bem-sucedida reinicia a política.
- 226 constantes de layout reproduzidas de forma idêntica via ctypes.
- Dez tokens de cor iguais ao DESIGN.md.
- Auditoria estática de UI: zero erros e zero avisos.
- Lint DESIGN.md: zero erros e nove avisos consultivos de esquema, sobre token
  primary e referências de componentes no frontmatter. Os consumidores nativos
  Theme/Painter usam a paleta explícita e são conferidos pelo teste de tokens.
- Renders preservados da 0.3, gerados pelo código real: normal, espera, pausa, nome longo, aviso de troca,
  início do gráfico à esquerda e volante sem faixa em graus. Revisão visual
  confirmou o posicionamento do traçado e o indicador normalizado em %.
  Textos longos recebem reticências calculadas; o volante mantém distância do
  valor do ângulo. Fundos com alfa e cantos transparentes.

O projeto de testes usa System.Drawing.Common 6.0/libgdiplus no Linux. O harness
emite CS1701 (referências entre .NET 6 e 8) e SYSLIB0014 (HttpWebRequest no .NET 8).
Ele executa com sucesso. A compilação do app .NET Framework não emite esses
avisos; as dependências do harness não são requisitos do executável Windows.
Fontes podem variar entre o render Linux e Segoe UI/Consolas no Windows.

**Não verificado ao vivo:** LMU e WinForms no Windows, leitura real dos endpoints
de nacionalidades e setup, composição sobre o jogo, recuperação nativa do DIB,
atalhos globais, arraste, escala, cliques atravessando as janelas, execução do
instalador/desinstalador e migração de configurações antigas.
Os cenários de dados são sintéticos e não confirmam a integração no computador
do usuário. Essa distinção também está no LEIA-ME e na identificação da prévia.

Compilação neste ambiente usa Roslyn diretamente porque o iniciador do MSBuild
não consegue consultar metadados de processos. COMPILAR.cmd permite compilar
no Windows usando o compilador instalado com o Framework, sem baixar pacotes.

Reprodução: python tools/generate_layout.py; python tools/make_fixture.py;
python tools/check_design.py; dotnet run --project tests/Checks.csproj --
tests/lmu-fixture.bin. Um segundo argumento fornece a pasta dos renders.
Dados de prévia ficam exclusivamente em tests/PreviewData.cs, fora do app.
Para gerar o instalador após o executável: makensis installer/Setup.nsi.
