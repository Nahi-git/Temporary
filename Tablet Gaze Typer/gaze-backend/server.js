const WebSocket = require("ws");

const wss = new WebSocket.Server({ port: 8765 });
console.log("WS server running on ws://localhost:8765");

wss.on("connection", ws => {
  console.log("Client connected");

  ws.on("message", msg => {
    console.log("Received:", msg.toString());

    wss.clients.forEach(c => {
      if (c.readyState === WebSocket.OPEN) {
        c.send(msg.toString());
      }
    });
  });
});
