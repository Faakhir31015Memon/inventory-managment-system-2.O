document.getElementById('current-date').textContent = new Date().toLocaleDateString('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric'
});

document.addEventListener('DOMContentLoaded', function() {
    // Students per Course Chart
    const studentsCtx = document.getElementById('studentsChart').getContext('2d');
    if (studentsCtx) {
        new Chart(studentsCtx, {
            type: 'bar',
            data: {
                labels: JSON.parse(document.getElementById('studentsChart').dataset.labels),
                datasets: [{
                    label: 'Students Enrolled',
                    data: JSON.parse(document.getElementById('studentsChart').dataset.values),
                    backgroundColor: [
                        'rgba(255, 159, 64, 0.7)',
                        'rgba(54, 162, 235, 0.7)',
                        'rgba(255, 99, 132, 0.7)',
                        'rgba(75, 192, 192, 0.7)',
                        'rgba(153, 102, 255, 0.7)'
                    ],
                    borderColor: [
                        'rgba(255, 159, 64, 1)',
                        'rgba(54, 162, 235, 1)',
                        'rgba(255, 99, 132, 1)',
                        'rgba(75, 192, 192, 1)',
                        'rgba(153, 102, 255, 1)'
                    ],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });
    }

    // Monthly Admissions Chart
    const admissionsCtx = document.getElementById('admissionsChart').getContext('2d');
    if (admissionsCtx) {
        new Chart(admissionsCtx, {
            type: 'line',
            data: {
                labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
                datasets: [{
                    label: 'Admissions',
                    data: JSON.parse(document.getElementById('admissionsChart').dataset.values),
                    fill: false,
                    backgroundColor: 'rgba(75, 192, 192, 0.7)',
                    borderColor: 'rgba(75, 192, 192, 1)',
                    tension: 0.1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });
    }
});