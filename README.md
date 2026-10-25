# Arts LMU OVERLAY

Overlay local para Le Mans Ultimate no Windows x64. Versão de referência: **0.12.4**. Inclui cards de classificação, pedais e RPM/volante, pneus/danos, voltas/combustível, mapa, delta e treino por trecho offline. Veja `LEIA-ME.txt` para operação e limitações.

## Abrir no VS Code

1. Extraia esta pasta e abra **esta pasta** no VS Code (`Arquivo > Abrir Pasta`).
2. Instale o SDK .NET capaz de compilar projetos `net48` e execute `dotnet build LMUOverlay.csproj -c Release` no Windows; a primeira compilação pode precisar de acesso ao NuGet para `Microsoft.NETFramework.ReferenceAssemblies`. Como alternativa, no Windows com compilador .NET Framework disponível, execute `COMPILAR.cmd`.
3. O EXE compilado fica em `bin/Release/net48/` pelo `dotnet build`, ou na raiz pelo `COMPILAR.cmd`. Para gerar o instalador, deixe `LMUOverlay.exe` na raiz, instale NSIS e compile `installer/Setup.nsi`.

Este pacote é **código-fonte para GitHub**, sem instalador nem EXE pré-compilado. O runtime original do Sector 1.0.0 está em `assets/SectorRuntime-1.0.0.zip` para empacotamento; licença em `third_party/Sector-MIT.txt`. Verifique os direitos de uso das marcas/logos antes de divulgar o repositório ou comercializar o produto; veja `docs/ASSETS.md` e `third_party/CarLogos-NOTICE.txt`.

## Continuar com o Codex

Abra esta pasta no VS Code e diga: **“Leia AGENTS.md, README.md e docs/DELTA-SECTOR.md; continue o Arts LMU OVERLAY a partir da versão 0.12.4.”**

`AGENTS.md` registra decisões e próximos passos. Os documentos em `docs/` guardam a validação e as limitações técnicas, mas não reproduzem integralmente o histórico do chat.

## Dados locais

Configurações, diagnósticos, sessões do Sector e voltas do usuário ficam no computador e não devem ser enviados ao GitHub. Este pacote não contém essas informações.
