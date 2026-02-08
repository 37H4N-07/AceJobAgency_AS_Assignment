// Session Timeout Handler
(function() {
    // Session timeout in milliseconds (20 minutes = 1200000ms)
    const SESSION_TIMEOUT = 2 * 60 * 1000;
    const WARNING_TIME = 1 * 60 * 1000;
    
    let timeoutTimer;
    let warningTimer;
    let lastActivity = Date.now();

    // Check if user is authenticated
    function isAuthenticated() {
        return document.body.classList.contains('authenticated');
    }

    // Reset activity timer
    function resetTimer() {
        lastActivity = Date.now();
        
        // Clear existing timers
        clearTimeout(timeoutTimer);
        clearTimeout(warningTimer);
        
        if (!isAuthenticated()) return;

        // Set warning timer (2 minutes before logout)
        warningTimer = setTimeout(showWarning, WARNING_TIME);
        
        // Set logout timer
        timeoutTimer = setTimeout(autoLogout, SESSION_TIMEOUT);
    }

    // Show warning modal
    function showWarning() {
        if (!isAuthenticated()) return;

        const remainingTime = Math.ceil((SESSION_TIMEOUT - WARNING_TIME) / 1000 / 60);
        
        if (confirm(`Your session will expire in ${remainingTime} minutes due to inactivity. Click OK to stay logged in.`)) {
            // User clicked OK, reset timer
            resetTimer();
            
            // Ping server to keep session alive
            fetch('/api/keepalive', { method: 'POST' })
                .catch(() => {}); // Ignore errors
        }
    }

    // Automatic logout
    function autoLogout() {
        if (!isAuthenticated()) return;

        alert('Your session has expired due to inactivity. You will be redirected to the login page.');
        window.location.href = '/Logout';
    }

    // Track user activity
    function trackActivity() {
        resetTimer();
    }

    // Initialize if authenticated
    if (isAuthenticated()) {
        // Activity events
        const events = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'];
        
        events.forEach(event => {
            document.addEventListener(event, trackActivity, true);
        });

        // Start timer
        resetTimer();

        // Check session validity every minute
        setInterval(async () => {
            if (!isAuthenticated()) return;

            try {
                const response = await fetch('/api/checksession');
                const data = await response.json();
                
                if (!data.isValid) {
                    alert('Your session has expired. You will be redirected to the login page.');
                    window.location.href = '/Logout';
                }
            } catch (error) {
                // If request fails, don't logout (could be network issue)
                console.warn('Session check failed:', error);
            }
        }, 60000); // Check every 1 minute
    }

    // Monitor for authentication state changes
    const observer = new MutationObserver(() => {
        if (isAuthenticated() && !timeoutTimer) {
            resetTimer();
        }
    });

    observer.observe(document.body, {
        attributes: true,
        attributeFilter: ['class']
    });
})();