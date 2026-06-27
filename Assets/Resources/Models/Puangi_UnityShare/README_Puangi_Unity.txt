Puangi Unity animation share package

Files
- Puangi_Walk_Polished.fbx
- Puangi_Walk_Polished.fbx.meta
- Puangi_Diffuse_1024.png
- Puangi_Diffuse_1024.png.meta
- Puangi_Walk_Polished_AnimatorOnModelRoot.anim
- Puangi_Walk_Polished_AnimatorOnArmature.anim

Recommended setup
1. Put this whole folder under Unity's Assets folder.
2. Use Puangi_Walk_Polished.fbx as the character model.
3. If the Animator component is on the imported model root object, use:
   Puangi_Walk_Polished_AnimatorOnModelRoot.anim
4. If the Animator component is directly on the SkeletonBindArmature object, use:
   Puangi_Walk_Polished_AnimatorOnArmature.anim

Why there were Missing bindings
- This dragon mascot uses a Generic rig, not a Humanoid avatar.
- Generic .anim files are matched by exact transform path and bone names.
- If the Animator is placed on a different hierarchy level, Unity shows Missing.

Avatar
- There is no separate .avatar file.
- Unity creates the Avatar as a sub-asset when it imports Puangi_Walk_Polished.fbx.
- Keep Puangi_Walk_Polished.fbx.meta next to the FBX so Unity imports it as a Generic rig with an Avatar from this model.
- In Unity, check the FBX Inspector > Rig tab:
  Animation Type = Generic
  Avatar Definition = Create From This Model

Notes
- The scale curves were removed to avoid the model being squashed or enlarged.
- Use the FBX and the matching .anim together. Sending only the .anim is not safe.
