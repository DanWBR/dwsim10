// MathJax 3 config for the DWSIM help system.
//
// Uses the `tex-svg-full.js` combined bundle which preloads:
//   - TeX input (incl. all extensions: ams, mhchem, color, ...)
//   - SVG output (no separate font files needed → file:// safe)
//
// Because the bundle preloads everything, we do NOT call loader.load — that
// would trigger an XHR for the already-bundled extension and fail offline.

window.MathJax = {
  tex: {
    packages: { '[+]': ['mhchem', 'ams', 'noerrors', 'noundefined'] },
    inlineMath: [['\\(', '\\)']],
    displayMath: [['\\[', '\\]']],
    processEscapes: true,
    processEnvironments: true
  },
  svg: { fontCache: 'global' },
  options: {
    // Process arithmatex-wrapped math AND the TOC sidebar nav links (where
    // headings containing math like `CO\(_{2}\)` end up as plain text without
    // arithmatex spans).
    ignoreHtmlClass: '.*|',
    processHtmlClass: 'arithmatex|md-nav__link|md-ellipsis'
  }
};

// mkdocs-material's instant-navigation observable: re-typeset on each page
// swap. Guarded so it works even when navigation.instant is disabled (the
// portable build).
if (typeof document$ !== 'undefined' && document$.subscribe) {
  document$.subscribe(function () {
    if (window.MathJax && MathJax.typesetClear) {
      MathJax.typesetClear();
      if (MathJax.texReset) MathJax.texReset();
      MathJax.typesetPromise();
    }
  });
}
