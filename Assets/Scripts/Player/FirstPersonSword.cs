using UnityEngine;
using UnityEngine.Rendering;
namespace MedusaHorror.Player
{
    /// <summary>复用角色实际剑网格作第一人称近战表现，伤害仍由现有近战系统判定。</summary>
    [DefaultExecutionOrder(200)]
    public class FirstPersonSword : MonoBehaviour
    {
        GameObject _visual;
        Mesh _mesh;
        Material[] _materials;
        MeleeWeapon _melee;
        WeaponLoadout _loadout;
        Camera _camera;
        public bool Visible => _visual != null && _visual.activeSelf;
        void Start() { _melee=GetComponent<MeleeWeapon>();_loadout=GetComponent<WeaponLoadout>(); }
        void LateUpdate()
        {
            if(_camera==null)_camera=Camera.main;
            if(_camera==null||_melee==null||_loadout==null)return;
            bool visible=PlayerViewMode.IsFirstPerson&&_loadout.Current==WeaponKind.Sword&&!InputReader.blocked&&(MedusaHorror.Core.GameFlow.Instance==null||MedusaHorror.Core.GameFlow.Instance.State==MedusaHorror.Core.RunState.Running);
            if(_visual==null && visible)Build();
            if(_visual==null)return;
            _visual.SetActive(visible);if(!visible)return;
            var arms=GetComponent<FirstPersonArms>();
            if(arms!=null&&arms.Ready&&arms.SwordGrip!=null)
                _visual.transform.SetPositionAndRotation(arms.RightHand.position+arms.SwordGrip.rotation*new Vector3(-.025f,0,.06f),arms.SwordGrip.rotation);
            else _visual.SetActive(false);
        }
        void Build()
        {
            Transform source=null;
            foreach(var t in GetComponentsInChildren<Transform>(true))
                if(t.name=="SwordRig"||t.name=="Kazuko_claymore"){source=t;break;}
            if(source==null)return;
            Renderer renderer=null;
            foreach(var candidate in source.GetComponentsInChildren<Renderer>(true))
                if(candidate is MeshRenderer||candidate is SkinnedMeshRenderer){renderer=candidate;break;}
            if(renderer==null)return;
            Mesh original=null;
            if(renderer is SkinnedMeshRenderer skin){_mesh=new Mesh();skin.BakeMesh(_mesh);original=_mesh;}
            else {var filter=renderer.GetComponent<MeshFilter>();if(filter!=null)original=filter.sharedMesh;}
            if(original==null)return;
            if(_mesh==null)_mesh=Instantiate(original);
            // 把最长轴作为剑身，统一为约0.62米，握点位于模型下缘。
            var bounds=_mesh.bounds;var vertices=_mesh.vertices;
            int axis=bounds.size.x>bounds.size.y&&bounds.size.x>bounds.size.z?0:bounds.size.z>bounds.size.y?2:1;
            Quaternion align=axis==0?Quaternion.FromToRotation(Vector3.right,Vector3.up):axis==2?Quaternion.FromToRotation(Vector3.forward,Vector3.up):Quaternion.identity;
            float length=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            if(length<0.001f)return;
            for(int i=0;i<vertices.Length;i++)vertices[i]=align*(vertices[i]-bounds.center)*(0.62f/length)+Vector3.up*0.26f;
            _mesh.vertices=vertices;_mesh.RecalculateNormals();_mesh.RecalculateBounds();
            _visual=new GameObject("FirstPersonSwordVisual",typeof(MeshFilter),typeof(MeshRenderer));_visual.transform.SetParent(transform,false);
            _visual.GetComponent<MeshFilter>().sharedMesh=_mesh;
            _materials=new Material[renderer.sharedMaterials.Length];
            for(int i=0;i<_materials.Length;i++)
            {
                var originalMaterial=renderer.sharedMaterials[i];
                _materials[i]=new Material(Shader.Find("Standard"));
                if(originalMaterial!=null){_materials[i].mainTexture=originalMaterial.mainTexture;_materials[i].color=originalMaterial.HasProperty("_Color")?originalMaterial.color:Color.white;}
                _materials[i].SetFloat("_Metallic",0.65f);_materials[i].SetFloat("_Glossiness",0.65f);
            }
            var mr=_visual.GetComponent<MeshRenderer>();mr.sharedMaterials=_materials;mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
        }
        void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_materials!=null)foreach(var material in _materials)if(material!=null)Destroy(material);}
    }
}
