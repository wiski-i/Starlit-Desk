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
}
