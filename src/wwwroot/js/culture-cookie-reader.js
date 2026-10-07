// Reads the culture preference cookie for the interactive culture provider.
//
// The cookie is deliberately NOT HttpOnly: an interactive Blazor circuit has no
// HTTP request behind each render, so the only way it can learn the chosen
// language is to read the cookie in the browser. The value is a culture tag, so
// it carries no secret and this exposure costs nothing.
/**
 * Reads and decodes the first cookie with the given name.
 * @param {string} name The cookie name to match exactly.
 * @returns {string|null} The decoded value, or null if the cookie or document is absent.
 * @throws {URIError} If the cookie value contains malformed URI encoding.
 */
export function read(name) {
    if (typeof document === "undefined") {
        return null;
    }

    const prefix = name + "=";
    const segments = document.cookie ? document.cookie.split(";") : [];

    for (let index = 0; index < segments.length; index += 1) {
        const segment = segments[index].trim();

        if (segment.startsWith(prefix)) {
            return decodeURIComponent(segment.substring(prefix.length));
        }
    }

    return null;
}
