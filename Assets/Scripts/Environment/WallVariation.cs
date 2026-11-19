using UnityEngine;

// 墙面 UV 打散：运行时给每面墙叠加随机的贴图偏移与细微缩放抖动，
// 消除"每面墙贴图完全一致"的复制感（不修改共享材质与网格）。
[DisallowMultipleComponent]
public class WallVariation : MonoBehaviour
{
    [Range(0f, 2f)] public float offsetRadius = 1f;
    [Range(0f, 0.3f)] public float scaleJitter = 0.10f;

    void Awake()
    {
        Apply();
    }

    public int Apply()
    {
        var mpb = new MaterialPropertyBlock();
        int i = 0;
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            string n = r.gameObject.name;
            if (n.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (n.StartsWith("WallStain")) continue;
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty("_MainTex") || m.GetTexture("_MainTex") == null) continue;

            var s = m.GetTextureScale("_MainTex");
            var o = m.GetTextureOffset("_MainTex");

            float a = Hash(i, 1), b = Hash(i, 2), cx = Hash(i, 3), d = Hash(i, 4);
            float jx = 1f + (a - 0.5f) * 2f * scaleJitter;
            float jy = 1f + (b - 0.5f) * 2f * scaleJitter;

            r.GetPropertyBlock(mpb);
            mpb.SetVector("_MainTex_ST", new Vector4(
                s.x * jx, s.y * jy,
                o.x + (cx - 0.5f) * 2f * offsetRadius,
                o.y + (d - 0.5f) * 2f * offsetRadius));
            if (m.HasProperty("_DetailAlbedoMap"))
            {
                var ds = m.GetTextureScale("_DetailAlbedoMap");
                mpb.SetVector("_DetailAlbedoMap_ST", new Vector4(
                    ds.x, ds.y,
                    (cx - 0.5f) * 2f * offsetRadius * 2f,
                    (d - 0.5f) * 2f * offsetRadius * 2f));
            }
            r.SetPropertyBlock(mpb);
            i++;
        }
        return i;
    }

    static float Hash(int i, int k)
    {
        int h = (i * 73856093) ^ (k * 19349663);
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (h & 0x7fffffff) / (float)0x7fffffff;
    }
}
