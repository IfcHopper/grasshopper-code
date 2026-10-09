using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class UnitsComponent : GH_Component
    {
        public UnitsComponent()
          : base("Units", "Units",
              "Defines the units written to an IFC file (IfcUnitAssignment). Lengths are converted from Rhino units on write.",
              ComponentCategory.Tab, ComponentCategory.Project)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Length", "L", $"Length unit: {Values(UnitKind.Length)}. Defaults to the Rhino document units.", GH_ParamAccess.item);
            pManager.AddTextParameter("Area", "A", $"Area unit: {Values(UnitKind.Area)}.", GH_ParamAccess.item, "m2");
            pManager.AddTextParameter("Volume", "V", $"Volume unit: {Values(UnitKind.Volume)}.", GH_ParamAccess.item, "m3");
            pManager.AddTextParameter("Angle", "An", $"Plane angle unit: {Values(UnitKind.Angle)}.", GH_ParamAccess.item, "rad");
            for (int i = 0; i < 4; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new UnitsParam(), "Units", "U", "IfcHopper project units.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string length = null, area = null, volume = null, angle = null;
            DA.GetData(0, ref length);
            DA.GetData(1, ref area);
            DA.GetData(2, ref volume);
            DA.GetData(3, ref angle);

            var units = new Units();
            units.Length = string.IsNullOrWhiteSpace(length) ? DocumentLength(this) : Parse(UnitKind.Length, length);
            units.Area = Parse(UnitKind.Area, area) ?? units.Area;
            units.Volume = Parse(UnitKind.Volume, volume) ?? units.Volume;
            units.Angle = Parse(UnitKind.Angle, angle) ?? units.Angle;
            if (RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0) return;

            DA.SetData(0, new UnitsGoo(units));
        }

        /// <summary>IFC length unit of the Rhino document; metres with a warning when IFC has no matching unit.</summary>
        internal static UnitDefinition DocumentLength(GH_Component component)
        {
            var unit = DocumentUnits.LengthUnit;
            if (unit != null) return unit;
            component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Rhino units ({DocumentUnits.System}) have no IFC equivalent; metres are used.");
            return new Units().Length;
        }

        private UnitDefinition Parse(UnitKind kind, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var unit = Units.Find(kind, text);
            if (unit == null) AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unknown {kind.ToString().ToLowerInvariant()} unit '{text}'. Use one of: {Values(kind)}.");
            return unit;
        }

        private static string Values(UnitKind kind) => string.Join(", ", Units.Of(kind).Select(u => u.Symbol));

        protected override Bitmap Icon => Properties.Resources.Units;

        public override Guid ComponentGuid => new Guid("b8e2c5f1-4a97-4d3e-9c16-f0d7a3b8e529");
    }
}
