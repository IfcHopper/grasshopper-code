using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// What an element is made of (IfcMaterialSelect): a single material or a set of materials.
    /// Definitions read from a file are shared by every element that uses them.
    /// </summary>
    public abstract class MaterialDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>Backend entity this definition was read from; null for definitions created in Grasshopper.</summary>
        public object Source { get; internal set; }

        /// <summary>IFC entity name of the definition, e.g. "IfcMaterialLayerSet".</summary>
        public abstract string IfcClass { get; }

        /// <summary>The materials of the definition in order; null where a layer, constituent or profile has none.</summary>
        public abstract IEnumerable<Material> Materials { get; }

        /// <summary>Colour to show an element in when its geometry has no style of its own: the first material colour, or null.</summary>
        public Colour DisplayColour => Materials.Select(m => m?.Colour).FirstOrDefault(c => c != null);

        /// <summary>
        /// True for an edited copy (Modify Material). Edited definitions read from a file are written back onto the file's material or set,
        /// which changes it for every element using it.
        /// </summary>
        public bool Edited { get; private set; }

        protected MaterialDefinition(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Property sets of the material or set (IfcMaterialProperties, IFC4 and later), e.g. Pset_MaterialConcrete. Lengths, areas and
        /// volumes in metres, m² and m³.
        /// </summary>
        public List<PropertySet> PropertySets { get; private set; } = new List<PropertySet>();

        /// <summary>Puts each set in place of the set with the same name, or adds it; a set without properties removes the set with its name.</summary>
        public void MergePropertySets(IEnumerable<PropertySet> sets)
        {
            foreach (var set in sets)
            {
                var index = PropertySets.FindIndex(s => s.Name == set.Name);
                if (index >= 0) PropertySets.RemoveAt(index);
                if (set.Count > 0) PropertySets.Insert(index >= 0 ? index : PropertySets.Count, set);
            }
        }

        /// <summary>True when both hold sets with the same names and values, in order.</summary>
        internal bool SamePropertySets(IReadOnlyList<PropertySet> other) =>
            PropertySets.Count == other.Count && PropertySets.Zip(other, (a, b) => a.SameAs(b)).All(same => same);

        /// <summary>An edited copy with the same source and values; the parts of a set are copied, their materials are shared.</summary>
        public MaterialDefinition Copy()
        {
            var copy = (MaterialDefinition)MemberwiseClone();
            copy.Edited = true;
            copy.PropertySets = PropertySets.ToList();
            copy.CopyParts();
            return copy;
        }

        /// <summary>Replaces the part lists shared by MemberwiseClone with copies.</summary>
        protected virtual void CopyParts() { }
    }

    /// <summary>A single material (IfcMaterial). Description and Category are IFC4 and later.</summary>
    public sealed class Material : MaterialDefinition
    {
        public string Category { get; set; }

        /// <summary>Surface colour of the material's style (IfcMaterialDefinitionRepresentation); null when not styled.</summary>
        public Colour Colour { get; set; }

        public override string IfcClass => "IfcMaterial";

        public override IEnumerable<Material> Materials => new[] { this };

        public Material(string name) : base(name) { }
    }

    /// <summary>
    /// Layers of materials with their thicknesses, e.g. the build-up of a wall or slab (IfcMaterialLayerSet).
    /// How the layers sit on an element (IfcMaterialLayerSetUsage) is kept in the file and not modelled.
    /// </summary>
    public sealed class MaterialLayerSet : MaterialDefinition
    {
        public List<MaterialLayer> Layers { get; private set; } = new List<MaterialLayer>();

        protected override void CopyParts() => Layers = Layers.Select(l => (MaterialLayer)l.Copy()).ToList();

        /// <summary>Sum of the layer thicknesses in metres.</summary>
        public double TotalThickness => Layers.Sum(l => l.Thickness);

        public override string IfcClass => "IfcMaterialLayerSet";

        public override IEnumerable<Material> Materials => Layers.Select(l => l.Material);

        public MaterialLayerSet(string name = null) : base(name) { }
    }

    public sealed class MaterialLayer : MaterialPart
    {
        /// <summary>Material of the layer; null for an air gap without material.</summary>
        public Material Material { get; set; }

        /// <summary>Thickness in metres.</summary>
        public double Thickness { get; set; }

        public string Name { get; set; }
        public string Category { get; set; }
    }

    /// <summary>
    /// Named parts of an element and their materials (IfcMaterialConstituentSet, IFC4 and later).
    /// IFC2X3 material lists are read as constituent sets without names.
    /// </summary>
    public sealed class MaterialConstituentSet : MaterialDefinition
    {
        public List<MaterialConstituent> Constituents { get; private set; } = new List<MaterialConstituent>();

        protected override void CopyParts() => Constituents = Constituents.Select(c => (MaterialConstituent)c.Copy()).ToList();

        public override string IfcClass => "IfcMaterialConstituentSet";

        public override IEnumerable<Material> Materials => Constituents.Select(c => c.Material);

        public MaterialConstituentSet(string name = null) : base(name) { }
    }

    public sealed class MaterialConstituent : MaterialPart
    {
        public Material Material { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }

        /// <summary>Share of the element made of this constituent, from 0 to 1; null when not given.</summary>
        public double? Fraction { get; set; }
    }

    /// <summary>Materials of cross-section profiles (IfcMaterialProfileSet, IFC4 and later). The profile shapes are not modelled.</summary>
    public sealed class MaterialProfileSet : MaterialDefinition
    {
        public List<MaterialProfile> Profiles { get; private set; } = new List<MaterialProfile>();

        protected override void CopyParts() => Profiles = Profiles.Select(p => (MaterialProfile)p.Copy()).ToList();

        public override string IfcClass => "IfcMaterialProfileSet";

        public override IEnumerable<Material> Materials => Profiles.Select(p => p.Material);

        public MaterialProfileSet(string name = null) : base(name) { }
    }

    public sealed class MaterialProfile : MaterialPart
    {
        public Material Material { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
    }

    /// <summary>A layer, constituent or profile of a material set.</summary>
    public abstract class MaterialPart
    {
        internal MaterialPart Copy() => (MaterialPart)MemberwiseClone();
    }
}
