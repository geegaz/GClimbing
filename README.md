# Geegaz's Climbing System
Another climbing system for VRChat, made with [UdonSharp](https://udonsharp.docs.vrchat.com/)

## Features

- Configurable climbing
- Configurable walljump
- Highlight material & shader using [z3y's Graphlit](https://github.com/z3y/Graphlit)
- VR & Desktop compatible

### Limitations
- No stamina system
- No mantling (getting over ledges) without walljump
- Can only grab with one hand at a time

## Installation

**The highlight shader needs [z3y's Graphlit](https://github.com/z3y/Graphlit) to work properly ! Make sure you have it installed first, and add the material to the Climbing System prefab.**

Download the repository, then **unpack it in the Assets folder** of your Unity project.
You must have **UdonSharp** installed in your project for this package to work.

UdonSharp has been integrated into the Worlds SDK - if it's not available in your project, check out their documentation for the installation steps: https://udonsharp.docs.vrchat.com/setup

## Prefabs

Name | Description | Path
---|---|---
**Climbing System** | Climbing system already set up and ready to use | [![](.img/Folder_Icon.png) ```/Climbing System```](./)

## How to Use

### Setup
1. In your Project Settings, in the Tags & Layers tab, **set the layer 23 to `Climbing`**
2. Drag the Climbing System prefab in the scene
3. Set the layer of any collider you want to climb to `Climbing`
4. *(If using the highlight)* Add `mat_effect_climbingHighlight` to the Climbing System prefab in the Climbing Highlight Material field
5. *(If using the highlight)* Add `mat_effect_climbingHighlight` as an additional material to the MeshRenderer of any climable object in your scene

### Climbing
**Desktop**
- Left click to grab, right click to let go *(grab distance & speed configurable)*
- Hold left click to move your body while grabbing *(distance configurable)*
- Space while grabbing to walljump *(jump strength configurable)*

**VR**
- Hold left/right grip to grab, release to let go *(grab button & radius configurable)*
- Move your hand fast & release to fling yourself *(max fling speed configurable)*
- Jump button while grabbing to walljump *(jump strength configurable)*

*Visit my [procedural climbing world](https://vrchat.com/home/launch?worldId=wrld_5ba11740-e5b8-41cb-a38d-756d49fb4e14) to try it !*