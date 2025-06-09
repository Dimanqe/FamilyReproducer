#region

using System.Collections.Generic;

#endregion

namespace FamilyReproducer.Models
{
    public class GeometryData
    {
        public string SketchPlane { get; set; }
        public PointData SketchPlaneOrigin { get; set; }
        public PointData SketchPlaneNormal { get; set; }
        public List<CurveData> Profile { get; set; }
        public bool IsSolid { get; set; }
        public string ExtrusionStartParam { get; set; }
        public string ExtrusionEndParam { get; set; }
        public double ExtrusionStart { get; set; }
        public double ExtrusionEnd { get; set; }
        public PointData ExtrusionDirection { get; set; }
        public string BooleanOperation { get; set; }
    }
}