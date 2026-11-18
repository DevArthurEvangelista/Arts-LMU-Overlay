# Configuração e Uso — Arts LMU OVERLAY

## 1. Posicionamento, Tamanho e Aparência dos Cards

- **Posicionar cards:** marque **Posicionar** no painel principal (ou pressione `Ctrl + Alt + L`). Arraste cada card pelo centro e redimensione livremente pelas bordas/cantos. Pressione `Esc` ou desmarque **Posicionar** para travar.
- **Fonte e dimensões por card:** na aba de painéis, clique em **Fonte e tamanho** no card desejado para ajustar escala da fonte, família tipográfica e largura/altura de forma independente. Se você estiver na pista, o card editado permanece visível em tempo real durante o ajuste.
- **Opacidade:** ajuste separadamente a opacidade do card de pedais e dos cards de informações, ou marque a opção de vincular opacidades para mover ambos juntos.

## 2. Visibilidade e Atalhos

- **Atalho na pista (`F8` padrão):** oculta ou reexibe todos os cards habilitados enquanto você está pilotando. Pode ser alterado na aba **Atalhos**.
- **Ocultação automática:** todos os cards (incluindo o Delta Central) ocultam sozinhos no menu, na garagem, em pausas de leitura, no `Alt + Tab` e imediatamente quando o processo `Le Mans Ultimate` é fechado.
- **Fechar janela vs Encerrar:** fechar a janela de configurações mantém os overlays ativos em segundo plano (ícone junto ao relógio). Para fechar tudo, use **Encerrar o app**.

## 3. Calibração do Volante Físico

1. Conecte o volante USB antes ou durante o uso.
2. Ative a leitura do volante físico e clique em **Calibrar volante**.
3. Siga os passos na tela para registrar o centro e a rotação além de 90°, garantindo correspondência angular 1:1 com o hardware e a faixa de direção do carro.

## 4. Treino por Trecho Offline (Sector)

> **Exclusivo para sessões Practice offline sem Easy Anti-Cheat (EAC) na versão suportada do LMU.**

1. Na aba **Treino por trecho**, clique em **Selecionar pasta do LMU** e aponte para a pasta onde está `Le Mans Ultimate.exe`.
2. Com o jogo fechado, clique em **Iniciar LMU sem EAC**.
3. Entre em uma sessão de **Treino Offline** no jogo; o Arts aguarda a admissão e conecta quando validar o contexto.
4. Na pista, use `F6` (ou botão vinculado do volante) para **salvar o ponto** e `F7` (ou segundo botão vinculado) para **retornar**.
5. Para correr online novamente, clique em **Desconectar**, feche o LMU e inicie normalmente pela Steam (com EAC).

## 5. Chat por Voz Local (Whisper — Experimental)

1. No Le Mans Ultimate, associe o **Realtime Chat** à tecla `Enter` e deixe o campo de texto fechado.
2. No Arts (**Atalhos > Falar no chat do LMU**):
   - Selecione o **Microfone**;
   - Confirme os caminhos de `whisper-cli.exe` e do modelo `.bin` (padrão: pasta `Whisper\` da instalação; use `ggml-small.en.bin` para Inglês ou um modelo multilíngue como `ggml-small.bin` para Português);
   - Escolha o modo de entrega (**Digitar automaticamente** ou **Colar com Ctrl+V**);
   - Clique em **Vincular botão** (volante) ou **Vincular tecla** (teclado) e marque **Ativar envio automático**.
3. Na pista com o LMU em foco, **segure** o botão/tecla, fale (até 12 s) e **solte**. O indicador visual mostrará *Ouvindo* → *Transcrevendo* → *Enviando* → *Concluído*. Se o jogo perder o foco, o envio é cancelado por segurança.
