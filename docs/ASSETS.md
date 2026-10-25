# Identidade visual — Arts LMU OVERLAY

`assets/BrandLogo.png` é uma cópia intacta da logo enviada pelo usuário.
O cabeçalho usa o conteúdo visível do PNG, sem redesenhar o personagem.
`assets/App.ico` é a conversão do retrato em ícone Windows com resoluções
16, 24, 32, 48, 64, 128 e 256 px, gerada por `Checks --export-icon`.

O volante em `Painter.Wheel` é desenho GDI+ nativo, baseado na foto enviada:
empunhaduras abertas, placa escura, controles geométricos, dois botões vermelhos
e seletor vermelho inferior direito. Não há marca ou emblema Porsche.
As aberturas deixam aparecer o fundo do overlay, sem retângulo opaco.

Uma referência de ilustração também foi produzida pela ferramenta de geração
de imagens, com a orientação: vista frontal centralizada, desenho limpo para
overlay, preservar a forma e os controles da foto, remover o emblema Porsche.
O bitmap de referência não faz parte do aplicativo. A implementação nativa
garante contorno e transparência na escala pequena do painel.

As bandeiras mantêm as atribuições existentes em third_party. A logo pessoal
é fornecida pelo usuário; sua inclusão não transfere direitos a terceiros.

## Fabricantes dos carros — 0.7

CarLogos.zip contém 14 PNGs embutidos, carregados uma vez por fabricante.
Doze vetores vêm de [Simple Icons 11.15.0](https://github.com/simple-icons/simple-icons/tree/11.15.0/icons):
porsche, ferrari, bmw, mercedes, astonmartin, mclaren, ford, chevrolet, toyota,
peugeot, lamborghini e cadillac. Originais SVG em assets/car-logos; conversão
vetorial para PNG colorido 96×96 com librsvg/FFmpeg. Cada fabricante recebe uma
cor de marca de alto contraste para permanecer reconhecível no overlay; a
licença CC0 está preservada
em third_party/SimpleIcons-LICENSE.md; direitos de marca permanecem dos titulares.

Alpine e Lexus são os PNGs originais, sem redesenho, do
[car-logos-dataset](https://github.com/filippofilip95/car-logos-dataset/tree/master/logos/optimized),
consultado em 13/09/2026. Ver CarLogos-NOTICE.txt para atribuição e ressalvas de marca.
O renderer mantém a proporção original; fundo pequeno escuro protege contraste.
Marca desconhecida ou não incluída usa o símbolo genérico de carro. Os arquivos
não são extraídos das texturas do LMU: o jogo publica nome/modelo na telemetria,
mas não uma API pública estável de logos de escuderias. Usar texturas internas
seria sensível a atualizações e desnecessário para esta instalação local.

Pneu é ícone geométrico nativo, com letra e cor do composto informado. Não há
logo Michelin/Goodyear inventada a partir da classe. Cores e semântica estão
documentadas em TELEMETRIA.md. A imagem do volante e a logo pessoal não foram
alteradas nesta atualização.
