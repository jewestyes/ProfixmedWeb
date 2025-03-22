document.addEventListener("DOMContentLoaded", () => {
    const burger = document.getElementById("burger");
    const nav = document.getElementById("nav");

    burger.addEventListener("click", () => {
        burger.classList.toggle("active");
        nav.classList.toggle("open");
    });
});
