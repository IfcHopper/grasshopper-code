using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;
using Rhino.DocObjects;

namespace IfcHopper.Components
{
    /// <summary>Geometry input of object components (world geometry in document units, faceted to meshes).</summary>
    internal static class ComponentGeometry
    {
        public const string Name = "Geometry";
        public const string NickName = "G";
        public const string Description = "Body geometry in world coordinates. Meshes are kept; Breps, extrusions, surfaces and SubDs are faceted; block instances are exploded.";

        public const string ColourName = "Colour";
        public const string ColourNickName = "Col";
        public const string ColourDescription = "Surface colour of the geometry (IfcStyledItem); alpha sets the transparency.";

        /// <summary>The colour at <paramref name="index"/>, or null when the input is empty.</summary>
        public static Colour ReadColour(IGH_DataAccess DA, int index)
        {
            var color = System.Drawing.Color.Empty;
            return DA.GetData(index, ref color) ? DocumentGeometry.ToColour(color) : null;
        }

        /// <summary>The meshes at <paramref name="index"/>; items that cannot be meshed are skipped with a warning.</summary>
        public static List<MeshGeometry> Read(GH_Component component, IGH_DataAccess DA, int index)
        {
            var geometry = new List<IGH_GeometricGoo>();
            DA.GetDataList(index, geometry);
            return Read(component, geometry);
        }

        public const string TypeDescription =
            "Body geometry in type coordinates, written as a representation map: meshes, Breps, extrusions, surfaces and SubDs (faceted), " +
            "or a block definition or instance (the objects of its definition; nested blocks are flattened).";

        /// <summary>Type geometry of the input at <paramref name="index"/>: geometry, or the objects of block definitions in block coordinates.</summary>
        public static List<MeshGeometry> ReadType(GH_Component component, IGH_DataAccess DA, int index, out InstanceDefinition firstBlock)
        {
            var data = new List<IGH_Goo>();
            DA.GetDataList(index, data);
            var meshes = new List<MeshGeometry>();
            firstBlock = null;
            foreach (var goo in data.Where(g => g != null))
            {
                if (DocumentBlocks.Find(goo) is InstanceDefinition block)
                {
                    firstBlock = firstBlock ?? block;
                    meshes.AddRange(DocumentBlocks.Meshes(block, Rhino.Geometry.Transform.Identity));
                }
                else if (goo is IGH_GeometricGoo geometry) meshes.AddRange(Read(component, new[] { geometry }));
                else component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{goo.TypeName} is not geometry or a block and is ignored.");
            }
            return meshes;
        }

        /// <summary>Meshes of geometry items; block instances are exploded and items that cannot be meshed are skipped with a warning.</summary>
        public static List<MeshGeometry> Read(GH_Component component, IEnumerable<IGH_GeometricGoo> geometry)
        {
            var meshes = new List<MeshGeometry>();
            foreach (var goo in geometry)
            {
                if (goo == null) continue;
                if (goo is GH_InstanceReference instance && DocumentBlocks.Find(instance) is InstanceDefinition block)
                {
                    meshes.AddRange(DocumentBlocks.Meshes(block, instance.Value.Xform));
                    continue;
                }
                var mesh = DocumentGeometry.ToMesh(GH_Convert.ToGeometryBase(goo));
                if (mesh != null) meshes.Add(mesh);
                else component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{goo.TypeName} cannot be converted to a mesh and is ignored.");
            }
            return meshes;
        }
    }
}
