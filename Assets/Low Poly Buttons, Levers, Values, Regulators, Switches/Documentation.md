# User Guide — Switches & Rotators (Unity Prefabs)

This package contains two prefab types: **Switches** (buttons / levers / toggles) and **Rotators** (valves / handwheels), plus an optional **Passcode / Keypad** that can unlock a switch.

---

## Switches (buttons, levers, toggles)

**Inside the prefab:** `PressController` + clickable parts with `HandleClick`.
Clicks are handled via `OnMouseDown`, so each clickable part must have an enabled `Collider`.

### 1) Operating mode (PressController)
- **Mode**
  - **Momentary** — pulse (press/release): uses `triggerNameB1` / `triggerNameB2`.
  - **Toggle** — latched state: uses `boolNameB1` / `boolNameB2`.
- **twoWayExclusive** — makes sides A/B mutually exclusive.
- **locked** — completely blocks interaction.

### 2) Clicks (HandleClick)
Each clickable part has `HandleClick` with `handleIndex`:
`1` = first side/button, `2` = second. The component auto-finds the nearest parent `PressController`. Active `Collider` is required.

### 3) Animation (if you use an Animator)
In `PressController`, set Animator parameter names: `boolNameB1`/`boolNameB2` (for Toggle) and/or `triggerNameB1`/`triggerNameB2` (for Momentary).

### 4) Events
Hook up UnityEvents in the Inspector: `OnPress1` / `OnRelease1` (and `OnPress2` / `OnRelease2` if you use the second side).
Example: turn a light **on** in `OnPress1`, **off** in `OnRelease1`.

---

## Rotators (valves, handwheels)

**Inside the prefab:** `RotateController`.
Requires a `Collider` on the object and a camera tagged **MainCamera** (used for angle calculation).

### 1) Geometry & travel
- **handle** — which `Transform` to rotate (defaults to this object).
- **rotationAxis** — rotation axis (usually **Y** or **Z**).
- **maxAngle** — working range in degrees (e.g., 270 for a regulator, 360 for a wheel).
- **startNormalized** — starting position (0..1).
- **invertDirection** — invert rotation if needed.

### 2) Dynamics
- **maxSpeed** — caps rotation speed/smoothness (°/s).
- **locked** — fully blocks rotation.

### 3) Events
- `OnValueChanged(float t)` — normalized value (0..1) for logic/animations.
- `OnFullyOpened` / `OnFullyClosed` — fire at range ends.

---

## Passcode / Keypad (Code Lock)

Use this to unlock a switch (e.g., a door handle) with a 4‑digit code.

### Setup (on the keypad root with `CodeLock3D`)
- **correctCode** — a 4‑digit string (e.g., `"1234"`).
- **targetButton** — `PressController` to unlock after a correct code (`locked` will be set to `false`).
- **maxAttempts** — wrong tries before long lockout.
- **buttons** — `Transform` list of all keys in the order **1..9** (digit is inferred by index; **0** is not used in this implementation).
- **enteredCodeText** — assign a `TMP_Text` for input/feedback.

### Each key object
Add `LockButton` and drag the keypad (`CodeLock3D`) into its `codeLock` field. Make sure the key has an enabled `Collider`.

### Built-in UX
- After 4 digits, the code is auto-checked.
- **Correct**: shows **“Access”** (green) for ~3s and unlocks the `targetButton`.
- **Wrong**: shows **“XXXX”** (red) for ~3s and counts an attempt. After `maxAttempts`, shows **“BLOCKED”** (red) for a longer time; keys are disabled during messages.

**Tip:** Keep the `buttons` list in the same order as the labels printed on the keypad.

---

## Quick wiring examples
- **Switch → Animator**: Toggle → bind `boolNameB1`/`boolNameB2`; Momentary → use `triggerNameB1`/`triggerNameB2`.
- **Switch → Logic**: turn something on in `OnPress1`, off in `OnRelease1`.
- **Rotator → Animator**: `OnValueChanged(t)` → `Animator.SetFloat("MyParam", t)`.

---

## Scene checklist
- Clickable parts **have a Collider** (both switches and rotators).
- There is a camera tagged **MainCamera** in the scene (for rotators).
- In `PressController` you chose the proper **Mode**, set Animator parameter names, and enabled **twoWayExclusive** if needed.
- Set **locked** on the relevant controller to temporarily lock an element.

© 2025. Support / Contact: (add your email or URL)
