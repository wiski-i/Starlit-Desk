using System;

namespace SnowDesk
{
    internal sealed class SnowAction
    {
        internal static readonly SnowAction ClearTrees = new SnowAction("Ağaçları temizle", field => field.ClearTrees());
        internal static readonly SnowAction[] All =
        {
            new SnowAction("Yılbaşı ağacı bırak", field => field.Trigger(Effect.Trees)),
            new SnowAction("Noel Baba yürüsün", field => field.Trigger(Effect.Santa)),
            new SnowAction("Kızağı gönder", field => field.Trigger(Effect.Sleigh)),
            new SnowAction("Kar küreme aracını çağır", field => field.Trigger(Effect.Plow)),
            new SnowAction("Teleferiği çalıştır", field => field.Trigger(Effect.Gondola)),
            new SnowAction("Rüzgâr estir", field => field.Gust()),
            ClearTrees
        };

        internal readonly string Name;
        internal readonly Action<SnowField> Apply;

        private SnowAction(string name, Action<SnowField> apply)
        {
            Name = name;
            Apply = apply;
        }

        public override string ToString() { return Name; }
    }

    /// <summary>One-shot actions of the summer theme, offered in the panel's action card.</summary>
    internal sealed class SummerAction
    {
        internal static readonly SummerAction[] All =
        {
            new SummerAction("Sağanak başlat", scene => scene.SetWeather(true)),
            new SummerAction("Güneşi aç", scene => scene.SetWeather(false)),
            new SummerAction("Simitli kızı çağır", scene => scene.InviteBather()),
            new SummerAction("Su pompasını çağır", scene => scene.CallPump()),
            new SummerAction("Koşan adamı gönder", scene => scene.SendRunner()),
            new SummerAction("Rüzgâr estir", scene => scene.Gust())
        };

        internal readonly string Name;
        internal readonly Action<SummerScene> Apply;

        private SummerAction(string name, Action<SummerScene> apply)
        {
            Name = name;
            Apply = apply;
        }

        public override string ToString() { return Name; }
    }
}
