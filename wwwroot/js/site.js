document.addEventListener("DOMContentLoaded", () => {
    const burger = document.getElementById("burger");
    const nav = document.getElementById("nav");

    // burger
    if (burger) {
        burger.addEventListener("click", () => {
            burger.classList.toggle("active");
            nav.classList.toggle("open");
        });
    }

    // Open modal with button
    document.querySelectorAll(".js-open-modal").forEach(button => {
        const targetId = button.getAttribute("data-modal-target");
        const modal = document.getElementById(targetId);

        if (modal) {
            button.addEventListener("click", () => {
                modal.classList.add("modal--visible");
            });
        }
    });

    // Close modal
    document.querySelectorAll(".js-close-modal").forEach(closeEl => {
        closeEl.addEventListener("click", () => {
            const modal = closeEl.closest(".modal");
            modal?.classList.remove("modal--visible");
        });
    });

    // Close modal with escape
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape") {
            document.querySelectorAll(".modal.modal--visible").forEach(modal => {
                modal.classList.remove("modal--visible");
            });
        }
    });

    // Open modal with hash
    if (window.location.hash.startsWith('#')) {
        const modalId = window.location.hash.slice(1);
        const modalToOpen = document.getElementById(modalId);
        if (modalToOpen) {
            setTimeout(() => {
                modalToOpen.classList.add("modal--visible");
                history.replaceState(null, "", window.location.pathname);
            }, 100);
        }
    }
});
