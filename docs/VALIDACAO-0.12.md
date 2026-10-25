# Verificação 0.12.0

- Roslyn C# 5, referências .NET Framework 4.8, WinExe x64: compilação aprovada.
- Manifesto nativo embutido: aprovado (machine x64, recurso RT_MANIFEST).
- tools/run_checks.py: 482 verificações da lógica/renderização; 20 de
  migração e salvar/reabrir. A execução usa Mono isolado, não Windows.
- Os testes usam fixtures binárias do SDK, workers reais, persistência real,
  delta por distância, transições de sessão, voltas inválidas/parciais,
  falhas de disco e protocolo Sector com processo substituto.
- Extração e reutilização do payload real do Sector aprovadas; SHA256 do ZIP
  oficial e 266 entradas do manifesto original conferidos. Python/host nativo
  originais não foram executados contra LMU neste ambiente.
- Painter GDI real renderizou delta transparente/negativo, positivo e sem
  referência; prévia principal inspecionada. Dados da prévia são ilustrativos.
- tools/check_design.py: 14 tokens iguais ao DESIGN.md.
- audit_project.py --mode strict: 0 achados. O auditor web não cobre WinForms;
  documentação de proprietários e testes complementam, sem afirmar cobertura UI.
- NSIS com -WX: instalador gerado sem avisos tratados como erro.
- tools/package_release.py compara payload extraído com binários originais,
  manifesto, componente Sector, licença e conteúdo dos ZIPs/SHA256.

Limites: não há execução interativa de instalador/WinForms, DPI, foco, DWM,
F6/F7, EAC ou LMU. Não declarar integração nativa validada. É necessário testar
no Windows do usuário, em treino offline da versão suportada. O componente
bloqueia versões/contextos desconhecidos; não tenta contornar as verificações.

Comandos reproduzíveis estão em tools/build_release.py, tools/run_checks.py,
installer/Setup.nsi e tools/package_release.py. O diretório tests/render-v012
é artefato intermediário; a prévia entregue é produzida por Render012.
