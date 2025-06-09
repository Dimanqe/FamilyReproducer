using Autodesk.Revit.DB;

public class ParameterData
{
    public string Name { get; set; }
    public string Type { get; set; } 
    public bool IsInstance { get; set; }
    public BuiltInParameterGroup Group { get; set; }
    public string Formula { get; set; }
    public object Value { get; set; }

}