const http = require("http");
const https = require("https");
const fs = require("fs");
const path = require("path");
const url = require("url");

const PORT = 8081;
const MEDIAPIPE_CDN = "https://cdn.jsdelivr.net/npm/@mediapipe/face_mesh@0.4.1633559619";
const WEBGAZER_CDN = "https://cdn.jsdelivr.net/npm/webgazer@latest/dist/webgazer.js";

//paths that should be proxied to the MediaPipe CDN 
const MEDIAPIPE_ASSETS = new Set([
  "face_mesh.binarypb",
  "face_mesh_solution_packed_assets_loader.js",
  "face_mesh_solution_packed_assets.data",
  "face_mesh_solution_simd_wasm_bin.js",
  "face_mesh_solution_simd_wasm_bin.wasm",
  "face_mesh_solution_wasm_bin.js",
  "face_mesh_solution_wasm_bin.wasm",
]);

function proxyFromCdn(reqPath, res) {
  const base = path.basename(reqPath);
  const decoded = decodeURIComponent(base);
  if (!MEDIAPIPE_ASSETS.has(decoded)) return false;
  const cdnUrl = `${MEDIAPIPE_CDN}/${decoded}`;
  https
    .get(cdnUrl, (proxyRes) => {
      res.writeHead(proxyRes.statusCode || 200, {
        "Content-Type": proxyRes.headers["content-type"] || "application/octet-stream",
        "Cache-Control": "public, max-age=86400",
      });
      proxyRes.pipe(res);
    })
    .on("error", (err) => {
      console.error("CDN proxy error:", err.message);
      res.writeHead(502);
      res.end("Bad Gateway");
    });
  return true;
}

function serveFile(filePath, res) {
  const ext = path.extname(filePath);
  const types = {
    ".html": "text/html",
    ".js": "application/javascript",
    ".json": "application/json",
    ".css": "text/css",
    ".ico": "image/x-icon",
    ".wasm": "application/wasm",
    ".data": "application/octet-stream",
    ".pb": "application/octet-stream",
  };
  fs.readFile(filePath, (err, data) => {
    if (err) {
      if (err.code === "ENOENT") {
        res.writeHead(404);
        res.end("Not Found");
      } else {
        res.writeHead(500);
        res.end("Server Error");
      }
      return;
    }
    res.writeHead(200, { "Content-Type": types[ext] || "application/octet-stream" });
    res.end(data);
  });
}

const server = http.createServer((req, res) => {
  const parsed = url.parse(req.url, true);
  let reqPath = decodeURIComponent(parsed.pathname);
  if (reqPath === "/") reqPath = "/gaze.html";

  if (reqPath === "/webgazer.js") {
    https
      .get(WEBGAZER_CDN, (proxyRes) => {
        res.writeHead(proxyRes.statusCode || 200, {
          "Content-Type": "application/javascript",
          "Cache-Control": "public, max-age=3600",
        });
        proxyRes.pipe(res);
      })
      .on("error", (err) => {
        console.error("WebGazer CDN proxy error:", err.message);
        res.writeHead(502);
        res.end("Bad Gateway");
      });
    return;
  }

  //try MediaPipe CDN proxy 
  const base = path.basename(reqPath);
  if (MEDIAPIPE_ASSETS.has(base) && proxyFromCdn(reqPath, res)) return;

  const filePath = path.join(__dirname, reqPath);
  if (!filePath.startsWith(__dirname + path.sep)) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }
  serveFile(filePath, res);
});

server.listen(PORT, () => {
  console.log(`Static server (with MediaPipe proxy) running on http://localhost:${PORT}`);
});
