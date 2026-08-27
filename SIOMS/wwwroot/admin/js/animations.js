// SIOMS Admin Panel - Animation JavaScript

document.addEventListener('DOMContentLoaded', function() {
    // ===== Animate Elements on Scroll =====
    var animateElements = document.querySelectorAll('.animate-on-scroll');
    
    if (animateElements.length > 0) {
        var observer = new IntersectionObserver(function(entries) {
            entries.forEach(function(entry) {
                if (entry.isIntersecting) {
                    var element = entry.target;
                    var animationClass = element.dataset.animation || 'animate-fade-up';
                    var delay = element.dataset.delay || 0;
                    
                    setTimeout(function() {
                        element.classList.add(animationClass);
                    }, delay);
                    
                    observer.unobserve(element);
                }
            });
        }, {
            threshold: 0.1,
            rootMargin: '0px 0px -50px 0px'
        });
        
        animateElements.forEach(function(element) {
            observer.observe(element);
        });
    }

    // ===== Table Row Animation =====
    var tableRows = document.querySelectorAll('table tbody tr');
    tableRows.forEach(function(row, index) {
        if (!row.classList.contains('animate-fade-in')) {
            var delay = (index * 0.05);
            row.style.animationDelay = delay + 's';
            row.classList.add('animate-fade-in');
        }
    });

    // ===== Counter Animation =====
    var counterElements = document.querySelectorAll('.counter');
    counterElements.forEach(function(counter) {
        var target = parseInt(counter.dataset.target);
        var duration = parseInt(counter.dataset.duration) || 2000;
        var step = target / (duration / 16);
        var current = 0;
        
        var observer = new IntersectionObserver(function(entries) {
            entries.forEach(function(entry) {
                if (entry.isIntersecting) {
                    var interval = setInterval(function() {
                        current += step;
                        if (current >= target) {
                            current = target;
                            clearInterval(interval);
                        }
                        counter.textContent = Math.floor(current);
                    }, 16);
                    
                    observer.unobserve(counter);
                }
            });
        }, { threshold: 0.5 });
        
        observer.observe(counter);
    });

    // ===== Parallax Effect =====
    var parallaxElements = document.querySelectorAll('.parallax');
    if (parallaxElements.length > 0) {
        window.addEventListener('scroll', function() {
            var scrollY = window.pageYOffset;
            parallaxElements.forEach(function(element) {
                var speed = parseFloat(element.dataset.speed) || 0.5;
                var offset = scrollY * speed;
                element.style.transform = 'translateY(' + offset + 'px)';
            });
        });
    }

    // ===== Floating Animation =====
    var floatElements = document.querySelectorAll('.float-animation');
    floatElements.forEach(function(element, index) {
        var delay = index * 0.2;
        var duration = 2 + (Math.random() * 2);
        element.style.animation = `float ${duration}s ease-in-out ${delay}s infinite`;
    });

    // Add float keyframes dynamically
    var styleSheet = document.createElement('style');
    styleSheet.textContent = `
        @keyframes float {
            0%, 100% {
                transform: translateY(0px);
            }
            50% {
                transform: translateY(-15px);
            }
        }
    `;
    document.head.appendChild(styleSheet);

    // ===== Page Load Animation =====
    // Add animation to page content with delay
    setTimeout(function() {
        document.querySelectorAll('.content-wrapper > .content-header, .content-wrapper > section.content').forEach(function(element, index) {
            var delay = index * 0.1;
            element.style.animationDelay = delay + 's';
            element.classList.add('animate-fade-up');
        });
    }, 100);
});