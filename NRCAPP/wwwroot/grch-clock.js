window.grchClock = {
    intervalId: null,
    start(elementId) {
        const element = document.getElementById(elementId);

        if (!element) {
            return;
        }

        const formatter = new Intl.DateTimeFormat(undefined, {
            weekday: "short",
            year: "numeric",
            month: "short",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit"
        });

        const update = () => {
            element.textContent = formatter.format(new Date());
        };

        update();
        window.clearInterval(window.grchClock.intervalId);
        window.grchClock.intervalId = window.setInterval(update, 1000);
    }
};
