// Minimal Node.js process polyfill for browser-loaded Yjs and friends.
// Yjs uses `process.env.NODE_ENV` for dev/prod detection; nothing else.
// If other libraries ever pull on more of the process API, this stub can grow.

const process = {
    env: {},
    nextTick: (cb, ...args) => queueMicrotask(() => cb(...args)),
    platform: "browser",
    versions: {},
};

export default process;
export const env = process.env;
export const nextTick = process.nextTick;
export const platform = process.platform;
export const versions = process.versions;
