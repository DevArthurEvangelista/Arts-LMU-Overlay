# Verificação — Arts LMU OVERLAY 0.8

Data: 20/09/2026. Revisão solicitada por Arthur: pausas periódicas nos pedais,
texto maior, cantos arredondados, gráfico de 15 segundos e somente sua classe.
Foram incluídos quatro painéis adicionais opcionais: lap timer, pneus,
combustível e mapa da classe, usando os dados publicados pelo jogo.

## Correção das pausas

A versão anterior chamava WheelInput.Read no timer da interface; esse método
enumerava DirectInput a cada três segundos. Diagnostico.txt também era gravado
no mesmo thread a cada cinco segundos. São caminhos capazes de bloquear desenho.

A versão atual tem WheelPump dedicado, com publicação atômica de snapshots.
Criação, enumeração, polling e descarte de dispositivos usam o thread do leitor.
Dispositivos conectados não são reenumerados periodicamente: chegada/remoção,
falha de leitura e calibração solicitam atualização. Sem dispositivos, a busca
é a cada cinco segundos, fora da interface. A escrita do diagnóstico também
é assíncrona. Snapshots físicos com mais de 300 ms não travam o último ângulo:
é usado o fallback disponível de direção do jogo.

Regressão exercitada: um IWheelReader artificialmente bloqueado, ao mesmo tempo
que o TelemetryPump real continua lendo. Dez mil consultas do controlador à
última amostra não bloqueiam; o leitor de telemetria continua atualizando;
após liberar o dispositivo, o volante publica novamente. A liberação dos
recursos ocorre no thread proprietário. Isso verifica a separação de tarefas;
não reproduz a instalação/driver específicos do usuário.

## Testes

290 asserções passaram. Incluem regressões anteriores de identificação por ID,
sessões 0–13, solo/bots/lobby, entradas analógicas, reconexão após pacote
inválido, nacionalidades ausentes, calibração, seleção de vizinhos/líder,
GAP por melhor volta, punições e compostos. O app publicado não inclui fixtures
nem modo de demonstração.

Novos campos foram preenchidos por ctypes usando o SDK vendorizado, antes de
serem lidos pelo parser C#: mLapNumber, mLapStartET, mWheels, mPos e extensão
da pista. Testados Kelvin → Celsius, kPa → bar, ausência de sensores,
previsão sem referência/volta inválida, consumo de voltas completas, rejeição
de entrada parcial/refuel/reinício, mapa sem outra classe/pit lane, e endpoints
do histórico de quinze segundos. Layout contém 259 constantes geradas do SDK.

## Visual

Renders do próprio Painter em tamanho real (1×), além dos casos anteriores:
classificação, pedais, cada painel adicional, vazio/pausado, longos nomes,
limitador, volante 90/180/450°, gráfico inicial, largura/altura independentes.
Inspecionados pedais/tabela em 1×, prévia composta, pneus, mapa vazio e lap timer
pausado. Fontes da máquina de render podem diferir do Segoe UI/Consolas do Windows.

Tabela: 900 px, linhas 32 px, piloto/gap 20 px, POS 22 px, tempos 18 px; só a
classe própria. Pedais: 760×280, percentuais 26 px, km/h 36 px, RPM 32 px.
Cantos 18 px. Redimensionamento preserva um tamanho mínimo legível, salvo
restrição física de monitor menor. Atualização migra gráfico/classe/opacidade
conforme solicitado e conserva atalho, calibração e posições.

Lint dos 14 tokens e auditoria estática de design em modo estrito: sem erros,
avisos ou achados. Auditoria estática não comprova comportamento WinForms.

## Compilação e limites

App GUI x64/C# 5/.NET Framework 4.8 compilado com Roslyn, sem erros.
O harness .NET 8 passa com os avisos existentes de compatibilidade do
System.Drawing.Common 6 e de obsolescência WebRequest; não são erros no alvo net48.
Instalador NSIS Unicode 0.8 e payload verificados na entrega.

Este ambiente é Linux: LMU, DirectInput real, UI WinForms, DPI, composição de
janelas e instalação não foram executados no Windows. O teste em pista é a
confirmação pendente. Não alegamos eliminar toda causa possível de travadas.
Mapa aprende o traçado durante a sessão; não possui contornos prontos. Combustível
até o fim/previsão de volta são estimativas explícitas, sem reserva automática.

Reprodução no Windows: COMPILAR.cmd; testes pelo projeto tests/Checks.csproj.
O parâmetro adicional dos testes seleciona a pasta dos renders.
