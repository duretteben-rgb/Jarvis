'use strict';

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('jarvis', {
  platform: process.platform,
  isElectron: true,
  apiBase: process.env.JARVIS_API_URL || 'http://localhost:5178',
  onWindowControl: (channel, handler) => {
    ipcRenderer.on(channel, handler);
  },
  windowControl: (action) => {
    ipcRenderer.send('window-control', action);
  },
});
