using UnityEngine;
namespace MedusaHorror.UI
{
    public sealed class SceneNoticeTheme
    {
        public string key,category;
        public Color accent;
        public Sprite Background => Resources.Load<Sprite>("UI/SceneNotices/"+key);
        public static SceneNoticeTheme For(string scene)
        {
            switch(scene)
            {
                case "W1_Hospital":return Make("Hospital","病区 / 探索记录",new Color(0.53f,0.78f,0.71f));
                case "W2_Stronghold":return Make("Wasteland","荒原堡垒 / 安全区",new Color(0.76f,0.69f,0.48f));
                case "W2_Wasteland":return Make("Wasteland","荒原 / 生存记录",new Color(0.8f,0.65f,0.4f));
                case "W3_Subway":return Make("Subway","站务系统 / 旅途信息",new Color(0.89f,0.72f,0.27f));
                case "W4_NeonStreet":return Make("Neon","霓虹网络 / 城市终端",new Color(0.48f,0.8f,0.94f));
                case "W5_AlienGate":return Make("Alien","异星接入 / 信号解析",new Color(0.68f,0.57f,0.97f));
                default:return Make("Base","保安队 / 训练记录",new Color(0.55f,0.73f,0.81f));
            }
        }
        static SceneNoticeTheme Make(string key,string category,Color accent){return new SceneNoticeTheme{key=key,category=category,accent=accent};}
    }
}
