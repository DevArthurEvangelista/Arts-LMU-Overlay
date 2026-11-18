# Arts LMU OVERLAY

## Documentação mestre do projeto, histórico de decisões e transferência técnica

> **Finalidade deste documento:** transferir o contexto conhecido do
> projeto para outro agente de IA (Google Carbon), permitindo organizar
> a documentação e o repositório GitHub e continuar o desenvolvimento
> sem perder decisões anteriores.
>
> **Nome do aplicativo:** Arts LMU OVERLAY\
> **Jogo-alvo:** Le Mans Ultimate (LMU)\
> **Responsável pelo projeto:** Arthur Gabriel Evangelista Santos\
> **Última referência de versão encontrada no histórico:** 0.12.2
> (outubro de 2026)\
> **Status deste documento:** consolidação do histórico disponível nas
> conversas. Não substitui auditoria do código-fonte.

------------------------------------------------------------------------

# 1. Visão geral

O Arts LMU OVERLAY é um aplicativo/overlay complementar para Le Mans
Ultimate, concebido para oferecer ao piloto informações de corrida,
telemetria visual, acompanhamento de adversários e ferramentas de
aprendizado e treino.

A proposta não é apenas exibir dados: o objetivo é construir uma
experiência de produto, visualmente profissional, configurável, legível
durante a pilotagem e com recursos que possam evoluir para uma solução
comercial.

O projeto foi desenvolvido de forma iterativa. O usuário avaliou o
comportamento dentro do jogo, apontou problemas de interface e
funcionamento e solicitou ajustes sucessivos. Por isso, **preservar a
experiência de uso e as decisões já tomadas é tão importante quanto
acrescentar funcionalidades**.

## 1.1 Objetivos principais

-   Exibir informações essenciais de corrida sem atrapalhar a visão da
    pista.
-   Apresentar tabela de pilotos e tempos com hierarquia visual clara.
-   Acompanhar posição, diferenças de tempo, pneus e penalidades.
-   Mostrar informações do volante, pedais, motor e RPM.
-   Oferecer ferramentas de análise de voltas, setores e microsetores.
-   Permitir treino por trecho no modo offline, com integração ao
    projeto Sector.
-   Disponibilizar configurações de visibilidade, opacidade,
    posicionamento e tamanho.
-   Manter o overlay consistente entre treino, classificação e corrida.
-   Evoluir para um produto estável, configurável e apresentável
    publicamente.

## 1.2 Princípios que devem ser preservados

1.  **Legibilidade em primeiro lugar:** dados importantes precisam ser
    fáceis de ler durante a pilotagem.
2.  **Personalização:** cada card deve poder ser ajustado sem obrigar o
    usuário a alterar os demais.
3.  **Fidelidade ao jogo e ao hardware:** dados físicos do volante e
    telemetria não devem ser substituídos por estimativas visuais quando
    houver fonte real disponível.
4.  **Interface limpa:** remover demonstrações, explicações e controles
    que não façam parte da experiência final.
5.  **Comportamento previsível:** atalhos, visibilidade e estados de
    conexão precisam funcionar de forma consistente.
6.  **Não regredir funcionalidades existentes:** mudanças devem
    preservar layout, ordem de colunas e comportamentos já aprovados,
    salvo pedido explícito.

------------------------------------------------------------------------

# 2. Módulos e funcionalidades

## 2.1 Tabela de pilotos / quadro de tempos

É um dos módulos centrais do overlay.

### Colunas e ordem definida

A tabela foi especificada com as seguintes colunas:

1.  `POS` --- posição atual.
2.  `+/-` --- posições ganhas ou perdidas.
3.  `GER` --- identificação/indicador de classe geral conforme o
    contexto da sessão.
4.  `PILOTO/CARRO` --- nome do piloto e identificação do carro.
5.  `ÚLTIMA` --- última volta.
6.  `MELHOR` --- melhor volta.
7.  `PNEU` --- composto/tipo de pneu.
8.  `GAP GER.` --- diferença para a referência geral.
9.  `PEN.` --- penalidades.
10. `BOX` --- informação de box.

**Regra importante:** manter a ordem e a estrutura das colunas quando
fizer ajustes visuais. Não reorganizar a tabela por iniciativa própria.

### Classes

O overlay deve lidar com as classes:

-   LMGT3
-   HYPERCAR
-   LMP2

### Identificação visual dos pilotos e carros

-   Exibir bandeira do país do piloto.
-   Exibir símbolo/identificação da escuderia antes da bandeira.
-   Exibir logo ou imagem colorida do carro.
-   Identificar visualmente os pneus com um círculo e uma letra na cor
    correspondente.
-   Penalidades devem ser apresentadas por siglas, incluindo `DT` e
    `SG`.
-   O líder da sessão deve permanecer fixo/destacado conforme a lógica
    de exibição definida.

### Quantidade de pilotos exibidos

-   Exibir os três pilotos à frente e os três atrás do usuário como
    referência de acompanhamento.
-   Na classe do usuário, trabalhar com uma janela de dez pilotos mais o
    próprio usuário (10 + 1), conforme a especificação discutida.
-   Disponibilizar opção **"Exibir somente o meu"**.
-   Em corridas multiclasse, exibir os cinco primeiros das demais
    classes, além do topo fixo do líder.
-   Preservar a lógica de contexto da classe do usuário e a referência
    geral.

### Diferenças de tempo

O comportamento do GAP depende do tipo de sessão:

-   **Treino:** gap em relação à melhor volta do usuário.
-   **Qualificação:** gap em relação à melhor volta do usuário.
-   **Corrida:** gap geral, conforme a referência de corrida definida
    para o quadro.

O cabeçalho também deve apresentar a melhor volta, o piloto
correspondente e a posição.

### Tipografia e hierarquia

Elementos críticos: - `#F2F2F2` como referência de cor clara. - Peso
tipográfico entre 600 e 700. - Prioridade para piloto, melhor volta, gap
geral e posição.

Metadados: - Cinza claro ou médio. - Peso regular (400).

Números: - Preferência por fonte monoespaçada para facilitar comparação
e alinhamento.

Ajustes: - Fonte maior e legível. - Ajuste de tamanho de fonte por
card. - Não sacrificar legibilidade para encaixar mais informações.

## 2.2 Pedais e motor

### Pedais

-   Barra de freio à esquerda.
-   Barra de acelerador à direita.
-   Barras com comportamento visual invertido conforme solicitado.
-   Sem rótulos textuais desnecessários.
-   Janela de histórico/visualização de 10 segundos; anteriormente havia
    sido considerada uma janela de 15 segundos.

### RPM e marcha

-   RPM apresentado em arco ao redor do volante.
-   Indicador de troca de marcha com mudança de cor.
-   Informação de combustível foi solicitada para remoção.
-   Foi relatado congelamento intermitente na visualização dos dados.

## 2.3 Volante

### Requisito principal

A rotação exibida deve acompanhar a rotação real do hardware,
independentemente de uma representação simplificada fornecida pelo jogo.

O objetivo é manter fidelidade ao volante físico, inclusive para futuros
modelos de volantes.

### Requisitos visuais e funcionais

-   Utilizar imagem/representação própria do volante.
-   Estilo de desenho/ilustração.
-   Não utilizar logotipo Porsche na representação solicitada.
-   Fidelidade visual e angular.
-   Rotação acima de 90 graus precisa acompanhar corretamente o
    movimento real.
-   O ângulo de rotação deve variar conforme o carro, quando aplicável.

### Problemas relatados

-   Inicialmente, a rotação parecia limitada a 90 graus, quando a
    expectativa era acompanhar até 180 graus ou mais conforme o
    hardware/carro.
-   Foi solicitada correção para refletir a rotação real, sem limitar
    artificialmente o movimento.

## 2.4 Card de pneus e danos

Foi solicitado um card integrado para apresentar informações de pneus e
danos do carro.

Itens desejados: - Temperatura dos pneus. - Condição/danos dos pneus. -
Danos de suspensão. - Danos da asa dianteira. - Danos da asa traseira.

A integração deve usar os dados disponíveis na fonte de telemetria do
jogo. Não assumir que todos os indicadores já estavam implementados ou
validados.

## 2.5 Mapa de pista

Foi solicitada uma opção de mapa da pista como módulo do overlay.

Pontos a preservar: - Deve ser configurável/ativável. - Precisa manter
legibilidade e não competir visualmente com a tabela de pilotos. - O
histórico disponível não confirma todos os detalhes de implementação ou
o estado final desse módulo.

## 2.6 Módulo Aprendizado

O módulo Aprendizado foi pensado para transformar dados de voltas em
informação prática para evolução do piloto.

### Modo treino/offline

Funcionalidades solicitadas: - Quantidade de voltas válidas. -
Quantidade de voltas deletadas/inválidas. - Análise de setores. -
Análise de microsetores. - Mapa da pista. - Comparação de voltas. -
Comparação volta a volta para identificar onde houve ganho ou perda.

### Modo online

-   Comparar tempos setoriais de outros pilotos.
-   Não incluir telemetria de pedais de outros pilotos, pois essa
    informação não está disponível/prevista para esse comparativo.
-   Foco em diferenças de tempo por setor e desempenho relativo.

### Cuidados

-   Não classificar uma volta como inválida sem evidência correta do
    estado da volta.
-   Diferenciar saída de pista, volta invalidada, volta em andamento e
    volta válida.
-   O cálculo de delta precisa respeitar o estado real da volta e não
    produzir comparações indevidas.

## 2.7 Delta bar

Foi solicitado um indicador de delta central inspirado na apresentação
visual do iRacing.

### Diretrizes visuais

-   Barra horizontal central.
-   Visual transparente.
-   Leitura rápida de ganho/perda.
-   Possibilidade de aumentar a espessura da barra.
-   Não exibir texto de "melhor sessão".
-   Deve ocultar e reaparecer junto com os demais cards do overlay.
-   Deve respeitar o comportamento de ocultação ao alternar para outra
    janela (Alt-Tab).

### Problemas encontrados

-   O delta indicava "volta inválida" indevidamente em determinadas
    situações.
-   O delta parava de funcionar após saída de pista.
-   A volta podia deixar de ser comparável mesmo quando o usuário
    esperava que a comparação continuasse ou fosse retomada
    corretamente.
-   O delta não ocultava no Alt-Tab como os demais cards.

### Referência de correção

Na versão 0.12.2, o histórico registra correções planejadas/apontadas
para: - Estado do delta durante saídas de pista e voltas invalidadas. -
Ajuste de espessura. - Ocultação no Alt-Tab. - Remoção do texto de
melhor sessão.

**Atenção:** os testes automatizados dessa versão foram reportados como
aprovados, mas a validação final dentro do LMU ainda estava pendente no
registro disponível.

------------------------------------------------------------------------

# 3. Configurações e experiência de uso

## 3.1 Opacidade

-   Opacidade configurável separadamente para o módulo de pedais e para
    as informações.
-   Deve existir um controle/botão para ajustar ambos em conjunto.
-   Os controles individuais não devem ser perdidos.

## 3.2 Visibilidade

-   Atalho para alternar a visibilidade do overlay.
-   `F8` foi definido como atalho de toggle.
-   Possibilidade de ocultar o overlay em menus e na garagem.
-   Overlay deve ocultar quando a leitura/telemetria estiver pausada,
    conforme o comportamento solicitado.
-   O delta deve acompanhar a regra geral de ocultação.
-   A opção "Exibir somente o meu" deve ficar disponível no
    menu/configurações.

## 3.3 Posicionamento

-   O posicionamento deve ocorrer por uma ação visual chamada
    **"Posicionar"**.
-   Não expor controles técnicos de X, Y ou porcentagem para o usuário
    final.
-   O processo deve ser simples e intuitivo.
-   Evitar que a função de posicionamento exija conhecimento técnico.

## 3.4 Redimensionamento

-   Permitir redimensionar cada card individualmente.
-   Ajuste independente de tamanho.
-   Não obrigar todos os cards a crescer ou diminuir juntos.
-   Preservar proporções e legibilidade.
-   Foram relatadas dificuldades anteriores para redimensionar; isso
    deve entrar na bateria de testes.

## 3.5 Interface final

Solicitações de acabamento: - Aumentar letras e melhorar legibilidade. -
Ajustar arredondamentos. - Remover botões de demonstração. - Remover
seção "Como usar". - Remover navegação desnecessária. - Manter somente
controles que façam sentido no produto final. - Aparência de software
comercial, não de protótipo.

------------------------------------------------------------------------

# 4. Treino por trecho e integração com Sector

## 4.1 Objetivo

Criar um modo de treino offline que permita salvar um trecho da pista e
retornar a ele para repetir a execução, facilitando o treinamento de
curvas e sequências específicas.

O projeto open source **Sector** foi identificado como
referência/integração para a função de retornar ao trecho.

A intenção é aproveitar especificamente a função de retorno ao trecho,
sem transformar o Arts LMU OVERLAY em uma cópia integral do Sector.

## 4.2 Atalhos definidos

-   `F6`: salvar trecho.
-   `F7`: voltar ao trecho salvo.

Esses atalhos são o padrão discutido para o modo treino por trecho.

## 4.3 Controles no aplicativo

Foram solicitados botões: - **Ativar** - **Conectar** - **Desconectar**

O usuário também solicitou: - Abrir o jogo pelo aplicativo sem Easy
Anti-Cheat (EAC), para o fluxo offline. - Botão **"Iniciar jogo"** sem
EAC. - Possibilidade de alternar o EAC automaticamente. - Vincular
botões do volante às ações de salvar e voltar trecho. - Os bindings do
volante devem atuar **somente no modo Treino por trecho**, sem
interferir em outros modos.

## 4.4 Comportamento observado e problemas

Registro de teste de outubro de 2026: - O LMU chegou a abrir sem EAC
pelo aplicativo. - `F6` salvou o trecho. - `F7` funcionou uma vez. -
Depois, apareceu a mensagem **"estado do Sector desconhecido"**. - O
aplicativo inicialmente não detectou o estado/conexão do Sector. - Em
algumas ocasiões, o jogo abria em modo offline, mas era necessário
clicar manualmente em **Conectar**. - O estado de conexão e a
comunicação entre aplicativo e Sector apresentaram comportamento
inconsistente.

## 4.5 Referência de versão

A versão 0.12.2 registrou correções relacionadas a estados do Sector
após o uso do F7 e tentativas de repetição (retries).

Os testes automatizados foram reportados como aprovados, porém a
validação real no LMU ainda precisava ser concluída.

## 4.6 Requisitos de segurança e estabilidade

-   Não presumir que o jogo está offline apenas porque o processo foi
    iniciado.
-   Detectar e exibir o estado real de conexão com o Sector.
-   Tratar estados desconhecidos explicitamente.
-   Implementar tentativas controladas de reconexão, sem loops
    infinitos.
-   Não enviar comandos de salvar/voltar se a integração não estiver
    pronta.
-   Informar ao usuário quando a ação não puder ser executada.
-   Restringir os bindings do volante ao modo de treino por trecho.
-   Não alterar o funcionamento normal do jogo ou do EAC fora do fluxo
    explicitamente escolhido pelo usuário.

------------------------------------------------------------------------

# 5. Histórico de problemas e correções

Esta seção separa os problemas relatados das correções registradas. Uma
correção descrita em histórico não deve ser considerada validada em
ambiente real sem evidência.

  --------------------------------------------------------------------------
  Área                    Problema relatado       Direção de correção /
                                                  situação conhecida
  ----------------------- ----------------------- --------------------------
  Sector                  "Estado do Sector       Versão 0.12.2 registrou
                          desconhecido" após F7   tratamento de
                                                  estados/retries; validação
                                                  no jogo pendente

  Sector                  Aplicativo não          Melhorar detecção e estado
                          detectava conexão       de conexão
                          inicialmente            

  Sector                  Jogo abria offline, mas Automatizar/estabilizar
                          exigia Conectar         conexão, mantendo botão
                          manualmente             manual como alternativa

  Delta                   Indicava volta inválida Corrigir leitura dos
                          indevidamente           estados da volta

  Delta                   Parava após saída de    Tratar saída de pista e
                          pista                   retomada da comparação

  Delta                   Não ocultava no Alt-Tab Alinhar visibilidade com
                                                  os outros cards; correção
                                                  apontada na 0.12.2

  Delta                   Espessura insuficiente  Permitir ajuste de
                                                  espessura

  Volante                 Rotação limitada ou     Usar rotação real do
                          incorreta acima de 90°  hardware e respeitar
                                                  ângulo do carro

  Pedais/motor            Congelamento            Investigar atualização de
                          intermitente            telemetria, concorrência e
                                                  recuperação

  Overlay                 Sumia ao entrar na      Problema relatado
                          pista                   anteriormente; validar
                                                  ciclo menu/garagem/pista

  Inicialização           Executável abria VS     Problema relatado; validar
                          Code em vez do          associação/empacotamento e
                          aplicativo              inicialização

  Execução                Congelamentos gerais    Testar estabilidade
                                                  prolongada e consumo de
                                                  recursos

  Redimensionamento       Dificuldade de ajustar  Revisar controles e
                          cards                   persistência do tamanho

  Download                Problema ao baixar EXE  Validar artefatos e links
                          0.12.0                  de release

  Sector                  Erro pontual ao voltar  Cobrir com testes de
                          trecho                  estado, timeout e
                                                  recuperação
  --------------------------------------------------------------------------

## 5.1 Versão 0.12.2 --- referência histórica

O registro disponível associa a versão 0.12.2 às seguintes correções: -
Tratamento dos estados do Sector após F7. - Retentativas (retries). -
Ajustes no delta durante saídas de pista e voltas inválidas. - Espessura
configurável do delta. - Ocultação do delta ao alternar de janela
(Alt-Tab).

**Situação registrada:** testes automatizados passaram; ainda faltava
validar o comportamento real dentro do Le Mans Ultimate.

## 5.2 Histórico anterior

-   A versão 0.12.0 foi citada em contexto de distribuição do executável
    e problemas de download.
-   O projeto já passou por problemas de inicialização, overlay que
    desaparecia ao entrar na pista, congelamentos e dificuldade de
    redimensionamento.
-   Não há, no contexto consolidado, uma lista confiável de todas as
    versões intermediárias nem um changelog completo. Não inventar
    versões ou datas.

------------------------------------------------------------------------

# 6. Requisitos funcionais consolidados

A lista abaixo funciona como checklist de produto. O status deve ser
atualizado após inspeção do código e testes.

## 6.1 Interface e overlay

-   [ ] Tabela de pilotos com colunas na ordem definida.
-   [ ] Identificação de pilotos, bandeiras, escuderias e carros.
-   [ ] Identificação visual dos pneus.
-   [ ] Penalidades DT/SG.
-   [ ] Líder fixo/destacado.
-   [ ] Três pilotos à frente e três atrás.
-   [ ] Janela de 10 + 1 para a classe do usuário.
-   [ ] Opção "Exibir somente o meu".
-   [ ] Multiclasse com cinco primeiros das outras classes e topo do
    líder.
-   [ ] GAP conforme treino, classificação e corrida.
-   [ ] Cabeçalho com melhor tempo, piloto e posição.
-   [ ] Coluna de posições ganhas/perdidas.
-   [ ] Ajuste de fonte por card.
-   [ ] Redimensionamento independente.
-   [ ] Opacidade individual e controle conjunto.
-   [ ] Posicionamento visual por "Posicionar".
-   [ ] F8 para visibilidade.
-   [ ] Ocultação em menu/garagem.
-   [ ] Ocultação ao pausar a leitura, conforme especificação.
-   [ ] Remoção de botões de demo/"Como usar"/navegação desnecessária.

## 6.2 Pedais, motor e volante

-   [ ] Freio à esquerda e acelerador à direita.
-   [ ] Sem rótulos nos pedais.
-   [ ] Janela visual de 10 segundos.
-   [ ] RPM em arco ao redor do volante.
-   [ ] Indicador de marcha com mudança de cor.
-   [ ] Remoção do combustível do módulo, conforme pedido.
-   [ ] Rotação real do volante.
-   [ ] Suporte a ângulos diferentes por carro.
-   [ ] Representação visual própria sem logotipo Porsche.
-   [ ] Corrigir congelamentos intermitentes.

## 6.3 Aprendizado e delta

-   [ ] Voltas válidas e inválidas.
-   [ ] Setores e microsetores.
-   [ ] Mapa da pista.
-   [ ] Comparação volta a volta.
-   [ ] Comparação de tempos setoriais online.
-   [ ] Não apresentar telemetria de pedais de terceiros.
-   [ ] Delta central transparente.
-   [ ] Espessura configurável.
-   [ ] Não exibir texto "melhor sessão".
-   [ ] Delta ocultar junto dos outros cards e no Alt-Tab.
-   [ ] Corrigir estados de volta e saída de pista.

## 6.4 Pneus e danos

-   [ ] Temperatura dos pneus.
-   [ ] Danos/condição dos pneus.
-   [ ] Danos de suspensão.
-   [ ] Danos da asa dianteira.
-   [ ] Danos da asa traseira.

## 6.5 Treino por trecho / Sector

-   [ ] Ativar modo treino por trecho.
-   [ ] Conectar ao Sector.
-   [ ] Desconectar do Sector.
-   [ ] Exibir estado de conexão confiável.
-   [ ] F6 salvar trecho.
-   [ ] F7 retornar ao trecho.
-   [ ] Tratamento de "estado desconhecido".
-   [ ] Retries com limite e timeout.
-   [ ] Iniciar jogo sem EAC no fluxo offline.
-   [ ] Alternância de EAC conforme fluxo autorizado.
-   [ ] Bindings do volante restritos ao modo treino por trecho.
-   [ ] Recuperação de falha sem travar o overlay.

------------------------------------------------------------------------

# 7. Requisitos não funcionais

## 7.1 Desempenho

-   Atualização de dados fluida durante a pilotagem.
-   Evitar congelamentos e bloqueios da interface.
-   Evitar processamento desnecessário em alta frequência.
-   Não deixar módulos inativos consumirem recursos excessivos.
-   Recuperar-se de falhas de telemetria e reconexão sem reiniciar todo
    o aplicativo.

## 7.2 Confiabilidade

-   Estados de conexão explícitos.
-   Tratamento de dados ausentes ou inválidos.
-   Logs úteis para diagnosticar falhas.
-   Erros apresentados de forma compreensível.
-   Não afirmar que uma função está conectada/ativa sem confirmação
    real.
-   Persistência correta das configurações.

## 7.3 Usabilidade

-   Controles acessíveis.
-   Textos curtos.
-   Legibilidade em movimento.
-   Configurações independentes por card.
-   Interface final sem elementos de demonstração.
-   Comportamento consistente entre menu, garagem, pista e Alt-Tab.

## 7.4 Compatibilidade

A compatibilidade deve ser verificada no ambiente real do usuário: -
Versão atual do Le Mans Ultimate. - Windows e permissões necessárias. -
Hardware de volante e pedais. - Diferentes ângulos de rotação por
carro. - Execução offline e integração com Sector. - Comportamento com e
sem EAC, conforme os fluxos previstos.

------------------------------------------------------------------------

# 8. Diretrizes para desenvolvimento

Estas são diretrizes de continuidade, não afirmações sobre a arquitetura
atual do código.

1.  Antes de modificar, inspecionar o repositório e identificar a
    arquitetura real.
2.  Não reescrever módulos inteiros sem necessidade.
3.  Mapear a origem dos dados de cada card.
4.  Separar captura de telemetria, processamento, estado da aplicação e
    renderização, se a arquitetura permitir.
5.  Centralizar e documentar os estados de sessão, volta, conexão e
    visibilidade.
6.  Evitar que uma falha em Sector derrube os demais módulos.
7.  Isolar os bindings de volante para que só atuem no modo treino por
    trecho.
8.  Criar testes para transições de estado e cenários de falha.
9.  Preservar os comportamentos já aprovados.
10. Registrar alterações em changelog com versão, data, descrição e
    validação.
11. Não marcar requisito como concluído apenas porque o código compila.
12. Diferenciar testes automatizados de testes dentro do LMU.

## 8.1 Estados que merecem tratamento explícito

### Estado do jogo

-   Não iniciado.
-   Inicializando.
-   Menu.
-   Garagem.
-   Pista.
-   Telemetria pausada.
-   Encerrado.
-   Estado desconhecido.

### Estado da conexão Sector

-   Desconectado.
-   Conectando.
-   Conectado.
-   Reconectando.
-   Erro.
-   Estado desconhecido.

### Estado da volta

-   Não iniciada.
-   Em andamento.
-   Válida.
-   Inválida.
-   Saída de pista / condição especial.
-   Concluída.
-   Sem dados suficientes.

Esses estados são uma proposta de organização para orientar a auditoria
e os testes; verificar como o código já os representa antes de
introduzir novos modelos.

------------------------------------------------------------------------

# 9. Testes de aceitação sugeridos

## 9.1 Teste de sessão

1.  Abrir o aplicativo.
2.  Abrir o LMU.
3.  Entrar no menu.
4.  Entrar na garagem.
5.  Entrar na pista.
6.  Verificar se o overlay aparece e permanece estável.
7.  Alternar Alt-Tab.
8.  Retornar ao jogo.
9.  Pausar/retomar a leitura, quando aplicável.
10. Encerrar a sessão.

**Critério:** o overlay deve respeitar as regras de visibilidade e não
desaparecer indevidamente.

## 9.2 Teste da tabela

1.  Entrar em sessão com múltiplas classes.
2.  Verificar identificação de classe, piloto e carro.
3.  Conferir ordem das colunas.
4.  Verificar três pilotos à frente e três atrás.
5.  Conferir a janela 10 + 1 da classe do usuário.
6.  Ativar "Exibir somente o meu".
7.  Conferir líderes e cinco primeiros das demais classes.
8.  Verificar mudanças de posição, gap e penalidades.

**Critério:** dados corretos, hierarquia legível e nenhuma alteração
indevida na ordem das colunas.

## 9.3 Teste do volante e pedais

1.  Girar o volante lentamente.
2.  Girar acima de 90 graus.
3.  Verificar o limite real do hardware/carro.
4.  Testar carros com ângulos diferentes.
5.  Pressionar freio e acelerador separadamente.
6.  Verificar posição das barras.
7.  Acompanhar RPM e troca de marcha.
8.  Deixar o módulo funcionando por período prolongado.

**Critério:** valores coerentes, sem congelamentos ou inversões
inesperadas.

## 9.4 Teste do delta

1.  Iniciar uma volta válida.
2.  Comparar o delta com a referência.
3.  Sair da pista.
4.  Retornar à pista.
5.  Completar volta inválida.
6.  Iniciar nova volta válida.
7.  Alternar Alt-Tab.
8.  Alterar a espessura do delta.

**Critério:** não classificar estados incorretamente, não travar após
saída de pista e respeitar a visibilidade geral.

## 9.5 Teste do Sector

1.  Iniciar o fluxo offline previsto.
2.  Abrir o LMU sem EAC pelo caminho configurado.
3.  Ativar o modo treino por trecho.
4.  Conectar.
5.  Confirmar estado conectado.
6.  Pressionar F6 e salvar.
7.  Pressionar F7 e retornar.
8.  Repetir F6/F7 várias vezes.
9.  Desconectar e conectar novamente.
10. Simular falha/indisponibilidade do Sector.
11. Testar bindings do volante.
12. Sair do modo treino por trecho e confirmar que os bindings não
    interferem em outros modos.

**Critério:** conexão e estados confiáveis; salvar/retornar
repetidamente sem estado desconhecido persistente ou travamento.

------------------------------------------------------------------------

# 10. GitHub e apresentação do projeto

O objetivo é deixar o repositório com aparência profissional e
documentação suficiente para que outro desenvolvedor consiga entender o
produto.

## 10.1 Organização sugerida

A estrutura real deve ser preservada quando já existir. A sugestão
abaixo é uma referência, não uma exigência de reorganização:

``` text
Arts-LMU-Overlay/
├── README.md
├── CHANGELOG.md
├── LICENSE
├── docs/
│   ├── MASTER_PROJECT.md
│   ├── FEATURES.md
│   ├── INSTALLATION.md
│   ├── CONFIGURATION.md
│   ├── TROUBLESHOOTING.md
│   ├── TESTING.md
│   └── media/
├── src/
├── tests/
├── assets/
└── releases/
```

Não criar pastas vazias nem mover arquivos sem primeiro entender a
estrutura atual.

## 10.2 README recomendado

O README público deve ser uma apresentação do produto, não um despejo de
detalhes técnicos.

Sugestão de seções: 1. Nome e banner do Arts LMU OVERLAY. 2. Descrição
curta do aplicativo. 3. Principais funcionalidades. 4. Imagens reais do
overlay. 5. Demonstração em funcionamento, se disponível. 6. Instalação
e requisitos. 7. Configuração de atalhos. 8. Status do projeto. 9.
Roadmap. 10. Créditos e dependências. 11. Aviso de compatibilidade com
versões do LMU.

Usar somente capturas reais do aplicativo. Não inventar telas, métricas,
recursos concluídos ou compatibilidade.

## 10.3 Changelog

Cada versão deve registrar: - Número da versão. - Data. - Novidades. -
Correções. - Alterações técnicas relevantes. - Testes executados. -
Pendências conhecidas.

## 10.4 Releases

Antes de publicar um executável: - Confirmar que o arquivo foi gerado
corretamente. - Testar em ambiente limpo. - Verificar inicialização sem
abrir VS Code. - Verificar dependências. - Confirmar que o download
funciona. - Informar versão e mudanças. - Não publicar build sem teste
mínimo.

------------------------------------------------------------------------

# 11. Orientações específicas para o Google Carbon

Você está recebendo este documento como contexto de um projeto já
desenvolvido e discutido ao longo de várias iterações.

## 11.1 Primeira tarefa: auditoria, não reescrita

Antes de alterar qualquer código:

1.  Acesse o repositório GitHub indicado pelo usuário.
2.  Leia o README, estrutura de diretórios, changelog e arquivos de
    configuração.
3.  Identifique linguagem, framework, bibliotecas, ponto de entrada e
    processo de build.
4.  Mapeie os módulos existentes para as funcionalidades descritas neste
    documento.
5.  Identifique quais requisitos já estão implementados, parcialmente
    implementados ou ausentes.
6.  Identifique bugs reproduzíveis e testes existentes.
7.  Apresente um diagnóstico antes de fazer alterações estruturais.

## 11.2 Classificação obrigatória de status

Use quatro estados:

-   **Implementado e validado:** existe no código e foi testado no
    ambiente adequado.
-   **Implementado, mas não validado:** existe no código, mas falta
    teste real.
-   **Parcial:** parte da funcionalidade existe.
-   **Pendente:** não foi localizada ou ainda precisa ser desenvolvida.

Não transforme um pedido antigo em uma afirmação de que a função está
pronta.

## 11.3 Restrições de trabalho

-   Não apagar funcionalidades existentes sem autorização.
-   Não mudar o layout e a ordem das colunas da tabela sem autorização.
-   Não substituir a identidade visual por um template genérico.
-   Não remover recursos apenas porque parecem complexos.
-   Não criar funcionalidades fictícias para preencher o README.
-   Não alterar atalhos definidos sem registrar e justificar.
-   Não tratar teste automatizado como validação dentro do jogo.
-   Não fazer refatoração ampla antes de compreender dependências e
    fluxo de dados.
-   Priorizar correções pequenas, testáveis e reversíveis.

## 11.4 Entregáveis esperados da auditoria

1.  Mapa da arquitetura atual.
2.  Inventário de funcionalidades e status.
3.  Lista de bugs e riscos.
4.  Lista de testes existentes e ausentes.
5.  Proposta de organização documental.
6.  Plano de trabalho priorizado.
7.  Confirmação de quais itens podem ser documentados publicamente.

Depois da auditoria, organizar o GitHub e atualizar a documentação com
informações confirmadas pelo código.

------------------------------------------------------------------------

# 12. Prioridades sugeridas para continuidade

A ordem abaixo é uma sugestão baseada nos problemas e objetivos
registrados.

### Prioridade 1 --- estabilidade básica

-   Inicialização correta do executável.
-   Overlay presente em menu, garagem e pista conforme regras.
-   Corrigir congelamentos.
-   Visibilidade e Alt-Tab.
-   Persistência das configurações.

### Prioridade 2 --- telemetria essencial

-   Tabela de pilotos.
-   Pedais e motor.
-   Rotação real do volante.
-   Estado de volta e delta.

### Prioridade 3 --- treino por trecho

-   Conexão confiável com Sector.
-   F6/F7 repetíveis.
-   Tratamento de estado desconhecido.
-   Fluxo offline e EAC.
-   Bindings restritos ao modo correto.

### Prioridade 4 --- recursos de análise

-   Aprendizado.
-   Setores e microsetores.
-   Comparação de voltas.
-   Card de pneus e danos.
-   Mapa de pista.

### Prioridade 5 --- acabamento e publicação

-   Redimensionamento e opacidade por card.
-   Ajustes visuais finais.
-   Capturas reais.
-   README e changelog.
-   Releases testadas.

------------------------------------------------------------------------

# 13. Pontos que ainda precisam ser confirmados no repositório

O histórico de conversa não contém todos os detalhes de implementação. O
agente que receber este documento deve verificar:

-   Linguagem e framework efetivamente utilizados.
-   Arquitetura e estrutura de pastas atuais.
-   Origem e formato dos dados de telemetria.
-   Como o overlay se comunica com o LMU.
-   Como o Sector é iniciado, detectado e controlado.
-   Como o EAC é tratado no fluxo offline.
-   Quais cards já existem no código.
-   Quais configurações são persistidas.
-   Quais atalhos estão efetivamente implementados.
-   Qual é a versão mais recente real do executável.
-   Quais correções da versão 0.12.2 chegaram ao build final.
-   Se a validação real no LMU foi concluída depois do registro de
    outubro de 2026.
-   Licenças e condições de uso das dependências, imagens, logos e do
    projeto Sector.

------------------------------------------------------------------------

# 14. Resumo executivo

O Arts LMU OVERLAY é um projeto de overlay para Le Mans Ultimate com
foco em informação de corrida, personalização, telemetria e aprendizado
do piloto.

Os principais blocos do produto são:

1.  **Race Dashboard:** tabela de pilotos, posições, gaps, classes,
    pneus e penalidades.
2.  **Driver Inputs:** pedais, RPM, marcha e volante com rotação real.
3.  **Learning:** voltas, setores, microsetores e comparação de
    desempenho.
4.  **Delta:** barra central transparente para acompanhamento de
    ganho/perda.
5.  **Vehicle Status:** pneus, temperaturas e danos.
6.  **Track Map:** mapa da pista.
7.  **Sector Training:** salvar e retornar a trechos no modo offline.
8.  **Overlay Settings:** visibilidade, opacidade, posicionamento,
    tamanho e atalhos.

A decisão mais importante para a continuidade é **auditar o código real
antes de implementar ou reescrever qualquer coisa**. Este documento
registra requisitos, decisões e problemas históricos; o repositório deve
determinar o que está efetivamente pronto.

O objetivo final é transformar o Arts LMU OVERLAY em um produto estável,
bonito, confiável e bem documentado, preservando as decisões de
experiência de uso construídas ao longo do desenvolvimento.

------------------------------------------------------------------------

## Nota de precisão histórica

Este documento foi montado a partir do contexto e dos registros de
conversas disponíveis. Ele é abrangente em relação aos requisitos e
decisões recuperados, mas não representa uma inspeção direta do
repositório nem prova de que cada funcionalidade foi implementada. Onde
o histórico não confirma o estado final, o texto indica que é necessário
verificar.


------------------------------------------------------------------------

# 15. Adendo de Auditoria Técnica — Consolidação `v0.12.11` (Novembro de 2026)

Após inspeção direta do repositório Git (`0.12.4`), dos pacotes intermediários (`0.12.5`/`0.12.6`) e descompilação comparativa do executável instalado (`0.12.11`), foram confirmados e consolidados no código-fonte:

1. **Stack real verificada:** C# 5 / `.NET Framework 4.8` (`x64`), WinForms + GDI+, `NAudio 2.2.1`, `DirectInput` nativo e `SectorRuntime-1.0.0.zip`.
2. **Módulo adicional incorporado (`0.12.5` → `0.12.11`):** **Chat por Voz Local com Whisper** (`src/VoiceChat.cs`, `src/VoiceChatSettings.cs`, `src/VoiceStatusOverlay.cs`), com acionamento por botão do volante ou tecla vinculada, overlay de status na pista (`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT`) e envio por digitação automática (*scan codes*) ou `Ctrl+V`.
3. **Ajustes de visibilidade da `0.12.11`:** verificação ativa do processo `Le Mans Ultimate` (`GameRunning()`), manutenção da visibilidade do card durante edição de aparência (`controllerForeground` / `appearancePanelIndex`) e contenção de `EnsureVisible()` fora do foco do jogo (`Alt+Tab`).
4. **Pontos parciais documentados com precisão (para evolução futura se desejado):**
   - Na tabela (`src/Renderer.cs`), a chamada atual usa `RaceView.Build(f, true)` (exibindo somente a classe do usuário, com até 3 carros à frente + piloto + 3 atrás + líder fixo);
   - No card de Pneus e Danos (`src/VehicleDamage.cs`), os danos aerodinâmicos aparecem consolidados em `AERO` geral + `PEÇA SOLTA` (pois asas dianteira e traseira separadas não possuem campos confirmados no endpoint REST local do LMU).
