# Verificação — Arts LMU OVERLAY 0.4

Data: 13/09/2026. A versão 0.3.1 foi confirmada pelo usuário dentro do jogo.
Este registro cobre as mudanças posteriores: calibração visual do volante,
menu de saída, identidade e instalador. O registro técnico anterior está em
VALIDACAO-0.3.1.md. As somas dos arquivos entregues ficam em SHA256.txt.

## Compilação e pacote

- Aplicativo GUI x64, C# 5 e referências oficiais do .NET Framework 4.8,
  compilado com Roslyn do SDK .NET 8.0.425: sem erros ou avisos.
- Nome de produto Arts LMU OVERLAY, versão de assembly 0.4.0.0, logo e bandeiras
  embutidas. O ícone contém sete resoluções de 16 a 256 px.
- Instalador NSIS 3.08 Unicode compilado com -WX, sem erros ou avisos.
  O instalador é PE32; o aplicativo que ele instala é PE32+ x64.
- Verificação de integridade do instalador e extração via 7-Zip. Aplicativo,
  configuração, LEIA-ME e três licenças comparados byte a byte com a origem.
- Instalador mantém pasta e chave de registro existentes. A migração do schema
  foi revisada no código: versões anteriores recebem ganho 50%; schema 4
  preserva o ganho salvo. Posições e preferências continuam na mesma pasta.

## Dados, movimento e recuperação

142 verificações passaram: 129 anteriores, 11 novas de calibração/política do
menu e duas de alfa. Os testes executam o parser com fixtures binárias e os
serviços reais de timer/ThreadPool com origem de dados injetada.

- Entrada +0,4 com faixa 900° e ganho 50% resulta em +90° no desenho e no valor.
  Entrada −0,4 produz −90°; +0,2 produz 45°. Ganho 100% restaura a conversão
  original, sem modificar a telemetria. Faixa manual e ganho compõem uma vez.
- Dados pausados não voltam a produzir ângulo válido. Sem faixa conhecida,
  o desenho normalizado recebe o ganho e o valor continua em porcentagem.
- A política do menu bloqueia trabalho de janela por toda sua duração,
  respeita os 400 ms após fechar e volta a bloquear quando o menu reabre.
- As regressões da 0.3.1 continuam aprovadas: pacote inválido e lobby sem países
  não encerram os serviços; telemetria e países voltam na tentativa posterior.
- Foram preservadas as verificações de todas as sessões 0–13, identificação
  por ID, solo/bots/online, classes, grupo do jogador e líder fixo, ganhos/perdas,
  flags ausentes, histórico analógico, suavização visual e recuperação de desenho.

Esses testes da política do menu não executam NotifyIcon, o menu da bandeja
ou SetWindowPos no Windows. O bloqueio foi conectado ao timer de interface
e a DrawFrame; a manutenção de janelas só ocorre com foco no app ou LMU.
Duplo clique é filtrado para o botão esquerdo. O cabeçalho oferece também
Encerrar o app, que usa a mesma ação de saída do menu.

## Verificação visual

Renders gerados pelo Painter real: normal, espera, pausa, nomes longos, troca
de marcha, início do gráfico, volante sem faixa, volante no centro e ±90°.
O cabeçalho usa BrandArtwork e a logo original. A prévia composta identifica
explicitamente os dados de teste e não se apresenta como captura do jogo.
Inspeção visual confirmou o novo desenho sem emblema, rotação no centro correto,
espaço para os graus e aberturas sem retângulo opaco. Os dois testes de alfa
confirmam opacidade de fundo e cantos transparentes.

Auditoria estática de UI: zero erros e zero avisos. Dez tokens de cor conferem
com DESIGN.md. Lint DESIGN.md: zero erros e nove avisos consultivos de esquema
(token primary e referências de componentes no frontmatter). A paleta nativa
explícita é verificada separadamente; não houve alteração do significado das cores.

## Limites e reprodução

O ambiente Linux não executa LMU/WinForms. Permanecem pendentes no Windows:
estabilidade do menu real de ícones ocultos, foco durante troca para a pista,
calibração com o volante físico, layout dos controles nativos em diferentes
DPIs, atualização de uma instalação anterior e desinstalação. A confirmação
do usuário na 0.3.1 é evidência daquela versão, não execução desses novos casos.

O harness usa System.Drawing.Common 6/libgdiplus e runtime .NET 8; emite os
avisos CS1701 e SYSLIB0014, que não aparecem na compilação .NET Framework do app.
As fontes do render Linux podem diferir das fontes nativas no Windows.

Reproduzir: `dotnet run --project tests/Checks.csproj -- tests/lmu-fixture.bin`.
Acrescente uma pasta como segundo argumento para os renders. A prévia e seus
dados ficam somente em tests, fora do aplicativo. No Windows, COMPILAR.cmd
compila sem baixar dependências; depois `makensis installer/Setup.nsi` gera
o instalador. O ambiente atual usa Roslyn diretamente devido à restrição de
consulta de metadados de processos no inicializador do MSBuild.

Converter novamente a logo em ICO: executar o harness com
`--export-icon assets/App.ico`. Isso não altera assets/BrandLogo.png.
