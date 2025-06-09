#region

using System.Collections.Generic;

#endregion

namespace FamilyReproducer.Models
{
    public class DimensionData
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public double? ValueNumeric { get; set; }
        public bool AreSegmentsEqual { get; set; }
        public List<ReferenceData> References { get; set; }
    }
}