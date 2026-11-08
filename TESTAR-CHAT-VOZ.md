# Chat por voz 0.12.6 — versão experimental

O Arts agora permite escolher um microfone, um idioma e um modelo Whisper local. Esta branch não foi compilada ou testada no Windows/LMU neste ambiente; não substitua a versão estável antes do teste.

1. Instale o SDK .NET para compilar net48 e execute `COMPILAR.cmd` (o projeto restaura NAudio 2.2.1 pelo NuGet).
2. Baixe uma compilação Windows x64 de `whisper-cli.exe` do projeto oficial `ggml-org/whisper.cpp` (inclua as DLLs distribuídas com ele na mesma pasta). Obtenha separadamente o modelo `ggml-small.en.bin` (inglês, ~466 MiB) ou `ggml-base.en.bin` (~142 MiB) de `ggerganov/whisper.cpp` no Hugging Face. Para português use um modelo multilíngue `ggml-small.bin` ou `ggml-base.bin`, não `.en`. O app não baixa o modelo automaticamente.
3. No Arts, em Atalhos > Falar no chat, escolha seu microfone, indique o caminho de `whisper-cli.exe` e do modelo `.bin`, escolha Inglês/Português, vincule um botão livre e ative o envio.
4. No LMU, vincule Realtime Chat à tecla Enter e deixe o campo fechado. Com LMU em primeiro plano, segure o botão, fale, solte. O app cria áudio temporário local de até 12 s, executa Whisper em segundo plano e apaga WAV e TXT depois; se o foco mudar, não envia.

A inferência pode demorar alguns segundos. O chat do LMU ainda precisa ser validado em Windows com microfone/volante real; `SendInput` Unicode pode não ser aceito pelo jogo. Este recurso não usa nem altera o Sector ou o EAC. Verifique as regras de chat do evento online.

Referências oficiais: https://github.com/ggml-org/whisper.cpp e https://huggingface.co/ggerganov/whisper.cpp
