using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class FindObjectsComponent : GH_Component
    {
        public FindObjectsComponent()
          : base("Find Objects", "Find",
              "Finds objects in a model or below an object by IFC class, name, GlobalId and property values. Found objects can be deconstructed, " +
              "modified and applied back with Apply Edits. Objects read from a file are loaded as the search reaches them (not their geometry).",
              ComponentCategory.Tab, ComponentCategory.Utilities)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", ComponentObjects.RootDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Classes", "C",
                "IFC classes, e.g. IfcWall, IfcDoor, or IfcBuiltElement for all built elements; an object matches one of them. Empty: any class.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Exact Class", "EC", "True: only the classes given, not their subclasses.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Name", "N", "Name pattern with * and ? wildcards, ignoring case, e.g. *wall*. Empty: any name.", GH_ParamAccess.item);
            pManager.AddTextParameter("GlobalIds", "Id", "GlobalIds (22 characters) or GUIDs; an object matches one of them. Empty: any.", GH_ParamAccess.list);
            pManager.AddTextParameter("Properties", "P",
                "Property and quantity filters, all of which must match: Set.Property (it exists) or Set.Property op value with op =, !=, <, <=, >, >=, " +
                "e.g. Pset_WallCommon.IsExternal = true or Qto_WallBaseQuantities.Length > 5 (lengths, areas and volumes in document units). " +
                "Sets of the object's type count.", GH_ParamAccess.list);
            for (int i = 1; i < 6; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Objects", "O", "Found objects, in tree order: spatial objects, elements, element parts and openings.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "The found objects that are elements (also parts and openings), previewed in their colours.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Parents", "Pa", "Parent of each found object: its spatial object, the element it is a part of, or the element an opening voids.", GH_ParamAccess.list);
            pManager.AddTextParameter("Paths", "Pt", "Names of the objects above each found object, e.g. Project / Site / House / Ground floor.", GH_ParamAccess.list);
            pManager.AddTextParameter("Classes", "C", "IFC class of each found object.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_Goo goo = null;
            if (!DA.GetData(0, ref goo)) return;
            var root = ComponentObjects.Root(this, goo, out _);
            if (root == null) return;

            var query = new ObjectQuery { NumberToSi = DocumentUnits.ToSi };
            var classes = new List<string>();
            DA.GetDataList(1, classes);
            query.Classes.AddRange(classes);
            bool exact = false;
            DA.GetData(2, ref exact);
            query.ExactClass = exact;
            string name = null;
            DA.GetData(3, ref name);
            query.Name = name;
            var ids = new List<string>();
            DA.GetDataList(4, ids);
            query.GlobalIds.AddRange(ids);
            var filters = new List<string>();
            DA.GetDataList(5, filters);

            List<SearchResult> found;
            try
            {
                query.Properties.AddRange(filters.Where(f => !string.IsNullOrWhiteSpace(f)).Select(PropertyFilter.Parse));
                found = ModelSearch.Find(root, query).ToList();
            }
            catch (ArgumentException ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }

            if (found.Count == 0) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No objects match.");
            DA.SetDataList(0, found.Select(r => ComponentObjects.Wrap(r.Object)));
            DA.SetDataList(1, found.Select(r => r.Object).OfType<Element>().Select(e => new ElementGoo(e)));
            DA.SetDataList(2, found.Select(r => ComponentObjects.Wrap(r.Parent)));
            DA.SetDataList(3, found.Select(r => r.Path));
            DA.SetDataList(4, found.Select(r => SpatialRules.IfcClassOf(r.Object)));
        }

        protected override Bitmap Icon => Properties.Resources.FindObjects;

        public override Guid ComponentGuid => new Guid("48c71766-0d77-48f8-8f20-47f1202913ff");
    }
}
