using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Geometric representation context of a project (IfcGeometricRepresentationContext), e.g. "Model" (3D) or "Plan" (2D),
    /// with its subcontexts (e.g. Body, Axis, Annotation) that geometry representations refer to.
    /// </summary>
    public class RepresentationContext
    {
        public const string ModelType = "Model";
        public const string PlanType = "Plan";
        public const double DefaultPrecision = 1e-5;

        public string ContextType { get; set; }

        /// <summary>Coordinate space dimension: 3 for model contexts, 2 for plan contexts.</summary>
        public int Dimension { get; set; }

        /// <summary>Geometric precision, in metres.</summary>
        public double Precision { get; set; } = DefaultPrecision;

        /// <summary>True north as a 2D direction (X, Y); null when not set, meaning north is +Y.</summary>
        public double[] TrueNorth { get; set; }

        public List<RepresentationSubContext> SubContexts { get; } = new List<RepresentationSubContext>();

        public RepresentationContext(string contextType = ModelType, int? dimension = null)
        {
            ContextType = contextType;
            Dimension = dimension ?? (string.Equals(contextType, PlanType, StringComparison.OrdinalIgnoreCase) ? 2 : 3);
        }
    }

    /// <summary>IfcGeometricRepresentationSubContext, e.g. "Body" seen in MODEL_VIEW.</summary>
    public class RepresentationSubContext
    {
        public const string UserDefined = "USERDEFINED";
        public const string DefaultTargetView = "MODEL_VIEW";

        /// <summary>IfcGeometricProjectionEnum values (USERDEFINED excluded).</summary>
        public static readonly IReadOnlyList<string> TargetViews = new[]
        {
            "ELEVATION_VIEW", "GRAPH_VIEW", "MODEL_VIEW", "PLAN_VIEW", "REFLECTED_PLAN_VIEW", "SECTION_VIEW", "SKETCH_VIEW", "NOTDEFINED",
        };

        /// <summary>ContextIdentifier, e.g. "Body", "Axis", "Box", "FootPrint", "Annotation".</summary>
        public string Identifier { get; set; }

        public string TargetView { get; private set; } = DefaultTargetView;

        /// <summary>Custom target view when <see cref="TargetView"/> is USERDEFINED.</summary>
        public string UserDefinedTargetView { get; private set; }

        /// <summary>Optional target scale, e.g. 0.01 for 1:100.</summary>
        public double? TargetScale { get; set; }

        public RepresentationSubContext(string identifier, string targetView = DefaultTargetView)
        {
            Identifier = identifier;
            SetTargetView(targetView);
        }

        /// <summary>
        /// Sets the target view. Values not in <see cref="TargetViews"/> become USERDEFINED with the value as user defined target view.
        /// Returns false when the value was stored as user defined.
        /// </summary>
        public bool SetTargetView(string view)
        {
            UserDefinedTargetView = null;
            if (string.IsNullOrWhiteSpace(view))
            {
                TargetView = DefaultTargetView;
                return true;
            }

            var match = TargetViews.FirstOrDefault(v => v.Equals(view.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                TargetView = match;
                return true;
            }
            TargetView = UserDefined;
            UserDefinedTargetView = view.Trim();
            return false;
        }
    }
}
