namespace FamilyReproducer.Models
{
    public class ReferenceData
    {
        public string GeometryType { get; set; }
        public string SketchOwnerId { get; set; }
        public string ReferencePlaneName { get; set; }
        public PointData StartPoint { get; set; }
        public PointData EndPoint { get; set; }
        public PointData FaceCenter { get; set; }
        public PointData PlaneOrigin { get; set; }
    }
}