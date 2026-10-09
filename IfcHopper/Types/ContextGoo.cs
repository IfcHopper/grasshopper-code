using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class ContextGoo : IfcHopperGoo<RepresentationContext>
    {
        public ContextGoo() { }

        public ContextGoo(RepresentationContext value) : base(value) { }

        public override string TypeName => "IFC Context";

        public override string TypeDescription => "IfcHopper representation context (IfcGeometricRepresentationContext with subcontexts).";

        public override IGH_Goo Duplicate() => new ContextGoo(Value);

        public override string ToString() =>
            Value == null ? "Null Context" : $"IfcGeometricRepresentationContext: {Value.ContextType} ({Value.SubContexts.Count} subcontexts)";

        public override bool Write(GH_IWriter writer)
        {
            if (Value == null) return true;
            writer.SetString("ContextType", Value.ContextType);
            writer.SetInt32("Dimension", Value.Dimension);
            writer.SetDouble("Precision", Value.Precision);
            if (Value.TrueNorth != null && Value.TrueNorth.Length >= 2)
            {
                writer.SetDouble("TrueNorthX", Value.TrueNorth[0]);
                writer.SetDouble("TrueNorthY", Value.TrueNorth[1]);
            }
            writer.SetInt32("SubContextCount", Value.SubContexts.Count);
            for (int i = 0; i < Value.SubContexts.Count; i++)
            {
                var sub = writer.CreateChunk("SubContext", i);
                var subContext = Value.SubContexts[i];
                sub.SetString("Identifier", subContext.Identifier);
                sub.SetString("TargetView", subContext.TargetView == RepresentationSubContext.UserDefined ? subContext.UserDefinedTargetView : subContext.TargetView);
                if (subContext.TargetScale.HasValue) sub.SetDouble("TargetScale", subContext.TargetScale.Value);
            }
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!reader.ItemExists("ContextType"))
            {
                Value = null;
                return true;
            }
            var context = new RepresentationContext(reader.GetString("ContextType"), reader.GetInt32("Dimension"))
            {
                Precision = reader.GetDouble("Precision"),
            };
            if (reader.ItemExists("TrueNorthX"))
                context.TrueNorth = new[] { reader.GetDouble("TrueNorthX"), reader.GetDouble("TrueNorthY") };
            var count = reader.GetInt32("SubContextCount");
            for (int i = 0; i < count; i++)
            {
                var sub = reader.FindChunk("SubContext", i);
                if (sub == null) continue;
                var subContext = new RepresentationSubContext(sub.GetString("Identifier"), sub.GetString("TargetView"));
                if (sub.ItemExists("TargetScale")) subContext.TargetScale = sub.GetDouble("TargetScale");
                context.SubContexts.Add(subContext);
            }
            Value = context;
            return true;
        }
    }
}
