using UnityEngine;
using UnityEngine.UI;
namespace MedusaHorror.UI
{
    /// <summary>主菜单、暂停和死亡共用现有Suriyun面板及按钮素材。</summary>
    public static class MenuTheme
    {
        public static void Apply(RectTransform root)
        {
            if(root==null)return;
            var bg=root.GetComponent<Image>();if(bg!=null)bg.color=new Color(0.025f,0.045f,0.055f,0.98f);
            UiSkin.Apply(root,false);
            foreach(var button in root.GetComponentsInChildren<Button>(true))
            {
                var image=button.GetComponent<Image>();if(image!=null){image.sprite=UiAtlas.Frame;image.type=Image.Type.Sliced;image.color=new Color(0.18f,0.35f,0.4f,0.95f);}
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(0.65f,1,1);colors.pressedColor=new Color(0.35f,0.7f,0.75f);button.colors=colors;
            }
            foreach(var text in root.GetComponentsInChildren<Text>(true))text.font=CjkFont.Get();
        }
    }
}
