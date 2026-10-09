using System;
using System.Drawing;
using System.Reflection;
using Grasshopper;
using Grasshopper.Kernel;

namespace IfcHopper
{
    public class IfcHopperInfo : GH_AssemblyInfo
    {
        public override string Name => "IfcHopper";

        //Return a 64x64 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => Properties.Resources.IfcHopper;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "CRUD operations on native IFC files.";

        public override Guid Id => new Guid("347916dc-8267-41e2-a7a3-6f5a7e574005");

        //Return a string identifying you or your company.
        public override string AuthorName => "Mattia Bressanelli, Luca Florio";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "https://github.com/IfcHopper/grasshopper-code";

        // The full version, e.g. 1.0.0-beta.1 (the assembly version has no pre-release label).
        public override string AssemblyVersion =>
            GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? GetType().Assembly.GetName().Version.ToString();
    }
}