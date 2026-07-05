// bridge.js — thin C#(WebView2) <-> prototype(JS) contract.
//
// The C# host (WebViewController / WebView2PrintRenderer) calls window.MLTL.* and waits
// for a postMessage({type:'rendered'}) once pagination completes. The methods below
// delegate to the mounted DesignCanvas component instance (window.__mltlComponent).
//
// OPEN DEPENDENCY: the vendored index.html is a DesignCanvas (React) component compiled
// by support.js, which requires window.React / window.ReactDOM / window.Babel plus a
// mount step. Until that bootstrap is added (see docs), __mltlComponent is undefined and
// these methods are no-ops. Wiring them to the component's real methods (setState with
// templateId, its resolveDoc/pagination, exec/insertVar/highlight) is the next task.
(function () {
  function comp() { return window.__mltlComponent || null; }

  function signalRendered() {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ type: 'rendered' });
    }
  }

  window.MLTL = {
    setTemplate: function (id) { const c = comp(); if (c) c.setState({ templateId: id }); requestAnimationFrame(signalRendered); },
    setRecord: function (json) { const c = comp(); if (c) c.__mltlRecord = JSON.parse(json); requestAnimationFrame(signalRendered); },
    setMapping: function (json) { const c = comp(); if (c) c.__mltlMapping = JSON.parse(json); },
    resolveHtml: function () { const c = comp(); return c && c.editorEl ? c.editorEl.innerHTML : ''; },
    measure: function () { const c = comp(); return c ? c.pageCount(c.__mltlRecord) : 0; },

    // Editor commands (P5) — forwarded to the component once wired.
    execFormat: function (cmd, val) { const c = comp(); if (c) c.exec(cmd, val); },
    insertVar: function (name) { const c = comp(); if (c) c.insertVar(name); },
    highlightVar: function (name) { const c = comp(); if (c) c.pvHighlight(name); },
    setZoom: function (z) { const c = comp(); if (c) c.setState({ zoom: z }); },
    setOrient: function (o) { const c = comp(); if (c) c.setState({ orient: o }); },
    setMode: function (m) { const c = comp(); if (c) c.setState({ mode: m }); },
  };
})();
