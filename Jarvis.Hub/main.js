'use strict';

const { app, BrowserWindow, ipcMain, shell } = require('electron');
const { spawn } = require('child_process');
const http = require('http');
const path = require('path');

const API_URL = process.env.JARVIS_API_URL || 'http://localhost:5178';

let mainWindow = null;

function isApiRunning() {
  return new Promise((resolve) => {
    const request = http.get(`${API_URL}/api/health`, { timeout: 1500 }, (response) => {
      response.resume();
      resolve(response.statusCode === 200);
    });
    request.on('error', () => resolve(false));
    request.on('timeout', () => {
      request.destroy();
      resolve(false);
    });
  });
}

// Best-effort: start the JARVIS API host if it is not already running. The DLL is located
// relative to this app in a repository checkout, so failures degrade gracefully to the
// renderer's "API offline" banner.
function startApiIfNeeded() {
  if (!app.isPackaged) {
    return;
  }

  isApiRunning().then((running) => {
    if (running) {
      return;
    }

    const candidates = [
      path.join(process.resourcesPath, '..', 'Jarvis.API', 'Jarvis.API.dll'),
      path.join(__dirname, '..', 'Jarvis.API', 'bin', 'Release', 'net8.0', 'Jarvis.API.dll'),
      path.join(__dirname, '..', 'Jarvis.API', 'bin', 'Debug', 'net8.0', 'Jarvis.API.dll'),
    ];

    const dllPath = candidates.find((candidate) => require('fs').existsSync(candidate));
    if (!dllPath) {
      return;
    }

    const child = spawn('dotnet', [dllPath], {
      stdio: 'ignore',
      detached: true,
      env: { ...process.env, ASPNETCORE_URLS: API_URL },
    });
    child.unref();
  });
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 820,
    minWidth: 940,
    minHeight: 620,
    frame: false,
    backgroundColor: '#1b1f26',
    titleBarStyle: 'hidden',
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));

  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
  });

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });
}

app.whenReady().then(() => {
  startApiIfNeeded();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

ipcMain.on('window-control', (event, action) => {
  const window = BrowserWindow.fromWebContents(event.sender);
  if (!window) {
    return;
  }

  switch (action) {
    case 'minimize':
      window.minimize();
      break;
    case 'maximize':
      window.isMaximized() ? window.unmaximize() : window.maximize();
      break;
    case 'close':
      window.close();
      break;
  }
});
