function validatePassword() {
    var password = document.getElementById('NewPassword').value;
    var strengthBar = document.getElementById('password-strength-bar');
    var strengthText = document.getElementById('password-strength-text');

    var strength = 0;
    var feedback = [];

    if (password.length >= 12) {
        strength += 20;
    } else {
        feedback.push("At least 12 characters");
    }

    if (password.match(/[a-z]/)) {
        strength += 20;
    } else {
        feedback.push("Add lowercase letters");
    }

    if (password.match(/[A-Z]/)) {
        strength += 20;
    } else {
        feedback.push("Add uppercase letters");
    }

    if (password.match(/[0-9]/)) {
        strength += 20;
    } else {
        feedback.push("Add numbers");
    }

    if (password.match(/[@$!%*?&#]/)) {
        strength += 20;
    } else {
        feedback.push("Add special characters (@$!%*?&#)");
    }

    strengthBar.style.width = strength + '%';

    if (strength < 40) {
        strengthBar.className = 'progress-bar bg-danger';
        strengthText.textContent = 'Very Weak - ' + feedback.join(', ');
        strengthText.style.color = 'red';
    } else if (strength < 60) {
        strengthBar.className = 'progress-bar bg-warning';
        strengthText.textContent = 'Weak - ' + feedback.join(', ');
        strengthText.style.color = 'orange';
    } else if (strength < 80) {
        strengthBar.className = 'progress-bar bg-info';
        strengthText.textContent = 'Medium - ' + feedback.join(', ');
        strengthText.style.color = 'blue';
    } else if (strength < 100) {
        strengthBar.className = 'progress-bar bg-primary';
        strengthText.textContent = 'Strong';
        strengthText.style.color = 'green';
    } else {
        strengthBar.className = 'progress-bar bg-success';
        strengthText.textContent = 'Excellent';
        strengthText.style.color = 'green';
    }
}