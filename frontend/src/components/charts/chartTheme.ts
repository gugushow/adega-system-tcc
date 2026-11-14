/**
 * Cores e medidas dos gráficos num lugar só.
 *
 * As cores são variáveis CSS definidas em styles/tokens.css, com um valor para o tema claro e outro
 * para o escuro. Assim o gráfico troca de cor junto com o resto da tela, sem precisar saber qual
 * tema está ativo (o navegador resolve o var(...) dentro do SVG).
 *
 * As duas cores de série (azul e laranja) foram conferidas com um validador de daltonismo nos dois
 * fundos: continuam bem diferentes para quem não enxerga vermelho/verde. Textos usam tons de cinza,
 * nunca a cor da série. Vermelho e verde ficam reservados para alertas.
 */
export const chartColors = {
  series1: 'var(--chart-series-1)', // faturamento
  series2: 'var(--chart-series-2)', // lucro
  grid: 'var(--chart-grid)',
  axisText: 'var(--chart-axis)',
  text: 'var(--text-primary)',
  cursor: 'var(--chart-cursor)',
  /** Contorno dos pontos da linha: a cor do cartão, para o ponto "descolar" da linha nos dois temas. */
  surface: 'var(--surface)',
}

export const axisTick = { fill: chartColors.axisText, fontSize: 12 }

/** Ponta arredondada só do lado oposto à base da barra. */
export const verticalBarRadius: [number, number, number, number] = [4, 4, 0, 0]
export const horizontalBarRadius: [number, number, number, number] = [0, 4, 4, 0]
