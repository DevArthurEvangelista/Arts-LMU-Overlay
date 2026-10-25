# Contexto para Codex — Arts LMU OVERLAY

Projeto pessoal de Arthur Gabriel. Responda em português claro. Base atual: versão 0.12.4, código C# 5 / .NET Framework 4.8 / WinForms e GDI, alvo Windows x64. Priorize compatibilidade com LMU em treino, classificação e corrida e preserve cartões, layouts, fontes, transparência, teclas e posições salvos. Não confunda testes Mono/Linux com validação real no Windows/LMU.

## Interface e comportamento acordados

- App local, sem login, cards reposicionáveis e dimensionáveis, F8 alterna visibilidade na pista; oculta automaticamente no menu/garagem/Alt+Tab.
- Classificação com POS, +/-, GER, PILOTO/CARRO, ÚLTIMA, MELHOR, PNEU, GAP GER., PEN., BOX. Exibir classe do usuário e permitir acompanhar vizinhos/líder. Bandeiras, logos coloridas, pneus por cor e penalidades. Legibilidade alta sobre fundo de jogo.
- Freio à esquerda, acelerador à direita, gráfico dos últimos 10 s, arco RPM em torno do volante, volante 1:1 conforme calibração, temperatura/danos de pneus, lap timer e mapa.
- Delta e treino por trecho são módulos separados. Delta compara voltas válidas por posição e conserva referências por pista/carro/classe. Treino por trecho utiliza Sector original para salvar/retornar durante treino offline compatível.
- Volante: botões vinculáveis para salvar/retornar somente com treino por trecho conectado e jogo em primeiro plano; F6/F7 continuam disponíveis. Vincular botão não cancela função que o próprio LMU tenha atribuído a ele.

## Segurança/compatibilidade do Sector

Leia `docs/DELTA-SECTOR.md` e `docs/VALIDACAO-0.12.4.md` antes de alterar o módulo. Treino por trecho exige jogo offline sem EAC, modo Practice 1–4 e build compatível (LMU 14200 / Steam 25661166 na versão integrada); nunca habilite retorno em modo online/qualificação/corrida ou com EAC. O app pode iniciar diretamente `Le Mans Ultimate.exe` escolhido pelo usuário, mas não deve fechar EAC, alterar executáveis ou remover verificações nativas. Runtime original empacotado em `assets/SectorRuntime-1.0.0.zip` permanece sem modificações.

O status do Sector é publicado por substituição atômica. Em 0.12.4, `SectorStatusReader` lê com `FileShare.ReadWrite | FileShare.Delete` e tolera conflito Windows 32/33 por até 3 s; erro persistente desconecta com mensagem. Não envie comando de botão quando a leitura atual foi adiada.

## Estrutura e checagens

- `src/`: aplicativo; `src/SectorIntegration.cs`, `src/SectorStatusReader.cs`, `src/TrainingWheelButtons.cs`, `src/TrainingButtonsSettings.cs` são centrais ao treino/bindings.
- `assets/`: recursos e runtime original; `third_party/`: licenças.
- `tests/`: fixtures e testes; `tools/`: compilação, validação e empacotamento; `installer/Setup.nsi`: instalador.
- `LEIA-ME.txt`, `DESIGN.md`, `docs/`: uso, design, telemetria, decisões e validações históricas.
- Última verificação: 565 asserções automatizadas e 24 de migração/configuração em Mono/Linux, compilação PE Windows x64 e pacote NSIS inspecionados. **Teste em LMU/Windows/volante físico ainda depende de Arthur.**

Ao alterar o código, atualize a versão em `src/Branding.cs`, `LMUOverlay.csproj`, `src/app.manifest` e `installer/Setup.nsi`, quando for preparar nova release. Execute testes pertinentes e preserve dados antigos. Não inclua `bin`, `obj`, instaladores, diagnósticos, configurações e voltas do usuário no Git.

## Próximo passo

Arthur está migrando o projeto para GitHub e Codex no VS Code. Primeiro confirme que compila no Windows dele e reproduza o erro real se surgir. Não afirme que novos recursos pedidos em conversas anteriores estejam pendentes sem conferir o código. Se ele quiser histórico integral da conversa, peça exportação do chat; este pacote registra decisões essenciais, não transcrição completa.
