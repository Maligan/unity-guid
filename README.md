# GUID Component & Reference
Unity GUID based cross scene reference package. This package is alternate to official Unity-Technologies' implementation:

- https://github.com/Unity-Technologies/guid-based-reference

## How To
##### 1. Add `GUIDComponent` to GameObject
##### 2. Add `GUIDReference` field to your script
```cs
[SerializeField]
private GUIDReference m_Reference;
```
##### 3. Resolve at runtime
```cs
// Resolve via GUIDReference
var component = m_Reference.GetComponent<GUIDComponent>();

// Resolve via GUIDComponent
var component = GUIDComponent.Find("OBJECT-GUID-HERE");
```

## Features

- **Safe** - `GUIDComponent` ignores component *resets* and prefab *reverts*. You will never lose your generated GUIDs. The only way to erase existing GUID is manually component removing.

- **Easy** - GUIDs are generated automatically and respect object duplication.

- **Handy** - Open/Close referenced scenes from property context menu or with double-click.

- **Neat** - `GUIDReference` property drawer looks like common object field.
No more ugly hand made multiline properties.

## Installation

```sh
openupm add com.maligan.guid
```