using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MedusaHorror.Player
{
    /// <summary>复用双臂蒙皮，自制关键帧驱动握点，定长双骨IK负责肩肘与手腕配合。</summary>
    [DefaultExecutionOrder(180)]
    public class FirstPersonArms:MonoBehaviour
    {
        GameObject _root;
        Transform _upperR,_lowerR,_handR,_upperL,_lowerL,_handL;
        MeleeWeapon _melee;WeaponLoadout _loadout;Camera _camera;
        readonly List<Mesh> _meshes=new List<Mesh>();
        readonly List<Material> _materials=new List<Material>();
        Quaternion _rightRotation,_leftRotation;
        FirstPersonMotion _motion;
        float _upperLengthR, _lowerLengthR, _upperLengthL, _lowerLengthL;
        public bool Ready=>_handR!=null && _motion!=null && _motion.Ready;
        public Transform RightHand=>_handR;
        public Transform LeftHand=>_handL;
        public Transform SwordGrip=>_motion!=null?_motion.Right:null;
        public string CurrentClip=>_motion!=null?_motion.CurrentClip:null;
        public float MaxArmLengthError { get; private set; }
        void Start(){_melee=GetComponent<MeleeWeapon>();_loadout=GetComponent<WeaponLoadout>();}
        void LateUpdate()
        {
            if(_camera==null)_camera=Camera.main;if(_camera==null||_melee==null||_loadout==null)return;
            if(_root==null)Build();if(!Ready)return;
            bool visible=PlayerViewMode.IsFirstPerson&&!InputReader.blocked&&(MedusaHorror.Core.GameFlow.Instance==null||MedusaHorror.Core.GameFlow.Instance.State==MedusaHorror.Core.RunState.Running);
            _root.SetActive(visible);if(!visible)return;
            _motion.Evaluate(_camera,Time.deltaTime);
            _root.transform.SetPositionAndRotation(_camera.transform.position,_camera.transform.rotation);
            float stow=_loadout.IsSwitching?_loadout.StowAmount:0;
            Vector3 right=_motion.Right.position,left=_motion.Left.position;
            Quaternion rotation=_motion.Right.rotation;
            if(_loadout.Current!=WeaponKind.Sword)
            {
                var gun=_loadout.Current==WeaponKind.Rifle?_loadout.rifle:_loadout.gun;
                if(gun!=null && gun.GripTarget!=null)
                {
                    // 枪体与手臂共用自制收拔片段，保留原有瞄准/后坐力的基础位置。
                    gun.GripTarget.position+=_camera.transform.TransformVector(_motion.Right.localPosition-new Vector3(.17f,-.31f,.25f));
                    gun.GripTarget.rotation*=_motion.Right.localRotation;
                    right=gun.GripTarget.TransformPoint(new Vector3(0,-.07f,-.07f));
                    left=gun.GripTarget.TransformPoint(new Vector3(-.035f,-.04f,.12f));
                    rotation=gun.GripTarget.rotation;
                }
            }
            Solve(_upperR,_lowerR,_handR,_upperLengthR,_lowerLengthR,new Vector3(.22f,-.30f-stow*.30f,.08f),new Vector3(.50f,-.48f,.18f),right,rotation*_rightRotation);
            Solve(_upperL,_lowerL,_handL,_upperLengthL,_lowerLengthL,new Vector3(-.22f,-.30f-stow*.30f,.08f),new Vector3(-.50f,-.48f,.18f),left,rotation*_leftRotation);
            MaxArmLengthError=Mathf.Max(Mathf.Abs(Vector3.Distance(_upperR.position,_lowerR.position)-_upperLengthR),Mathf.Abs(Vector3.Distance(_lowerR.position,_handR.position)-_lowerLengthR),Mathf.Abs(Vector3.Distance(_upperL.position,_lowerL.position)-_upperLengthL),Mathf.Abs(Vector3.Distance(_lowerL.position,_handL.position)-_lowerLengthL));
        }
        void Solve(Transform upper,Transform lower,Transform hand,float a,float b,Vector3 shoulder,Vector3 pole,Vector3 target,Quaternion rotation)
        {
            // 两骨解析IK只旋转骨骼，保留原有臂长；肘部由外下方的pole约束。
            Vector3 start=_camera.transform.TransformPoint(shoulder);
            // 小幅肩胛前伸补偿跨身挥砍，禁止通过拉长前臂追逐不可达目标。
            Vector3 reach=target-start;float extension=Mathf.Clamp(reach.magnitude-(a+b-.01f),0,.16f);
            start+=reach.normalized*extension;upper.position=start;
            Vector3 delta=target-start;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.002f,a+b-.002f);
            Vector3 axis=delta.sqrMagnitude>.000001f?delta.normalized:_camera.transform.forward;
            Vector3 bend=Vector3.ProjectOnPlane(_camera.transform.TransformPoint(pole)-start,axis).normalized;
            if(bend.sqrMagnitude<.001f)bend=Vector3.ProjectOnPlane(-_camera.transform.up,axis).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            Vector3 elbow=start+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 end=start+axis*distance;
            upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,end-lower.position)*lower.rotation;
            hand.rotation=rotation;
        }
        Transform Bone(Animator animator,HumanBodyBones bone,Dictionary<Transform,Transform> map)
        {var t=animator.GetBoneTransform(bone);return t!=null&&map.ContainsKey(t)?map[t]:null;}
        void Build()
        {
            var animator=GetComponentInChildren<Animator>();if(animator==null||!animator.isHuman)return;
            _root=new GameObject("FirstPersonArms");_root.transform.SetParent(transform,false);
            var map=new Dictionary<Transform,Transform>();Clone(animator.transform,_root.transform,map);
            _upperR=Bone(animator,HumanBodyBones.RightUpperArm,map);_lowerR=Bone(animator,HumanBodyBones.RightLowerArm,map);_handR=Bone(animator,HumanBodyBones.RightHand,map);
            _upperL=Bone(animator,HumanBodyBones.LeftUpperArm,map);_lowerL=Bone(animator,HumanBodyBones.LeftLowerArm,map);_handL=Bone(animator,HumanBodyBones.LeftHand,map);
            if(_handR==null||_handL==null||_upperR==null||_upperL==null||_lowerR==null||_lowerL==null)return;
            _upperLengthR=Vector3.Distance(_upperR.position,_lowerR.position);_lowerLengthR=Vector3.Distance(_lowerR.position,_handR.position);
            _upperLengthL=Vector3.Distance(_upperL.position,_lowerL.position);_lowerLengthL=Vector3.Distance(_lowerL.position,_handL.position);
            _rightRotation=GripFrame(animator,map,true);
            _leftRotation=GripFrame(animator,map,false);
            foreach(bool rightSide in new[]{true,false})
            {
                string prefix=rightSide?"Right":"Left";
                var hand=rightSide?_handR:_handL;
                var middle=Bone(animator,rightSide?HumanBodyBones.RightMiddleProximal:HumanBodyBones.LeftMiddleProximal,map);
                var index=Bone(animator,rightSide?HumanBodyBones.RightIndexProximal:HumanBodyBones.LeftIndexProximal,map);
                var little=Bone(animator,rightSide?HumanBodyBones.RightLittleProximal:HumanBodyBones.LeftLittleProximal,map);
                Vector3 normal=Vector3.Cross(middle.position-hand.position,index.position-little.position).normalized*(rightSide?1:-1);
                foreach(string finger in new[]{"Index","Middle","Ring","Little","Thumb"})
                foreach(string segment in new[]{"Distal","Intermediate","Proximal"})
                {
                    if(!System.Enum.TryParse<HumanBodyBones>(prefix+finger+segment,out var id))continue;
                    var bone=Bone(animator,id,map);if(bone==null||bone.childCount==0)continue;
                    Vector3 axis=Vector3.Cross(bone.GetChild(0).position-bone.position,normal).normalized;
                    float angle=finger=="Thumb"?25:segment=="Proximal"?65:segment=="Intermediate"?80:55;
                    bone.rotation=Quaternion.AngleAxis(angle,axis)*bone.rotation;
                }
            }
            _motion=new FirstPersonMotion(transform,_melee,_loadout);
            var armBones=new HashSet<Transform>();
            foreach(var root in new[]{animator.GetBoneTransform(HumanBodyBones.RightUpperArm),animator.GetBoneTransform(HumanBodyBones.LeftUpperArm)})
                if(root!=null)foreach(var b in root.GetComponentsInChildren<Transform>())armBones.Add(b);
            foreach(var source in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                string name=source.name.ToLowerInvariant();
                if(name.Contains("claymore")||name.Contains("sword")||name.Contains("weapon")||name.Contains("hair"))continue;
                var original=source.sharedMesh;if(original==null||original.boneWeights.Length==0)continue;
                var visibleBones=armBones;
                if(name.Contains("armor"))
                {
                    // 仅显示前臂护甲，肩甲尖刺不进入第一人称镜头。
                    visibleBones=new HashSet<Transform>();
                    foreach(var elbow in new[]{animator.GetBoneTransform(HumanBodyBones.RightLowerArm),animator.GetBoneTransform(HumanBodyBones.LeftLowerArm)})
                        if(elbow!=null)foreach(var b in elbow.GetComponentsInChildren<Transform>())visibleBones.Add(b);
                }
                var weights=original.boneWeights;var allowed=new bool[weights.Length];
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i];
                    w.weight0=Weight(w.boneIndex0,w.weight0);w.weight1=Weight(w.boneIndex1,w.weight1);
                    w.weight2=Weight(w.boneIndex2,w.weight2);w.weight3=Weight(w.boneIndex3,w.weight3);
                    float total=w.weight0+w.weight1+w.weight2+w.weight3;allowed[i]=total>0.7f;
                    // 独立双臂不能残留胸背骨骼权重，否则肩部移动后会拉出长条蒙皮。
                    if(allowed[i]){w.weight0/=total;w.weight1/=total;w.weight2/=total;w.weight3/=total;weights[i]=w;}
                }
                var mesh=Instantiate(original);mesh.boneWeights=weights;bool any=false;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var indices=original.GetTriangles(sub);var kept=new List<int>();
                    for(int i=0;i<indices.Length;i+=3)if(allowed[indices[i]]&&allowed[indices[i+1]]&&allowed[indices[i+2]]){kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]);}
                    mesh.SetTriangles(kept,sub);any|=kept.Count>0;
                }
                if(!any){Destroy(mesh);continue;}
                _meshes.Add(mesh);var go=new GameObject("ArmsMesh",typeof(SkinnedMeshRenderer));go.transform.SetParent(map[source.transform],false);
                var renderer=go.GetComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;
                var materials=new Material[source.sharedMaterials.Length];
                for(int i=0;i<materials.Length;i++)
                {
                    var originalMaterial=source.sharedMaterials[i];var material=new Material(Shader.Find("Standard"));
                    if(originalMaterial!=null){material.mainTexture=originalMaterial.mainTexture;material.color=originalMaterial.HasProperty("_Color")?originalMaterial.color:Color.white;}
                    material.SetFloat("_Glossiness",0.25f);materials[i]=material;_materials.Add(material);
                }
                renderer.sharedMaterials=materials;
                var bones=new Transform[source.bones.Length];for(int i=0;i<bones.Length;i++)if(source.bones[i]!=null)map.TryGetValue(source.bones[i],out bones[i]);
                renderer.bones=bones;if(source.rootBone!=null)renderer.rootBone=map[source.rootBone];renderer.updateWhenOffscreen=true;renderer.shadowCastingMode=ShadowCastingMode.Off;
                float Weight(int index,float weight)=>index>=0&&index<source.bones.Length&&visibleBones.Contains(source.bones[index])?weight:0;
            }
        }
        Quaternion GripFrame(Animator animator,Dictionary<Transform,Transform> map,bool right)
        {
            var hand=right?_handR:_handL;
            var middle=Bone(animator,right?HumanBodyBones.RightMiddleProximal:HumanBodyBones.LeftMiddleProximal,map);
            var index=Bone(animator,right?HumanBodyBones.RightIndexProximal:HumanBodyBones.LeftIndexProximal,map);
            var little=Bone(animator,right?HumanBodyBones.RightLittleProximal:HumanBodyBones.LeftLittleProximal,map);
            Vector3 forward=(middle.position-hand.position).normalized;
            Vector3 normal=Vector3.Cross(forward,index.position-little.position).normalized*(right?1:-1);
            Quaternion localFrame=Quaternion.Inverse(hand.rotation)*Quaternion.LookRotation(forward,normal);
            return Quaternion.LookRotation(Vector3.forward,right?Vector3.left:Vector3.right)*Quaternion.Inverse(localFrame);
        }
        static void Clone(Transform source,Transform parent,Dictionary<Transform,Transform> map)
        {
            var t=new GameObject(source.name).transform;t.SetParent(parent,false);t.localPosition=source.localPosition;t.localRotation=source.localRotation;t.localScale=source.localScale;map[source]=t;
            foreach(Transform child in source)Clone(child,t,map);
        }
        void OnDestroy(){_motion?.Dispose();if(_root!=null)Destroy(_root);foreach(var mesh in _meshes)if(mesh!=null)Destroy(mesh);foreach(var material in _materials)if(material!=null)Destroy(material);}
    }
}
