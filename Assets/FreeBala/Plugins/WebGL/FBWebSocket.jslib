// Ponte WebSocket para a build WebGL (o System.Net.WebSockets não existe no navegador): repassa abrir/mensagem/fechar para o FBNet.
mergeInto(LibraryManager.library, {
  FBWS_Connect: function (urlPtr, goPtr) {
    var url = UTF8ToString(urlPtr), go = UTF8ToString(goPtr);
    var ws = new WebSocket(url); window.__fbws = ws;
    ws.onopen = function () { SendMessage(go, 'WSOpen', ''); };
    ws.onmessage = function (e) { SendMessage(go, 'WSMessage', String(e.data)); };
    ws.onclose = function () { SendMessage(go, 'WSClose', ''); };
    ws.onerror = function () { SendMessage(go, 'WSClose', ''); };
  },
  FBWS_Send: function (msgPtr) { var ws = window.__fbws; if (ws && ws.readyState === 1) ws.send(UTF8ToString(msgPtr)); }
});
