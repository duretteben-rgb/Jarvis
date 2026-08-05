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
  const modelOptions = state.models
    .map((model) => `<option value="${escapeHtml(model.id)}">${escapeHtml(model.displayName || model.id)}</option>`)
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
      <div class="modal-actions">
        <button class="btn btn-ghost" data-modal-close>Cancel</button>
        <button class="btn btn-primary" id="cmd-run">Run</button>
      </div>
    </div>`;
  backdrop.classList.add('is-open');

  const close = () => backdrop.classList.remove('is-open');
  backdrop.addEventListener('click', (event) => { if (event.target === backdrop) close(); });
  $$('[data-modal-close]', backdrop).forEach((button) => button.addEventListener('click', close));

  $('#cmd-run').addEventListener('click', async () => {
    let parameters = {};
    const raw = $('#cmd-params').value.trim();
    if (raw) {
      try { parameters = JSON.parse(raw); } catch { alert('Invalid JSON parameters.'); return; }
    }

    $('#cmd-run').disabled = true;
    try {
      const response = await api(`/plugins/${encodeURIComponent(pluginId)}/commands/${encodeURIComponent(command)}`, {
        method: 'POST',
        body: JSON.stringify({ parameters }),
      });
      alert(response.success ? `Result: ${response.result}` : `Error: ${response.error}`);
    } catch (error) {
      alert(error.message);
    } finally {
      $('#cmd-run').disabled = false;
      close();
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
      <div class="setting-row">
        <div>
          <div>Cloud API key</div>
          <div style="color:var(--text-tertiary);font-size:12.5px">Set <code>JARVIS_OPENAI_API_KEY</code> (e.g. a free Groq key) or <code>AI:OpenAI:ApiKey</code> to enable cloud models.</div>
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

  checkConnection();
  setInterval(checkConnection, 15000);
  navigate('dashboard');
}

boot();
