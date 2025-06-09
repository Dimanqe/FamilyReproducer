#region

using Autodesk.Revit.DB;

#endregion

namespace FamilyReproducer.Models
{
    public class ParameterData
    {
        public string Name { get; set; }
        public ParameterType Type { get; set; }
        public bool IsInstance { get; set; }
        public BuiltInParameterGroup Group { get; set; }
        public string Formula { get; set; }
        public object Value { get; set; }
    }
}