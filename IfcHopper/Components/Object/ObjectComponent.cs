using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using IfcHopper.Types;
using Rhino.DocObjects;

namespace IfcHopper.Components
{
    public class ObjectComponent : GH_Component
    {
        public ObjectComponent()
          : base("Object", "Object",
              "Creates an IFC element (any instantiable IfcElement subclass, e.g. IfcWall). Connect it to a storey or facility part.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Class", "C", "IFC element class, e.g. IfcWall, IfcSlab, IfcColumn, IfcBeam.", GH_ParamAccess.item, Element.DefaultIfcClass);
            pManager.AddTextParameter("Name", "N", "Object name.", GH_ParamAccess.item, Element.DefaultName);
            pManager.AddTextParameter("Description", "D", "Object description.", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Predefined type of the class (e.g. SOLIDWALL for IfcWall). Other values are written as USERDEFINED with the value as object type.", GH_ParamAccess.item);
            pManager.AddGeometryParameter(ComponentGeometry.Name, ComponentGeometry.NickName, ComponentGeometry.Description, GH_ParamAccess.list);
            pManager.AddColourParameter(ComponentGeometry.ColourName, ComponentGeometry.ColourNickName, ComponentGeometry.ColourDescription, GH_ParamAccess.item);
            pManager.AddParameter(new ElementParam(), "Parts", "P", "Elements this object is made of (IfcRelAggregates), e.g. the members of an IfcElementAssembly.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), ComponentOpenings.Name, ComponentOpenings.NickName, ComponentOpenings.Description, GH_ParamAccess.list);
            pManager.AddParameter(new MaterialParam(), ComponentMaterial.Name, ComponentMaterial.NickName, ComponentMaterial.Description, GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.Description, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.Description);
            pManager.AddParameter(new ElementTypeParam(), "Element Type", "ET",
                "Type of the object (IfcRelDefinesByType). Without geometry the object is an instance of the type at its placement (IfcMappedItem); " +
                "with geometry it keeps its own. A single block instance as geometry makes the object an instance of the block's type instead. " +
                "Sets the class when Class is not given.", GH_ParamAccess.item);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.Description + " For a block instance, empty means the block's insertion plane.");
            pManager.AddTextParameter(ComponentGlobalId.Name, ComponentGlobalId.NickName, ComponentGlobalId.Description, GH_ParamAccess.item);
            for (int i = 2; i < 14; i++) pManager[i].Optional = true;
            ComponentAttributes.RegisterInputs(this, false);
        }

        /// <summary>Types made from block definitions in this solution, so instances of one block share one type.</summary>
        private readonly Dictionary<(Guid, string), ElementType> _blockTypes = new Dictionary<(Guid, string), ElementType>();

        protected override void BeforeSolveInstance()
        {
            _blockTypes.Clear();
            base.BeforeSolveInstance();
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ElementParam(), "Object", "O", "IfcHopper element.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string className = null, name = null, description = null, type = null;
            if (!DA.GetData(0, ref className)) return;
            if (!DA.GetData(1, ref name)) return;
            DA.GetData(2, ref description);
            DA.GetData(3, ref type);

            ElementTypeGoo typeGoo = null;
            var elementType = DA.GetData(11, ref typeGoo) && typeGoo != null && typeGoo.IsValid ? typeGoo.Value : null;
            if (elementType != null && Params.Input[0].SourceCount == 0 && className == Element.DefaultIfcClass)
                className = IfcBackend.Schema.ElementClassOf(elementType.IfcClass) ?? className;

            var ifcClass = IfcBackend.Schema.FindElementClass(className);
            if (ifcClass == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"'{className}' is not an instantiable IFC element class (e.g. IfcWall, IfcSlab, IfcColumn).");
                return;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }

            var element = new Element(name.Trim(), ifcClass) { Description = description };
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (!element.HasPredefinedType)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{ifcClass} has no predefined type; Type is ignored.");
                else if (!element.SetType(type))
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        $"'{type}' is not a predefined type of {ifcClass} ({string.Join(", ", element.PredefinedTypes)}); written as USERDEFINED.");
            }

            var globalId = ComponentGlobalId.Resolve(this, DA, 13);
            if (globalId == null) return;
            element.GlobalId = globalId;
            element.Placement = ComponentPlacement.Read(DA, 12);
            var colour = ComponentGeometry.ReadColour(DA, 5);
            var geometry = new List<IGH_GeometricGoo>();
            DA.GetDataList(4, geometry);
            geometry.RemoveAll(g => g == null);

            if (elementType == null && geometry.Count == 1 && geometry[0] is GH_InstanceReference instance && DocumentBlocks.Find(instance) is InstanceDefinition block)
            {
                var typeClass = IfcBackend.Schema.TypeClassOf(ifcClass) ?? "IfcBuildingElementProxyType";
                if (!_blockTypes.TryGetValue((block.Id, typeClass), out elementType))
                    _blockTypes[(block.Id, typeClass)] = elementType = DocumentBlocks.TypeOf(block, typeClass);
                var transform = DocumentBlocks.ToAffine(instance.Value.Xform);
                element.Placement = element.Placement ?? DocumentBlocks.PlacementOf(transform);
                element.PlaceType(elementType, transform);
                if (colour != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Colour is ignored for block instances: colour the block's objects instead.");
            }
            else
            {
                var meshes = ComponentGeometry.Read(this, geometry).Select(m => colour == null ? m : m.WithColour(colour)).ToList();
                if (elementType != null && meshes.Count == 0) element.PlaceType(elementType, (element.Placement ?? Placement.World).ToAffine());
                else
                {
                    element.Geometry.AddRange(meshes);
                    element.AssignType(elementType);
                }
            }
            var parts = new List<ElementGoo>();
            DA.GetDataList(6, parts);
            element.Parts.AddRange(parts.Where(p => p != null && p.IsValid).Select(p => p.Value));
            element.Openings.AddRange(ComponentOpenings.Read(this, DA, 7));
            ComponentMaterial.Apply(DA, 8, element.SetMaterial);
            ComponentPropertySets.Apply(this, DA, 9, element);
            ComponentClassifications.Apply(this, DA, element);
            if (!ComponentAttributes.Apply(this, DA, 14, element.IfcClass, t => element.Tag = t, element.Attributes)) return;
            DA.SetData(0, new ElementGoo(element));
        }

        protected override Bitmap Icon => Properties.Resources.Element;

        public override Guid ComponentGuid => new Guid("5d1c8e3a-b7f2-4964-a0d5-e9c3f6b2a871");
    }
}
