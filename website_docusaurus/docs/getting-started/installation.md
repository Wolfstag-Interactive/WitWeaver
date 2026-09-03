---
sidebar_position: 1
title: Installation
---

# Installation

This page walks you through installing WitWeaver™ into a Unity project via the Unity Package Manager, verifying the installation, and completing first-time setup.

---

## Requirements

Before installing, make sure your project meets the following requirements:

| Requirement | Minimum version |
|---|---|
| Unity | 2021.3 LTS |
| .NET Standard | 2.1 |
| Other packages | None required for the core feature set |

WitWeaver ships with its YAML parser (YamlDotNet) bundled inside the package, so you do not need to install any other Unity packages to get started. Delivering dialogue as DLC or remote content works through your existing content pipeline (see the bottom of this page).

---

## Install via Unity Package Manager

The Unity Package Manager (UPM) is the standard way to install WitWeaver. It keeps the package files separate from your project's `Assets/` folder and makes future updates straightforward.

:::note
The **Unity Package Manager** is a built-in Unity tool for installing reusable code packages - think of it like an app store for Unity project features. Packages live outside your `Assets/` folder so they don't clutter your project, and you can add, update, or remove them without manually copying files.
:::

**Steps:**

1. Open Unity and go to **Window → Package Manager**. The Package Manager window will open.

2. Find **WitWeaver** in the package list and click **Install**.

:::tip
If WitWeaver does not appear in the list, make sure your Package Manager is pointed at the correct registry. Check the registry configuration in **Edit → Project Settings → Package Manager**.
:::

---

## Verify the installation

After the import finishes:

1. Look at the Unity menu bar along the top of the editor. A **WitWeaver** menu entry should appear.

2. Open the Unity **Console** (Window → General → Console) and confirm there are no errors related to WitWeaver.

If the WitWeaver menu does not appear, check the Console for compilation errors. The most common causes are:

- A .NET compatibility mismatch (ensure your project is set to .NET Standard 2.1 in **Edit → Project Settings → Player → Other Settings → Api Compatibility Level**).
- A corrupted package download - try removing and re-adding the package via the Package Manager.

---

## First-time setup

Once the package is installed, complete this two-minute setup before creating your first conversation.

**Step 1: Open WitWeaver Settings**

Go to **Tools → WitWeaver → Open Settings** in the menu bar.

If no settings asset exists yet, WitWeaver will automatically create one at:

```
Assets/Resources/WitWeaverSettings.asset
```

The Inspector will open showing the settings asset.

:::note
**What is a ScriptableObject?** A ScriptableObject is a reusable data container that Unity stores as an asset file in your project, like a config file you can edit in the Inspector and share across multiple scenes. `WitWeaverSettings` is a ScriptableObject that holds global configuration for the framework (supported languages, default fallback behavior, etc.). You edit it once and it applies everywhere.
:::

**Step 2: Add a supported language**

In the `WitWeaverSettings` Inspector, find the **Supported Languages** list and add at least one language code. For English, type `EN`. This code must match the language keys you'll write in your YAML dialogue files.

You can add more languages at any time (e.g., `FR`, `DE`, `ES`, `JA`). WitWeaver's language manager uses these codes to look up the correct line of dialogue at runtime.

:::tip
Use short, uppercase ISO 639-1 codes (`EN`, `FR`, `DE`) to stay consistent with the YAML examples throughout this documentation.
:::

---

## Shipping dialogue as Addressables (optional)

WitWeaver needs no special setup to work with Unity Addressables or asset bundles. Dialogue YAML is embedded inside each Conversation Data asset, so marking the `WitWeaverConversationData` asset as Addressable is all it takes: the embedded YAML travels with the asset, and no scripting defines or loader configuration are required.

See [YAML Loading](../yaml-reference/yaml-loading#shipping-conversations-as-dlc-or-remote-content) for the full DLC and remote-content workflow, and for the persistent override mechanism used to hotfix dialogue text post-ship.

---

## Next steps

With WitWeaver installed and configured, you're ready to create your first conversation.

- [Quick Start →](./quick-start) - Build a working "Hello World" conversation in under ten minutes.
- [Project Structure →](./project-structure) - Understand where to put your files and how the package is organized.
