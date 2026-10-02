(function () {
    var root = document.documentElement;
    var media = window.matchMedia('(prefers-color-scheme: dark)');

    function apply(theme) {
        root.setAttribute('data-theme', theme);
    }

    // Browsers that block site data throw on any localStorage access. The theme then just isn't remembered.
    function stored() {
        try { return localStorage.getItem('sp-theme'); } catch (e) { return null; }
    }

    function store(theme) {
        try { localStorage.setItem('sp-theme', theme); } catch (e) { }
    }

    // Stored choice wins, otherwise follow the system.
    apply(stored() || (media.matches ? 'dark' : 'light'));

    media.addEventListener('change', function (e) {
        if (!stored()) {
            apply(e.matches ? 'dark' : 'light');
        }
    });

    document.addEventListener('click', function (e) {
        if (!e.target.closest('.sp-theme-toggle')) return;
        var next = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        store(next);
        // Crossfade between the themes where the browser supports view transitions; otherwise switch at once.
        if (document.startViewTransition) {
            document.startViewTransition(function () { apply(next); });
        } else {
            apply(next);
        }
    });

    // Narrow screens: the sidebar is a drawer opened by the "Menu" button in the top bar.
    function setMenu(open) {
        root.classList.toggle('sp-nav-open', open);
        var button = document.querySelector('.sp-menu-toggle');
        if (button) button.setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    document.addEventListener('click', function (e) {
        if (e.target.closest('.sp-menu-toggle')) {
            setMenu(!root.classList.contains('sp-nav-open'));
        }
        // Links in the menu load a new page, so the drawer is left open rather than animated out first.
    });

    // The backdrop closes the menu as soon as it is touched rather than on click, which on touch screens
    // only fires after the finger lifts (and the browser has ruled out a gesture).
    document.addEventListener('pointerdown', function (e) {
        if (e.target.closest('.sp-backdrop')) setMenu(false);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && root.classList.contains('sp-nav-open')) setMenu(false);
    });

    // Going back restores the page from the back/forward cache, drawer and all. Close it without animating,
    // so the page comes back as it looked before the menu was opened.
    window.addEventListener('pageshow', function (e) {
        if (!e.persisted || !root.classList.contains('sp-nav-open')) return;
        root.classList.add('sp-no-transition');
        setMenu(false);
        void root.offsetWidth; // apply the closed state before transitions come back
        root.classList.remove('sp-no-transition');
    });

    // "On this page": highlight the link of the top-level section currently being read.
    // A section runs from its heading to the next top-level heading, so subsections belong to their parent.
    var offset = 48;
    var ticking = false;
    var clicked = null;

    function updateToc() {
        ticking = false;
        var links = document.querySelectorAll('.sp-toc-level-2 a');
        if (!links.length) return;

        var active = null;
        if (clicked && document.contains(clicked)) {
            // After clicking a link, keep it active until the reader scrolls on their own. The last
            // sections can't always reach the top of the window, so scroll position alone would be wrong.
            active = clicked;
        } else {
            // The line a heading must pass to become active moves from near the top of the window at
            // the top of the page to the bottom of the window at the end of the page. That way short
            // sections at the end of the page get their turn instead of being skipped.
            var maxScroll = document.documentElement.scrollHeight - window.innerHeight;
            var progress = maxScroll > 0 ? Math.min(1, Math.max(0, window.scrollY / maxScroll)) : 0;
            var line = offset + (window.innerHeight - offset) * progress;

            links.forEach(function (link) {
                var heading = document.getElementById(decodeURIComponent(link.hash.slice(1)));
                if (heading && heading.getBoundingClientRect().top <= line) active = link;
            });
        }

        links.forEach(function (link) {
            link.classList.toggle('sp-active', link === active);
        });
    }

    function release() {
        if (!clicked) return;
        clicked = null;
        schedule();
    }

    document.addEventListener('click', function (e) {
        var link = e.target.closest('.sp-toc-level-2 a');
        if (link) {
            clicked = link;
            schedule();
        }
    });
    window.addEventListener('wheel', release, { passive: true });
    window.addEventListener('touchmove', release, { passive: true });
    window.addEventListener('keydown', release);

    function schedule() {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(updateToc);
    }

    window.addEventListener('scroll', schedule, { passive: true });
    window.addEventListener('resize', schedule);
    window.addEventListener('hashchange', schedule);
    document.addEventListener('DOMContentLoaded', schedule);
})();
