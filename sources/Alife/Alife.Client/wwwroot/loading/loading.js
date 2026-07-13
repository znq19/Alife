/**
 * Alife Loading Screen - 动画与控制脚本
 */
(function () {
    'use strict';

    // 配置
    const STAR_COUNT = 50;
    const FADE_DELAY = 500; // Blazor 加载后延迟隐藏 (ms)

    // 生成星星
    function createStars() {
        const container = document.getElementById('starsContainer');
        if (!container) return;

        for (let i = 0; i < STAR_COUNT; i++) {
            const star = document.createElement('div');
            const size = Math.random();
            let sizeClass = 'star--small';
            if (size > 0.85) sizeClass = 'star--large';
            else if (size > 0.6) sizeClass = 'star--medium';

            star.className = 'star ' + sizeClass;
            star.style.left = Math.random() * 100 + '%';
            star.style.top = Math.random() * 70 + '%'; // 集中在上半部分
            star.style.setProperty('--duration', (2 + Math.random() * 4) + 's');
            star.style.setProperty('--min-opacity', (0.2 + Math.random() * 0.3).toFixed(2));
            star.style.setProperty('--max-opacity', (0.7 + Math.random() * 0.3).toFixed(2));
            star.style.animationDelay = (Math.random() * 5) + 's';

            container.appendChild(star);
        }
    }

    // 隐藏加载屏幕
    function hideLoadingScreen() {
        const screen = document.getElementById('loadingScreen');
        if (screen) {
            screen.classList.add('fade-out');
            // 动画结束后移除 DOM
            setTimeout(function () {
                if (screen.parentNode) {
                    screen.parentNode.removeChild(screen);
                }
            }, 700);
        }
    }

    // 监听 Blazor 加载完成
    function watchBlazorLoad() {
        // 方式1: 监听 Blazor 的 DOM 变化
        var appEl = document.getElementById('app');
        if (!appEl) return;

        var observer = new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var mutation = mutations[i];
                // 检查是否有非加载画面的内容被渲染
                if (mutation.addedNodes.length > 0) {
                    for (var j = 0; j < mutation.addedNodes.length; j++) {
                        var node = mutation.addedNodes[j];
                        // Blazor 渲染的组件节点
                        if (node.nodeType === 1 && node.id !== 'loadingScreen' && !node.classList?.contains('loading-screen')) {
                            observer.disconnect();
                            setTimeout(hideLoadingScreen, FADE_DELAY);
                            return;
                        }
                    }
                }
            }
        });

        observer.observe(appEl, { childList: true, subtree: true });

        // 兜底: 8秒后强制隐藏
        setTimeout(function () {
            observer.disconnect();
            hideLoadingScreen();
        }, 8000);
    }

    // 初始化
    function init() {
        createStars();
        watchBlazorLoad();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
