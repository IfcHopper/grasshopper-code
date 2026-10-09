using System;
using System.IO;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    /// <summary>Core rules that do not involve reading or writing files.</summary>
    public class ModelRulesTests
    {
        [Fact]
        public void FilePath_UsesDefaults()
        {
            var path = IfcFilePath.Resolve(null, null);
            Assert.Equal(Path.Combine(IfcFilePath.DefaultDirectory, IfcFilePath.DefaultFileName), path);
        }

        [Theory]
        [InlineData("model", "model.ifc")]
        [InlineData("My.Model", "My.Model.ifc")]
        [InlineData("model.IFC", "model.IFC")]
        public void FilePath_AppendsExtensionWhenMissing(string name, string expected)
        {
            var directory = Path.GetTempPath();
            Assert.Equal(Path.Combine(directory, expected), IfcFilePath.Resolve(name, directory));
        }

        [Fact]
        public void FilePath_RejectsInvalidNames()
        {
            Assert.Throws<ArgumentException>(() => IfcFilePath.Resolve("bad|name", Path.GetTempPath()));
        }

        [Theory]
        [InlineData(UnitKind.Length, "mm", "Millimetre")]
        [InlineData(UnitKind.Length, "Millimeter", "Millimetre")]
        [InlineData(UnitKind.Length, "FOOT", "Foot")]
        [InlineData(UnitKind.Area, "square foot", "Square foot")]
        [InlineData(UnitKind.Volume, "cubic inch", "Cubic inch")]
        [InlineData(UnitKind.Angle, "deg", "Degree")]
        public void Units_FindByNameSymbolOrIfcName(UnitKind kind, string text, string expected)
        {
            Assert.Equal(expected, Units.Find(kind, text)?.Name);
        }

        [Fact]
        public void Units_UnknownTextOrFactorReturnsNull()
        {
            Assert.Null(Units.Find(UnitKind.Length, "furlong"));
            Assert.Null(Units.FromFactor(UnitKind.Length, 0.9144));
        }

        [Fact]
        public void Units_FromFactorMatchesKnownUnits()
        {
            Assert.Equal("Inch", Units.FromFactor(UnitKind.Length, 0.0254).Name);
            Assert.Equal("Square millimetre", Units.FromFactor(UnitKind.Area, 1e-6).Name);
        }

        [Fact]
        public void PredefinedType_MatchesIgnoringCase()
        {
            var part = new FacilityPart(FacilityType.Bridge);
            Assert.True(part.SetType("deck"));
            Assert.Equal("DECK", part.PredefinedType);
            Assert.Null(part.ObjectType);
        }

        [Fact]
        public void PredefinedType_UnknownValueBecomesUserDefined()
        {
            var part = new FacilityPart(FacilityType.Bridge);
            Assert.False(part.SetType("My Part"));
            Assert.Equal(TypedModelObject.UserDefined, part.PredefinedType);
            Assert.Equal("My Part", part.ObjectType);
        }

        [Fact]
        public void PredefinedType_RoadOnlyAllowsUserDefined()
        {
            var road = new Facility(FacilityType.Road);
            Assert.True(road.HasPredefinedType);
            Assert.False(road.SetType("Motorway"));
            Assert.Equal(TypedModelObject.UserDefined, road.PredefinedType);
        }

        [Fact]
        public void PredefinedType_BuildingHasNone()
        {
            var building = new Building();
            Assert.False(building.HasPredefinedType);
            Assert.Throws<InvalidOperationException>(() => building.SetType("x"));
        }

        [Fact]
        public void FacilityParts_AcceptOwnKindAndCommonParts()
        {
            var road = new Facility(FacilityType.Road);
            Assert.True(road.Accepts(new FacilityPart(FacilityType.Road)));
            Assert.True(road.Accepts(new FacilityPart(FacilityType.Facility)));
            Assert.False(road.Accepts(new FacilityPart(FacilityType.Bridge)));
            Assert.Throws<ArgumentException>(() => new FacilityPart(FacilityType.Building));
        }

        [Fact]
        public void SubContext_UnknownTargetViewBecomesUserDefined()
        {
            var sub = new RepresentationSubContext("Box");
            Assert.True(sub.SetTargetView("graph_view"));
            Assert.Equal("GRAPH_VIEW", sub.TargetView);
            Assert.False(sub.SetTargetView("MY_VIEW"));
            Assert.Equal(RepresentationSubContext.UserDefined, sub.TargetView);
            Assert.Equal("MY_VIEW", sub.UserDefinedTargetView);
        }

        [Fact]
        public void Context_PlanIsTwoDimensional()
        {
            Assert.Equal(3, new RepresentationContext().Dimension);
            Assert.Equal(2, new RepresentationContext(RepresentationContext.PlanType).Dimension);
        }
    }
}
