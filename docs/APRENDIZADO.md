# Aprendizado — 0.10.0

Pedido de Arthur em 27–28/09/2026: registrar voltas próprias e tempos dos
adversários para comparar ganhos/perdas no traçado. O módulo usa a leitura
local do LMU. Adversários não precisam enviar arquivos nem usar o Arts.
Seus pedais não são capturados; não há serviço externo de telemetria.

## Usar

1. Deixe o Arts aberto na sessão. A gravação é automática.
2. Abra **Aprendizado** na configuração ou no ícone junto ao relógio.
3. Escolha sessão, piloto A e volta A. O histórico mostra tempo, três setores,
   validade e cobertura. A numeração segue a sequência de registros do piloto,
   começando quando o Arts o observa, incluindo saídas interrompidas.
4. Em **Comparar no mapa**, escolha piloto B e volta B. **Melhor válida**
   seleciona a menor volta válida gravada do piloto B, excluindo boxes.
5. Escolha trechos de 100, 200 ou 500 m. Clique no traçado ou na tabela para
   destacar o trecho nos gráficos. O mapa também aceita as setas do teclado.

**A menos B**: positivo/vermelho é perda de A; negativo/verde é ganho de A.
O delta acumulado mede a diferença até aquela distância; a cor do mapa mede
apenas o trecho. Velocidade A branca/B azul em km/h vem da classificação.
Setores são exibidos em segundos. Compara apenas voltas da mesma sessão/traçado.

Sessões podem ser reabertas após reiniciar. Não importa voltas feitas com o
Arts fechado nem compara sessões diferentes nesta versão. Combustível, pneus,
clima e tráfego influem nos tempos; a comparação não atribui uma causa.

## Dados e validade

SDK do LMU já distribuído em third_party/lmu_data.py: VehicleScoringInfo,
mLapStartET, mLapDist, mPos, mLocalVel, mTotalLaps, mCountLapFlag, mInGarageStall,
mControl, mCurSector1/2, mLastSector1/2 e mLastLapTime.

Os três setores usam tempos do jogo: S2 = mCurSector2 − mCurSector1 porque
mCurSector2 é acumulado; S3 = volta oficial − acumulado de S1+S2. Campos da
última volta só são usados quando correspondem à passagem sendo fechada.
Não misturar melhores setores individuais de voltas diferentes.

Microsetores são **estimativas**, não splits oficiais. O relógio é interpolado
entre amostras que cercam a mesma distância para cada piloto. A frequência
depende do jogo, limitada a 25 amostras/s no gravador. As casas decimais não
representam promessa de precisão em milissegundos.

Intervalos >1 s, perda de fila, pausa, saltos, marcha a ré e início não observado
interrompem a continuidade. Trechos afetados ficam **Sem dados**/cinza, mesmo
quando existem amostras nas extremidades. Não extrapola pontos ausentes. Mapa
usa coordenadas capturadas e não liga saltos por linhas inventadas.

Válida = completa, com tempo oficial, sem invalidação, sem passagem não
cronometrada e sem boxes.
mCountLapFlag=1 e sinal local de invalidação são tratados conservadoramente
como invalidada. A causa não é fornecida aqui: não afirmar sempre track limits.
Voltas inválidas continuam selecionáveis, fora da referência automática.
Duração calculada sem tempo oficial leva ≈; setor ausente usa —. Volta oficial
capturada em parte mantém seu tempo e recebe **parcial**, sem inventar mapa.

Sessões 0–13: teste, treinos, qualificação, aquecimento e corrida, sozinho,
com IA ou online. Garagem interrompe a saída local; adversários continuam
registrados com classificação ativa. Controle replay explícito (=3) e carros
sem controle são ignorados. Trocar identidade no mesmo slot separa registros.
Nova sessão ou regressão do relógio separa históricos.

## Persistência e desempenho

`%LOCALAPPDATA%\ArthurLMUOverlay\Aprendizado`: pasta GUID por sessão, session.xml,
índice .xml e .lap.gz por volta. Formato versão 1. Dados e nomes ficam locais.
Salva ao concluir volta, interromper saída ou encerrar normalmente o app.
Fechar só Aprendizado mantém a gravação. Encerramento forçado/perda de energia
pode perder a volta em memória. Atualizar/desinstalar preserva o histórico.
Não há exclusão automática por idade ou botão de apagar histórico.

TelemetryPump produz snapshots. LearningPump usa thread própria/fila de 128.
Compressão, escrita, enumeração e comparação ficam fora do leitor e da UI.
Fila cheia marca lacunas. Falha de disco mantém até 64 voltas para repetir;
perdas/erros aparecem no estado e diagnóstico. Encerrar espera até 4 s.
Conteúdo e índice usam temporário/renomeação; índice é publicado por último.
Corrupção não apaga original. Limite de 100 mil amostras por volta, com excesso
marcado como falha. Leituras têm limites de tamanho, sem DTD/XML externo.

## Interface

LearningWindow: Histórico e Comparar no mapa, em janela nativa redimensionável.
SettingsUi fornece botões, rótulos e selects; LearningPainter desenha gráficos.
Theme mantém os 14 tokens. Tabelas rolam na própria área. Controles têm nomes
acessíveis, seleção pelo teclado e larguras de popup adaptadas a nomes longos.

Atualizar preserva seleção. Respostas antigas não sobrescrevem uma escolha
nova. Comparação imutável não é recalculada a cada nova volta salva. Trocar
sessão limpa a comparação enquanto carrega. Vazio, erro, carregamento e lacunas
têm texto próprio. Popups suspendem a manutenção de topmost.

Não muda schema 9, posições, tamanhos, fontes, calibração, F8 ou ocultação dos
seis overlays. A janela Aprendizado não é ocultada pelo F8. Sem demonstração no
produto; fixtures/renders artificiais existem somente nos testes.
