# Validação 0.12.2

Correções verificadas a partir do protocolo do Sector 1.0.0 empacotado e do
relato/print: timeout de navigation/state e estados paused/stopping rejeitados.
Os binários originais do Sector e suas restrições de versão não foram alterados.

- 526 verificações automatizadas de lógica, workers e renderer GDI passaram.
- 24 verificações de migração/salvar/reabrir passaram, incluindo espessura.
- Novas regressões: todos os estados reais do protocolo; pausa após retorno e
  retomada; emissão única de reset; recuperação de timeout; falha persistente;
  bloqueio imediato de contexto incompatível; comparação durante saída de pista
  e volta invalidada; ruído na distância; inicialização de contagem da volta;
  interrupção por F7; Alt-Tab, F8 e posicionamento; pixels de espessura e ausência
  de cabeçalho no delta. Prévia do renderer inspecionada.
- Compilação Windows x64/.NET Framework 4.8/C# 5, manifesto e recursos verificados.
- Instalador NSIS (-WX), extração e comparação dos componentes, inventário dos
  ZIPs e SHA256 verificados por tools/package_release.py.

Limites: execução de testes em Mono/Linux. Não houve jogo/Windows nativo nesta
máquina. Testar a conexão automática, F6/F7 repetidos, pausa/retomada, uma volta
completa válida seguida de outra para comparação, saída de pista e Alt-Tab.
Uma referência exige início e fim observados; abrir o Arts no meio de uma volta
não permite usar aquela volta parcial como referência. Após F7 o delta suspende
a comparação até outra volta devido à possível inconsistência do relógio LMU.
Falhas de rede prolongadas ainda desconectam; se persistirem, os diagnósticos da
máquina são necessários. Não foi expandida compatibilidade além do build 14200.
