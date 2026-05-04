(function() {
    const BG_IMAGES = [
        'bg/bg-1.jpg',
        'bg/bg-2.jpg',
        'bg/bg-3.jpg'
    ];

    const BG_KEY = 'remote_rescue_bg_index';
    const BG_TIMESTAMP = 'remote_rescue_bg_time';

    function initBackground() {
        const bgLayer = document.createElement('div');
        bgLayer.id = 'bg-layer';

        const bgOverlay = document.createElement('div');
        bgOverlay.id = 'bg-overlay';

        document.body.insertBefore(bgLayer, document.body.firstChild);
        document.body.insertBefore(bgOverlay, document.body.firstChild);

        const savedIndex = sessionStorage.getItem(BG_KEY);
        const savedTime = sessionStorage.getItem(BG_TIMESTAMP);
        const now = Date.now();

        let currentIndex;

        if (savedIndex !== null && savedTime !== null && (now - parseInt(savedTime)) < 60000) {
            currentIndex = parseInt(savedIndex);
        } else {
            currentIndex = Math.floor(Math.random() * BG_IMAGES.length);
            sessionStorage.setItem(BG_KEY, currentIndex.toString());
            sessionStorage.setItem(BG_TIMESTAMP, now.toString());
        }

        const selectedImage = BG_IMAGES[currentIndex];
        bgLayer.style.backgroundImage = `url('${selectedImage}')`;

        preloadOtherImages(currentIndex);
    }

    function preloadOtherImages(currentIndex) {
        BG_IMAGES.forEach((img, index) => {
            if (index !== currentIndex) {
                const preloadImg = new Image();
                preloadImg.src = img;
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initBackground);
    } else {
        initBackground();
    }
})();
