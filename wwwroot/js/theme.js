// Applies the selected accent palette. The colors themselves live in app.css.

export function applyTheme(themeId) {
    document.documentElement.dataset.theme = themeId;
}
