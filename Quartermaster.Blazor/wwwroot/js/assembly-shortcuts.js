// Keyboard shortcuts of the assembly console; ignored while the focus is in a form field.
window.qmAssemblyShortcuts = {
    register: function (dotnetRef) {
        const handler = function (e) {
            const target = e.target;
            const tag = target && target.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || (target && target.isContentEditable))
                return;
            if (e.ctrlKey || e.altKey || e.metaKey)
                return;

            let action = null;
            if (e.code === 'Space')
                action = 'give_floor';
            else if (e.key === 'f' || e.key === 'F')
                action = 'done';
            else if (e.key === '+')
                action = 'add_minute';
            if (action === null)
                return;

            e.preventDefault();
            dotnetRef.invokeMethodAsync('OnShortcut', action);
        };
        document.addEventListener('keydown', handler);
        return {
            dispose: function () {
                document.removeEventListener('keydown', handler);
            }
        };
    }
};
