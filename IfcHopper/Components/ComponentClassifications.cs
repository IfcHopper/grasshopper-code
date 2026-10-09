using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    /// <summary>Classifications input and output of object, type and spatial components; right after Property Sets, found by name.</summary>
    internal static class ComponentClassifications
    {
        public const string Name = "Classifications";
        public const string NickName = "Cl";
        public const string Description = "Classification references (Classification component). References in the same system: the last one is kept.";
        public const string ModifyDescription =
            "Classification references put in place of the references in the same system, or added; a reference without a code removes the references of its system. Unconnected: unchanged.";
        public const string OutputDescription = "Classification references. References of the type are included unless the object has its own in the same system (From Type on Deconstruct Classification).";

        /// <summary>Adds the Classifications input (optional, list); call from RegisterInputParams after the Property Sets input.</summary>
        public static void RegisterInput(GH_Component component, string description) =>
            component.Params.RegisterInputParam(new ClassificationParam { Name = Name, NickName = NickName, Description = description, Access = GH_ParamAccess.list, Optional = true });

        /// <summary>Adds the Classifications output (list); call from RegisterOutputParams after the Property Sets output.</summary>
        public static void RegisterOutput(GH_Component component) =>
            component.Params.RegisterOutputParam(new ClassificationParam { Name = Name, NickName = NickName, Description = OutputDescription, Access = GH_ParamAccess.list });

        /// <summary>Merges the references of the Classifications input into the object; does nothing when the input is not connected.</summary>
        public static void Apply(GH_Component component, IGH_DataAccess DA, ModelObject obj)
        {
            var index = component.Params.IndexOfInputParam(Name);
            if (index < 0 || component.Params.Input[index].SourceCount == 0) return;
            var references = new List<ClassificationGoo>();
            DA.GetDataList(index, references);
            obj.MergeClassifications(references.Where(r => r != null && r.IsValid).Select(r => r.Value));
        }

        public static void SetOutput(GH_Component component, IGH_DataAccess DA, ModelObject obj) =>
            DA.SetDataList(component.Params.IndexOfOutputParam(Name), obj.Classifications.Select(c => new ClassificationGoo(c)));
    }
}
