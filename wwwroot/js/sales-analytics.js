/**
 * Sales Analytics Hub — Chart.js inventory (Classic A).
 * Expects payload keys matching canvas [data-chart] attributes,
 * each value: { labels: string[], datasets: [{ label, data: number[] }] }
 */
(function (global) {
  'use strict';

  const palette = {
    gross: '#64748b',
    net: '#15803d',
    paid: '#3b82f6',
    cancel: '#ef4444',
    unit: '#81c408',
    soft: '#e2e8f0',
    cat: ['#81c408', '#3b82f6', '#f59e0b', '#8b5cf6', '#94a3b8', '#ec4899', '#14b8a6', '#f97316']
  };

  const charts = {};
  let reducedMotion = false;

  try {
    reducedMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  } catch (_) { /* ignore */ }

  function destroyAll() {
    Object.keys(charts).forEach(function (k) {
      try { charts[k].destroy(); } catch (_) { /* ignore */ }
      delete charts[k];
    });
  }

  function canvasFor(key) {
    return document.querySelector('canvas[data-chart="' + key + '"]')
      || document.getElementById('sa-chart-' + key)
      || document.getElementById('sa-chart-' + key.replace(/([A-Z])/g, '-$1').toLowerCase());
  }

  function series(payload, key) {
    var s = payload && payload[key];
    if (!s) return null;
    return {
      labels: s.labels || s.Labels || [],
      datasets: (s.datasets || s.Datasets || []).map(function (d) {
        return {
          label: d.label || d.Label || '',
          data: (d.data || d.Data || []).map(function (n) { return Number(n); })
        };
      })
    };
  }

  function make(key, config) {
    var el = canvasFor(key);
    if (!el || typeof Chart === 'undefined') return;
    if (charts[key]) {
      try { charts[key].destroy(); } catch (_) { /* ignore */ }
      delete charts[key];
    }
    if (reducedMotion) {
      config.options = config.options || {};
      config.options.animation = false;
    }
    charts[key] = new Chart(el, config);
  }

  function baseOptions(extra) {
    var opts = {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          position: 'bottom',
          labels: { boxWidth: 10, usePointStyle: true }
        }
      },
      scales: {
        x: { grid: { display: false } },
        y: { grid: { color: '#f1f5f9' } }
      }
    };
    if (extra) {
      if (extra.plugins) Object.assign(opts.plugins, extra.plugins);
      if (extra.scales) Object.assign(opts.scales, extra.scales);
      if (extra.indexAxis) opts.indexAxis = extra.indexAxis;
    }
    return opts;
  }

  function lineDual(key, s, colors, fillSecond) {
    if (!s || !s.datasets.length) return;
    var ds = s.datasets.map(function (d, i) {
      var c = colors[i] || palette.unit;
      return {
        label: d.label,
        data: d.data,
        borderColor: c,
        backgroundColor: c + '22',
        tension: 0.35,
        fill: i === 1 && !!fillSecond,
        borderWidth: 2,
        pointRadius: 2
      };
    });
    make(key, {
      type: 'line',
      data: { labels: s.labels, datasets: ds },
      options: baseOptions()
    });
  }

  function barGroup(key, s, colors, stacked) {
    if (!s || !s.datasets.length) return;
    var ds = s.datasets.map(function (d, i) {
      var c = colors[i] || palette.unit;
      return {
        label: d.label,
        data: d.data,
        backgroundColor: Array.isArray(c) ? c : (c + (i === 0 ? 'cc' : 'aa')),
        borderRadius: 4
      };
    });
    make(key, {
      type: 'bar',
      data: { labels: s.labels, datasets: ds },
      options: baseOptions({
        scales: {
          x: { stacked: !!stacked, grid: { display: false } },
          y: { stacked: !!stacked, grid: { color: '#f1f5f9' } }
        }
      })
    });
  }

  function doughnut(key, s, colors) {
    if (!s || !s.datasets.length) return;
    var data = s.datasets[0].data;
    var bg = colors || palette.cat;
    while (bg.length < data.length) bg = bg.concat(palette.cat);
    make(key, {
      type: 'doughnut',
      data: {
        labels: s.labels,
        datasets: [{
          data: data,
          backgroundColor: bg.slice(0, data.length),
          borderWidth: 0
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '58%',
        plugins: {
          legend: {
            position: 'bottom',
            labels: { boxWidth: 10, usePointStyle: true, font: { size: 10 } }
          }
        }
      }
    });
  }

  function hbar(key, s, color) {
    if (!s || !s.datasets.length) return;
    var d = s.datasets[0];
    make(key, {
      type: 'bar',
      data: {
        labels: s.labels,
        datasets: [{
          label: d.label || 'Net',
          data: d.data,
          backgroundColor: color || (palette.net + 'cc'),
          borderRadius: 6,
          barThickness: 16
        }]
      },
      options: baseOptions({
        indexAxis: 'y',
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { color: '#f1f5f9' } },
          y: { grid: { display: false } }
        }
      })
    });
  }

  function dualAxis(key, s) {
    if (!s || s.datasets.length < 2) return;
    var a = s.datasets[0];
    var b = s.datasets[1];
    make(key, {
      type: 'bar',
      data: {
        labels: s.labels,
        datasets: [
          {
            type: 'bar',
            label: a.label,
            data: a.data,
            backgroundColor: palette.unit + 'cc',
            borderRadius: 4,
            yAxisID: 'y',
            order: 2
          },
          {
            type: 'line',
            label: b.label,
            data: b.data,
            borderColor: palette.net,
            backgroundColor: palette.net,
            tension: 0.3,
            yAxisID: 'y1',
            order: 1,
            pointRadius: 3
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'bottom', labels: { boxWidth: 10, usePointStyle: true } }
        },
        scales: {
          y: {
            position: 'left',
            grid: { color: '#f1f5f9' },
            title: { display: true, text: a.label, font: { size: 10 } }
          },
          y1: {
            position: 'right',
            grid: { drawOnChartArea: false },
            title: { display: true, text: b.label, font: { size: 10 } }
          },
          x: { grid: { display: false } }
        }
      }
    });
  }

  function growthBar(key, s) {
    if (!s || !s.datasets.length) return;
    var data = s.datasets[0].data;
    make(key, {
      type: 'bar',
      data: {
        labels: s.labels,
        datasets: [{
          label: s.datasets[0].label || 'Δ %',
          data: data,
          backgroundColor: data.map(function (v) { return v >= 0 ? '#22c55e' : '#ef4444'; }),
          borderRadius: 6
        }]
      },
      options: baseOptions({
        plugins: { legend: { display: false } },
        scales: {
          y: {
            grid: { color: '#f1f5f9' },
            ticks: { callback: function (v) { return v + '%'; } }
          },
          x: { grid: { display: false } }
        }
      })
    });
  }

  function cancelTrend(key, s) {
    if (!s || !s.datasets.length) return;
    if (s.datasets.length >= 2) {
      dualAxis(key, s);
      return;
    }
    var d = s.datasets[0];
    make(key, {
      type: 'bar',
      data: {
        labels: s.labels,
        datasets: [{
          label: d.label || 'Cancelled',
          data: d.data,
          backgroundColor: '#fca5a5',
          borderRadius: 4
        }]
      },
      options: baseOptions({
        plugins: { legend: { position: 'bottom' } }
      })
    });
  }

  function pipelineBar(key, s) {
    if (!s || !s.datasets.length) return;
    var colors = ['#94a3b8', '#60a5fa', '#818cf8', '#22c55e', '#ef4444', '#f59e0b', '#a78bfa', '#fb7185'];
    var data = s.datasets[0].data;
    var bg = data.map(function (_, i) { return colors[i % colors.length]; });
    make(key, {
      type: 'bar',
      data: {
        labels: s.labels,
        datasets: [{
          label: s.datasets[0].label || 'Orders',
          data: data,
          backgroundColor: bg,
          borderRadius: 4
        }]
      },
      options: baseOptions({
        plugins: { legend: { display: false } }
      })
    });
  }

  function periodCompare(key, s) {
    if (!s || !s.datasets.length) return;
    // Backend: Current then Previous — display Previous first for visual parity with prototype
    var ordered = s.datasets.slice();
    var colors = [palette.net + 'cc', palette.soft];
    if (ordered.length >= 2) {
      var curIdx = ordered.findIndex(function (d) {
        return /current|kỳ này/i.test(d.label || '');
      });
      var prevIdx = ordered.findIndex(function (d) {
        return /previous|kỳ trước/i.test(d.label || '');
      });
      if (curIdx >= 0 && prevIdx >= 0) {
        ordered = [ordered[prevIdx], ordered[curIdx]];
        colors = [palette.soft, palette.net + 'cc'];
      }
    }
    barGroup(key, { labels: s.labels, datasets: ordered }, colors, false);
  }

  /** Map payload key → renderer */
  var renderers = {
    trend: function (s) { lineDual('trend', s, [palette.gross, palette.net], true); },
    ordersVolume: function (s) { barGroup('ordersVolume', s, [palette.paid, palette.cancel], false); },
    categoryMix: function (s) { doughnut('categoryMix', s, palette.cat); },
    aovTrend: function (s) { lineDual('aovTrend', s, [palette.gross, palette.net], false); },
    unitsTrend: function (s) { barGroup('unitsTrend', s, [palette.unit], false); },
    pipeline: function (s) { pipelineBar('pipeline', s); },
    periodCompare: function (s) { periodCompare('periodCompare', s); },
    topProductsBar: function (s) { hbar('topProductsBar', s, palette.net + 'cc'); },
    rankBar: function (s) { hbar('rankBar', s, palette.net + 'cc'); },
    unitsVsNet: function (s) { dualAxis('unitsVsNet', s); },
    growth: function (s) { growthBar('growth', s); },
    cancelTrend: function (s) { cancelTrend('cancelTrend', s); },
    reasons: function (s) { doughnut('reasons', s, ['#f87171', '#fb923c', '#fbbf24', '#94a3b8', '#c084fc']); },
    valueByProduct: function (s) { hbar('valueByProduct', s, '#ef4444aa'); },
    valueByCategory: function (s) { barGroup('valueByCategory', s, ['#f87171'], false); }
  };

  // Also accept alternate canvas data-chart ids used in markup
  var keyAliases = {
    'orders': 'ordersVolume',
    'category-mix': 'categoryMix',
    'aov': 'aovTrend',
    'units': 'unitsTrend',
    'compare': 'periodCompare',
    'top-hbar': 'topProductsBar',
    'rank-bar': 'rankBar',
    'merch-cat': 'categoryMix',
    'units-vs-net': 'unitsVsNet',
    'cancel-trend': 'cancelTrend',
    'value-by-product': 'valueByProduct',
    'value-by-category': 'valueByCategory'
  };

  function init(payload) {
    if (typeof Chart === 'undefined') {
      console.warn('SalesAnalytics: Chart.js not loaded');
      return;
    }

    Chart.defaults.font.family = "'Be Vietnam Pro', system-ui, sans-serif";
    Chart.defaults.font.size = 11;
    Chart.defaults.color = '#6b7280';
    Chart.defaults.plugins.legend.labels.boxWidth = 10;
    Chart.defaults.plugins.legend.labels.usePointStyle = true;

    destroyAll();
    payload = payload || {};

    Object.keys(renderers).forEach(function (key) {
      var s = series(payload, key);
      if (s && s.labels && s.labels.length) {
        try {
          renderers[key](s);
        } catch (err) {
          console.error('SalesAnalytics chart error [' + key + ']:', err);
        }
      }
    });
  }

  global.SalesAnalytics = {
    init: init,
    destroy: destroyAll,
    palette: palette,
    _keyAliases: keyAliases
  };
})(typeof window !== 'undefined' ? window : this);
