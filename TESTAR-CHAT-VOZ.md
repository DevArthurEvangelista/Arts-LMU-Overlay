# Chat por voz 0.12.11

O instalador completo contém o aplicativo, o Whisper, suas DLLs e o modelo inglês. Não é necessário baixar arquivos separados. As configurações e posições dos painéis são preservadas.

1. No Arts, abra **Falar no chat do LMU**, escolha o microfone e mantenha **Ativar envio automático** marcado.
2. Em **Envio ao chat**, use **Digitar automaticamente (recomendado)**. Este é o padrão ao atualizar: não depende de o LMU aceitar Ctrl+V.
3. No LMU, configure **Realtime Chat = Enter**. Entre na pista com o jogo em primeiro plano e o chat fechado.
4. Segure o botão/tecla de voz, fale e solte. Aguarde **Ouvindo → Transcrevendo → Enviando → Concluído** antes da próxima gravação. O resultado fica visível por cinco segundos.
5. O Arts abre o chat com Enter, digita a frase por teclas e confirma com Enter. Cada tecla permanece pressionada por 70 ms, com intervalo entre teclas. Frases longas levam mais tempo; o máximo é 180 caracteres. Não mexa no chat durante a sequência.

A opção **Colar com Ctrl+V (compatibilidade)** permanece disponível, com as mesmas pausas nas teclas. Ela copia a frase para a área de transferência e depende de suporte à colagem no chat. Não há tentativa automática de trocar de modo ou reenviar: isso poderia publicar duas mensagens.

Ao perder o foco, sair da pista ou desativar a função, o envio para e as teclas pressionadas pelo app são soltas. Se uma sequência for interrompida com a caixa aberta, feche o chat manualmente antes de tentar de novo. O app não consegue consultar se a caixa do LMU está aberta ou se a mensagem foi recebida. **Concluído** indica que o Windows aceitou a sequência, não uma confirmação do servidor.

Se ainda falhar, clique em **Abrir diagnóstico do envio**. O arquivo registra modo, quantidade de caracteres, etapas e erros, sem registrar o texto da conversa. Ele fica em `%LOCALAPPDATA%\ArthurLMUOverlay\erro.txt`. Informe se parou ao abrir, ao digitar ou ao confirmar e envie esse arquivo. Jogo e Arts devem estar no mesmo nível de permissão; não é necessário desligar o EAC para usar voz.

Validação automatizada: comandos com duração, três envios consecutivos, cancelamento, liberação de teclas após erro, modelo de transcrição e testes de interface Windows. O receptor simulado a 30 Hz demonstra que a sequência antiga de 0.12.10 podia passar sem nenhuma tecla detectada. Isso não comprova qual mecanismo de entrada o LMU usa; a recepção dentro do jogo precisa ser validada nele.
