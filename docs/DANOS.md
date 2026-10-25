# Pneus e danos — 0.10.0

Solicitação: integrar a condição do carro ao card de temperatura dos pneus.
O app exibe os dados publicados; não simula porcentagens de avaria a partir
da força de um impacto, temperatura ou curso da suspensão.

## Contrato de dados

| Informação | Fonte | Significado |
| --- | --- | --- |
| Temperatura | SDK nativo, sensores superficiais | Média em °C; contrato anterior preservado |
| Pressão | SDK nativo `mPressure` | Convertida para bar |
| PNEU | SDK nativo `mWear` | Fração restante: 1 = novo; 0 = totalmente gasto |
| FURADO / SEM RODA | SDK nativo `mFlat` / `mDetached` da roda | Avisos independentes do desgaste |
| SUSP. | `suspensionDamage[4]` | Dano 0–1, ordem DE, DD, TE, TD |
| AERO | `vehicleDamage` | Dano aerodinâmico geral 0–1 |
| PEÇA SOLTA | `mDetached` do veículo | Há outra parte desprendida, localização não informada |
| Asa dianteira/traseira separadas | Sem campo confirmado | Não disponíveis nesta versão |

Suspensão e aero vêm de `GET http://127.0.0.1:6397/rest/garage/getVehicleCondition`,
com alternativa em IPv6 local. O app não altera setup, arquivos do jogo ou
condição do carro. A leitura não exige conta nem serviço externo.

Aceita somente números finitos entre 0 e 1 nos campos conhecidos. Uma roda
inválida não elimina as demais. Array de suspensão precisa ter quatro itens.
Chaves desconhecidas e formatos não confirmados permanecem sem valor; não há
tentativa de adivinhar qual número representa cada asa. Zero só aparece quando
explicitamente recebido. PNEU restante e dano usam sentidos opostos e são
identificados no cabeçalho e no rodapé.

## Vida útil e desempenho

O serviço roda fora da interface e do leitor de telemetria. Requisições no
máximo a cada 500 ms, uma por vez, com timeout de 700 ms por host, resposta
limitada a 128 KiB e sem redirecionamento. Falha/ausência espera cinco segundos.
Valores expiram após dois segundos; os pneus nativos continuam sendo desenhados.
Troca de carro, regressão de relógio, saída da pista, card desativado e fechamento
invalidam respostas anteriores. Não há inferência de pneu saudável por falta
de resposta. O serviço não consulta o endpoint enquanto o card está desativado.

## Evidência e limite

Mapeamento nativo em `third_party/lmu_data.py`; parser e cenários do serviço em
`tests/V010DamageChecks.cs`. Testes exercitam endpoint demorado, perda de dados,
troca de carro, formatos inesperados, pneu furado e peças soltas.

Referências primárias consultadas em setembro de 2026:

- [Race Engineer, documentação do próprio integrador: sondagem do endpoint e campos](https://github.com/Alexander-Gro/race-engineer/blob/main/docs/03-LMU-INTEGRATION.md).
- [TinyPedal, guia do próprio desenvolvedor: danos por suspensão e aerodinâmica geral](https://github.com/TinyPedal/TinyPedal/wiki/User-Guide).
- [Team LINQ, propriedades LMU: frações de dano de suspensão e aero](https://docs.lsr.gg/properties/lmu.html).

O endpoint local pode variar conforme a versão do LMU. Não foi executado
contra o jogo neste ambiente Linux. O teste no Windows deve confirmar a
resposta do jogo instalado. Se não houver o formato esperado, SUSP./AERO
mostram travessões. As asas não são apresentadas como medição individual.
