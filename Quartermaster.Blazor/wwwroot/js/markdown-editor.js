function MarkdownEditorInsertAtCursor(textarea, text) {
    if (!textarea || typeof text !== "string") {
        return;
    }
    const start = textarea.selectionStart ?? textarea.value.length;
    const end = textarea.selectionEnd ?? textarea.value.length;
    const before = textarea.value.substring(0, start);
    const after = textarea.value.substring(end);
    textarea.value = before + text + after;
    const newPos = start + text.length;
    textarea.setSelectionRange(newPos, newPos);
    textarea.focus();
    textarea.dispatchEvent(new Event("input", { bubbles: true }));
}
