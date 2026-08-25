// AxarDB Web Interface
let editor;
let jsonViewEditor = null;
let currentCollections = [];
let currentMemoryCollections = [];
let currentBulkCollections = [];
let collectionFields = { db: {}, memory: {}, bulk: {} };
let collectionSamples = { db: {}, memory: {}, bulk: {} };
let queryResults = [];
let currentDisplayData = []; // To easily reference data for grid actions
let currentGridKeys = []; // Stores keys currently visible in the grid
let sortCol = null;
let sortDir = 1;
let filters = {};
let lastCollectionName = "sysusers";
let lastCollectionType = "db"; // 'db', 'memory', or 'bulk'
let activeHistoryId = null;
let _historyDebounceTimer = null;
let _suppressHistorySync = false;
let activeQueryController = null;

// --- Custom Alert System ---
window.showAlert = function(message, type = 'info', duration = 4000) {
    const container = document.getElementById('alertContainer');
    if (!container) {
        console.warn("Alert container not found for message:", message);
        return;
    }
    
    const alertEl = document.createElement('div');
    const isError = type === 'error' || String(message).toLowerCase().includes('error') || String(message).toLowerCase().includes('failed');
    
    // Bootstrap-like styling
    const bgColor = isError ? 'rgba(220, 53, 69, 0.95)' : 'rgba(25, 135, 84, 0.95)';
    const borderColor = isError ? 'rgba(220, 53, 69, 1)' : 'rgba(25, 135, 84, 1)';
    
    alertEl.style.background = bgColor;
    alertEl.style.color = 'white';
    alertEl.style.padding = '0.75rem 1.25rem';
    alertEl.style.borderRadius = '0.375rem';
    alertEl.style.fontWeight = '500';
    alertEl.style.fontSize = '0.875rem';
    alertEl.style.boxShadow = '0 0.5rem 1rem rgba(0, 0, 0, 0.15)';
    alertEl.style.border = `1px solid ${borderColor}`;
    alertEl.style.backdropFilter = 'blur(4px)';
    alertEl.style.pointerEvents = 'auto'; // allow clicking
    alertEl.style.cursor = 'pointer';
    alertEl.style.transition = 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)';
    alertEl.style.opacity = '0';
    alertEl.style.transform = 'translateY(-10px)';
    alertEl.style.wordBreak = 'break-word';
    alertEl.style.minWidth = '250px';
    alertEl.style.maxWidth = '400px';
    
    // Inner structure to look like a toast
    alertEl.innerHTML = `
        <div style="display: flex; align-items: center; justify-content: space-between;">
            <div style="display: flex; align-items: center; gap: 8px;">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    ${isError ? '<circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line>' : '<path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline>'}
                </svg>
                <span>${escapeHtml(String(message))}</span>
            </div>
            <button type="button" style="background: none; border: none; color: white; opacity: 0.8; cursor: pointer; padding: 0; margin-left: 12px; line-height: 1;" aria-label="Close">
                <span style="font-size: 1.25rem;">&times;</span>
            </button>
        </div>
    `;
    
    // Add click to dismiss on the close button
    alertEl.querySelector('button').onclick = (e) => {
        e.stopPropagation();
        alertEl.style.opacity = '0';
        alertEl.style.transform = 'translateY(-10px)';
        setTimeout(() => alertEl.remove(), 300);
    };
    
    container.appendChild(alertEl);
    
    // Animate in
    requestAnimationFrame(() => {
        alertEl.style.opacity = '1';
        alertEl.style.transform = 'translateY(0)';
    });
    
    // Auto dismiss
    if (duration > 0) {
        setTimeout(() => {
            if (alertEl.parentElement) {
                alertEl.style.opacity = '0';
                alertEl.style.transform = 'translateY(-10px)';
                setTimeout(() => {
                    if (alertEl.parentElement) alertEl.remove();
                }, 300);
            }
        }, duration);
    }
};

window.alert = function(message) {
    window.showAlert(message);
};

// --- Tab System ---
let tabs = [];
let activeTabId = null;
let _tabCounter = 0;

function generateTabId() {
    return 'tab_' + (++_tabCounter);
}

function createTab(title, script) {
    const id = generateTabId();
    const tab = {
        id,
        title: title || `Query ${_tabCounter}`,
        script: script || "// Type your JavaScript query here\n// Use 'db.collection' to access data\n",
        queryResults: [],
        filters: {},
        sortCol: null,
        sortDir: 1,
        lastCollectionName: lastCollectionName || 'sysusers'
    };
    tabs.push(tab);
    switchTab(id);
    return id;
}

function saveCurrentTabState() {
    if (!activeTabId) return;
    const tab = tabs.find(t => t.id === activeTabId);
    if (!tab) return;
    tab.script = editor ? editor.getValue() : '';
    tab.queryResults = queryResults;
    tab.filters = { ...filters };
    tab.sortCol = sortCol;
    tab.sortDir = sortDir;
    tab.lastCollectionName = lastCollectionName;
    tab.lastCollectionType = lastCollectionType;
}

function updateSysconfigBanner(force) {
    const banner = document.querySelector('.sysconfig-banner');
    if (!banner) return;
    if (localStorage.getItem('sysconfig_banner_dismissed')) {
        banner.style.display = 'none';
        return;
    }
    banner.style.display = (force || lastCollectionName === 'sysconfig') ? 'flex' : 'none';
}

function initSysconfigBanner() {
    const banner = document.querySelector('.sysconfig-banner');
    if (!banner) return;
    banner.style.cursor = 'pointer';
    banner.title = 'Click to dismiss permanently';
    banner.addEventListener('click', () => {
        banner.style.display = 'none';
        localStorage.setItem('sysconfig_banner_dismissed', '1');
    });
}

function restoreTabState(tab) {
    queryResults = tab.queryResults || [];
    filters = tab.filters || {};
    sortCol = tab.sortCol || null;
    sortDir = tab.sortDir || 1;
    lastCollectionName = tab.lastCollectionName || 'sysusers';
    lastCollectionType = tab.lastCollectionType || 'db';
    updateSysconfigBanner();
    if (editor) {
        _suppressHistorySync = true;
        editor.setValue(tab.script);
        _suppressHistorySync = false;
    }
    renderGrid();
}

function switchTab(id) {
    if (activeTabId === id) return;
    saveCurrentTabState();
    activeTabId = id;
    activeHistoryId = null;
    const tab = tabs.find(t => t.id === id);
    if (tab) restoreTabState(tab);
    renderTabBar();
}

function closeTab(id) {
    if (tabs.length <= 1) return;
    const idx = tabs.findIndex(t => t.id === id);
    if (idx === -1) return;
    tabs.splice(idx, 1);
    if (activeTabId === id) {
        const newIdx = Math.min(idx, tabs.length - 1);
        activeTabId = null;
        switchTab(tabs[newIdx].id);
    } else {
        renderTabBar();
    }
}

function closeOtherTabs() {
    if (tabs.length <= 1) return;
    tabs = [tabs[0]];
    activeTabId = null;
    switchTab(tabs[0].id);
}

function renderTabBar() {
    const list = document.getElementById('tabBarList');
    if (!list) return;
    list.innerHTML = tabs.map(tab => {
        const isActive = tab.id === activeTabId;
        const titleHtml = escapeHtml(tab.title);
        return `<button class="tab-item ${isActive ? 'active' : ''}" data-tab="${tab.id}" onclick="switchTab('${tab.id}')">
            <span class="tab-item-title">${titleHtml}</span>
            ${tabs.length > 1 ? `<span class="tab-close" onclick="event.stopPropagation(); closeTab('${tab.id}')">&times;</span>` : ''}
        </button>`;
    }).join('');
}

document.addEventListener('DOMContentLoaded', () => {
    initResizers();
    initEditor();
    initButtons();
    initLogin();
    checkAuthAndLoad();
    initIcons();
    initSysconfigBanner();
    initAiQuery();
    createTab('Query 1');
});

function initLogin() {
    const form = document.getElementById('loginForm');
    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const u = document.getElementById('username').value;
        const p = document.getElementById('password').value;
        const auth = btoa(`${u}:${p}`);

        // Quick verify
        try {
            const res = await fetch('/collections', { headers: { 'Authorization': `Basic ${auth}` } });
            if (res.ok) {
                localStorage.setItem('AxarDB_auth', auth);
                document.getElementById('loginModal').style.display = 'none';
                loadCollections();
            } else {
                showLoginError();
            }
        } catch (err) {
            showLoginError('Connection failed');
        }
    });
}

function showLoginError(msg = 'Invalid credentials') {
    const el = document.getElementById('loginError');
    el.textContent = msg;
    el.style.display = 'block';
}

function checkAuthAndLoad() {
    if (!localStorage.getItem('AxarDB_auth')) {
        document.getElementById('loginModal').style.display = 'flex';
    } else {
        loadCollections();
    }
}


function initEditor() {
    require.config({ paths: { vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.43.0/min/vs' } });
    require(['vs/editor/editor.main'], function () {
        editor = monaco.editor.create(document.getElementById('editorContainer'), {
            value: "// Type your JavaScript query here\n// Use 'db.collection' to access data\n",
            language: 'javascript',
            theme: 'vs-dark',
            automaticLayout: true,
            minimap: { enabled: false },
            fontSize: 14,
            padding: { top: 16 }
        });

        monaco.languages.registerCompletionItemProvider('javascript', {
            triggerCharacters: ['.'],
            provideCompletionItems: function (model, position) {
                const textBefore = model.getValueInRange({
                    startLineNumber: 1,
                    startColumn: 1,
                    endLineNumber: position.lineNumber,
                    endColumn: position.column
                });

                const wordInfo = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber: position.lineNumber,
                    startColumn: wordInfo.startColumn,
                    endColumn: wordInfo.endColumn
                };

                const textBeforeWord = textBefore.substring(0, textBefore.length - wordInfo.word.length);

                if (textBeforeWord.endsWith('.')) {
                    const lineUntilDot = textBeforeWord.substring(0, textBeforeWord.length - 1);
                    const tokenMatch = lineUntilDot.match(/([a-zA-Z0-9_]+)$/);

                    if (tokenMatch) {
                        const token = tokenMatch[1];

                        // Case A: db., memory., bulk.
                        if (token === 'db') {
                            return {
                                suggestions: currentCollections.map(name => ({
                                    label: name,
                                    kind: monaco.languages.CompletionItemKind.Class,
                                    insertText: name,
                                    detail: "Database Collection",
                                    range: range
                                }))
                            };
                        }
                        if (token === 'memory') {
                            return {
                                suggestions: currentMemoryCollections.map(name => ({
                                    label: name,
                                    kind: monaco.languages.CompletionItemKind.Class,
                                    insertText: name,
                                    detail: "TTL Memory Collection",
                                    range: range
                                }))
                            };
                        }
                        if (token === 'bulk') {
                            return {
                                suggestions: currentBulkCollections.map(name => ({
                                    label: name,
                                    kind: monaco.languages.CompletionItemKind.Class,
                                    insertText: name,
                                    detail: "JSONL Bulk Collection",
                                    range: range
                                }))
                            };
                        }

                        // Case B: db.collection., memory.collection., bulk.collection., or after any ResultSet chain
                        const isDbCol = currentCollections.includes(token);
                        const isMemCol = currentMemoryCollections.includes(token);
                        const isBulkCol = currentBulkCollections.includes(token);
                        const chainRegex = /\b(db|memory|bulk)\.([a-zA-Z0-9_]+)\b/;

                        if (isDbCol || isMemCol || isBulkCol || chainRegex.test(lineUntilDot)) {
                            const methods = [
                                { label: 'findall', insertText: 'findall()', detail: 'Find all documents in collection', kind: monaco.languages.CompletionItemKind.Method },
                                { label: 'find', insertText: 'find(${1:u} => $2)', detail: 'Find first matching document', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'insert', insertText: 'insert($1)', detail: 'Insert a new document', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'update', insertText: 'update($1)', detail: 'Update matching documents', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'delete', insertText: 'delete()', detail: 'Delete matching documents or collection', kind: monaco.languages.CompletionItemKind.Method },
                                { label: 'index', insertText: 'index(${1:x} => x.$2)', detail: 'Create an index', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'contains', insertText: 'contains(${1:x} => x.$2)', detail: 'Case-insensitive search', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'take', insertText: 'take(${1:10})', detail: 'Limit result size', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'skip', insertText: 'skip(${1:10})', detail: 'Skip results (pagination)', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'select', insertText: 'select(${1:x} => $2)', detail: 'Project/map fields', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'toList', insertText: 'toList()', detail: 'Convert to list (alias of ToList)', kind: monaco.languages.CompletionItemKind.Method },
                                { label: 'ToList', insertText: 'ToList()', detail: 'Convert to list', kind: monaco.languages.CompletionItemKind.Method },
                                { label: 'first', insertText: 'first()', detail: 'Get first document', kind: monaco.languages.CompletionItemKind.Method },
                                { label: 'count', insertText: 'count(${1:x} => $2)', detail: 'Count documents', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'distinct', insertText: 'distinct(${1:x} => x.$2)', detail: 'Distinct values', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'foreach', insertText: 'foreach(${1:x} => $2)', detail: 'Iterate over documents', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'where', insertText: 'where(${1:x} => $2)', detail: 'Filter records', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'orderBy', insertText: 'orderBy(${1:x} => x.$2)', detail: 'Order records ascending', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'orderByDesc', insertText: 'orderByDesc(${1:x} => x.$2)', detail: 'Order records descending', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'max', insertText: 'max(${1:x} => x.$2)', detail: 'Get maximum value', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                                { label: 'min', insertText: 'min(${1:x} => x.$2)', detail: 'Get minimum value', kind: monaco.languages.CompletionItemKind.Method, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet }
                            ];
                            return {
                                suggestions: methods.map(m => ({ ...m, range: range }))
                            };
                        }

                        // Case C: parameter. (e.g. u. in u => u.)
                        const resolvedCol = resolveCollectionForParam(lineUntilDot, token);
                        if (resolvedCol) {
                            const type = resolvedCol.type;
                            const name = resolvedCol.name;
                            const fields = (collectionFields[type] && collectionFields[type][name]) || [];
                            return {
                                suggestions: fields.map(f => ({
                                    label: f,
                                    kind: monaco.languages.CompletionItemKind.Field,
                                    insertText: f,
                                    detail: `Field in ${type}.${name}`,
                                    range: range
                                }))
                            };
                        }
                    }
                } else {
                    // Case D: Top-level keywords / functions
                    const globals = [
                        { label: 'db', insertText: 'db', detail: 'Database collections bridge', kind: monaco.languages.CompletionItemKind.Keyword },
                        { label: 'memory', insertText: 'memory', detail: 'TTL In-memory collections bridge', kind: monaco.languages.CompletionItemKind.Keyword },
                        { label: 'bulk', insertText: 'bulk', detail: 'JSONL Bulk collections bridge', kind: monaco.languages.CompletionItemKind.Keyword },
                        { label: 'alias', insertText: 'alias(${1:db.collection}, "${2:aliasName}")', detail: 'Alias a collection in joins', kind: monaco.languages.CompletionItemKind.Function, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                        { label: 'mysqlRead', insertText: 'mysqlRead("${1:connectionString}", "${2:SELECT * FROM table}")', detail: 'Read from MySQL/MariaDB database', kind: monaco.languages.CompletionItemKind.Function, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                        { label: 'mysqlExec', insertText: 'mysqlExec("${1:connectionString}", "${2:UPDATE table SET col = @val}", ${3:{ val: 1 }})', detail: 'Execute command on MySQL/MariaDB database', kind: monaco.languages.CompletionItemKind.Function, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                        { label: 'pgsqlRead', insertText: 'pgsqlRead("${1:connectionString}", "${2:SELECT * FROM table}")', detail: 'Read from PostgreSQL database', kind: monaco.languages.CompletionItemKind.Function, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                        { label: 'pgsqlExec', insertText: 'pgsqlExec("${1:connectionString}", "${2:UPDATE table SET col = @val}", ${3:{ val: 1 }})', detail: 'Execute command on PostgreSQL database', kind: monaco.languages.CompletionItemKind.Function, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                        { label: 'queue', insertText: 'queue("${1:db.logs.insert({msg: \'Background job\'})}")', detail: 'Queue a background script task', kind: monaco.languages.CompletionItemKind.Function, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet }
                    ];
                    return {
                        suggestions: globals.map(g => ({ ...g, range: range }))
                    };
                }

                return { suggestions: [] };
            }
        });

        editor.addAction({
            id: 'execute-query',
            label: 'Execute Query',
            keybindings: [
                monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter
            ],
            run: () => {
                executeSelectedQuery();
            }
        });

        // Live sync: update localStorage on every keystroke (debounced)
        editor.onDidChangeModelContent(() => {
            if (_suppressHistorySync || !activeHistoryId) return;
            clearTimeout(_historyDebounceTimer);
            _historyDebounceTimer = setTimeout(() => {
                updateHistoryEntry(activeHistoryId, editor.getValue());
            }, 300);
        });
    });
}

function initResizers() {
    const sidebar = document.getElementById('sidebar');
    const resizerV = document.getElementById('resizerV');
    const querySection = document.getElementById('querySection');
    const resizerH = document.getElementById('resizerH');
    const overlay = document.getElementById('resizeOverlay');

    const startResizing = (cursor) => { overlay.style.display = 'block'; overlay.style.cursor = cursor; };
    const stopResizing = () => { overlay.style.display = 'none'; resizerV.classList.remove('active'); resizerH.classList.remove('active'); };

    resizerV.addEventListener('mousedown', (e) => {
        resizerV.classList.add('active');
        startResizing('col-resize');
        const move = (e) => { if (e.clientX > 150 && e.clientX < 800) sidebar.style.width = e.clientX + 'px'; };
        const up = () => { stopResizing(); document.removeEventListener('mousemove', move); document.removeEventListener('mouseup', up); };
        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', up);
    });

    resizerH.addEventListener('mousedown', (e) => {
        resizerH.classList.add('active');
        startResizing('row-resize');
        const move = (e) => {
            const height = e.clientY - sidebar.offsetTop;
            if (height > 100 && height < window.innerHeight - 100) querySection.style.height = height + 'px';
        };
        const up = () => { stopResizing(); document.removeEventListener('mousemove', move); document.removeEventListener('mouseup', up); };
        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', up);
    });

    const resizerJson = document.getElementById('resizerJsonView');
    const jsonPanel = document.getElementById('jsonViewPanel');
    if (resizerJson && jsonPanel) {
        resizerJson.addEventListener('mousedown', (e) => {
            e.preventDefault();
            e.stopPropagation();
            resizerJson.classList.add('active');
            startResizing('col-resize');
            const startX = e.pageX;
            const startWidth = jsonPanel.offsetWidth;
            const move = (ev) => {
                const newWidth = startWidth - (ev.pageX - startX);
                if (newWidth > 150 && newWidth < window.innerWidth * 0.85) jsonPanel.style.width = newWidth + 'px';
            };
            const up = () => {
                resizerJson.classList.remove('active');
                stopResizing();
                document.removeEventListener('mousemove', move);
                document.removeEventListener('mouseup', up);
            };
            document.addEventListener('mousemove', move);
            document.addEventListener('mouseup', up);
        });
    }
}

async function loadCollections() {
    const tree = document.getElementById('collectionTree');
    try {
        // Fetch collections, views, triggers, memory, and bulk collections in parallel
        const [colRes, viewRes, trigRes, memRes, bulkRes] = await Promise.all([
            fetchWithAuth('/collections'),
            fetchWithAuth('/query', { method: 'POST', body: 'db.listViews()' }),
            fetchWithAuth('/query', { method: 'POST', body: 'db.listTriggers()' }),
            fetchWithAuth('/memory/list'),
            fetchWithAuth('/bulk/list')
        ]);

        if (!colRes.ok) throw new Error('Auth failed');
        const collections = await colRes.json();
        currentCollections = collections;

        let views = [];
        if (viewRes.ok) {
            views = await viewRes.json();
            if (!Array.isArray(views)) views = [];
        }

        let triggers = [];
        if (trigRes.ok) {
            triggers = await trigRes.json();
            if (!Array.isArray(triggers)) triggers = [];
        }

        let memoryCollections = [];
        if (memRes.ok) {
            memoryCollections = await memRes.json();
        }
        currentMemoryCollections = memoryCollections.map(mc => mc.name);

        let bulkCollections = [];
        if (bulkRes.ok) {
            bulkCollections = await bulkRes.json();
        }
        currentBulkCollections = bulkCollections.map(bc => bc.name);

        // Render Tree Atomically
        tree.innerHTML = '';

        // 1. DATA HEADER
        const dataHeader = document.createElement('div');
        dataHeader.className = 'tree-header';
        dataHeader.innerHTML = '<span style="font-weight:bold; color:var(--text-secondary); font-size:0.8rem; margin: 10px 0 5px 5px; display:block">DATABASE COLLECTIONS</span>';
        tree.appendChild(dataHeader);

        const dbCollections = collections.filter(name => !name.startsWith('sys'));
        const sysCollections = collections.filter(name => name.startsWith('sys'));

        if (dbCollections.length === 0) {
            const emptyItem = document.createElement('div');
            emptyItem.className = 'tree-item';
            emptyItem.style.opacity = '0.5';
            emptyItem.style.fontStyle = 'italic';
            emptyItem.innerHTML = '<i data-lucide="info"></i> <span>No database collections</span>';
            tree.appendChild(emptyItem);
        }

        // 2. COLLECTIONS
        dbCollections.forEach(name => {
            const item = document.createElement('div');
            item.className = 'tree-item';
            item.innerHTML = `<i data-lucide="database"></i> <span>${name}</span>`;

            item.onclick = () => {
                lastCollectionName = name;
                lastCollectionType = 'db';
                updateSysconfigBanner();
                createTab(name, `// Find, Filter, Limit and List for '${name}'
// Returns top 100 documents
db.${name}
  .findall() // No filter
  .take(100)`);
                executeSelectedQuery();
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    { label: `Default Query`, action: () => { createTab(name, `db.${name}.findall().take(5)`); executeSelectedQuery(); } },
                    { label: `Clear ${name}`, action: () => { createTab(`Clear ${name}`, `db.${name}.findall().delete()`); } },
                    { label: `Delete ${name}`, action: () => { if (confirm(`Are you sure you want to delete collection '${name}' and all its data?`)) { deleteCollection(name); } } }
                ]);
            };
            tree.appendChild(item);
        });

        // 2.0. SYSTEM COLLECTIONS
        const sysHeader = document.createElement('div');
        sysHeader.innerHTML = '<span style="font-weight:bold; color:var(--accent); font-size:0.8rem; margin: 15px 0 5px 5px; display:block">SYSTEM COLLECTIONS</span>';
        tree.appendChild(sysHeader);

        if (sysCollections.length === 0) {
            const emptyItem = document.createElement('div');
            emptyItem.className = 'tree-item';
            emptyItem.style.opacity = '0.5';
            emptyItem.style.fontStyle = 'italic';
            emptyItem.innerHTML = '<i data-lucide="info"></i> <span>No system collections</span>';
            tree.appendChild(emptyItem);
        }

        sysCollections.forEach(name => {
            const item = document.createElement('div');
            item.className = 'tree-item';
            item.style.color = 'var(--accent)';
            item.innerHTML = `<i data-lucide="shield"></i> <span>${name}</span>`;

            item.onclick = () => {
                lastCollectionName = name;
                lastCollectionType = 'db';
                updateSysconfigBanner();
                createTab(name, `// Find, Filter, Limit and List for '${name}'
// Returns top 100 documents
db.${name}
  .findall() // No filter
  .take(100)`);
                executeSelectedQuery();
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                const menuActions = [
                    { label: `Default Query`, action: () => { createTab(name, `db.${name}.findall().take(5)`); executeSelectedQuery(); } }
                ];
                if (name !== 'syslogs' && name !== 'sysconfig') {
                    menuActions.push({ label: `Clear ${name}`, action: () => { createTab(`Clear ${name}`, `db.${name}.findall().delete()`); } });
                    menuActions.push({ label: `Delete ${name}`, action: () => { if (confirm(`Are you sure you want to delete collection '${name}' and all its data?`)) { deleteCollection(name); } } });
                }
                showContextMenu(e, menuActions);
            };
            tree.appendChild(item);
        });

        // 2.1 MEMORY HEADER
        const memHeader = document.createElement('div');
        memHeader.innerHTML = '<span style="font-weight:bold; color:#a855f7; font-size:0.8rem; margin: 15px 0 5px 5px; display:block">MEMORY COLLECTIONS (TTL)</span>';
        tree.appendChild(memHeader);

        if (memoryCollections.length === 0) {
            const emptyItem = document.createElement('div');
            emptyItem.className = 'tree-item';
            emptyItem.style.opacity = '0.5';
            emptyItem.style.fontStyle = 'italic';
            emptyItem.innerHTML = '<i data-lucide="info"></i> <span>No active memory tables</span>';
            tree.appendChild(emptyItem);
        }

        memoryCollections.forEach(mc => {
            const item = document.createElement('div');
            item.className = 'tree-item';
            item.style.color = '#c084fc';
            item.innerHTML = `<i data-lucide="cpu"></i> <span>${mc.name} (${mc.count})</span>`;
            item.onclick = () => {
                setEditorValue(`// Query Temporary Memory Store
memory.${mc.name}.findall().take(10).toList()`);
                executeSelectedQuery();
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    { label: `Default Query`, action: () => { createTab(mc.name, `memory.${mc.name}.findall()`); executeSelectedQuery(); } },
                    { label: `Clear ${mc.name}`, action: () => { createTab(`Clear ${mc.name}`, `memory.${mc.name}.findall().delete()`); } }
                ]);
            };
            tree.appendChild(item);
        });

        // 2.2 BULK HEADER
        const bulkHeader = document.createElement('div');
        bulkHeader.innerHTML = '<span style="font-weight:bold; color:#10b981; font-size:0.8rem; margin: 15px 0 5px 5px; display:block">BULK COLLECTIONS (JSONL)</span>';
        tree.appendChild(bulkHeader);

        if (bulkCollections.length === 0) {
            const emptyItem = document.createElement('div');
            emptyItem.className = 'tree-item';
            emptyItem.style.opacity = '0.5';
            emptyItem.style.fontStyle = 'italic';
            emptyItem.innerHTML = '<i data-lucide="info"></i> <span>No bulk tables</span>';
            tree.appendChild(emptyItem);
        }

        bulkCollections.forEach(bc => {
            const item = document.createElement('div');
            item.className = 'tree-item';
            item.style.color = '#34d399';
            item.innerHTML = `<i data-lucide="archive"></i> <span>${bc.name} (${bc.recordCount})</span>`;
            item.onclick = () => {
                setEditorValue(`// Query Bulk (JSONL) collection
bulk.${bc.name}.findall().take(10).toList()`);
                executeSelectedQuery();
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    { label: `Default Query`, action: () => { createTab(bc.name, `bulk.${bc.name}.findall()`); executeSelectedQuery(); } },
                    { label: `Reload ${bc.name}`, action: () => { createTab(`Reload ${bc.name}`, `bulk.reload("${bc.name}")`); executeSelectedQuery(); } }
                ]);
            };
            tree.appendChild(item);
        });

        // 3. VIEWS HEADER
        const vHeader = document.createElement('div');
        vHeader.innerHTML = '<span style="font-weight:bold; color:var(--text-secondary); font-size:0.8rem; margin: 15px 0 5px 5px; display:block">VIEWS</span>';
        tree.appendChild(vHeader);

        // 4. ADD VIEW BUTTON
        const btnAddView = document.createElement('div');
        btnAddView.className = 'tree-item';
        btnAddView.style.opacity = '0.7';
        btnAddView.innerHTML = '<i data-lucide="plus-circle"></i> <span>New View</span>';
        btnAddView.onclick = () => {
            createTab("New View", `// Create/Update View with Projection, Filtering, and Parameters
// @access private
db.saveView("ActiveUsers", \`
    // Example: Find active users older than 'minAge' param
    var minAge = parameters.minAge || 18;
    var limit = parameters.limit || 50;

    // Use a Vault secret if needed (e.g. for external API enrichment)
    // var apiKey = $API_KEY; 

    return db.sysusers
        .findall(u => u.active == true && u.age >= minAge)
        .select(u => ({ 
            id: u._id, 
            fullName: u.firstName + " " + u.lastName, 
            role: u.role 
        }))
        .take(limit);
\`);`);
        };
        tree.appendChild(btnAddView);

        // 5. VIEWS LIST
        views.forEach(vName => {
            const item = document.createElement('div');
            item.className = 'tree-item';
            item.innerHTML = `<i data-lucide="file-code"></i> <span>${vName}</span>`;
            item.onclick = async () => {
                // Fetch view code to detect parameters
                try {
                    const res = await fetchWithAuth('/query', { method: 'POST', body: `db.getView("${vName}")` });
                    if (res.ok) {
                        const code = await res.json();
                        const params = extractViewParams(code);
                        if (Object.keys(params).length > 0) {
                            createTab(vName, `db.view("${vName}", ${JSON.stringify(params, null, 2)})`);
                        } else {
                            createTab(vName, `db.view("${vName}")`);
                        }
                    } else {
                        createTab(vName, `db.view("${vName}")`);
                    }
                } catch {
                    createTab(vName, `db.view("${vName}")`);
                }
                executeSelectedQuery();
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    { label: 'Run View', action: () => { createTab(vName, `db.view("${vName}")`); executeSelectedQuery(); } },
                    {
                        label: 'Edit/View Code', action: async () => {
                            const res = await fetchWithAuth('/query', { method: 'POST', body: `db.getView("${vName}")` });
                            if (res.ok) {
                                const raw = await res.json();
                                const prettyCode = raw.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
                                createTab(`Edit ${vName}`, `db.saveView("${vName}", \`\n${prettyCode}\n\`);`);
                            }
                        }
                    },
                    {
                        label: 'Delete View',
                        action: async () => {
                            if (confirm(`Delete view '${vName}'?`)) {
                                const res = await fetchWithAuth(`/views/${vName}`, { method: 'DELETE' });
                                if (res.ok) loadCollections();
                                else alert('Failed to delete view');
                            }
                        }
                    }
                ]);
            };
            tree.appendChild(item);
        });

        // 6. TRIGGERS HEADER
        const tHeader = document.createElement('div');
        tHeader.innerHTML = '<span style="font-weight:bold; color:var(--text-secondary); font-size:0.8rem; margin: 15px 0 5px 5px; display:block">TRIGGERS</span>';
        tree.appendChild(tHeader);

        // 7. ADD TRIGGER BUTTON
        const btnAddTrig = document.createElement('div');
        btnAddTrig.className = 'tree-item';
        btnAddTrig.style.opacity = '0.7';
        btnAddTrig.innerHTML = '<i data-lucide="zap"></i> <span>New Trigger</span>';
        btnAddTrig.onclick = () => {
            // New signature: saveTrigger(name, target, code)
            createTab("New Trigger", `// Create Trigger - Responds to data changes
// Defines an async event listener for 'sysusers' collection
db.saveTrigger("NotifyAdminOnUserChange", "sysusers", \`
    // Event object contains: type (created/changed/deleted), collection, documentId
    console.log("Trigger [" + event.type + "] on " + event.collection + ": " + event.documentId);

    // Example logic:
    if (event.type == "created") {
        // Log to a specialized collection or call generic webhook
        // db.audit_logs.insert({ action: "user_created", targetId: event.documentId, time: new Date() });
    }
\`);`);
        };
        tree.appendChild(btnAddTrig);

        // 8. TRIGGERS LIST
        triggers.forEach(tName => {
            const item = document.createElement('div');
            item.className = 'tree-item';
            item.innerHTML = `<i data-lucide="zap" style="color:red"></i> <span>${tName}</span>`;
            item.onclick = async () => {
                // View Code
                try {
                    const res = await fetchWithAuth('/query', { method: 'POST', body: `db.getTrigger("${tName}")` });
                    if (res.ok) {
                        const code = await res.json();
                        if (code) {
                            // Format for editing: We wrap it in saveTrigger
                            // Extract target? Regex?
                            // Simple: Just let user edit body and re-save
                            // Or show full wrapper command
                            createTab(`Edit ${tName}`, `// Update Trigger\ndb.saveTrigger("${tName}", "sysusers", ${JSON.stringify(code)});\n// Note: Update "sysusers" to your target parameter if changed.`);
                        }
                    }
                } catch (e) { console.error(e); }
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    {
                        label: 'View Code', action: async () => {
                            const res = await fetchWithAuth('/query', { method: 'POST', body: `db.getTrigger("${tName}")` });
                            if (res.ok) createTab(`View ${tName}`, await res.json());
                        }
                    },
                    {
                        label: 'Delete Trigger',
                        action: async () => {
                            if (confirm(`Delete trigger '${tName}'?`)) {
                                const res = await fetchWithAuth(`/triggers/${tName}`, { method: 'DELETE' });
                                if (res.ok) loadCollections();
                                else alert('Failed to delete trigger');
                            }
                        }
                    }
                ]);
            };
            tree.appendChild(item);
        });

        // 9. FILTER LOGIC
        const filterInput = document.getElementById('sidebarFilter');
        filterInput.oninput = () => {
            const val = filterInput.value.toLowerCase();
            const items = tree.querySelectorAll('.tree-item');
            const headers = tree.querySelectorAll('.tree-header');

            items.forEach(el => {
                const txt = el.textContent.toLowerCase();
                el.style.display = txt.includes(val) ? 'flex' : 'none';
            });

            // Optional: Hide headers if all children are hidden? 
            // That's complex because we flattened structure. 
            // Simple version: just hide items.
        };

        // Re-apply filter if value exists (e.g. after refresh)
        if (filterInput.value) filterInput.oninput();

        initIcons();
        triggerFetchCollectionFields(collections, currentMemoryCollections, currentBulkCollections);
    } catch (err) {
        if (err.message === 'Auth failed') document.getElementById('loginModal').style.display = 'flex';
    }
}

async function executeSelectedQuery() {
    const btn = document.getElementById('btnExecute');

    if (activeQueryController) {
        activeQueryController.abort();
        activeQueryController = null;
        return;
    }

    const script = editor.getValue();
    const originalText = `<i data-lucide="play"></i> Execute (Ctrl+Enter)`;

    let lastColMatchName = null;
    let lastColMatchType = null;

    const regex = /(db|memory|bulk)\.([a-zA-Z0-9_]+)\./g;
    let m;
    while ((m = regex.exec(script)) !== null) {
        lastColMatchType = m[1];
        lastColMatchName = m[2];
    }

    if (lastColMatchName) {
        lastCollectionName = lastColMatchName;
        lastCollectionType = lastColMatchType;
        if (lastColMatchType === 'db') updateSysconfigBanner();
    }

    btn.innerHTML = '<i data-lucide="square" style="fill: currentColor; width: 14px; height: 14px;"></i> Cancel Executing';
    btn.style.backgroundColor = '#ef4444'; // Red background for cancel
    if (window.lucide) lucide.createIcons();

    activeQueryController = new AbortController();

    try {
        const response = await fetchWithAuth('/query', {
            method: 'POST',
            body: script,
            signal: activeQueryController.signal
        });
        const text = await response.text();
        let data = null;
        try { data = text ? JSON.parse(text) : null; } catch (e) { data = text; }

        if (response.ok) {
            if (Array.isArray(data)) {
                queryResults = data;
            } else if (data === null || data === undefined) {
                queryResults = [];
            } else {
                queryResults = [data];
            }
            filters = {};
            renderGrid();
            loadCollections();

            // Show sysconfig banner if an update was performed on sysconfig
            if (/db\.sysconfig\b.*\.update\s*\(/.test(script)) {
                updateSysconfigBanner(true);
            }

            // Update active tab title
            if (activeTabId) {
                const tab = tabs.find(t => t.id === activeTabId);
                if (tab) {
                    const viewMatch = script.match(/db\.view\(["']([^"']+)["']/);
                    if (viewMatch) {
                        tab.title = viewMatch[1];
                    } else if (lastColMatchName) {
                        tab.title = lastColMatchName;
                    }
                    tab.queryResults = queryResults;
                    renderTabBar();
                }
            }

            // Save to history (always new entry)
            addHistoryEntry(script);
        } else {
            alert('Error: ' + (data?.detail || data?.error || data || 'Query failed'));
        }
    } catch (err) {
        if (err.name === 'AbortError') {
            console.log('Query cancelled by user');
        } else {
            alert('Server unreachable or request failed');
            console.error(err);
        }
    } finally {
        activeQueryController = null;
        btn.innerHTML = originalText;
        btn.style.backgroundColor = '';
        if (window.lucide) lucide.createIcons();
    }
}

function renderGrid() {
    const tableHead = document.getElementById('tableHead');
    const tableBody = document.getElementById('tableBody');
    const table = document.getElementById('resultsTable');
    const noResults = document.getElementById('noResults');
    const container = document.getElementById('gridContainer');

    // Preserve focus state if user is typing in filter
    const activeElement = document.activeElement;
    let focusedFilterCol = null;
    let selectionStart = 0;
    let selectionEnd = 0;
    if (activeElement && activeElement.classList.contains('filter-input')) {
        focusedFilterCol = activeElement.getAttribute('data-filter-col');
        selectionStart = activeElement.selectionStart;
        selectionEnd = activeElement.selectionEnd;
    }

    // Clear previous view
    tableHead.innerHTML = '';
    tableBody.innerHTML = '';
    noResults.style.display = 'none';

    // Clean up any existing iframe if present
    const existingFrame = container.querySelector('iframe');
    if (existingFrame) existingFrame.remove();
    table.style.display = 'table';

    if (!queryResults || queryResults.length === 0) {
        noResults.style.display = 'block';
        return;
    }

    // Check if data should be rendered as HTML in iframe
    // Conditions for iframe rendering:
    // 1. Single string result (e.g. "<h1>test</h1>")
    // 2. Single primitive (number, boolean)
    // 3. All items are not objects (non-table data)

    const isTableData = queryResults.length > 0 &&
        queryResults.every(item => item !== null && typeof item === 'object' && !Array.isArray(item));

    if (!isTableData) {
        // Render in iframe - convert to string representation
        table.style.display = 'none';
        const frame = document.createElement('iframe');
        frame.style.width = '100%';
        frame.style.height = '100%';
        frame.style.border = 'none';
        frame.style.background = 'white';
        container.appendChild(frame);

        let content = '';
        if (queryResults.length === 1) {
            content = String(queryResults[0]);
        } else {
            // Multiple non-object items
            content = queryResults.map(item => String(item)).join('<br>');
        }

        const doc = frame.contentWindow.document;
        doc.open();
        doc.write(content);
        doc.close();
        return;
    }

    // Table data - get all unique keys from objects
    const keys = [];
    const keySet = new Set();
    for (const obj of queryResults) {
        if (obj && typeof obj === 'object') {
            for (const key of Object.keys(obj)) {
                if (!keySet.has(key)) {
                    keySet.add(key);
                    keys.push(key);
                }
            }
        }
    }

    if (keys.length === 0) {
        noResults.style.display = 'block';
        noResults.textContent = 'No displayable columns found';
        return;
    }

    let displayData = [...queryResults];
    if (sortCol) {
        displayData.sort((a, b) => {
            const valA = a[sortCol]; const valB = b[sortCol];
            if (valA < valB) return -1 * sortDir; if (valA > valB) return 1 * sortDir;
            return 0;
        });
    } else {
        const currentScript = editor ? editor.getValue().toLowerCase() : '';
        const hasOrderBy = currentScript.includes('.orderby');

        if (!hasOrderBy) {
            // Default sort: UUIDv7 latest first, ignore others but keep v7 on top
            displayData.sort((a, b) => {
                const idA = a._id !== undefined && a._id !== null ? String(a._id) : '';
                const idB = b._id !== undefined && b._id !== null ? String(b._id) : '';

                const isV7A = idA.length === 36 && idA.charAt(14) === '7' && idA.charAt(8) === '-';
                const isV7B = idB.length === 36 && idB.charAt(14) === '7' && idB.charAt(8) === '-';

                if (isV7A && !isV7B) return -1;
                if (!isV7A && isV7B) return 1;

                if (isV7A && isV7B) {
                    if (idA < idB) return 1;
                    if (idA > idB) return -1;
                    return 0;
                }

                return 0;
            });
        }
    }

    Object.keys(filters).forEach(key => {
        const query = filters[key].toLowerCase();
        if (query) displayData = displayData.filter(row => String(row[key] || '').toLowerCase().includes(query));
    });

    currentDisplayData = displayData;
    currentGridKeys = keys;

    let hRow = `<tr>
        <th style="width: 50px">#</th>
        ${keys.map((k, i) => `
            <th style="position: relative; min-width: 100px;">
                <div class="sort-header" onclick="setSort('${k}')">
                    ${k} <i data-lucide="${sortCol === k ? (sortDir === 1 ? 'chevron-up' : 'chevron-down') : 'chevrons-up-down'}" size="12"></i>
                </div>
                <input class="filter-input" data-filter-col="${k}" placeholder="Filter..." value="${escapeHtml(filters[k] || '')}" oninput="setFilter('${k}', this.value)">
                <div class="col-resizer" onmousedown="initColResize(event, this)"></div>
            </th>`).join('')}
    </tr>`;

    tableHead.innerHTML = hRow;

    tableBody.innerHTML = displayData.map((row, idx) => `
        <tr>
            <td>
                <div class="row-action-btn" onclick="handleRowAction(event)" data-row-id="${row._id !== undefined ? escapeHtml(JSON.stringify(row._id)) : ''}">${idx + 1}</div>
            </td>
            ${keys.map(k => {
                const valStr = row[k] === null || row[k] === undefined ? '' : (typeof row[k] === 'object' ? JSON.stringify(row[k]) : String(row[k]));
                const encodedVal = encodeURIComponent(valStr).replace(/'/g, "%27");
                return `<td ondblclick="makeEditable(this, ${idx}, '${k.replace(/'/g, "\\'")}')" onclick="copyToClipboard(decodeURIComponent('${encodedVal}'), event)" title="Click to copy, Double click to edit" style="cursor: pointer;">${formatValue(row[k], k, idx)}</td>`;
            }).join('')}
        </tr>
    `).join('');
    initIcons();

    // Restore focus
    if (focusedFilterCol) {
        const inputToFocus = container.querySelector(`input[data-filter-col="${focusedFilterCol}"]`);
        if (inputToFocus) {
            inputToFocus.focus();
            try {
                inputToFocus.setSelectionRange(selectionStart, selectionEnd);
            } catch (e) { } // Ignore if types don't support selection
        }
    }
}

window.makeEditable = function(td, rowIdx, key) {
    if (td.querySelector('input, select')) return; // Already editing
    if (key === '_id' || key.toLowerCase().endsWith('id')) return; // Cannot modify ID fields
    if (lastCollectionType === 'memory' || lastCollectionType === 'bulk' || lastCollectionName === 'syslogs' || lastCollectionName === 'sysconfig') return; // Enforce collection-types-rule

    const row = currentDisplayData[rowIdx];
    if (!row) return;
    const val = row[key];

    const id = row._id;
    if (id === undefined || id === null || !lastCollectionName) return; 

    const prefix = 'db';
    
    let inputHtml = '';
    if (typeof val === 'boolean') {
        inputHtml = `<select class="inline-edit-input" data-key="${escapeHtml(key)}" style="width: 100%; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--accent); padding: 4px; outline: none; border-radius: 4px;">
            <option value="true" ${val ? 'selected' : ''}>true</option>
            <option value="false" ${!val ? 'selected' : ''}>false</option>
        </select>`;
    } else if (typeof val === 'number') {
        inputHtml = `<input type="number" class="inline-edit-input" data-key="${escapeHtml(key)}" value="${val}" style="width: 100%; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--accent); padding: 4px; outline: none; border-radius: 4px;">`;
    } else if (typeof val === 'object' && val !== null) {
        const strVal = JSON.stringify(val);
        inputHtml = `<input type="text" class="inline-edit-input" data-key="${escapeHtml(key)}" data-type="json" value="${escapeHtml(strVal)}" style="width: 100%; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--accent); padding: 4px; outline: none; border-radius: 4px;" title="Edit as JSON">`;
    } else if (typeof val === 'string' && val.length >= 19 && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(val)) {
        const dateStr = val.substring(0, 19);
        const originalTail = val.substring(19);
        inputHtml = `<input type="datetime-local" step="1" class="inline-edit-input" data-key="${escapeHtml(key)}" data-type="datetime" data-tail="${escapeHtml(originalTail)}" value="${escapeHtml(dateStr)}" style="width: 100%; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--accent); padding: 4px; outline: none; border-radius: 4px;">`;
    } else {
        const safeVal = (val === null || val === undefined) ? '' : escapeHtml(String(val));
        inputHtml = `<input type="text" class="inline-edit-input" data-key="${escapeHtml(key)}" value="${safeVal}" style="width: 100%; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--accent); padding: 4px; outline: none; border-radius: 4px;">`;
    }

    td.innerHTML = inputHtml;
    const input = td.querySelector('input, select');
    if (!input) return;
    input.focus();

    const updateQueryInEditor = () => {
        let newVal;
        if (input.tagName === 'SELECT') {
            newVal = input.value === 'true';
        } else if (input.type === 'number') {
            newVal = input.value === '' ? null : parseFloat(input.value);
            if (newVal !== null && isNaN(newVal)) newVal = 0;
        } else if (input.getAttribute('data-type') === 'json') {
            try {
                newVal = input.value === '' ? null : JSON.parse(input.value);
            } catch (e) {
                newVal = input.value;
            }
        } else if (input.getAttribute('data-type') === 'datetime') {
            if (input.value === '') {
                newVal = null;
            } else {
                const tail = input.getAttribute('data-tail') || '';
                let timeStr = input.value;
                if (timeStr.length === 16) timeStr += ':00';
                newVal = timeStr + tail;
            }
        } else {
            newVal = input.value;
        }

        const updateObj = {};
        updateObj[key] = newVal;
        
        const safeId = JSON.stringify(id);
        const queryStr = `${prefix}.${lastCollectionName}.update(x => x._id == ${safeId}, ${JSON.stringify(updateObj, null, 2)});\n${prefix}.${lastCollectionName}.findall();`;
        
        if (window.editor) {
            setEditorValue(queryStr);
        }
        return newVal;
    };

    const finishEdit = () => {
        const newVal = updateQueryInEditor();
        td.innerHTML = formatValue(newVal, key, rowIdx);
    };

    input.addEventListener('input', updateQueryInEditor);
    input.addEventListener('change', updateQueryInEditor);
    input.addEventListener('blur', finishEdit);
    input.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
            e.preventDefault();
            finishEdit();
            executeSelectedQuery();
        } else if (e.key === 'Enter') {
            e.preventDefault();
            finishEdit();
        } else if (e.key === 'Escape') {
            e.preventDefault();
            td.innerHTML = formatValue(val, key, rowIdx);
        }
    });
};

function formatValue(v, key, rowIdx = -1) {
    if (v === null || v === undefined) return '';
    if (key === '_id' || key.toLowerCase().endsWith('id')) return `<span class="badge badge-id">${escapeHtml(String(v))}</span>`;
    if (typeof v === 'boolean') return `<span class="badge badge-bool">${v}</span>`;
    if (typeof v === 'number') return `<span class="badge badge-number">${v}</span>`;
    if (typeof v === 'object') {
        const str = JSON.stringify(v);
        const shortStr = str.length > 40 ? str.substring(0, 40) + '...' : str;
        return `<div class="badge badge-json" onclick="openNestedJsonModal(${rowIdx}, '${key}')" style="cursor:pointer; border: 1px solid var(--accent); background: rgba(99, 102, 241, 0.1); color: var(--text-primary); text-transform:none; font-family:monospace; display:inline-flex; align-items:center; gap:4px;" title="Click to view details"><i data-lucide="braces" style="width:12px;height:12px;"></i>${escapeHtml(shortStr)}</div>`;
    }
    return `<span class="badge badge-string">${escapeHtml(String(v))}</span>`;
}

// --- Nested JSON Modal Logic ---
let nestedJsonCards = [];

window.openNestedJsonModal = function (rowIdx, key) {
    if (rowIdx < 0 || rowIdx >= currentDisplayData.length) return;
    const obj = currentDisplayData[rowIdx][key];
    if (!obj || typeof obj !== 'object') return;

    nestedJsonCards = [{ title: key, obj: obj }];
    renderNestedJsonModal();
    document.getElementById('nestedJsonModal').style.display = 'flex';
};

window.closeNestedJsonModal = function () {
    document.getElementById('nestedJsonModal').style.display = 'none';
};

window.pushNestedJsonCard = function (parentIdx, key) {
    nestedJsonCards = nestedJsonCards.slice(0, parentIdx + 1);
    const parentObj = nestedJsonCards[parentIdx].obj;
    const childObj = parentObj[key];
    nestedJsonCards.push({ title: Array.isArray(parentObj) ? `[${key}]` : key, obj: childObj });
    renderNestedJsonModal();
};

function renderNestedJsonModal() {
    const container = document.getElementById('nestedJsonCardsContainer');
    container.innerHTML = nestedJsonCards.map((card, idx) => {
        let itemsHtml = '';
        if (typeof card.obj === 'object' && card.obj !== null) {
            const isArray = Array.isArray(card.obj);
            Object.keys(card.obj).forEach(k => {
                const val = card.obj[k];
                const isObj = typeof val === 'object' && val !== null;
                const valStr = isObj ? (Array.isArray(val) ? `[Array(${val.length})]` : '{Object}') : escapeHtml(String(val));
                const encodedVal = !isObj ? encodeURIComponent(String(val)).replace(/'/g, "%27") : '';
                itemsHtml += `<div class="nested-json-item" ${isObj ? `onclick="pushNestedJsonCard(${idx}, '${escapeHtml(k).replace(/'/g, "\\'")}')"` : `onclick="copyToClipboard(decodeURIComponent('${encodedVal}'))"`} style="padding: 10px 12px; border-bottom: 1px solid var(--border); display: flex; justify-content: space-between; align-items: flex-start; transition: background 0.2s; cursor:pointer;" onmouseover="this.style.background='rgba(255,255,255,0.05)'" onmouseout="this.style.background='transparent'" title="${!isObj ? 'Click to copy' : 'Click to view details'}">
                    <span style="font-weight: 500; color: var(--accent); margin-right: 12px; font-size: 0.85rem; flex-shrink: 0; padding-top: 2px;">${escapeHtml(k)}</span>
                    <span style="color: ${isObj ? 'var(--text-secondary)' : 'var(--text-primary)'}; word-break: break-word; font-size: 0.85rem; text-align: left; flex: 1;">${valStr}</span>
                    ${isObj ? '<i data-lucide="chevron-right" style="width:14px;height:14px;color:var(--text-secondary);margin-left:8px;flex-shrink:0;margin-top:2px;"></i>' : ''}
                </div>`;
            });
        }
        return `
            <div class="nested-json-card" style="min-width: 300px; width: 300px; height: 100%; border-right: 1px solid var(--border); display: flex; flex-direction: column;">
                <div class="card-header" style="padding: 12px 16px; background: rgba(0,0,0,0.3); border-bottom: 1px solid var(--border); font-weight: 600; font-size:0.9rem; position: sticky; top: 0; color: var(--text-primary); display:flex; align-items:center; gap:8px;">
                    ${escapeHtml(card.title)}
                </div>
                <div class="card-body" style="flex: 1; overflow-y: auto;">
                    ${itemsHtml || '<div style="padding:1rem;color:var(--text-secondary);text-align:center;">Empty</div>'}
                </div>
            </div>
        `;
    }).join('');
    if (window.lucide) lucide.createIcons();
    // Scroll right smoothly without blocking thread
    setTimeout(() => {
        container.scrollTo({ left: container.scrollWidth, behavior: 'smooth' });
    }, 10);
}

function setSort(col) {
    if (sortCol === col) sortDir *= -1; else { sortCol = col; sortDir = 1; }
    renderGrid();
}

function setFilter(col, val) { filters[col] = val; renderGrid(); }

function showContextMenu(e, items) {
    const menu = document.getElementById('contextMenu');
    menu.style.display = 'block'; menu.style.left = e.pageX + 'px'; menu.style.top = e.pageY + 'px';
    menu.innerHTML = items.map(i => `<div class="context-menu-item">${i.label}</div>`).join('');
    const elements = menu.querySelectorAll('.context-menu-item');
    elements.forEach((el, idx) => {
        el.onclick = () => { items[idx].action(); menu.style.display = 'none'; };
    });
    const closeHandler = () => { menu.style.display = 'none'; document.removeEventListener('click', closeHandler); };
    setTimeout(() => document.addEventListener('click', closeHandler), 10);
}

function handleRowAction(e) {
    e.preventDefault(); e.stopPropagation();
    const btn = e.target.closest('.row-action-btn');
    if (!btn) return;
    const tr = btn.closest('tr');
    if (!tr) return;

    const rawId = btn.getAttribute('data-row-id');
    if (!rawId) return;
    const id = JSON.parse(rawId);
    const row = currentDisplayData.find(r => r._id === id);
    if (!row) return;

    // JsonView is the first action in the context menu
    const actions = [
        { label: 'JsonView', action: () => openJsonView(row) }
    ];
    
    if (lastCollectionName && id !== undefined) {
        let prefix = 'db';
        
        // Enforce collection-types-rule.md: memory, bulk, syslogs & sysconfig collections cannot be updated/deleted directly
        if (lastCollectionType !== 'memory' && lastCollectionType !== 'bulk' && lastCollectionName !== 'syslogs' && lastCollectionName !== 'sysconfig') {
            actions.push({
                label: 'Edit Record', action: () => {
                    enableInlineEdit(tr, row);
                }
            });

            actions.push({
                label: 'Delete Record', action: () => {
                    const safeId = JSON.stringify(id);
                    setEditorValue(`${prefix}.${lastCollectionName}.findall(x => x._id == ${safeId}).delete();\n${prefix}.${lastCollectionName}.findall();`);
                }
            });
        }
    }

    // Always allow exporting the record
    actions.push({ label: 'Export specific record (JSON)', action: () => exportData([row], 'json') });

    showContextMenu(e, actions);
}

window.openJsonView = function (rowData) {
    if (!rowData) return;
    const data = rowData;
    const formattedJson = JSON.stringify(data, null, 2);

    document.getElementById('resizerJsonView').style.display = 'block';
    document.getElementById('jsonViewPanel').style.display = 'flex';

    if (!jsonViewEditor) {
        require(['vs/editor/editor.main'], function () {
            jsonViewEditor = monaco.editor.create(document.getElementById('jsonViewEditorContainer'), {
                value: formattedJson,
                language: 'json',
                theme: 'vs-dark',
                readOnly: true,
                automaticLayout: true,
                minimap: { enabled: false },
                fontSize: 14,
                padding: { top: 16 },
                scrollBeyondLastLine: false,
                wordWrap: 'on'
            });
        });
    } else {
        jsonViewEditor.setValue(formattedJson);
    }
};

window.closeJsonView = function () {
    document.getElementById('resizerJsonView').style.display = 'none';
    document.getElementById('jsonViewPanel').style.display = 'none';
};

function exportData(data, format) {
    if (!data || data.length === 0) return;
    let content = '';
    if (format === 'json') {
        content = JSON.stringify(data, null, 2);
    } else if (format === 'csv') {
        const keys = [...new Set(data.flatMap(obj => Object.keys(obj || {})))];
        content = keys.join(',') + '\n' + data.map(r => keys.map(k => {
            let v = r[k] === null || r[k] === undefined ? '' : r[k];
            if (typeof v === 'object') v = JSON.stringify(v);
            v = String(v).replace(/"/g, '""');
            return `"${v}"`;
        }).join(',')).join('\n');
    }
    const blob = new Blob([content], { type: format === 'json' ? 'application/json' : 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a'); a.href = url; a.download = `export_${new Date().getTime()}.${format} `; a.click();
}

async function deleteCollection(name) {
    try {
        const res = await fetchWithAuth(`/collections/${name}`, { method: 'DELETE' });
        if (res.ok) {
            loadCollections();
        } else {
            const data = await res.json();
            alert('Delete failed: ' + (data.error || 'Unknown error'));
        }
    } catch (err) {
        alert('Delete failed: ' + err.message);
    }
}

async function fetchWithAuth(url, options = {}) {
    const auth = localStorage.getItem('AxarDB_auth');
    if (!auth) {
        document.getElementById('loginModal').style.display = 'flex';
        throw new Error('Auth failed');
    }
    const res = await fetch(url, { ...options, headers: { 'Authorization': `Basic ${auth}`, ...options.headers } });
    if (res.status === 401) {
        localStorage.removeItem('AxarDB_auth');
        document.getElementById('loginModal').style.display = 'flex';
        throw new Error('Auth failed');
    }
    return res;
}

window.copyToClipboard = function(text, event) {
    if (event) {
        if (event.target.closest('input, select, .badge-json')) return;
    }
    
    const showToast = () => {
        const toast = document.createElement('div');
        const shortText = text.length > 30 ? text.substring(0, 30) + '...' : text;
        toast.textContent = 'Panoya kopyalanan: ' + shortText;
        toast.style.position = 'fixed';
        toast.style.bottom = '20px';
        toast.style.right = '20px';
        toast.style.background = '#10b981'; // Green toast
        toast.style.color = 'white';
        toast.style.padding = '10px 20px';
        toast.style.borderRadius = '6px';
        toast.style.zIndex = '10000';
        toast.style.boxShadow = '0 4px 12px rgba(0,0,0,0.15)';
        toast.style.transition = 'opacity 0.3s, transform 0.3s';
        toast.style.transform = 'translateY(10px)';
        toast.style.opacity = '0';
        toast.style.fontWeight = '500';
        document.body.appendChild(toast);
        
        void toast.offsetWidth; // trigger reflow
        toast.style.transform = 'translateY(0)';
        toast.style.opacity = '1';
        
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateY(10px)';
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    };

    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text).then(showToast).catch(err => console.error('Copy failed', err));
    } else {
        // Fallback for non-HTTPS or unsupported browsers
        let textArea = document.createElement("textarea");
        textArea.value = text;
        textArea.style.position = "fixed";
        textArea.style.left = "-999999px";
        textArea.style.top = "-999999px";
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        try {
            document.execCommand('copy');
            showToast();
        } catch (err) {
            console.error('Fallback copy failed', err);
        }
        textArea.remove();
    }
}

function initColResize(e, resizer) {
    e.preventDefault();
    e.stopPropagation();
    const th = resizer.parentElement;
    const startX = e.pageX;
    const startWidth = th.offsetWidth;

    resizer.classList.add('active');
    document.body.style.cursor = 'col-resize';

    const onMove = (moveEvent) => {
        const width = startWidth + (moveEvent.pageX - startX);
        if (width > 50) {
            th.style.width = width + 'px';
            th.style.minWidth = width + 'px'; // Enforce min-width
            th.style.maxWidth = width + 'px'; // Enforce max-width for fixed layout
        }
    };

    const onUp = () => {
        document.removeEventListener('mousemove', onMove);
        document.removeEventListener('mouseup', onUp);
        resizer.classList.remove('active');
        document.body.style.cursor = 'default';
    };

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
}


function initIcons() { if (window.lucide) lucide.createIcons(); }

// Programmatic editor.setValue wrapper — resets activeHistoryId
function setEditorValue(value) {
    _suppressHistorySync = true;
    activeHistoryId = null;
    editor.setValue(value);
    _suppressHistorySync = false;
}

function initButtons() {
    document.getElementById('btnExecute').onclick = executeSelectedQuery;
    document.getElementById('btnAddDb').onclick = () => {
        const name = prompt("Enter new collection name:");
        if (name) {
            createTab(`New DB: ${name}`, `// Create Collection '${name}' by inserting a document
    // Collections are created automatically on first write
    db.${name}.insert({
        created: new Date(),
        desc: "Initial document"
    });
    // List collections to confirm
    showCollections(); `);
        }
    };

    // Export buttons
    document.getElementById('btnExportJson').onclick = () => exportData(queryResults, 'json');
    document.getElementById('btnExportCsv').onclick = () => exportData(queryResults, 'csv');

    // History
    document.getElementById('btnHistory').onclick = openHistoryModal;
    document.getElementById('btnCloseHistory').onclick = closeHistoryModal;

    // Tab management
    document.getElementById('btnAddTab').onclick = () => createTab();
    
    const btnCloseOther = document.getElementById('btnCloseOtherTabs');
    if (btnCloseOther) {
        btnCloseOther.onclick = () => closeOtherTabs();
    }
}

// --- View Parameter Extraction ---
function extractViewParams(code) {
    const params = {};

    // 1. Match @param patterns (e.g. @email, @password)
    const atRegex = /@([a-zA-Z_][a-zA-Z0-9_]*)/g;
    let match;
    while ((match = atRegex.exec(code)) !== null) {
        const paramName = match[1];
        // Skip known directives like @access
        if (paramName === 'access') continue;
        if (params[paramName] === undefined) {
            params[paramName] = '';
        }
    }

    // 2. Match parameters.xxx patterns
    const paramRegex = /parameters\.([a-zA-Z_][a-zA-Z0-9_]*)/g;
    while ((match = paramRegex.exec(code)) !== null) {
        const paramName = match[1];
        if (params[paramName] !== undefined) continue;
        // Try to find default: parameters.xxx || defaultValue
        const defaultRegex = new RegExp(`parameters\\.${paramName} \\s *\\|\\|\\s * (.+?) \\s *; `);
        const defaultMatch = code.match(defaultRegex);
        if (defaultMatch) {
            const raw = defaultMatch[1].trim();
            if (!isNaN(Number(raw))) {
                params[paramName] = Number(raw);
            } else if (raw.startsWith('"') || raw.startsWith("'")) {
                params[paramName] = raw.replace(/^["']|["']$/g, '');
            } else if (raw === 'true' || raw === 'false') {
                params[paramName] = raw === 'true';
            } else {
                params[paramName] = raw;
            }
        } else {
            params[paramName] = '';
        }
    }

    return params;
}

// --- Query History (localStorage) ---

function getHistory() {
    try {
        return JSON.parse(localStorage.getItem('AxarDB_queryHistory') || '[]');
    } catch { return []; }
}

function saveHistory(entries) {
    localStorage.setItem('AxarDB_queryHistory', JSON.stringify(entries));
}

function addHistoryEntry(script) {
    const entries = getHistory();
    const id = 'q_' + Date.now();
    entries.unshift({ id, script, timestamp: Date.now() });
    saveHistory(entries);
    activeHistoryId = id;
}

function updateHistoryEntry(id, script) {
    const entries = getHistory();
    const entry = entries.find(e => e.id === id);
    if (entry) {
        entry.script = script;
        saveHistory(entries);
    }
}

function deleteHistoryEntry(id) {
    let entries = getHistory();
    entries = entries.filter(e => e.id !== id);
    saveHistory(entries);
    if (activeHistoryId === id) activeHistoryId = null;
    renderHistoryList();
}

function loadHistoryItem(id) {
    const entries = getHistory();
    const entry = entries.find(e => e.id === id);
    if (!entry) return;
    _suppressHistorySync = true;
    activeHistoryId = id;
    editor.setValue(entry.script);
    _suppressHistorySync = false;
    closeHistoryModal();
}

function openHistoryModal() {
    document.getElementById('historyModal').style.display = 'flex';
    renderHistoryList();
    initIcons();
}

function closeHistoryModal() {
    document.getElementById('historyModal').style.display = 'none';
}

function renderHistoryList() {
    const list = document.getElementById('historyList');
    const entries = getHistory();
    const filterEl = document.getElementById('historyFilter');
    const filterVal = filterEl ? filterEl.value.toLowerCase() : '';

    const filtered = filterVal
        ? entries.filter(e => e.script.toLowerCase().includes(filterVal))
        : entries;

    if (entries.length === 0) {
        list.innerHTML = `
            <div class="empty-state">
                <i data-lucide="history" style="width:48px;height:48px;margin-bottom:1rem;opacity:0.2"></i>
                <p>No query history yet</p>
            </div>`;
        initIcons();
        return;
    }

    if (filtered.length === 0) {
        list.innerHTML = `
            <div class="empty-state">
                <i data-lucide="search-x" style="width:48px;height:48px;margin-bottom:1rem;opacity:0.2"></i>
                <p>No matching queries found</p>
            </div>`;
        initIcons();
        return;
    }

    list.innerHTML = filtered.map(entry => {
        const preview = entry.script
            .replace(/\/\/.*$/gm, '')
            .replace(/\s+/g, ' ')
            .trim()
            .substring(0, 70) || '(empty)';
        const date = new Date(entry.timestamp);
        const timeStr = date.toLocaleString();
        const isActive = entry.id === activeHistoryId;

        return `<div class="history-item ${isActive ? 'active' : ''}" data-id="${entry.id}">
            <div class="history-item-icon">
                <i data-lucide="terminal" style="width:16px;height:16px"></i>
            </div>
            <div class="history-item-content" onclick="loadHistoryItem('${entry.id}')">
                <div class="preview">${escapeHtml(preview)}</div>
                <div class="timestamp">${timeStr}</div>
            </div>
            <div class="history-item-actions">
                <button class="btn-delete" onclick="event.stopPropagation(); deleteHistoryEntry('${entry.id}')" title="Delete">
                    <i data-lucide="trash-2" style="width:14px;height:14px"></i>
                </button>
            </div>
        </div>`;
    }).join('');

    initIcons();
}

function escapeHtml(str) {
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// --- Inline Edit Logic ---

function enableInlineEdit(tr, rowData) {
    if (lastCollectionType === 'memory' || lastCollectionType === 'bulk' || lastCollectionName === 'syslogs' || lastCollectionName === 'sysconfig') return;
    if (!tr || !rowData) return;

    // Store the _id on the tr so updateInlineEdit can find the row by identity
    tr.setAttribute('data-edit-id', JSON.stringify(rowData._id));

    currentGridKeys.forEach((key, colIdx) => {
        // Skip ID fields
        if (key === '_id' || key.toLowerCase().endsWith('id')) {
            return;
        }

        // +1 because td index 0 is the row action button (#)
        const td = tr.children[colIdx + 1];
        if (!td) return;
        const val = rowData[key];

        let inputHtml = '';
        if (typeof val === 'boolean') {
            inputHtml = `<select class="inline-edit-input" data-key="${escapeHtml(key)}" onchange="updateInlineEdit(this.closest('tr'))" style="width: 100%; padding: 4px; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--border); border-radius: 4px;">
                <option value="true" ${val ? 'selected' : ''}>true</option>
                <option value="false" ${!val ? 'selected' : ''}>false</option>
            </select>`;
        } else if (typeof val === 'number') {
            inputHtml = `<input type="number" class="inline-edit-input" data-key="${escapeHtml(key)}" value="${val}" oninput="updateInlineEdit(this.closest('tr'))" style="width: 100%; padding: 4px; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--border); border-radius: 4px;">`;
        } else if (typeof val === 'object' && val !== null) {
            const strVal = JSON.stringify(val);
            inputHtml = `<input type="text" class="inline-edit-input" data-key="${escapeHtml(key)}" data-type="json" value="${escapeHtml(strVal)}" oninput="updateInlineEdit(this.closest('tr'))" style="width: 100%; padding: 4px; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--border); border-radius: 4px;" title="Edit as JSON">`;
        } else if (typeof val === 'string' && val.length >= 19 && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(val)) {
            const dateStr = val.substring(0, 19);
            const originalTail = val.substring(19);
            inputHtml = `<input type="datetime-local" step="1" class="inline-edit-input" data-key="${escapeHtml(key)}" data-type="datetime" data-tail="${escapeHtml(originalTail)}" value="${escapeHtml(dateStr)}" oninput="updateInlineEdit(this.closest('tr'))" style="width: 100%; padding: 4px; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--border); border-radius: 4px;">`;
        } else {
            const strVal = val === null || val === undefined ? '' : String(val);
            inputHtml = `<input type="text" class="inline-edit-input" data-key="${escapeHtml(key)}" value="${escapeHtml(strVal)}" oninput="updateInlineEdit(this.closest('tr'))" style="width: 100%; padding: 4px; box-sizing: border-box; background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--border); border-radius: 4px;">`;
        }

        td.innerHTML = inputHtml;
    });

    const rowInputs = tr.querySelectorAll('.inline-edit-input');
    rowInputs.forEach(input => {
        input.addEventListener('keydown', (e) => {
            if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                e.preventDefault();
                updateInlineEdit(tr);
                executeSelectedQuery();
            }
        });
    });

    if (rowInputs.length > 0) {
        rowInputs[0].focus();
    }

    updateInlineEdit(tr);
}

function updateInlineEdit(tr) {
    if (!tr) return;

    const rawId = tr.getAttribute('data-edit-id');
    if (!rawId) return;
    const editId = JSON.parse(rawId);
    const originalRow = currentDisplayData.find(r => r._id === editId);
    if (!originalRow) return;

    const updateObj = { ...originalRow };
    delete updateObj._id; // Ensure we don't try to update _id

    const inputs = tr.querySelectorAll('.inline-edit-input');
    inputs.forEach(input => {
        const key = input.getAttribute('data-key');
        let val;

        if (input.tagName === 'SELECT') {
            val = input.value === 'true';
        } else if (input.type === 'number') {
            val = input.value === '' ? null : Number(input.value);
        } else if (input.getAttribute('data-type') === 'json') {
            try {
                val = input.value === '' ? null : JSON.parse(input.value);
            } catch (e) {
                val = input.value;
            }
        } else if (input.getAttribute('data-type') === 'datetime') {
            if (input.value === '') {
                val = null;
            } else {
                const tail = input.getAttribute('data-tail') || '';
                let timeStr = input.value;
                if (timeStr.length === 16) timeStr += ':00';
                val = timeStr + tail;
            }
        } else {
            val = input.value;
        }

        if (val !== undefined) {
            updateObj[key] = val;
        }
    });

    const prefix = 'db';
    const safeId = JSON.stringify(originalRow._id);
    const script = `${prefix}.${lastCollectionName}.update(x => x._id == ${safeId}, ${JSON.stringify(updateObj, null, 2)});\n${prefix}.${lastCollectionName}.findall();`;

    setEditorValue(script);
}

// --- Autocomplete Helpers ---

async function triggerFetchCollectionFields(dbCols, memCols, bulkCols) {
    if (dbCols.length === 0 && memCols.length === 0 && bulkCols.length === 0) return;

    const dbParts = dbCols.map(c => `"${c}": (function() { try { return db.${c}.findall().take(5).toList(); } catch(e) { return []; } })()`);
    const memParts = memCols.map(c => `"${c}": (function() { try { return memory.${c}.findall().take(5).toList(); } catch(e) { return []; } })()`);
    const bulkParts = bulkCols.map(c => `"${c}": (function() { try { return bulk.${c}.findall().take(5).toList(); } catch(e) { return []; } })()`);

    const batchScript = `({
        db: { ${dbParts.join(', ')} },
        memory: { ${memParts.join(', ')} },
        bulk: { ${bulkParts.join(', ')} }
    })`;

    try {
        const res = await fetchWithAuth('/query', {
            method: 'POST',
            body: batchScript
        });
        if (!res.ok) return;
        const data = await res.json();

        // Reset cache
        collectionFields = { db: {}, memory: {}, bulk: {} };
        collectionSamples = { db: {}, memory: {}, bulk: {} };

        // Parse unique keys
        for (const type of ['db', 'memory', 'bulk']) {
            if (data[type]) {
                for (const colName in data[type]) {
                    const docs = data[type][colName];
                    const keys = new Set();
                    if (Array.isArray(docs)) {
                        if (docs.length > 0) {
                            collectionSamples[type][colName] = docs[0];
                        }
                        for (const doc of docs) {
                            if (doc && typeof doc === 'object') {
                                for (const key in doc) {
                                    keys.add(key);
                                }
                            }
                        }
                    }
                    collectionFields[type][colName] = Array.from(keys);
                }
            }
        }
    } catch (e) {
        console.error("Failed to fetch collection fields: ", e);
    }
}

function resolveCollectionForParam(textBeforeCursor, paramName) {
    const lambdaRegex = new RegExp(`\\b${paramName}\\s*=>|\\(\\s*${paramName}\\s*(?:,\\s*[a-zA-Z0-9_]+\\s*)*\\)\\s*=>|\\bfunction\\s*\\(\\s*${paramName}\\s*\\)`);

    let match;
    let lastDefIdx = -1;
    const regexGlobal = new RegExp(lambdaRegex.source, 'g');
    while ((match = regexGlobal.exec(textBeforeCursor)) !== null) {
        lastDefIdx = match.index;
    }

    const searchString = lastDefIdx !== -1 ? textBeforeCursor.substring(0, lastDefIdx) : textBeforeCursor;
    const colRegex = /\b(db|memory|bulk)\.([a-zA-Z0-9_]+)\b/g;
    let colMatch;
    let lastColMatch = null;
    while ((colMatch = colRegex.exec(searchString)) !== null) {
        lastColMatch = colMatch;
    }

    if (lastColMatch) {
        return { type: lastColMatch[1], name: lastColMatch[2] };
    }
    return null;
}

// --- AI Query Logic ---
function initAiQuery() {
    const btnAiQuery = document.getElementById('btnAiQuery');
    const modal = document.getElementById('aiQueryModal');
    const btnClose = document.getElementById('btnCloseAiQuery');
    const header = document.getElementById('aiQueryModalHeader');
    const aiApiUrl = document.getElementById('aiApiUrl');
    const aiModelName = document.getElementById('aiModelName');
    const aiApiKey = document.getElementById('aiApiKey');
    const aiQueryInput = document.getElementById('aiQueryInput');
    const btnGenerate = document.getElementById('btnAiGenerate');
    const accordion = document.getElementById('aiSettingsAccordion');

    if (!btnAiQuery || !modal) return;

    // Load settings without hardcoded defaults
    aiApiUrl.value = localStorage.getItem('AxarDB_aiApiUrl') || '';
    aiModelName.value = localStorage.getItem('AxarDB_aiModelName') || '';
    aiApiKey.value = localStorage.getItem('AxarDB_aiApiKey') || '';

    const saveSettings = () => {
        localStorage.setItem('AxarDB_aiApiUrl', aiApiUrl.value);
        localStorage.setItem('AxarDB_aiModelName', aiModelName.value);
        localStorage.setItem('AxarDB_aiApiKey', aiApiKey.value);
    };

    aiApiUrl.addEventListener('change', saveSettings);
    aiModelName.addEventListener('change', saveSettings);
    aiApiKey.addEventListener('change', saveSettings);

    // Open/Close
    btnAiQuery.onclick = () => {
        modal.style.display = modal.style.display === 'none' ? 'flex' : 'none';
        if (modal.style.display === 'flex') aiQueryInput.focus();
    };
    btnClose.onclick = () => modal.style.display = 'none';

    // Drag Modal Logic
    let isDragging = false;
    let startX, startY, initialLeft, initialTop;
    header.onmousedown = (e) => {
        if (e.target.closest('button')) return;
        isDragging = true;
        startX = e.clientX;
        startY = e.clientY;
        const rect = modal.getBoundingClientRect();
        initialLeft = rect.left;
        initialTop = rect.top;
        document.body.style.userSelect = 'none';
    };
    
    document.addEventListener('mousemove', (e) => {
        if (!isDragging) return;
        const dx = e.clientX - startX;
        const dy = e.clientY - startY;
        modal.style.left = (initialLeft + dx) + 'px';
        modal.style.top = (initialTop + dy) + 'px';
        modal.style.right = 'auto';
    });
    
    document.addEventListener('mouseup', () => {
        if (isDragging) {
            isDragging = false;
            document.body.style.userSelect = '';
        }
    });

    // Auto-grow textarea up to 10 rows
    aiQueryInput.addEventListener('input', function () {
        this.style.height = 'auto';
        const computed = window.getComputedStyle(this);
        const lineHeight = parseInt(computed.lineHeight) || 18;
        const paddingTop = parseInt(computed.paddingTop) || 8;
        const paddingBottom = parseInt(computed.paddingBottom) || 8;
        
        const padding = paddingTop + paddingBottom;
        const maxHeight = (lineHeight * 10) + padding;
        
        if (this.scrollHeight > maxHeight) {
            this.style.height = maxHeight + 'px';
            this.style.overflowY = 'auto';
        } else {
            this.style.height = this.scrollHeight + 'px';
            this.style.overflowY = 'hidden';
        }
    });

    // Generate action
    const doGenerate = async () => {
        const query = aiQueryInput.value.trim();
        if (!query) return;
        
        let hasError = false;
        aiApiUrl.style.borderColor = '';
        aiModelName.style.borderColor = '';
        aiApiKey.style.borderColor = '';

        const apiUrl = aiApiUrl.value.trim();
        const modelName = aiModelName.value.trim();
        const apiKey = aiApiKey.value.trim();

        if (!apiUrl) {
            aiApiUrl.style.borderColor = 'red';
            hasError = true;
        }
        if (!modelName) {
            aiModelName.style.borderColor = 'red';
            hasError = true;
        }
        if (!apiKey) {
            aiApiKey.style.borderColor = 'red';
            hasError = true;
        }

        if (hasError) {
            if (accordion) accordion.open = true;
            return;
        }

        btnGenerate.disabled = true;
        btnGenerate.innerHTML = '<i data-lucide="loader"></i> Generating...';
        if (window.lucide) lucide.createIcons();

        try {
            const res = await fetch('/api/ai-query', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    apiUrl: apiUrl,
                    modelName: modelName,
                    apiKey: apiKey,
                    query: query,
                    schemaContext: JSON.stringify(collectionSamples)
                })
            });

            if (!res.ok) {
                const errData = await res.json().catch(() => ({}));
                throw new Error(errData.detail || errData.error?.message || 'API request failed');
            }

            const data = await res.json();
            let code = data.choices && data.choices.length > 0 ? data.choices[0].message.content : '';
            
            // Clean markdown if present
            if (code.startsWith('```')) {
                code = code.replace(/^\`\`\`[a-zA-Z]*\n/, '').replace(/\n\`\`\`$/, '');
            }
            code = code.trim();

            if (editor) {
                setEditorValue(code);
            }
            
            // Clear input and close modal on success
            aiQueryInput.value = '';
            aiQueryInput.style.height = 'auto';
            modal.style.display = 'none';

        } catch (err) {
            alert('AI Generation Error: ' + err.message);
        } finally {
            btnGenerate.disabled = false;
            btnGenerate.innerHTML = '<i data-lucide="sparkles"></i> Generate (Ctrl+Enter)';
            if (window.lucide) lucide.createIcons();
        }
    };

    btnGenerate.onclick = doGenerate;

    aiQueryInput.addEventListener('keydown', (e) => {
        if (e.ctrlKey && e.key === 'Enter') {
            e.preventDefault();
            doGenerate();
        }
    });
}
