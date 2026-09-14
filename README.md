# Editor Unicode Text Fix

Makes unsupported / complex languages render correctly inside the **Unity Editor**.
This relies on Unity's new Advanced Text Generator and targets **Unity 6000.5+**.

> **Disclaimer:** This does not affect in-game / runtime text, I recommend using [UniText](http://github.com/LightSideKittens/UniText/) or other solutions for that. This may also work on Unity 6000.3+, but complex text layout (shaping) may not work as expected.

| Before | After |
|---|---|
| <img width="640" height="360" alt="Before" src="https://github.com/user-attachments/assets/85765c7e-f03e-4fdd-bcf0-ca81f02413bf" /> | <img width="640" height="360" alt="After" src="https://github.com/user-attachments/assets/232a9650-e88e-4168-bead-bd84dc91e216" /> |

## Features

- Scans installed system fonts and adds the best match as an Editor fallback for any language Unity can't normally render.
- Automatically fixes shaping for languages with Complex Text Layouts (CTL) like Khmer, Lao, Burmese, etc.
- Supports third-party plugins and custom inspectors (may require [Compatibility Refresh](#usage) to be enabled to fix text shaping).

## Requirements

- Unity **6000.5** or newer (*may also partially work* with Unity **6000.3+**)
- Installed system fonts that support the target languages

## Install

1. Open the Package Manager from Window > Package Manager
2. Click the "+" button > Add package from git URL
3. Enter the following URL:

```
https://github.com/Annswwr/EditorUnicodeTextFix.git
```

Alternatively, you can also just download the whole repo and paste it into your `Assets` or `Packages` folder.

## Usage

Should work automatically once installed.

- Toggle it **On/Off** via `Tools -> EditorUnicodeTextFix -> Enabled`.
- Toggle **Compatibility Refresh** via `Tools -> EditorUnicodeTextFix -> Compatibility Refresh` (off by default), which can fix shaping issues in certain third-party plugins and custom inspectors.
