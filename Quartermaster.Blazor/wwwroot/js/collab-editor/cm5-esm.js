// ESM wrapper around the UMD-distributed CodeMirror 5.
// CM5 is loaded as a regular <script> tag before this module runs, which sets
// window.CodeMirror. This wrapper re-exports that global as a default ESM export
// so `import CodeMirror from "codemirror"` works for y-codemirror and other
// Yjs bindings that expect the ESM shape.

const CodeMirror = globalThis.CodeMirror;
if (!CodeMirror) {
    throw new Error(
        "cm5-esm.js: window.CodeMirror is not defined. " +
        "Make sure cm5.js is loaded as a regular <script> tag BEFORE any ES module that imports from 'codemirror'."
    );
}

export default CodeMirror;
