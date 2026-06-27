# Puangi Character Asset

Unity-ready Puangi mascot character asset.

## Files
- `../Models/Puangi_Walk_Polished.fbx`: rigged Unity FBX with walk animation.
- `../Source/Puangi_Walk_Polished.blend`: compressed Blender source file.
- `../Textures/Puangi_Diffuse_1024.png`: optimized 1024px diffuse texture.
- `../Preview/Puangi_Walk_Polished_Preview.png`: preview render.

## Animation
- Clip/action name: `Walk_Polished_HeadSway_ArmSwing`
- Frames: `1-48`
- Loop duplicate key: `49`
- Motion style: in-place walk, natural arm swing, subtle head sway.

## Optimization Notes
- Source texture was resized from 2048px to 1024px for repository and runtime size.
- Blender source was saved with `compress=True`.
- The FBX keeps the rig, skin weights, and baked animation for Unity import.
