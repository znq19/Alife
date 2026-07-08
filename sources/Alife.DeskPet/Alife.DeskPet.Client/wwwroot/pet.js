// ---- 消息总线 ----
const messageBus = {
    _handlers: {},
    on(type, handler) { (this._handlers[type] ??= []).push(handler); },
    _dispatch(msg) {
        // 统一守卫：非初始化/加载消息且 model 未就绪时拦截
        if (msg.type !== '_init' && msg.type !== 'load' && !model) {
            postLog('warn', '[Pet] Ignore ' + msg.type + ': model not loaded');
            return;
        }
        (this._handlers[msg.type] ?? []).forEach(h => h(msg));
    }
};

// ---- 前端日志转发 ----
function formatLogArg(arg) {
    if (arg instanceof Error) return arg.name + ': ' + arg.message + '\n' + (arg.stack || '');
    if (typeof arg === 'object') {
        try { return JSON.stringify(arg); } catch { return String(arg); }
    }
    return String(arg);
}

function postLog(level) {
    try {
        var args = Array.prototype.slice.call(arguments, 1);
        window.chrome.webview.postMessage({
            type: 'log',
            level: level,
            text: args.map(formatLogArg).join(' ')
        });
    } catch {}
}

{
    var rawWarn = console.warn.bind(console);
    var rawError = console.error.bind(console);
    console.warn = function() {
        postLog.apply(null, ['warn'].concat(Array.prototype.slice.call(arguments)));
        rawWarn.apply(console, arguments);
    };
    console.error = function() {
        postLog.apply(null, ['error'].concat(Array.prototype.slice.call(arguments)));
        rawError.apply(console, arguments);
    };
}

window.addEventListener('error', function(e) {
    var target = e.target;
    if (target && target !== window) {
        postLog('resource-error', target.tagName || 'unknown', target.src || target.href || '');
        return;
    }
    postLog('error', e.message, e.filename + ':' + e.lineno + ':' + e.colno, e.error || '');
}, true);

window.addEventListener('unhandledrejection', function(e) {
    postLog('unhandledrejection', e.reason);
});

// ---- PIXI 应用 ----
const app = new PIXI.Application({
    view: document.getElementById('canvas'),
    autoStart: true,
    resizeTo: window,
    transparent: true,
    backgroundAlpha: 0,
});

let model = null;

// ---- Live2D 模型加载 ----
async function loadModel(url) {
    console.log('[Pet] Loading model:', url);
    if (model) app.stage.removeChild(model);
    try {
        model = await PIXI.live2d.Live2DModel.from(url, {autoInteract: false});
        console.log('[Pet] Model loaded successfully');
    } catch (err) {
        console.error('[Pet] Live2D model load failed:', err);
        postMessage({type: 'loaded'});
        return;
    }
    app.stage.addChild(model);

    var bh = model.internalModel.originalHeight || (model.height / model.scale.y);

    var updateLayout = function() {
        var s = window.innerHeight / 540;
        document.documentElement.style.setProperty('--ui-scale', s);
        var sc = (window.innerHeight * 0.9) / bh;
        model.scale.set(sc);
        model.position.set(window.innerWidth / 2, window.innerHeight / 2);
    };

    model.anchor.set(0.5, 0.5);
    updateLayout();
    model.interactive = true;
    window.addEventListener('resize', updateLayout);

    postMessage({type: 'loaded'});
}

// ---- C# 通讯 ----
window.chrome.webview.addEventListener('message', function(e) {
    messageBus._dispatch(e.data);
});

function postMessage(data) {
    window.chrome.webview.postMessage(data);
}

// ---- 前端资源注入辅助 ----
function injectCSS(css) {
    var style = document.createElement('style');
    style.textContent = css;
    document.head.appendChild(style);
}

function injectHTML(html) {
    var div = document.createElement('div');
    div.innerHTML = html;
    while (div.firstChild) document.body.appendChild(div.firstChild);
}

// ---- 核心消息 ----
messageBus.on('_init', function() { postMessage({type: 'ready'}); });
messageBus.on('load', function(msg) { loadModel(msg.url); });
