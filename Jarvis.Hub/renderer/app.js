'use strict';

/* ============================================================
   JARVIS HUB — renderer logic
   Talks to the JARVIS.API host over REST + SSE.
   Works in the browser preview (relative /api) and in Electron
   (absolute http://localhost:5178 via CORS).
   ============================================================ */

const API_BASE = window.jarvis?.apiBase || (location.protocol === 'file:' ? 'http://localhost:5178' : '');

const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));

const state = {
  view: 'dashboard',
  sessionId: localStorage.getItem('jarvis.session') || crypto.randomUUID(),
  chatHistory: [],
  models: [],
  providers: [],
  plugins: [],
  streaming: false,
};

/* ---------- helpers ------------------------------------------- */

async function api(path, options = {}) {
  const response = await fetch(`${API_BASE}/api${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error || `${response.status} ${response.statusText}`);
  }
  return response.status === 204 ? null : response.json();
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function timeAgo(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const seconds = Math.floor((Date.now() - date.getTime()) / 1000);
  if (seconds < 60) return 'just now';
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
  return `${Math.floor(seconds / 86400)}d ago`;
}

/* ---------- connection + theme -------------------------------- */

async function checkConnection() {
  const pill = $('#connection-pill');
  try {
    const health = await api('/health');
    if (health.status === 'ok') {
      pill.textContent = 'online';
      pill.className = 'pill pill-ok';
    }
  } catch {
    pill.textContent = 'offline';
    pill.className = 'pill pill-err';
  }
}

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
  const toggle = $('#theme-toggle');
  if (toggle) toggle.classList.toggle('is-on', theme === 'dark');
}

function initTheme() {
  const saved = localStorage.getItem('jarvis.theme');
  const preferred = saved || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
  applyTheme(preferred);
}

/* ---------- navigation ---------------------------------------- */

function navigate(view) {
  state.view = view;
  $$('.nav-item').forEach((item) => item.classList.toggle('is-active', item.dataset.view === view));
  $$('.view').forEach((section) => section.classList.toggle('is-active', section.id === `view-${view}`));
  const renderers = {
    dashboard: renderDashboard,
    chat: renderChat,
    memory: renderMemory,
    plugins: renderPlugins,
    system: renderSystem,
    studio: renderStudio,
    senses: renderSenses,
    settings: renderSettings,
  };
  renderers[view]();
}

/* ---------- dashboard ----------------------------------------- */

async function renderDashboard() {
  const container = $('#view-dashboard');
  container.innerHTML = `
    <div class="page-head">
      <div class="page-title">Dashboard</div>
      <div class="page-subtitle">System overview of your JARVIS OS instance.</div>
    </div>
    <div class="grid grid-4">
      <div class="card stat"><span class="stat-value" id="d-version">-</span><span class="stat-label">Platform version</span></div>
      <div class="card stat"><span class="stat-value" id="d-plugins">-</span><span class="stat-label">Plugins loaded</span></div>
      <div class="card stat"><span class="stat-value" id="d-providers">-</span><span class="stat-label">AI providers online</span></div>
      <div class="card stat"><span class="stat-value" id="d-uptime">-</span><span class="stat-label">Uptime (s)</span></div>
    </div>
    <div class="grid grid-2" style="margin-top:14px">
      <div class="card">
        <div class="card-title">AI providers</div>
        <div id="d-providers-list"><div class="spinner"></div></div>
      </div>
      <div class="card">
        <div class="card-title">Recent memories</div>
        <div id="d-memories"><div class="spinner"></div></div>
      </div>
    </div>
  `;

  try {
    const status = await api('/status');
    $('#d-version').textContent = status.version;
    $('#d-plugins').textContent = status.plugins.loaded;
    $('#d-uptime').textContent = status.uptimeSeconds;
    $('#d-providers').textContent = status.ai.providers.filter((provider) => provider.isAvailable).length;
  } catch { /* pill reflects offline */ }

  renderProviderList($('#d-providers-list'));
  renderRecentMemories($('#d-memories'));
}

async function renderProviderList(container) {
  try {
    const providers = await api('/ai/providers');
    container.innerHTML = providers.length
      ? providers.map((provider) => `
          <div class="provider-row">
            <span class="dot ${provider.isAvailable ? 'dot-ok' : 'dot-err'}"></span>
            <div>
              <div class="provider-name">${escapeHtml(provider.displayName)}${provider.isLocal ? ' <span class="pill pill-info">local</span>' : ''}</div>
              <div class="provider-detail">${escapeHtml(provider.isAvailable ? `${provider.models.length} model(s) ready` : provider.error || 'unavailable')}</div>
            </div>
          </div>`).join('')
      : '<div class="empty-state">No AI providers configured.</div>';
  } catch (error) {
    container.innerHTML = `<div class="empty-state">${escapeHtml(error.message)}</div>`;
  }
}

async function renderRecentMemories(container) {
  try {
    const entries = await api('/memory/recent?limit=4');
    container.innerHTML = entries.length
      ? `<div class="memory-list">${entries.map((entry) => `
          <div class="memory-item">
            <div class="content">${escapeHtml(entry.content)}</div>
            <div class="meta">${escapeHtml(entry.kind)}</div>
          </div>`).join('')}</div>`
      : '<div class="empty-state">No memories stored yet. Ask the assistant to remember something.</div>';
  } catch (error) {
    container.innerHTML = `<div class="empty-state">${escapeHtml(error.message)}</div>`;
  }
}

/* ---------- assistant / chat ---------------------------------- */

async function renderChat() {
  const container = $('#view-chat');
  const providerNames = Object.fromEntries((state.providers || []).map((provider) => [provider.id, provider.displayName]));
  const groups = {};
  state.models.forEach((model) => {
    const label = providerNames[model.provider] || model.provider || 'Other';
    (groups[label] ||= []).push(model);
  });
  const modelOptions = Object.entries(groups)
    .map(([groupName, models]) => `
      <optgroup label="${escapeHtml(groupName)}">
        ${models.map((model) => `<option value="${escapeHtml(model.id)}">${escapeHtml(model.displayName || model.id)}${model.isDefault ? ' · default' : ''}</option>`).join('')}
      </optgroup>`)
    .join('');

  container.innerHTML = `
    <div class="chat-layout">
      <div>
        <div class="chat-toolbar">
          <select id="chat-model"><option value="">auto (smart routing)</option>${modelOptions}</select>
          <select id="chat-task">
            <option value="Simple">Simple</option>
            <option value="Complex">Complex</option>
            <option value="Reasoning">Reasoning</option>
            <option value="Coding">Coding</option>
            <option value="Summarization">Summarization</option>
          </select>
          <button class="btn btn-ghost" id="chat-clear">New conversation</button>
        </div>
        <div class="chat-scroll" id="chat-scroll"></div>
      </div>
      <div class="composer">
        <textarea id="chat-input" placeholder="Message your assistant… (Enter to send, Shift+Enter for newline)"></textarea>
        <div class="composer-actions">
          <button class="btn btn-primary" id="chat-send">Send</button>
        </div>
      </div>
    </div>
  `;

  const input = $('#chat-input');
  const sendButton = $('#chat-send');

  sendButton.addEventListener('click', () => sendChatMessage());
  input.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      sendChatMessage();
    }
  });

  $('#chat-clear').addEventListener('click', async () => {
    try {
      await api(`/ai/sessions/${encodeURIComponent(state.sessionId)}/clear`, { method: 'POST' });
    } catch { /* keep going */ }
    state.chatHistory = [];
    renderMessages();
    input.focus();
  });

  renderMessages();
  input.focus();
}

function renderMessages() {
  const scroll = $('#chat-scroll');
  if (!scroll) return;
  if (state.chatHistory.length === 0) {
    scroll.innerHTML = '<div class="empty-state">Start a conversation with your JARVIS assistant.</div>';
    return;
  }
  scroll.innerHTML = state.chatHistory.map((message, index) => `
    <div class="msg ${message.role === 'user' ? 'msg-user' : message.role === 'system' ? 'msg-system' : 'msg-assistant'}">
      ${escapeHtml(message.content)}
      ${message.model ? `<div class="msg-meta">${escapeHtml(message.model)}</div>` : ''}
    </div>`).join('');
  scroll.scrollTop = scroll.scrollHeight;
}

async function sendChatMessage() {
  if (state.streaming) return;
  const input = $('#chat-input');
  const text = input.value.trim();
  if (!text) return;

  input.value = '';
  state.chatHistory.push({ role: 'user', content: text });
  renderMessages();

  const assistantIndex = state.chatHistory.length;
  state.chatHistory.push({ role: 'assistant', content: '', streaming: true });
  renderMessages();

  const bubble = $$('.msg-assistant')[$$('.msg-assistant').length - 1];
  bubble.innerHTML = '<span class="caret"></span>';
  bubble.classList.add('caret');

  const model = $('#chat-model').value;
  const taskKind = $('#chat-task').value;

  state.streaming = true;
  try {
    const body = {
      model: model || undefined,
      taskKind,
      sessionId: state.sessionId,
      prompt: text,
    };

    const response = await fetch(`${API_BASE}/api/ai/chat/stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (!response.ok || !response.body) throw new Error(`HTTP ${response.status}`);

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let fullText = '';

    const tick = () => {
      bubble.innerHTML = escapeHtml(fullText) + '<span class="caret"></span>';
      $('#chat-scroll').scrollTop = $('#chat-scroll').scrollHeight;
    };

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      const frames = buffer.split('\n\n');
      buffer = frames.pop() || '';
      for (const frame of frames) {
        for (const line of frame.split('\n')) {
          if (!line.startsWith('data: ')) continue;
          const data = line.slice(6).trim();
          if (data === '[DONE]') continue;
          const chunk = JSON.parse(data);
          if (chunk.error) throw new Error(chunk.error);
          fullText += chunk.delta || '';
          tick();
        }
      }
    }

    state.chatHistory[assistantIndex] = { role: 'assistant', content: fullText };
    renderMessages();
  } catch (error) {
    state.chatHistory[assistantIndex] = { role: 'assistant', content: `Error: ${error.message}` };
    renderMessages();
  } finally {
    state.streaming = false;
  }
}

/* ---------- memory -------------------------------------------- */

async function renderMemory() {
  const container = $('#view-memory');
  container.innerHTML = `
    <div class="page-head">
      <div class="page-title">Memory</div>
      <div class="page-subtitle">Facts, knowledge and preferences persisted by JARVIS.</div>
    </div>
    <div class="grid grid-2">
      <div class="card">
        <div class="card-title">Semantic search</div>
        <div class="field">
          <input type="search" id="memory-q" placeholder="Search memories… e.g. what theme do I prefer" />
        </div>
        <div id="memory-results"><div class="spinner"></div></div>
      </div>
      <div class="card">
        <div class="card-title">Recent entries</div>
        <div id="memory-recent"><div class="spinner"></div></div>
      </div>
    </div>
    <div class="card" style="margin-top:14px">
      <div class="card-title">Preferences</div>
      <div class="kv-list" id="preference-list"><div class="spinner"></div></div>
      <div class="kv-row" style="margin-top:14px">
        <input type="text" id="pref-key" placeholder="key (e.g. theme)" style="min-width:180px" />
        <input type="text" id="pref-value" placeholder="value (e.g. dark)" />
        <button class="btn btn-primary" id="pref-add">Set</button>
      </div>
    </div>
  `;

  $('#memory-q').addEventListener('input', debounce(async () => {
    const query = $('#memory-q').value.trim();
    if (!query) { await renderRecentMemories($('#memory-results')); return; }
    const results = await api(`/memory/search?q=${encodeURIComponent(query)}`);
    $('#memory-results').innerHTML = renderMemoryResults(results);
  }, 300));

  await Promise.all([
    renderRecentMemories($('#memory-recent')),
    renderMemorySearchInitial($('#memory-results')),
    renderPreferences(),
  ]);

  $('#pref-add').addEventListener('click', async () => {
    const key = $('#pref-key').value.trim();
    const value = $('#pref-value').value;
    if (!key) return;
    try {
      await api('/memory/preferences', {
        method: 'POST',
        body: JSON.stringify({ key, value }),
      });
      $('#pref-key').value = '';
      $('#pref-value').value = '';
      await renderPreferences();
    } catch (error) {
      alert(error.message);
    }
  });
}

function renderMemoryResults(results) {
  if (!results || results.length === 0) return '<div class="empty-state">No matching memories.</div>';
  return `<div class="memory-list">${results.map((result) => `
    <div class="memory-item">
      <div class="score">${(result.score * 100).toFixed(0)}%</div>
      <div class="content">${escapeHtml(result.entry.content)}</div>
      <div class="meta">${escapeHtml(result.entry.kind)}</div>
    </div>`).join('')}</div>`;
}

async function renderMemorySearchInitial(container) {
  try {
    const entries = await api('/memory/recent?limit=5');
    container.innerHTML = renderMemoryResults(entries.map((entry) => ({ entry, score: 0 })));
  } catch (error) {
    container.innerHTML = `<div class="empty-state">${escapeHtml(error.message)}</div>`;
  }
}

async function renderPreferences() {
  const container = $('#preference-list');
  try {
    const stored = await api('/memory/preferences');
    const rows = Object.entries(stored);
    container.innerHTML = rows.length
      ? rows.map(([key, value]) => `
          <div class="kv-row">
            <span class="key">${escapeHtml(key)}</span>
            <span class="value">${escapeHtml(value)}</span>
            <button class="btn btn-ghost" data-pref-remove="${escapeHtml(key)}">Remove</button>
          </div>`).join('')
      : '<div class="empty-state">No preferences stored yet.</div>';

    $$('[data-pref-remove]', container).forEach((button) => {
      button.addEventListener('click', async () => {
        try {
          await api(`/memory/preferences/${encodeURIComponent(button.dataset.prefRemove)}`, { method: 'DELETE' });
        } catch { /* ignore */ }
        await renderPreferences();
      });
    });
  } catch (error) {
    container.innerHTML = `<div class="empty-state">${escapeHtml(error.message)}</div>`;
  }
}

/* ---------- plugins ------------------------------------------- */

async function renderPlugins() {
  const container = $('#view-plugins');
  container.innerHTML = `<div class="page-head"><div class="page-title">Plugins</div><div class="page-subtitle">Capabilities loaded by JARVIS as independent modules.</div></div><div class="spinner"></div>`;

  try {
    state.plugins = await api('/plugins');
  } catch (error) {
    container.innerHTML = `<div class="page-head"><div class="page-title">Plugins</div></div><div class="card"><div class="empty-state">${escapeHtml(error.message)}</div></div>`;
    return;
  }

  container.innerHTML = `<div class="page-head"><div class="page-title">Plugins</div><div class="page-subtitle">${state.plugins.length} plugin(s) active.</div></div>` +
    state.plugins.map((plugin) => `
      <div class="card plugin-card">
        <div class="card-title">
          <span>${escapeHtml(plugin.name)} <span class="pill pill-info">${escapeHtml(plugin.version)}</span></span>
          <span class="pill ${plugin.state === 'Running' ? 'pill-ok' : 'pill-warn'}">${escapeHtml(plugin.state)}</span>
        </div>
        <div class="plugin-desc">${escapeHtml(plugin.description)}</div>
        <div class="perm-list">${plugin.permissions.map((permission) => `<span class="perm-tag">${escapeHtml(permission)}</span>`).join('')}</div>
        ${plugin.commands.length ? `
          <div class="command-list">
            ${plugin.commands.map((command) => `<button class="command-chip" data-plugin="${escapeHtml(plugin.id)}" data-command="${escapeHtml(command.name)}">${escapeHtml(command.name)}</button>`).join('')}
          </div>` : ''}
      </div>`).join('');

  $$('.command-chip').forEach((chip) => {
    chip.addEventListener('click', () => openCommandModal(chip.dataset.plugin, chip.dataset.command));
  });
}

function openCommandModal(pluginId, command) {
  const plugin = state.plugins.find((item) => item.id === pluginId);
  const definition = plugin?.commands.find((item) => item.name === command);
  const backdrop = $('#command-modal');

  backdrop.innerHTML = `
    <div class="modal">
      <div class="modal-title">${escapeHtml(command)}</div>
      <p style="color:var(--text-secondary);margin-bottom:14px">${escapeHtml(definition?.description || '')}</p>
      <div class="field"><label>Parameters (optional JSON object)</label><textarea id="cmd-params"></textarea></div>
      <div id="cmd-result" class="cmd-result" hidden></div>
      <div class="modal-actions">
        <button class="btn btn-ghost" data-modal-close>Cancel</button>
        <button class="btn btn-primary" id="cmd-run">Run</button>
      </div>
    </div>`;
  backdrop.classList.add('is-open');

  const resultBox = $('#cmd-result');
  const showResult = (kind, text) => {
    resultBox.hidden = false;
    resultBox.className = `cmd-result ${kind}`;
    resultBox.textContent = text;
  };

  const close = () => backdrop.classList.remove('is-open');
  backdrop.addEventListener('click', (event) => { if (event.target === backdrop) close(); });
  $$('[data-modal-close]', backdrop).forEach((button) => button.addEventListener('click', close));

  $('#cmd-run').addEventListener('click', async () => {
    let parameters = {};
    const raw = $('#cmd-params').value.trim();
    if (raw) {
      try { parameters = JSON.parse(raw); } catch { showResult('err', 'Invalid JSON parameters.'); return; }
    }

    const runButton = $('#cmd-run');
    runButton.disabled = true;
    resultBox.hidden = true;
    try {
      const response = await api(`/plugins/${encodeURIComponent(pluginId)}/commands/${encodeURIComponent(command)}`, {
        method: 'POST',
        body: JSON.stringify({ parameters }),
      });
      if (response.success) {
        showResult('ok', response.result || 'Command completed.');
      } else {
        showResult('err', response.error || 'Command failed.');
      }
    } catch (error) {
      showResult('err', error.message);
    } finally {
      runButton.disabled = false;
    }
  });
}

/* ---------- system panel --------------------------------------- */

const ICON_DIR = '<svg viewBox="0 0 24 24" width="16" height="16"><path fill="currentColor" d="M4 4h6l2 2h8a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z"/></svg>';
const ICON_FILE = '<svg viewBox="0 0 24 24" width="16" height="16"><path fill="currentColor" d="M6 2h8l6 6v14H6V2zm8 1.5V8h4.5L14 3.5z"/></svg>';

async function sysCmd(command, parameters = {}) {
  return api(`/plugins/jarvis.system/commands/${encodeURIComponent(command)}`, {
    method: 'POST',
    body: JSON.stringify({ parameters }),
  });
}

async function pluginCmd(pluginId, command, parameters = {}) {
  return api(`/plugins/${encodeURIComponent(pluginId)}/commands/${encodeURIComponent(command)}`, {
    method: 'POST',
    body: JSON.stringify({ parameters }),
  });
}

function fmtBytes(bytes) {
  if (!bytes && bytes !== 0) return '—';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let value = bytes;
  let index = 0;
  while (value >= 1024 && index < units.length - 1) { value /= 1024; index += 1; }
  return `${value.toFixed(value >= 100 || index === 0 ? 0 : 1)} ${units[index]}`;
}

function fmtUptime(value) {
  const match = String(value ?? '').match(/(?:(\d+)\.)?(\d+):(\d+):(\d+)/);
  if (!match) return String(value ?? '');
  const days = Number(match[1] || 0);
  const hours = Number(match[2]);
  const minutes = Number(match[3]);
  if (days) return `${days}d ${hours}h ${minutes}m`;
  if (hours) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}

function meter(percent, label, sub) {
  const clamped = Math.max(0, Math.min(100, percent));
  return `
    <div class="sys-bar-row">
      <div class="sys-bar-head"><span>${escapeHtml(label)}</span><span class="sys-bar-val">${escapeHtml(sub)}</span></div>
      <div class="sys-bar"><div class="sys-bar-fill" style="width:${clamped}%"></div></div>
    </div>`;
}

async function renderSystem() {
  const container = $('#view-system');
  container.innerHTML = `
    <div class="page-head">
      <div class="page-title">System</div>
      <div class="page-subtitle">PC control panel — hardware, processes, files and applications.</div>
    </div>
    <div class="card sys-card">
      <div class="card-title"><span>Hardware</span><button class="btn btn-ghost" id="sys-hw-refresh">Refresh</button></div>
      <div id="sys-hardware"><div class="spinner"></div></div>
    </div>
    <div class="card sys-card">
      <div class="card-title"><span>Processes</span><button class="btn btn-ghost" id="sys-proc-refresh">Refresh</button></div>
      <div class="sys-toolbar">
        <input id="sys-proc-q" type="search" placeholder="filter by name or pid">
      </div>
      <div id="sys-proc-status" class="sys-status" hidden></div>
      <div id="sys-processes"><div class="spinner"></div></div>
    </div>
    <div class="card sys-card">
      <div class="card-title"><span>Files</span><button class="btn btn-ghost" id="sys-file-up">Up one level</button></div>
      <div class="sys-toolbar">
        <input id="sys-file-path" type="text" placeholder="directory path" value="/workspace">
        <button class="btn btn-primary" id="sys-file-go">Open</button>
      </div>
      <div id="sys-files"><div class="spinner"></div></div>
    </div>
    <div class="card sys-card">
      <div class="card-title"><span>Applications</span></div>
      <div class="sys-form-row">
        <div class="sys-form">
          <input id="sys-app-name" type="text" placeholder="app name or path">
          <input id="sys-app-args" type="text" placeholder="arguments (optional)">
          <button class="btn btn-primary" id="sys-app-launch">Launch</button>
        </div>
        <div class="sys-form">
          <input id="sys-stop-name" type="text" placeholder="app name to stop">
          <button class="btn btn-ghost" id="sys-app-stop">Stop</button>
          <button class="btn btn-ghost" id="sys-app-check">Check running</button>
        </div>
      </div>
      <div id="sys-app-status" class="sys-status" hidden></div>
    </div>`;

  try {
    state.plugins = await api('/plugins');
    if (!state.plugins.some((plugin) => plugin.id === 'jarvis.system')) {
      throw new Error('jarvis.system plugin is not loaded.');
    }
  } catch (error) {
    container.innerHTML = `<div class="page-head"><div class="page-title">System</div></div><div class="card"><div class="empty-state">${escapeHtml(error.message)}</div></div>`;
    return;
  }

  const stateful = {
    files: '/workspace',
    processQuery: '',
  };

  const showStatus = (target, kind, text) => {
    const box = $(target);
    box.hidden = false;
    box.className = `sys-status ${kind}`;
    box.textContent = text;
  };

  const loadHardware = async () => {
    const box = $('#sys-hardware');
    try {
      const response = await sysCmd('system.hardware.metrics');
      if (!response.data) { box.textContent = response.result; return; }
      const d = response.data;
      const memPercent = d.memoryTotalBytes ? (1 - d.memoryAvailableBytes / d.memoryTotalBytes) * 100 : 0;
      const memUsed = d.memoryTotalBytes - d.memoryAvailableBytes;
      box.innerHTML =
        meter(d.cpuPercent, 'CPU', `${d.cpuPercent.toFixed(1)}%`) +
        meter(memPercent, 'Memory', `${fmtBytes(memUsed)} / ${fmtBytes(d.memoryTotalBytes)}`) +
        d.disks.map((disk) => {
          const percent = disk.totalBytes ? (1 - disk.freeBytes / disk.totalBytes) * 100 : 0;
          return meter(percent, `Disk ${disk.name}`, `${fmtBytes(disk.totalBytes - disk.freeBytes)} / ${fmtBytes(disk.totalBytes)}`);
        }).join('') +
        `<div class="sys-meta">Host ${escapeHtml(d.hostName)} · ${escapeHtml(d.operatingSystem)} · up ${escapeHtml(fmtUptime(d.uptime))}</div>`;
    } catch (error) {
      box.innerHTML = `<div class="empty-state">${escapeHtml(error.message)}</div>`;
    }
  };

  const loadProcesses = async (query) => {
    const box = $('#sys-processes');
    try {
      const response = await sysCmd('system.process.list', { name: query, limit: 100 });
      const processes = response.data || [];
      if (!processes.length) { box.innerHTML = '<div class="empty-state">No processes found.</div>'; return; }
      box.innerHTML = `
        <div class="sys-table">
          <div class="sys-row sys-row-head"><span>Process</span><span>PID</span><span>Memory</span><span>CPU (s)</span><span>Threads</span><span></span></div>
          ${processes.map((process) => `
            <div class="sys-row">
              <span class="sys-name" title="${escapeHtml(process.path || '')}">${escapeHtml(process.name)}</span>
              <span class="sys-mono">${process.pid}</span>
              <span>${fmtBytes(process.memoryBytes)}</span>
              <span>${(process.cpuSeconds || 0).toFixed(1)}</span>
              <span>${process.threads}</span>
              <span><button class="btn btn-ghost sys-kill" data-pid="${process.pid}" data-name="${escapeHtml(process.name)}">Kill</button></span>
            </div>`).join('')}
        </div>`;
      $$('.sys-kill', box).forEach((button) => button.addEventListener('click', () => {
        killProcess(button.dataset.pid, button.dataset.name, loadProcesses, stateful, showStatus);
      }));
    } catch (error) {
      box.innerHTML = `<div class="empty-state">${escapeHtml(error.message)}</div>`;
    }
  };

  const loadFiles = async (path) => {
    const box = $('#sys-files');
    stateful.files = path;
    $('#sys-file-path').value = path;
    try {
      const response = await sysCmd('system.file.list', { path });
      const entries = response.data || [];
      box.innerHTML = `
        <div class="sys-path">${escapeHtml(path)}</div>
        ${entries.length ? `<div class="sys-files-list">
          ${entries.map((entry) => entry.isDirectory
            ? `<div class="sys-file sys-dir" data-path="${escapeHtml(entry.path)}">${ICON_DIR}<span>${escapeHtml(entry.path)}</span></div>`
            : `<div class="sys-file" data-path="${escapeHtml(entry.path)}">${ICON_FILE}<span>${escapeHtml(entry.path)}</span><span class="sys-file-meta">${fmtBytes(entry.size)} · ${escapeHtml(String(entry.lastModified).slice(0, 10))}</span></div>`).join('')}
        </div>` : '<div class="empty-state">Empty directory.</div>'}
        <div id="sys-file-preview" class="sys-file-preview" hidden></div>`;
      $$('.sys-file', box).forEach((row) => row.addEventListener('click', () => onFileRowClick(row, stateful, loadFiles)));
    } catch (error) {
      box.innerHTML = `<div class="empty-state">${escapeHtml(error.message)}</div>`;
    }
  };

  const onFileRowClick = async (row, context, refresh) => {
    const path = row.dataset.path;
    const preview = $('#sys-file-preview');
    if (row.classList.contains('sys-dir')) {
      preview.hidden = true;
      await refresh(path);
      return;
    }
    try {
      const response = await sysCmd('system.file.read', { path, maxBytes: 8192 });
      preview.hidden = false;
      preview.textContent = response.result;
      preview.scrollIntoView({ block: 'nearest' });
    } catch (error) {
      preview.hidden = false;
      preview.textContent = error.message;
    }
  };

  const killProcess = async (pid, name, refresh, context, status) => {
    if (!window.confirm(`Stop process ${name} (pid ${pid})?`)) return;
    try {
      const response = await sysCmd('system.process.kill', { id: pid });
      status('#sys-proc-status', 'ok', response.result);
    } catch (error) {
      status('#sys-proc-status', 'err', error.message);
    }
    await refresh(context.processQuery);
  };

  $('#sys-hw-refresh').addEventListener('click', loadHardware);
  $('#sys-proc-refresh').addEventListener('click', () => {
    stateful.processQuery = $('#sys-proc-q').value.trim();
    loadProcesses(stateful.processQuery);
  });
  $('#sys-proc-q').addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      stateful.processQuery = event.target.value.trim();
      loadProcesses(stateful.processQuery);
    }
  });
  $('#sys-file-go').addEventListener('click', () => loadFiles($('#sys-file-path').value.trim() || '/'));
  $('#sys-file-path').addEventListener('keydown', (event) => {
    if (event.key === 'Enter') loadFiles(event.target.value.trim() || '/');
  });
  $('#sys-file-up').addEventListener('click', () => {
    const current = stateful.files || '/';
    const parent = current === '/' ? '/' : current.replace(/[\/\\][^\/\\]*$/, '') || '/';
    loadFiles(parent);
  });
  $('#sys-app-launch').addEventListener('click', async () => {
    const name = $('#sys-app-name').value.trim();
    if (!name) { showStatus('#sys-app-status', 'err', 'Enter an application name or path.'); return; }
    const args = $('#sys-app-args').value.trim();
    try {
      const response = await sysCmd('system.app.launch', { name, arguments: args });
      showStatus('#sys-app-status', 'ok', response.result);
    } catch (error) {
      showStatus('#sys-app-status', 'err', error.message);
    }
  });
  $('#sys-app-stop').addEventListener('click', async () => {
    const name = $('#sys-stop-name').value.trim();
    if (!name) { showStatus('#sys-app-status', 'err', 'Enter an application name to stop.'); return; }
    try {
      const response = await sysCmd('system.app.stop', { name });
      showStatus('#sys-app-status', 'ok', response.result);
    } catch (error) {
      showStatus('#sys-app-status', 'err', error.message);
    }
  });
  $('#sys-app-check').addEventListener('click', async () => {
    const name = $('#sys-stop-name').value.trim();
    if (!name) { showStatus('#sys-app-status', 'err', 'Enter an application name to check.'); return; }
    try {
      const response = await sysCmd('system.app.running', { name });
      showStatus('#sys-app-status', 'ok', response.result);
    } catch (error) {
      showStatus('#sys-app-status', 'err', error.message);
    }
  });

  loadHardware();
  loadProcesses('');
  loadFiles('/workspace');
}

/* ---------- studio ------------------------------------------- */

async function renderStudio() {
  const container = $('#view-studio');
  container.innerHTML = `
    <div class="page-head">
      <div class="page-title">JARVIS Studio</div>
      <div class="page-subtitle">Scaffold, generate, build, test and run developer projects.</div>
    </div>
    <div class="grid grid-2">
      <div class="card sys-card">
        <div class="card-title">New project</div>
        <div class="sys-form">
          <input id="st-name" type="text" placeholder="project name (e.g. MyApp)">
          <select id="st-template">
            <option value="dotnet-console">C# console (dotnet)</option>
            <option value="node-app">Node.js app</option>
            <option value="python-app">Python app</option>
          </select>
          <button class="btn btn-primary" id="st-create">Create</button>
        </div>
        <div id="st-create-status" class="sys-status" hidden></div>
      </div>
      <div class="card sys-card">
        <div class="card-title">AI generate code</div>
        <div class="sys-form">
          <input id="st-gen-path" type="text" placeholder="project file path, e.g. MyApp/Program.cs">
          <input id="st-gen-prompt" type="text" placeholder="describe the code to write">
          <button class="btn btn-primary" id="st-generate">Generate</button>
        </div>
        <div id="st-gen-status" class="sys-status" hidden></div>
      </div>
    </div>
    <div class="card sys-card">
      <div class="card-title"><span>Projects</span><button class="btn btn-ghost" id="st-refresh">Refresh</button></div>
      <div id="st-projects"><div class="spinner"></div></div>
    </div>
    <div class="card sys-card">
      <div class="card-title">Output</div>
      <pre id="st-output" class="code-output">Select a project and action to see the output.</pre>
    </div>`;

  try {
    state.plugins = await api('/plugins');
    if (!state.plugins.some((plugin) => plugin.id === 'jarvis.developer')) {
      throw new Error('jarvis.developer plugin is not loaded.');
    }
  } catch (error) {
    container.innerHTML = `<div class="page-head"><div class="page-title">Studio</div></div><div class="card"><div class="empty-state">${escapeHtml(error.message)}</div></div>`;
    return;
  }

  const showStatus = (target, kind, text) => {
    const box = $(target);
    box.hidden = false;
    box.className = `sys-status ${kind}`;
    box.textContent = text;
  };

  const setOutput = (text) => { $('#st-output').textContent = text; };

  const runAction = async (action, name) => {
    const command = {
      info: 'developer.project.info',
      build: 'developer.build',
      test: 'developer.test',
      run: 'developer.run',
    }[action];
    setOutput(`Running ${action} on ${name}...`);
    try {
      const response = await pluginCmd('jarvis.developer', command, { name });
      setOutput(response.result);
    } catch (error) {
      setOutput(`Error: ${error.message}`);
    }
  };

  const loadProjects = async () => {
    const box = $('#st-projects');
    try {
      const response = await pluginCmd('jarvis.developer', 'developer.project.list');
      const projects = (response.data || []).map((line) => line.split(' [')[0]);
      if (!projects.length) {
        box.innerHTML = '<div class="empty-state">No projects yet. Create one above.</div>';
        return;
      }
      box.innerHTML = `
        <div class="sys-table">
          <div class="sys-row sys-row-head"><span>Project</span><span></span><span></span><span></span><span></span><span></span></div>
          ${projects.map((name) => `
            <div class="sys-row">
              <span class="sys-name">${escapeHtml(name)}</span>
              <span><button class="btn btn-ghost st-act" data-name="${escapeHtml(name)}" data-act="info">Info</button></span>
              <span><button class="btn btn-ghost st-act" data-name="${escapeHtml(name)}" data-act="build">Build</button></span>
              <span><button class="btn btn-ghost st-act" data-name="${escapeHtml(name)}" data-act="test">Test</button></span>
              <span><button class="btn btn-ghost st-act" data-name="${escapeHtml(name)}" data-act="run">Run</button></span>
              <span></span>
            </div>`).join('')}
        </div>`;
      $$('.st-act', box).forEach((button) => button.addEventListener('click', () => {
        runAction(button.dataset.act, button.dataset.name);
      }));
    } catch (error) {
      box.innerHTML = `<div class="empty-state">${escapeHtml(error.message)}</div>`;
    }
  };

  $('#st-create').addEventListener('click', async () => {
    const name = $('#st-name').value.trim();
    const template = $('#st-template').value;
    if (!name) { showStatus('#st-create-status', 'err', 'Enter a project name.'); return; }
    try {
      const response = await pluginCmd('jarvis.developer', 'developer.project.create', { name, template });
      showStatus('#st-create-status', 'ok', response.result);
      $('#st-name').value = '';
      await loadProjects();
    } catch (error) {
      showStatus('#st-create-status', 'err', error.message);
    }
  });

  $('#st-generate').addEventListener('click', async () => {
    const path = $('#st-gen-path').value.trim();
    const prompt = $('#st-gen-prompt').value.trim();
    if (!path || !prompt) { showStatus('#st-gen-status', 'err', 'Enter both a file path and a prompt.'); return; }
    setOutput(`Generating ${path} ...`);
    try {
      const response = await pluginCmd('jarvis.developer', 'developer.generate', { path, prompt });
      showStatus('#st-gen-status', 'ok', `Generated ${path}.`);
      setOutput(response.result);
    } catch (error) {
      showStatus('#st-gen-status', 'err', error.message);
      setOutput(`Error: ${error.message}`);
    }
  });

  $('#st-refresh').addEventListener('click', loadProjects);
  loadProjects();
}

/* ---------- senses ------------------------------------------- */

function speakBrowser(text) {
  if (!('speechSynthesis' in window)) return false;
  const utterance = new SpeechSynthesisUtterance(text);
  utterance.lang = 'en-US';
  window.speechSynthesis.cancel();
  window.speechSynthesis.speak(utterance);
  return true;
}

async function renderSenses() {
  const container = $('#view-senses');
  container.innerHTML = `
    <div class="page-head">
      <div class="page-title">Senses</div>
      <div class="page-subtitle">Voice synthesis, transcription and computer vision for JARVIS.</div>
    </div>
    <div class="grid grid-2">
      <div class="card sys-card">
        <div class="card-title">Speak</div>
        <div class="sys-form">
          <input id="sn-text" type="text" placeholder="text to speak">
          <input id="sn-voice" type="text" placeholder="voice name (optional)">
          <button class="btn btn-primary" id="sn-speak">Speak</button>
        </div>
        <div id="sn-speak-status" class="sys-status" hidden></div>
      </div>
      <div class="card sys-card">
        <div class="card-title">Transcribe</div>
        <div class="sys-form">
          <input id="sn-audio" type="text" placeholder="audio file path (requires whisper CLI)">
          <button class="btn btn-primary" id="sn-transcribe">Transcribe</button>
        </div>
        <div id="sn-transcribe-status" class="sys-status" hidden></div>
      </div>
    </div>
    <div class="grid grid-2" style="margin-top:14px">
      <div class="card sys-card">
        <div class="card-title">Vision analysis</div>
        <div class="sys-form">
          <input id="sn-image-url" type="text" placeholder="image URL or server path">
          <input id="sn-image-file" type="file" accept="image/*">
          <input id="sn-prompt" type="text" placeholder="prompt (default: describe the image)">
          <button class="btn btn-primary" id="sn-analyze">Analyze</button>
        </div>
        <div id="sn-image-preview" class="vision-preview" hidden></div>
        <div id="sn-vision-result" class="vision-result"></div>
        <div id="sn-analyze-status" class="sys-status" hidden></div>
      </div>
      <div class="card sys-card">
        <div class="card-title">Screen capture</div>
        <div class="sys-form">
          <input id="sn-screen-prompt" type="text" placeholder="prompt to analyze the capture (optional)">
          <button class="btn btn-primary" id="sn-screen">Capture screen</button>
        </div>
        <div id="sn-screen-status" class="sys-status" hidden></div>
      </div>
    </div>`;

  try {
    state.plugins = await api('/plugins');
    if (!state.plugins.some((plugin) => plugin.id === 'jarvis.senses')) {
      throw new Error('jarvis.senses plugin is not loaded.');
    }
  } catch (error) {
    container.innerHTML = `<div class="page-head"><div class="page-title">Senses</div></div><div class="card"><div class="empty-state">${escapeHtml(error.message)}</div></div>`;
    return;
  }

  const showStatus = (target, kind, text) => {
    const box = $(target);
    box.hidden = false;
    box.className = `sys-status ${kind}`;
    box.textContent = text;
  };

  const sensesCmd = (command, parameters = {}) => pluginCmd('jarvis.senses', command, parameters);

  $('#sn-speak').addEventListener('click', async () => {
    const text = $('#sn-text').value.trim();
    if (!text) { showStatus('#sn-speak-status', 'err', 'Enter some text to speak.'); return; }
    const voice = $('#sn-voice').value.trim();
    try {
      const response = await sensesCmd('voice.speak', { text, voice });
      if (response.data && response.data.synthesized) {
        showStatus('#sn-speak-status', 'ok', `Synthesized locally (${response.data.audioPath}).`);
      } else {
        const spoken = speakBrowser(text);
        showStatus('#sn-speak-status', 'ok', spoken
          ? 'No local TTS; spoken via browser speech synthesis.'
          : 'Local TTS unavailable and browser speech is not supported here.');
      }
    } catch (error) {
      showStatus('#sn-speak-status', 'err', error.message);
    }
  });

  $('#sn-transcribe').addEventListener('click', async () => {
    const file = $('#sn-audio').value.trim();
    if (!file) { showStatus('#sn-transcribe-status', 'err', 'Enter an audio file path.'); return; }
    try {
      const response = await sensesCmd('voice.transcribe', { file });
      showStatus('#sn-transcribe-status', 'ok', (response.data && response.data.text) || response.result);
    } catch (error) {
      showStatus('#sn-transcribe-status', 'err', error.message);
    }
  });

  const currentImage = { source: null };

  $('#sn-image-file').addEventListener('change', async (event) => {
    const file = event.target.files && event.target.files[0];
    if (!file) return;
    const dataUri = await new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result);
      reader.onerror = () => reject(new Error('Could not read the image file.'));
      reader.readAsDataURL(file);
    });
    currentImage.source = dataUri;
    $('#sn-image-url').value = file.name;
    const preview = $('#sn-image-preview');
    preview.hidden = false;
    preview.innerHTML = `<img src="${dataUri}" alt="image preview">`;
  });

  $('#sn-analyze').addEventListener('click', async () => {
    const source = currentImage.source || $('#sn-image-url').value.trim();
    const prompt = $('#sn-prompt').value.trim() || 'Describe this image in detail.';
    if (!source) { showStatus('#sn-analyze-status', 'err', 'Choose an image file or enter an image URL/path.'); return; }
    $('#sn-vision-result').textContent = '';
    showStatus('#sn-analyze-status', 'warn', 'Analyzing image...');
    try {
      const response = await sensesCmd('vision.analyze', { image: source, prompt });
      $('#sn-vision-result').textContent = response.data.description || response.result;
      showStatus('#sn-analyze-status', 'ok', `Analyzed with ${response.data.model} (${response.data.provider}).`);
    } catch (error) {
      showStatus('#sn-analyze-status', 'err', error.message);
    }
  });

  $('#sn-screen').addEventListener('click', async () => {
    const prompt = $('#sn-screen-prompt').value.trim();
    try {
      const response = await sensesCmd('vision.screen', prompt ? { prompt } : {});
      showStatus('#sn-screen-status', 'ok', response.result);
    } catch (error) {
      showStatus('#sn-screen-status', 'err', error.message);
    }
  });
}

/* ---------- settings ------------------------------------------ */

async function renderSettings() {
  const container = $('#view-settings');
  container.innerHTML = `
    <div class="page-head">
      <div class="page-title">Settings</div>
      <div class="page-subtitle">JARVIS HUB preferences and AI configuration.</div>
    </div>
    <div class="card">
      <div class="card-title">Appearance</div>
      <div class="setting-row">
        <div>
          <div>Dark theme</div>
          <div style="color:var(--text-tertiary);font-size:12.5px">Windows 11 Fluent / Mica styling</div>
        </div>
        <div class="toggle is-on" id="theme-toggle"></div>
      </div>
    </div>
    <div class="card">
      <div class="card-title">Connection</div>
      <div class="setting-row">
        <div>
          <div>API host</div>
          <div style="color:var(--text-tertiary);font-size:12.5px">JARVIS.API web host used by this interface</div>
        </div>
        <div style="font-family:monospace;font-size:13px">${escapeHtml(API_BASE)}</div>
      </div>
      <div class="setting-row">
        <div>
          <div>AI models</div>
          <div id="settings-models" style="color:var(--text-tertiary);font-size:12.5px"><div class="spinner"></div></div>
        </div>
      </div>
    </div>
    <div class="card">
      <div class="card-title">AI providers</div>
      <div id="settings-providers"><div class="spinner"></div></div>
    </div>
    <div class="card">
      <div class="card-title">Connection</div>
      <div class="setting-row">
        <div>
          <div>API host</div>
          <div style="color:var(--text-tertiary);font-size:12.5px">JARVIS.API web host used by this interface</div>
        </div>
        <div style="font-family:monospace;font-size:13px">${escapeHtml(API_BASE)}</div>
      </div>
      <div class="setting-row">
        <div>
          <div>Cloud API keys</div>
          <div style="color:var(--text-tertiary);font-size:12.5px">Set per-provider keys in <code>AI:OpenAICompat</code> or via env vars (<code>JARVIS_OPENAI_API_KEY</code>, <code>JARVIS_OPENROUTER_API_KEY</code>, <code>JARVIS_GEMINI_API_KEY</code>, ...). Local Ollama needs no key.</div>
        </div>
        <button class="btn btn-ghost" id="btn-test">Test connection</button>
      </div>
    </div>
  `;

  const toggle = $('#theme-toggle');
  const currentTheme = localStorage.getItem('jarvis.theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
  toggle.classList.toggle('is-on', currentTheme === 'dark');
  toggle.addEventListener('click', () => {
    const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
    localStorage.setItem('jarvis.theme', next);
    applyTheme(next);
  });

  $('#btn-test').addEventListener('click', async () => {
    try {
      await api('/health');
      alert('Connection OK — JARVIS.API is reachable.');
    } catch (error) {
      alert(`Connection failed: ${error.message}`);
    }
  });

  try {
    const models = await api('/ai/models');
    $('#settings-models').textContent = models.length ? models.map((model) => model.id).join(' · ') : 'none configured';
  } catch (error) {
    $('#settings-models').textContent = error.message;
  }

  renderProviderList($('#settings-providers'));
}

/* ---------- modal shell --------------------------------------- */

function ensureModal() {
  if ($('#command-modal')) return;
  const backdrop = document.createElement('div');
  backdrop.id = 'command-modal';
  backdrop.className = 'modal-backdrop';
  document.body.appendChild(backdrop);
}

/* ---------- misc ---------------------------------------------- */

function debounce(fn, wait) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), wait);
  };
}

/* ---------- boot ---------------------------------------------- */

async function boot() {
  ensureModal();
  initTheme();

  $('#btn-minimize').addEventListener('click', () => window.jarvis?.windowControl('minimize'));
  $('#btn-maximize').addEventListener('click', () => window.jarvis?.windowControl('maximize'));
  $('#btn-close').addEventListener('click', () => window.jarvis?.windowControl('close'));

  $$('.nav-item').forEach((item) => item.addEventListener('click', () => navigate(item.dataset.view)));
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') $('#command-modal')?.classList.remove('is-open');
  });

  try {
    state.models = await api('/ai/models');
  } catch { state.models = []; }

  try {
    state.providers = await api('/ai/providers');
  } catch { state.providers = []; }

  checkConnection();
  setInterval(checkConnection, 15000);
  navigate('dashboard');
}

boot();
