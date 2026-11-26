"""Reapply Unity 2022 compatibility fixes after importing local vendor assets."""
from pathlib import Path
import re
ROOT = Path(__file__).resolve().parents[1]
changed = []
def edit(relative, transform):
    path = ROOT / relative
    if not path.exists():
        print('SKIP missing:', relative)
        return
    raw = path.read_bytes()
    text = raw.decode('utf-8-sig')
    updated = transform(text)
    if updated != text:
        path.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'') + updated.encode('utf-8'))
        changed.append(relative)
def image_effects(text):
    # supportsImageEffects is always true in Unity 2022.
    text = re.sub(r'(?m)^[ \t]*// Disable if we don.t support image effects\r?\n', '', text)
    text = re.sub(r'(?m)^[ \t]*if\s*\(\s*!SystemInfo\.supportsImageEffects\s*\)\s*\{\s*(?:enabled\s*=\s*false;\s*return;|NotSupported\s*\(\s*\);\s*return false;)\s*\}\s*', '', text)
    text = text.replace('!SystemInfo.supportsImageEffects || ', '')
    # MarkRestoreExpected has no effect; RT content handling stays unchanged.
    text = re.sub(r'(?m)^[ \t]*[\w.]+\.MarkRestoreExpected\(\);[^\r\n]*(?:\r?\n|$)', '', text)
    text = re.sub(r'(?m)^if\s', '            if ', text)
    return re.sub(r'(?m)^// Disable if the shader', '            // Disable if the shader', text)
for folder in ['Assets/Space/Standard Assets/Effects/ImageEffects/Scripts', 'Assets/Standard Assets']:
    for path in (ROOT / folder).rglob('*.cs'):
        edit(path.relative_to(ROOT).as_posix(), image_effects)
edit('Assets/Space/Scripts/Enemy.cs', lambda s: s.replace('agent.Stop();', 'agent.isStopped = true;').replace('agent.Resume();', 'agent.isStopped = false;'))
edit('Assets/Space/Scripts/SpaceShipControl.cs', lambda s: s.replace('engineTrace.startLifetime = 0.2f + ThrottleInput / 2;', 'var engineMain = engineTrace.main;\n                    engineMain.startLifetime = 0.2f + ThrottleInput / 2;'))
def vr_input(s):
    # Use the inherited helpers, rather than copies of the Unity 4.6 implementation.
    return re.sub(r'\s*// NEXT TWO FUNCTIONS.*?\s*// This is the real function', '\n\t// This is the real function', s, flags=re.S)
edit('Assets/Space/Scripts/VRInputModule.cs', vr_input)
# This rig already subscribes to EditorApplication.update, including target changes.
edit('Assets/Space/Standard Assets/CrossPlatformInput/Scripts/MobileControlRig.cs', lambda s: re.sub(r'(?m)^[ \t]*EditorUserBuildSettings\.activeBuildTargetChanged [+-]= Update;\r?\n', '', s))
edit('Assets/beffio/The Hunt/Content/Plugins/VolumetricLighting-master/LightOverride/LightOverride.cs', lambda s: s.replace('new public Light light', 'public Light WrappedLight').replace('public Light light', 'public Light WrappedLight'))
edit('Assets/beffio/The Hunt/Content/Plugins/VolumetricLighting-master/VolumetricFog/Scripts/VolumetricFog.cs', lambda s: s.replace('fl.light', 'fl.WrappedLight').replace('m_DirectionalLight.light', 'm_DirectionalLight.WrappedLight'))
def compass(s):
    s = re.sub(r'public (?:new )?GameObject camera;', '[UnityEngine.Serialization.FormerlySerializedAs("camera")]\n    public GameObject followCamera;', s)
    return s.replace('if (camera!=null)', 'if (followCamera!=null)').replace('-camera.transform', '-followCamera.transform')
edit('Assets/beffio/The Hunt/Content/Scripts/Compass.cs', compass)
edit('Assets/Dark City 2/Assets/Source/Shaders/Windows.shader', lambda s: s.replace('projNormal /= projNormal.x + projNormal.y + projNormal.z;', '''// Use a valid top projection for degenerate normals in unused passes.
            float normalSum = projNormal.x + projNormal.y + projNormal.z;
            projNormal = normalSum > 1e-5 ? projNormal / max(normalSum, 1e-5) : float3(0, 1, 0);'''))
for relative in changed:
    print('FIXED', relative)
print('Changed', len(changed), 'files')
