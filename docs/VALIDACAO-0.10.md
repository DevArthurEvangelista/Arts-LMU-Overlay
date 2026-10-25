# Verificação — Arts LMU OVERLAY 0.10.0

Data: 28/09/2026. Escopo: Aprendizado local e danos integrados ao card de pneus.

## Resultado observado

- Compilação Microsoft Roslyn, C# 5, .NET Framework 4.8, WinExe x64: passou.
- Gate dos bytes do PE: aplicação GUI x64, entry point CLR, recursos de ícone,
  versão e manifesto presentes. Manifesto corresponde exatamente ao fonte;
  mantém `asInvoker` e configuração de DPI.
- `tools/run_checks.py`: 429 asserções de núcleo; 431 incluindo renders.
- Mais 14 verificações no AppSettings real: migração, salvar/reabrir,
  dimensões/fontes independentes, preferências e política de visibilidade.
- Auditoria estática estrita: zero erros/avisos (`docs/audit-v010.json`).
- `tools/check_design.py`: os 14 tokens conferem com o renderer nativo.
- `npx --yes -p @google/design.md designmd lint DESIGN.md`: zero erros,
  13 avisos. Os avisos são ausência de `primary` e tokens sem referência na
  configuração de componentes do frontmatter. A paleta semântica de 14 cores
  é de propriedade do Theme C#, com mapeamento verificado pelo teste acima;
  não foi criada uma nova cor primária nem alterada a identidade aprovada.
- Instalador NSIS Unicode compilado com `-WX` (avisos como erro).

## Cenários cobertos

Gravador: modos de sessão 0–13, entrada parcial, cruzamento de volta, setores
oficiais cumulativos, invalidação, boxes, troca de sessão/carro/identidade,
pausa, ausência de carro, perda de amostras e fila cheia. Adversários continuam
observados quando a classificação está ativa e o usuário está na garagem.
Snapshots não retêm pedais de terceiros. O TelemetryPump real continua
atualizando pedais enquanto a escrita de voltas está artificialmente bloqueada.

Persistência: escrita atômica, reabertura, proteção de caminhos, índice após
payload, GZip/XML corrompidos, quota de volta longa, repetição após erro e
encerramento. Comparação: mesma distância, A−B, lacunas internas, marcha a ré,
posição estacionária, início não observado e recusa de sessões incompatíveis.

Danos: `mWear` com 1 novo e 0 gasto; valores inválidos não derrubam os pedais;
furo, roda solta e outra peça solta independentes. Campos conhecidos do JSON,
frações fora do intervalo, arrays incompletos e valores ausentes. Endpoint
demorado não bloqueia UI nem sobrepõe requisições; resposta de carro anterior
é rejeitada; garagem/desativação/expiração removem dados antigos.

Renders pelo código real: aprendizado em 1200 e 960 px e estado vazio; danos
disponíveis e ausentes em 420×402; regressões dos seis cards e escalas de fonte.
Inspeção visual confirmou o novo card e o mapa/gráficos; todas as imagens de
teste usam dados artificiais e não são capturas do jogo. A seleção do mapa foi
ajustada para desenhar contorno contínuo em vez de marcas por amostra.

## Comandos reproduzíveis

`tools/build_release.py` recebe dotnet, Roslyn csc.dll e referências net48.
`tools/run_checks.py` recebe os mesmos caminhos e uma instalação Mono em
`--mono-root`; dados de teste isolados em `tests/arts-v09-testdata`.

`tools/package_release.py --seven-zip <7z> --output <pasta>` inspeciona o
conteúdo extraído do instalador, compara binário/configuração/LEIA-ME byte a
byte, repete o gate de manifesto, gera SHA256 e confere os dois ZIPs. Exclui
renders temporários, executáveis de testes e preferências dos testes.

## Limites da validação

Este ambiente é Linux. Não há execução do LMU, instalação nativa Windows,
DirectInput físico, DWM/topmost ou inspeção interativa da nova janela WinForms.
A etapa gráfica nativa de teste não pôde executar porque o ambiente não permite
os sockets exigidos pelo Xvfb; não foi substituída por uma alegação de teste
no jogo. Renders GDI/Mono não provam DPI, menu popup, foco ou cliques no Windows.

O contrato REST de danos está fundamentado em integrações primárias publicadas,
mas a resposta do LMU instalado pelo usuário ainda precisa de confirmação.
Se estiver ausente/incompatível, SUSP./AERO mostram — e os pneus nativos continuam.
Não há valores separados para asas dianteira e traseira. Microsetores dos
pilotos são estimados a partir das amostras recebidas, sujeitos à frequência
e às lacunas online. Histórico começa enquanto o Arts está aberto e compara
voltas da mesma sessão. Esses limites aparecem também na interface/documentação.
