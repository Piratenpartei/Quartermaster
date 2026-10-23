window.qmDropdown = {
    // Arrow keys would otherwise move the caret to the start or end of the search text while the user walks the list.
    attach: function (input) {
        input.addEventListener("keydown", function (e) {
            if (input.dataset.dropdownOpen !== "true") {
                return;
            }
            if (e.key === "ArrowDown" || e.key === "ArrowUp" || e.key === "Enter") {
                e.preventDefault();
            }
        });
    },

    scrollToHighlighted: function (list) {
        const highlighted = list ? list.querySelector("[aria-selected='true']") : null;
        if (highlighted) {
            highlighted.scrollIntoView({ block: "nearest" });
        }
    }
};
