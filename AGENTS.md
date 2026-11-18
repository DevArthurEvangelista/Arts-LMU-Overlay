# Contexto para Agentes — Arts LMU OVERLAY

Projeto de Arthur Gabriel Evangelista Santos. Responda em português claro. Base atual: versão **0.12.11**, código C# 5 / .NET Framework 4.8 / WinForms e GDI+, alvo Windows x64 (`NAudio 2.2.1` via NuGet). Priorize compatibilidade com Le Mans Ultimate em treino, classificação e corrida e preserve cartões, layouts, fontes, transparência, teclas e posições salvos (`SchemaVersion = 11`). Não confunda testes automatizados com validação real dentro do LMU com hardware físico.

## Interface e comportamento acordados

- App local, sem login, cards reposicionáveis e dimensionáveis individualmente, `F8` alterna visibilidade na pista; oculta automaticamente no menu/garagem/`Alt+Tab` e quando o processo `Le Mans Ultimate` não está aberto (`GameRunning()`). Enquanto o painel principal ou o editor de aparência de um card está em foco na pista, o card permanece visível para pré-visualização ao vivo.
- Classificação com a ordem fixa de colunas: `POS`, `+/-`, `GER`, `PILOTO / CARRO`, `ÚLTIMA`, `MELHOR`, `PNEU`, `GAP GER.` (corrida) / `Δ SUA VOLTA` (treino/quali), `PEN.`, `BOX`. Exibe a classe do usuário com vizinhos e líder fixo, bandeiras, logos coloridas, pneus por cor e penalidades (`DT`, `SG`). Legibilidade alta sobre fundo de jogo.
- Freio à esquerda, acelerador à direita, gráfico dos últimos 10 s, arco RPM circular em torno do volante, volante 1:1 conforme calibração física DirectInput, temperatura/pressão/desgaste/danos de pneus e suspensão/aero geral, lap timer, combustível e mapa da pista.
- Delta e treino por trecho são módulos separados. Delta compara voltas válidas por distância e conserva referências por pista/carro/classe. Treino por trecho utiliza Sector original (`v1.0.0`) para salvar (`F6`) e retornar (`F7`) durante treino offline compatível.
- Volante: botões vinculáveis para salvar/retornar somente com treino por trecho conectado e jogo em primeiro plano.
- Chat por voz local (`VoiceChat.cs`, `VoiceChatSettings.cs`, `VoiceStatusOverlay.cs`): usa `NAudio` + `whisper.cpp` (`Whisper/whisper-cli.exe` e modelo `.bin` local), acionado por botão do volante ou tecla configurável, com indicador visual na pista (`WS_EX_NOACTIVATE | WS_EX_TRANSPARENT`) e envio por digitação nativa (*scan codes*) ou `Ctrl+V`.

## Segurança e compatibilidade do Sector

Leia `docs/MASTER_PROJECT.md`, `docs/DELTA-SECTOR.md` e `docs/VALIDACAO-0.12.4.md` antes de alterar o módulo. Treino por trecho exige jogo offline sem EAC, modo Practice 1–4 e build compatível; nunca habilite retorno em modo online/qualificação/corrida ou com EAC. O app pode iniciar diretamente `Le Mans Ultimate.exe` escolhido pelo usuário, mas não deve fechar EAC, alterar executáveis ou remover verificações nativas. O pacote `assets/SectorRuntime-1.0.0.zip` permanece sem modificações.

## Estrutura e checagens

- `src/`: código-fonte do aplicativo (`LMUOverlay.csproj`).
- `assets/`: recursos embutidos (`App.ico`, `BrandLogo.png`, `CarLogos.zip`, `Flags.zip`) e `SectorRuntime-1.0.0.zip`.
- `third_party/`: licenças de terceiros e referência `lmu_data.py`.
- `tests/`: fixtures e bateria automatizada (`dotnet run --project tests/Checks.csproj -c Release -- tests/lmu-fixture.bin tests/render-check`, executada a partir da raiz do repositório).
- `docs/`, `README.md`, `CHANGELOG.md`, `LEIA-ME.txt`: documentação pública e técnica.
- `installer/Setup.nsi` e `COMPILAR.cmd`: compilação e geração do instalador NSIS.

Ao alterar o código, atualize a versão em `src/Branding.cs`, `LMUOverlay.csproj`, `src/app.manifest` e `installer/Setup.nsi` quando preparar nova release. Execute os testes automatizados e preserve configurações antigas. **Nunca versione** `bin/`, `obj/`, `Whisper/` (modelos `.bin` ultrapassam 100 MB), instaladores `.exe`, diagnósticos, configurações ou voltas do usuário no Git.
