using System;

namespace SnowDesk
{
    internal sealed class SpaceAction
    {
        internal static readonly SpaceAction[] All =
        {
            new SpaceAction("Uyduyu gönder", scene => scene.SendSatellite()),
            new SpaceAction("Meteor geçir", scene => scene.SendMeteor()),
            new SpaceAction("Aurorayı göster", scene => scene.ShowAurora())
        };

        internal readonly string Name;
        internal readonly Action<SpaceScene> Apply;

        private SpaceAction(string name, Action<SpaceScene> apply)
        {
            Name = name;
            Apply = apply;
        }

        public override string ToString() { return Name; }
    }

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

    /// <summary>One-shot actions of the autumn and spring themes; commands go to the season's cast.</summary>
    internal sealed class SeasonAction
    {
        internal static readonly SeasonAction[] Autumn =
        {
            new SeasonAction("Rüzgâr estir", scene => scene.Gust()),
            new SeasonAction("Tırmıkçıyı çağır", scene => scene.Cast.Trigger(1)),
            new SeasonAction("Çocuğu yaprağa atlat", scene => scene.Cast.Trigger(2)),
            new SeasonAction("Sincabı gönder", scene => scene.Cast.Trigger(3))
        };

        internal static readonly SeasonAction[] Spring =
        {
            new SeasonAction("Kelebek sürüsü", scene => scene.Cast.Trigger(1)),
            new SeasonAction("Bahçıvanı çağır", scene => scene.Cast.Trigger(2)),
            new SeasonAction("Kırlangıçları gönder", scene => scene.Cast.Trigger(3)),
            new SeasonAction("Rüzgâr estir", scene => scene.Gust())
        };

        internal readonly string Name;
        internal readonly Action<SeasonScene> Apply;

        private SeasonAction(string name, Action<SeasonScene> apply)
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
