using System;
using System.Drawing;
using IfcHopper.Core.IO;
using Grasshopper.Kernel;

namespace IfcHopper.Components
{
    public class ReadIfcHeaderComponent : GH_Component
    {
        public ReadIfcHeaderComponent()
          : base("Read IFC Header", "Header",
              "Reads the header of an IFC file and the project name.",
              ComponentCategory.Tab, ComponentCategory.File)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Path", "P", "Path of the IFC file." + DocumentPaths.Note, GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Project", "Pr", "Name of the IfcProject.", GH_ParamAccess.item);
            pManager.AddTextParameter("Schema", "S", "FILE_SCHEMA identifiers.", GH_ParamAccess.list);
            pManager.AddTextParameter("Description", "D", "FILE_DESCRIPTION description (view definitions).", GH_ParamAccess.list);
            pManager.AddTextParameter("Implementation Level", "IL", "FILE_DESCRIPTION implementation level.", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "FILE_NAME name.", GH_ParamAccess.item);
            pManager.AddTextParameter("Time Stamp", "T", "FILE_NAME time stamp.", GH_ParamAccess.item);
            pManager.AddTextParameter("Author", "A", "FILE_NAME author.", GH_ParamAccess.list);
            pManager.AddTextParameter("Organization", "O", "FILE_NAME organization.", GH_ParamAccess.list);
            pManager.AddTextParameter("Preprocessor", "PV", "FILE_NAME preprocessor version.", GH_ParamAccess.item);
            pManager.AddTextParameter("Originating System", "OS", "FILE_NAME originating system.", GH_ParamAccess.item);
            pManager.AddTextParameter("Authorization", "Au", "FILE_NAME authorization.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string path = null;
            if (!DA.GetData(0, ref path)) return;
            path = DocumentPaths.Resolve(this, path);
            if (path == null) return;

            IfcHeader header;
            try
            {
                header = IfcBackend.Reader.ReadHeader(path);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }

            DA.SetData(0, header.ProjectName);
            DA.SetDataList(1, header.Schemas);
            DA.SetDataList(2, header.Description);
            DA.SetData(3, header.ImplementationLevel);
            DA.SetData(4, header.Name);
            DA.SetData(5, header.TimeStamp);
            DA.SetDataList(6, header.Author);
            DA.SetDataList(7, header.Organization);
            DA.SetData(8, header.PreprocessorVersion);
            DA.SetData(9, header.OriginatingSystem);
            DA.SetData(10, header.Authorization);
        }

        protected override Bitmap Icon => Properties.Resources.ReadIfcHeader;

        public override Guid ComponentGuid => new Guid("6f0b2c8e-3d1a-4c55-9b7e-2a41d9e3f8c1");
    }
}
