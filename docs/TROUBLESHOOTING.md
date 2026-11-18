# Solução de Problemas (Troubleshooting) — Arts LMU OVERLAY

## Os overlays não aparecem dentro do jogo
1. **Modo de vídeo do LMU:** certifique-se de que o Le Mans Ultimate está em **Janela sem bordas (*Borderless*)** (tela cheia exclusiva impede sobreposição de janelas GDI+ do Windows).
2. **Você está no menu ou na garagem?** Por projeto, os cards ficam ocultos fora da pista e só aparecem quando há telemetria ao vivo com o carro na pista (ou enquanto o modo **Posicionar** está marcado).
3. **Atalho `F8` acionado:** pressione `F8` (ou use o menu do ícone junto ao relógio) na pista para verificar se os painéis foram ocultados manualmente.
4. **Plugin / Memória Compartilhada:** verifique no `diagnostico.txt` (em `%LOCALAPPDATA%\ArthurLMUOverlay`) se a leitura de memória compartilhada do LMU está conectada.

## O Treino por Trecho (Sector) recusa conectar ou desconecta
- **Jogo aberto pela Steam com EAC:** o Sector bloqueia qualquer sessão com Easy Anti-Cheat ativo, sessões online, qualificação ou corrida. Feche o jogo e abra pelo botão **Iniciar LMU sem EAC** no Arts, entrando em **Practice offline**.
- **Atualização recente do LMU:** o runtime `Sector 1.0.0` valida o build exato do jogo (`appBuild 14200`). Quando o LMU recebe patch na Steam, o Sector recusa a conexão por segurança até que haja runtime compatível. Os demais módulos do Arts (tabela, pedais, delta, aprendizado) continuam funcionando normalmente.
- **Mensagem após usar `F7`:** na `0.12.4+`, conflitos rápidos de leitura do `status.json` são tolerados por até 3 s; se persistir, verifique `%LOCALAPPDATA%\ArthurLMUOverlay\erro.txt`.

## O Chat por Voz transcreve, mas o texto não entra no chat do LMU
1. Confirme nas opções de controles do LMU que **Realtime Chat** está vinculado à tecla **`Enter`** e que o chat não estava aberto antes de soltar o botão.
2. Verifique se o LMU e o Arts estão rodando com o **mesmo nível de permissão** do Windows (se o jogo estiver como Administrador e o Arts como usuário comum, o Windows bloqueia `SendInput`).
3. Em **Envio ao chat**, alterne de *Digitar automaticamente* para **Colar com Ctrl+V (compatibilidade)** e teste novamente.
4. Não mantenha `Ctrl`, `Alt`, `Shift` ou `Win` pressionados após soltar o atalho de voz.
5. Clique em **Abrir diagnóstico do envio** para inspecionar o passo exato registrado em `erro.txt`.

## Onde ficam os arquivos locais e logs?
Todos os dados locais ficam em `%LOCALAPPDATA%\ArthurLMUOverlay`:
- `config.xml`: suas preferências, posições, fontes e atalhos.
- `diagnostico.txt`: fotografia em tempo real do estado de telemetria, volante e visibilidade.
- `erro.txt`: histórico detalhado de erros e etapas do envio de voz.
- `Aprendizado\`: voltas gravadas por sessão.
- `ReferenciasDelta\`: melhores voltas pessoais por pista/carro/classe.
