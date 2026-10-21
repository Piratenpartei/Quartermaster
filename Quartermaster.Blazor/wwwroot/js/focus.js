window.qmFocus = {
    // Focuses the first input inside the element with the given id, e.g. a picker component without its own reference.
    firstInput: function (containerId) {
        const container = document.getElementById(containerId);
        const input = container ? container.querySelector("input, select, textarea") : null;
        if (input) {
            input.focus();
        }
    }
};
