// Theme Switcher Logic
document.addEventListener("DOMContentLoaded", function () {


    const sidebarToggleBtn = document.getElementById("sidebarToggleBtn");
    if (sidebarToggleBtn) {
        sidebarToggleBtn.addEventListener("click", function () {
            const sidebar = document.getElementById("sidebar");
            if (sidebar) {
                sidebar.classList.toggle("show");
            }
        });
    }

    // Form Submit Loading Feedback
    document.addEventListener("submit", function (e) {
        const form = e.target;
        // Giriş formlarında çakışma yaşanmaması için genel spinner atlanır
        if (form.querySelector('input[name="portalType"]')) return;

        const submitBtn = form.querySelector('button[type="submit"]');
        if (submitBtn && !submitBtn.disabled) {
            setTimeout(function () {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> İşleniyor...';
            }, 10);
        }
    });

    // Initialize Timers
    initCountdowns();

    // SignalR Setup
    initSignalR();
});


// 24 Hour Countdown Timer
function initCountdowns() {
    const countdownElements = document.querySelectorAll("[data-countdown-end]");
    countdownElements.forEach(el => {
        const endTimeStr = el.getAttribute("data-countdown-end");
        if (!endTimeStr) return;

        const endTime = new Date(endTimeStr).getTime();

        const timer = setInterval(function () {
            const now = new Date().getTime();
            const distance = endTime - now;

            if (distance < 0) {
                clearInterval(timer);
                el.innerHTML = "<span class='text-danger font-weight-bold'>SÜRE DOLDU</span>";
                return;
            }

            const hours = Math.floor((distance % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
            const minutes = Math.floor((distance % (1000 * 60 * 60)) / (1000 * 60));
            const seconds = Math.floor((distance % (1000 * 60)) / 1000);

            el.innerHTML = `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
        }, 1000);
    });
}

// SignalR Connection
function initSignalR() {
    if (typeof signalR === "undefined") return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveNotification", function (data) {
        showToast(data.baslik, data.mesaj, data.tip);
        updateUnreadNotificationCount();
    });

    connection.start().then(function () {
        const currentUserId = document.body.getAttribute("data-user-id");
        const currentUserRole = document.body.getAttribute("data-user-role");

        if (currentUserId) {
            connection.invoke("JoinUserGroup", currentUserId).catch(err => console.error(err));
        }
        if (currentUserRole) {
            connection.invoke("JoinRoleGroup", currentUserRole).catch(err => console.error(err));
        }
    }).catch(err => console.error("SignalR Bağlantı hatası: ", err));
}

function showToast(title, message, type) {
    if (typeof Swal !== "undefined") {
        Swal.fire({
            toast: true,
            position: 'top-end',
            icon: type === 'MesaiTeklifi' ? 'warning' : 'info',
            title: title,
            text: message,
            showConfirmButton: false,
            timer: 5000,
            timerProgressBar: true
        });
    } else {
        alert(`${title}\n${message}`);
    }
}

function updateUnreadNotificationCount() {
    fetch('/Notification/GetUnreadCount')
        .then(res => res.json())
        .then(data => {
            const badge = document.getElementById("unreadNotifBadge");
            if (badge) {
                if (data.count > 0) {
                    badge.innerText = data.count;
                    badge.classList.remove("d-none");
                } else {
                    badge.classList.add("d-none");
                }
            }
        });
}
