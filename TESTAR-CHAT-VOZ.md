# Chat por voz 0.12.8

O instalador completo contém o aplicativo, o `whisper-cli.exe`, suas DLLs e o modelo inglês. Não é necessário selecionar os arquivos ou baixar o modelo após instalar.

1. No Arts, abra **Falar no chat do LMU**, selecione o microfone, escolha **Vincular botão** para um botão físico do volante ou **Vincular tecla** para uma tecla/combinação de teclado, e marque **Ativar envio automático**.
2. No LMU, configure **Realtime Chat = Enter**. Entre na pista com o jogo em primeiro plano.
3. Segure o botão/atalho enquanto fala (até 12 segundos), depois solte. O indicador mostra **Ouvindo**, **Transcrevendo** e **Concluído** ou o motivo da falha. O último estado permanece cinco segundos.
4. Para diagnóstico, observe a última mensagem na seção de voz do painel. **Concluído** significa que o comando de digitação foi enviado ao Windows; o jogo ainda precisa aceitar essa entrada no chat.

Teste manual necessário no LMU online: confirme que Enter abre o chat, que a mensagem aparece na caixa e que Enter a publica. O teste automatizado do build valida o executável Whisper e a transcrição com o modelo incluído, mas não consegue simular o jogo, o volante, o microfone nem o EAC.
