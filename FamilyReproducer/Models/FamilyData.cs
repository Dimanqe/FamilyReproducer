#region

using System.Collections.Generic;

#endregion

namespace FamilyReproducer.Models
{
    public class FamilyData
    {
        public string FamilyName { get; set; }
        public List<ParameterData> Parameters { get; set; }
        public List<GeometryData> Geometries { get; set; }
        public List<DimensionData> Dimensions { get; set; }
        public List<ReferencePlaneData> ReferencePlanes { get; set; } = new List<ReferencePlaneData>();
    }
}