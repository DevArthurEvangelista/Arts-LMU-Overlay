# Validação 0.12.3 — botões do volante

556 verificações de lógica/workers/renderer e 24 de migração/persistência
passaram em Mono/Linux. Novas regressões cobrem captura de snapshots de botões
sem eixos, idade dos dados, borda de pressão, botão segurado, reconexão,
dispositivo diferente, comandos simultâneos, estado desconectado e retorno de
foco. O vínculo persiste com as configurações anteriores dos cards.

Protocolo real do desktop Sector inspecionado no pacote original. Testes do
writer/worker verificam nome dos campos JSON, host, timestamp, expiração,
checkpoint, fase active, ação em andamento, fila única, não sobrescrever pedido,
foco perdido e parada. Não há simulação de F6/F7 nem encerramento de processos.

Compilação Windows x64 .NET Framework 4.8/C#5 e manifesto verificados.
Instalador NSIS -WX, extração, comparação de payload e ZIPs verificados.

Limitação: sem volante USB ou LMU/Windows nativo disponíveis neste ambiente.
A enumeração DirectInput e o uso real dos botões precisam de teste na máquina.
A leitura de eixos usa os mesmos offsets anteriores; os botões são acrescentados
ao formato. Não há suporte a POV/eixos como vínculos nesta versão.

Teste manual sugerido:
- Vincular dois botões, reiniciar Arts e confirmar os nomes persistidos.
- No treino por trecho conectado, salvar e retornar; segurar não repete.
- Pressionar fora do jogo, na garagem ou desconectado: nenhuma ação de treino.
- Voltar do Alt-Tab/reconectar USB segurando um botão: soltar e pressionar para agir.
- Fechar o treino e abrir LMU normalmente pela Steam: comandos do Arts inativos.
- Conferir rotação do volante e pedais, pois o formato DirectInput foi ampliado.
