# Instalação e Compilação — Arts LMU OVERLAY

## 1. Instalação pelo Executável (Usuários)

1. Se uma versão anterior estiver aberta, clique em **Encerrar o app** no painel ou no ícone junto ao relógio do Windows.
2. Baixe e execute `Instalar-Arts-LMU-Overlay-0.12.11.exe`.
   - A instalação é feita na pasta do seu usuário (`%LOCALAPPDATA%\Programs\ArthurLMUOverlay`), **sem pedir administrador**.
   - Suas posições, tamanhos, fontes, opacidades, calibração do volante e histórico de voltas (`%LOCALAPPDATA%\ArthurLMUOverlay`) são preservados automaticamente.
3. No **Le Mans Ultimate**, configure o modo de tela em **Janela sem bordas (*Borderless*)** para permitir que os overlays fiquem visíveis sobre o jogo.

### Requisitos de Sistema
- Windows 10 ou 11 de **64 bits**.
- **.NET Framework 4.8** (nativo no Windows 10/11 atualizado).

---

## 2. Compilação pelo Código-Fonte (VS Code / Desenvolvedores)

### Pré-requisitos
- [.NET SDK](https://dotnet.microsoft.com/download) instalado (com suporte a compilação `net48`).
- *(Opcional, para gerar o instalador `.exe`)* [NSIS 3.x](https://nsis.sourceforge.io/).
- *(Opcional, para empacotar o Chat por Voz local no instalador)* pasta `Whisper\` na raiz do projeto contendo `whisper-cli.exe`, DLLs do `whisper.cpp` (`whisper.dll`, `ggml*.dll`) e o modelo `.bin` (ex.: `ggml-small.en.bin`).
  > **Importante:** a pasta `Whisper/` está no `.gitignore` porque o modelo `.bin` possui ~465 MB (acima do limite de 100 MB por arquivo no Git).

### Compilar o aplicativo
No terminal na pasta do projeto:

```powershell
dotnet build LMUOverlay.csproj -c Release
```

Ou dê dois cliques em `COMPILAR.cmd`, que compila em modo `Release` e copia `LMUOverlay.exe` e `NAudio*.dll` para a raiz.

### Executar os testes automatizados
Sempre a partir da raiz do repositório:

```powershell
dotnet run --project tests/Checks.csproj -c Release -- tests/lmu-fixture.bin tests/render-check
```
