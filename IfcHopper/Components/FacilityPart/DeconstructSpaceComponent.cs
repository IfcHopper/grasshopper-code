using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructSpaceComponent : DeconstructComponentBase
    {
        public DeconstructSpaceComponent()
          : base("Deconstruct Space", "DeSpace",
              "Deconstructs an IFC space. Children of a space read from a file are loaded on demand.",
              ComponentCategory.FacilityPart)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new SpaceParam(), "Space", "Sp", "IfcHopper space.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            RegisterCommonOutputs(pManager);
            pManager.AddTextParameter("Type", "T", "Predefined type, or the object type when user defined.", GH_ParamAccess.item);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Nested spaces.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Elements contained in the space.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.OutputDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterOutput(this);
            RegisterPlacementOutput(pManager);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            SpaceGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var space = goo.Value;
            SetCommonOutputs(DA, space);
            DA.SetData(3, TypeOf(space));
            DA.SetDataList(4, space.Spaces.Select(s => new SpaceGoo(s)));
            DA.SetDataList(5, space.Elements.Select(e => new ElementGoo(e)));
            ComponentPropertySets.SetOutput(DA, 6, space);
            ComponentClassifications.SetOutput(this, DA, space);
            SetPlacement(DA, 8, space);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructSpace;

        public override Guid ComponentGuid => new Guid("1b5e45b5-8342-40c4-acf0-c6453d5725b2");
    }
}
