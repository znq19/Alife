var STAR_COUNT = 50;
var FADE_DELAY = 500;

function createStars() {
    var container = document.getElementById('starsContainer');
    if (!container) return;
    for (var i = 0; i < STAR_COUNT; i++) {
        var star = document.createElement('div');
        var size = Math.random();
        var sizeClass = 'star--small';
        if (size > 0.85) sizeClass = 'star--large';
        else if (size > 0.6) sizeClass = 'star--medium';
        star.className = 'star ' + sizeClass;
        star.style.left = Math.random() * 100 + '%';
        star.style.top = Math.random() * 70 + '%';
        star.style.setProperty('--duration', (2 + Math.random() * 4) + 's');
        star.style.setProperty('--min-opacity', (0.2 + Math.random() * 0.3).toFixed(2));
        star.style.setProperty('--max-opacity', (0.7 + Math.random() * 0.3).toFixed(2));
        star.style.animationDelay = (Math.random() * 5) + 's';
        container.appendChild(star);
    }
}

function hideLoadingScreen() {
    var screen = document.getElementById('loadingScreen');
    if (screen) {
        screen.classList.add('fade-out');
        setTimeout(function () { screen.style.display = 'none'; }, 700);
    }
}

function watchBlazorLoad() {
    var body = document.body;
    if (!body) return;

    var observer = new MutationObserver(function (mutations) {
        for (var i = 0; i < mutations.length; i++) {
            var mutation = mutations[i];
            if (mutation.addedNodes.length > 0) {
                for (var j = 0; j < mutation.addedNodes.length; j++) {
                    var node = mutation.addedNodes[j];
                    if (node.nodeType === 1 && node.id !== 'loadingScreen' && !node.classList?.contains('loading-screen')) {
                        observer.disconnect();
                        setTimeout(hideLoadingScreen, FADE_DELAY);
                        return;
                    }
                }
            }
        }
    });

    observer.observe(body, { childList: true, subtree: true });

    setTimeout(function () {
        observer.disconnect();
        hideLoadingScreen();
    }, 8000);
}

createStars();
watchBlazorLoad();
