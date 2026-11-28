using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Gameplay;
namespace MedusaHorror.UI
{
    public static class SceneGuidance
    {
        public static void Enter(Scene scene)
        {
            string title,sub;
            switch(scene.name)
            {
                case "W2_Stronghold":title="荒原堡垒";sub="高墙之内 · 保安队驻地";break;
                case "W0_Base":title="保安队训练场";sub="近战练习 / 战术观察训练";break;
                case "W1_Hospital":title="无名医院";sub="序章 · 苏醒";break;
                case "W2_Wasteland":title="旧村庄";sub="野外探索 · 旧村庄与荒原村道";break;
                case "W3_Subway":title="废弃地铁";sub="探索站台 · 前往列车入口，准备 200 信用点车费";break;
                case "W4_NeonStreet":title="霓虹街区";sub="黑刃工坊 / 义体门诊 · 整备后前往离场星门";break;
                case "W5_AlienGate":title="异星之门";sub="抵达星门控制台 · 按 E 点亮后走入门内";break;
                default:StoryBanner.Clear();return;
            }
            StoryBanner.EnterScene(scene.name,title,sub);
            if(scene.name!="W1_Hospital")return;
            foreach(var pickup in Object.FindObjectsOfType<W1ObjectivePickup>())
            {
                var notice=pickup.GetComponent<ProximityNotice>();if(notice!=null)continue;
                notice=pickup.gameObject.AddComponent<ProximityNotice>();notice.radius=5;
                notice.title=pickup.label.Contains("钥匙")?"值班室":pickup.label.Contains("门禁")?"检查室":pickup.label.Contains("阀门")?"药房":pickup.label.Contains("保险")?"物资库":"手术室";
                notice.description="寻找线索："+pickup.label+"。靠近并用准星对准后，按 E 拾取。";
            }
        }
    }
}
