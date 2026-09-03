# WitWeaver™
WitWeaver™ is a Unity dialogue and conversation framework built for scalable multi character conversations.
It is designed as a tooling product rather than a game specific system, with a strong focus on editor usability, data driven workflows, and long term maintainability.

Dialogue is authored in YAML as the single source of truth and compiled into ScriptableObjects for runtime and editor use. This avoids manual synchronization while keeping runtime logic lightweight and predictable.

WitWeaver supports conversations ranging from a few lines to several hundred, with multiple simultaneous participants, extensible character representations, expression systems, and hookable pre line and post line behavior via ScriptableObjects. All systems are modular and replaceable, allowing developers to extend or remove subsystems without breaking the core runner.

The framework makes no assumptions about UI layout, rendering pipeline, camera setup, or game genre. Sample implementations are provided, but all presentation and input layers are fully replaceable.

## License and trademarks

Copyright © 2025-2026 Wolfstag Interactive LLC. All rights reserved. WitWeaver is proprietary software distributed under the terms in [LICENSE.md](WolfstagInteractive/WitWeaver/LICENSE.md).

WitWeaver™ is a trademark of Wolfstag Interactive LLC. Unity is a trademark or registered trademark of Unity Technologies. FMOD is a trademark of Firelight Technologies Pty Ltd. Wwise is a trademark of Audiokinetic Inc. All other product names are trademarks of their respective owners.
