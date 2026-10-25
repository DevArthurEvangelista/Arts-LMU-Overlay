# Validação 0.12.4

Correção do conflito intermitente de acesso a status.json no supervisor do treino por trecho.

O runtime original publica o estado por substituição atômica. O leitor agora abre o arquivo com FileShare.ReadWrite | FileShare.Delete, permitindo essa substituição sem bloquear o escritor. Violações de compartilhamento/bloqueio do Windows (32/33) adiam a leitura para o próximo ciclo de 40 ms, por no máximo 3 segundos de falhas contínuas. Uma leitura válida reinicia esse prazo. Nenhum comando do volante é enviado no ciclo em que a leitura foi adiada. Erros não relacionados e protocolo inválido continuam sendo reportados. O runtime Sector não foi modificado.

Validação: 565 asserções automatizadas, incluindo recuperação após bloqueio, prazo de falha persistente, tratamento seletivo de erros e leitura real com outro handle aberto. Mais 24 verificações de migração e persistência das configurações. Compilação Windows x64 / C#5 / .NET Framework 4.8 e inspeção de recursos PE concluídas.

Testes executados em Mono/Linux. Não houve validação do instalador em Windows nem reprodução com LMU/volante físico. O comportamento específico de compartilhamento Win32 é coberto por injeção dos códigos de erro; o teste com dois handles é local ao ambiente de testes.
