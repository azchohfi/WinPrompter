(function () {
    'use strict';

    const state = {
        isScrolling: false,
        speed: 1.0,
        basePixelsPerSecond: 50,
        scrollPosition: 0,
        contentHeight: 0,
        viewportHeight: 0,
        fontSize: 36,
        lastTimestamp: 0,
        animFrameId: null,
        isPausedByCue: false,
        cueResumeTimeout: null,
        wordsPerMinute: 0,
        wordCount: 0,
    };

    const content = document.getElementById('content');
    const progressFill = document.getElementById('progress-fill');
    const wpmDisplay = document.getElementById('wpm-display');

    function updateMetrics() {
        state.contentHeight = content.scrollHeight;
        state.viewportHeight = window.innerHeight;
    }

    function getMaxScroll() {
        return Math.max(0, state.contentHeight - state.viewportHeight);
    }

    function updateWpm() {
        if (!state.isScrolling || state.contentHeight <= 0 || state.wordCount <= 0) {
            wpmDisplay.textContent = '';
            return;
        }
        const totalScrollHeight = getMaxScroll();
        if (totalScrollHeight <= 0) return;
        const pixelsPerSecond = state.basePixelsPerSecond * state.speed;
        const totalSeconds = totalScrollHeight / pixelsPerSecond;
        state.wordsPerMinute = Math.round((state.wordCount / totalSeconds) * 60);
        wpmDisplay.textContent = state.wordsPerMinute + ' WPM';
    }

    function scrollStep(timestamp) {
        if (!state.isScrolling || state.isPausedByCue) {
            state.lastTimestamp = timestamp;
            state.animFrameId = requestAnimationFrame(scrollStep);
            return;
        }

        const delta = timestamp - state.lastTimestamp;
        state.lastTimestamp = timestamp;

        // Cap delta to avoid huge jumps when tab is backgrounded
        const clampedDelta = Math.min(delta, 100);
        const pxPerMs = (state.basePixelsPerSecond * state.speed) / 1000;
        state.scrollPosition += pxPerMs * clampedDelta;

        const maxScroll = getMaxScroll();
        if (state.scrollPosition >= maxScroll) {
            state.scrollPosition = maxScroll;
            state.isScrolling = false;
            notify({ type: 'scrollComplete' });
            notify({ type: 'playbackChanged', isPlaying: false });
        }

        content.style.transform = 'translateY(' + (-state.scrollPosition) + 'px)';

        const percent = maxScroll > 0 ? (state.scrollPosition / maxScroll) * 100 : 0;
        progressFill.style.width = percent + '%';

        // Throttle progress updates to ~4 per second
        if (Math.floor(timestamp / 250) !== Math.floor((timestamp - clampedDelta) / 250)) {
            notify({ type: 'progress', percent: Math.round(percent) });
        }

        checkCueMarkers();
        state.animFrameId = requestAnimationFrame(scrollStep);
    }

    function checkCueMarkers() {
        const markers = content.querySelectorAll('.cue-marker:not(.triggered)');
        for (const marker of markers) {
            const markerTop = marker.offsetTop;
            if (markerTop <= state.scrollPosition + state.viewportHeight * 0.5) {
                marker.classList.add('triggered');
                const duration = parseFloat(marker.dataset.duration) || 3;
                state.isPausedByCue = true;
                notify({ type: 'cue', action: 'pause', duration: duration });
                state.cueResumeTimeout = setTimeout(function () {
                    state.isPausedByCue = false;
                }, duration * 1000);
            }
        }
    }

    function notify(msg) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(JSON.stringify(msg));
        }
    }

    function countWords(element) {
        const text = element.textContent || '';
        const words = text.trim().split(/\s+/);
        return text.trim().length > 0 ? words.length : 0;
    }

    // ── Public API (called from C#) ──

    window.prompter = {
        setContent: function (html) {
            content.innerHTML = html;
            state.scrollPosition = 0;
            content.style.transform = 'translateY(0)';
            progressFill.style.width = '0%';
            content.querySelectorAll('.cue-marker').forEach(function (m) {
                m.classList.remove('triggered');
            });
            state.wordCount = countWords(content);
            updateMetrics();
            updateWpm();
        },

        setTheme: function (theme) {
            document.body.className = document.body.className
                .replace(/theme-[\w-]+/g, '')
                .trim();
            document.body.classList.add('theme-' + theme);
        },

        setFontSize: function (size) {
            state.fontSize = size;
            content.style.fontSize = size + 'px';
            updateMetrics();
            updateWpm();
        },

        play: function () {
            if (!state.isScrolling) {
                state.isScrolling = true;
                state.lastTimestamp = performance.now();
                updateWpm();
                if (!state.animFrameId) {
                    state.animFrameId = requestAnimationFrame(scrollStep);
                }
                notify({ type: 'playbackChanged', isPlaying: true });
            }
        },

        pause: function () {
            state.isScrolling = false;
            wpmDisplay.textContent = '';
            notify({ type: 'playbackChanged', isPlaying: false });
        },

        stop: function () {
            state.isScrolling = false;
            state.scrollPosition = 0;
            content.style.transform = 'translateY(0)';
            progressFill.style.width = '0%';
            wpmDisplay.textContent = '';
            if (state.cueResumeTimeout) {
                clearTimeout(state.cueResumeTimeout);
                state.isPausedByCue = false;
            }
            content.querySelectorAll('.cue-marker').forEach(function (m) {
                m.classList.remove('triggered');
            });
            notify({ type: 'playbackChanged', isPlaying: false });
        },

        setSpeed: function (multiplier) {
            state.speed = Math.max(0.1, Math.min(10, multiplier));
            updateWpm();
        },

        setMirror: function (enabled) {
            document.body.classList.toggle('mirror', enabled);
        },

        scrollToPosition: function (percent) {
            updateMetrics();
            var maxScroll = getMaxScroll();
            state.scrollPosition = (percent / 100) * maxScroll;
            content.style.transform = 'translateY(' + (-state.scrollPosition) + 'px)';
            progressFill.style.width = percent + '%';
        },

        scrollToWordIndex: function (charOffset) {
            // For voice-advance: smoothly scroll to the position of a character offset
            updateMetrics();
            var walker = document.createTreeWalker(content, NodeFilter.SHOW_TEXT);
            var totalChars = 0;
            var node;
            while ((node = walker.nextNode())) {
                totalChars += node.textContent.length;
                if (totalChars >= charOffset) {
                    var rect = node.parentElement.getBoundingClientRect();
                    var targetY = rect.top + state.scrollPosition - state.viewportHeight * 0.3;
                    // Smooth interpolation toward target
                    var diff = targetY - state.scrollPosition;
                    state.scrollPosition += diff * 0.15;
                    content.style.transform = 'translateY(' + (-state.scrollPosition) + 'px)';
                    var maxScroll = getMaxScroll();
                    var percent = maxScroll > 0 ? (state.scrollPosition / maxScroll) * 100 : 0;
                    progressFill.style.width = percent + '%';
                    break;
                }
            }
        },

        getHeadings: function () {
            var headings = [];
            content.querySelectorAll('h1, h2, h3').forEach(function (el) {
                headings.push({
                    level: parseInt(el.tagName[1]),
                    text: el.textContent,
                    offsetPercent: state.contentHeight > 0
                        ? (el.offsetTop / state.contentHeight) * 100
                        : 0,
                });
            });
            return JSON.stringify(headings);
        },

        getState: function () {
            updateMetrics();
            var maxScroll = getMaxScroll();
            return JSON.stringify({
                isScrolling: state.isScrolling,
                speed: state.speed,
                fontSize: state.fontSize,
                scrollPercent: maxScroll > 0 ? (state.scrollPosition / maxScroll) * 100 : 0,
                contentHeight: state.contentHeight,
                wpm: state.wordsPerMinute,
            });
        },
    };

    // ── Forward keyboard events to C# ──
    document.addEventListener('keydown', function (e) {
        notify({
            type: 'keydown',
            key: e.key,
            code: e.code,
            ctrl: e.ctrlKey,
            alt: e.altKey,
            shift: e.shiftKey,
        });
        // Prevent browser defaults for our shortcuts
        if (
            e.key === ' ' ||
            e.key === 'F11' ||
            (e.ctrlKey && (e.key === '+' || e.key === '-' || e.key === '='))
        ) {
            e.preventDefault();
        }
    });

    window.addEventListener('resize', function () {
        updateMetrics();
        updateWpm();
    });

    // Start idle animation loop
    state.animFrameId = requestAnimationFrame(scrollStep);
    updateMetrics();
})();
