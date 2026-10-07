// Reads the culture preference cookie for the interactive culture provider.
//
// The cookie is deliberately NOT HttpOnly: an interactive Blazor circuit has no
// HTTP request behind each render, so the only way it can learn the chosen
// language is to read the cookie in the browser. The value is a culture tag, so
// it carries no secret and this exposure costs nothing.
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
