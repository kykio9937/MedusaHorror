using UnityEngine;
namespace MedusaHorror.UI
{
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public class WorldSignText : MonoBehaviour
    {
        Material _material;
        void OnEnable()
        {
            var shader=Shader.Find("Medusa/WorldSignText");
            if(shader==null)return;
            _material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            GetComponent<MeshRenderer>().sharedMaterial=_material;
        }
        void OnWillRenderObject()
        {
            var text=GetComponent<TextMesh>();
            if(_material!=null&&text.font!=null)_material.mainTexture=text.font.material.mainTexture;
        }
        void OnDisable()
        {
            if(_material==null)return;
            if(Application.isPlaying)Destroy(_material);else DestroyImmediate(_material);
            _material=null;
        }
    }
}
