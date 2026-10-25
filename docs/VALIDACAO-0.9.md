> Errata em 26/09/2026: a afirmação de manifesto embutido nesta validação
> estava incorreta. A inspeção dos bytes em 0.9.1 detectou ausência de RT_MANIFEST.
> A revisão 0.9.1 adiciona uma barreira de verificação do pacote.

# Verificação — Arts LMU OVERLAY 0.9

Data: 26/09/2026. Solicitação: liberar o redimensionamento e permitir configurar
fonte e dimensões de cada card sem alterar a aparência geral aprovada da 0.8.

## Correção e comportamento

OverlayWindow.FitSize usava a dimensão original como mínimo. Agora usa limites
de operação menores via PanelSizing.Minimum, inclusive com monitor reduzido.
As quatro bordas e quatro cantos redimensionam com a borda oposta ancorada.
Proporção é livre; perda de captura conclui e salva o gesto. Há oito alças visuais.
Roda do mouse e Shift+setas continuam disponíveis. O editor oferece alternativa
sem arraste: largura e altura em pixels.

Cada card tem PanelAppearance próprio com família de texto e 60–200% de letras.
Os números mantêm Consolas. Aumento de fonte oferece espaço ao card, limitado
pelo monitor; os valores numéricos se ajustam à célula sem ocultar seus dígitos.
O editor aplica imediatamente e mostra o estado do salvamento automático.
Restaurar padrão altera fonte e dimensões apenas do card selecionado.
Schema 8 preserva posições, dimensões, opacidade, atalho e calibração da 0.8.

## Verificações executadas

- Compilação C# 5, WinExe x64, recursos/ícone/manifesto embutidos: passou.
- Suite de parser, sessões, telemetria, volante bloqueado, histórico, seleção
  de pilotos, combustível, mapa e redimensionamento: 307 asserções passaram.
- Sete verificações adicionais no AppSettings real: gravação de config.xml,
  migração da 0.8, reabertura, persistência independente das fontes/dimensões,
  restauração da aparência sem alterar preferências não relacionadas.
- Os seis renderizadores reais foram exercitados com 60%, 100%, 150% e 200%
  de tamanho de texto e dimensão. Inspeção visual dos extremos e da tabela e
  pedais a 150%: colunas, números, proporção dos glifos e arte preservados.
- Auditoria de interface em modo estrito: zero erros/avisos. Tokens de cor:
  14 valores conferem com DESIGN.md.
- Instalador NSIS Unicode compilado com avisos tratados como erro; integridade
  e conteúdo do instalador e ZIPs verificados antes da entrega.

## Limites da evidência

Compilação, testes e renders ocorreram no Linux com Mono/libgdiplus. O teste
nativo do editor está incluído em tests/AppearanceUiChecks.cs, mas sua etapa de
interação não pôde executar: o ambiente não permite os sockets locais necessários
para iniciar um servidor gráfico Xvfb. A parte de migração/gravação/reabertura
executou isoladamente. Não foi realizado teste dos cliques, DirectInput,
composição de janelas ou instalação no Windows/LMU. O teste no computador do
usuário continua necessário para confirmar integração nativa.

As imagens de teste usam fixtures artificiais, excluídas do aplicativo instalado.
A versão não altera a aquisição de telemetria, física do volante ou regras de
classificação. Os testes do driver lento continuam protegendo a correção da 0.8.
