#region

using System.IO;
using Autodesk.Revit.UI;

#endregion

namespace FamilyReproducer.Handlers
{
    public class FamilyOpener : IExternalEventHandler
    {
        private string _familyPath;

        public void Execute(UIApplication app)
        {
            if (!string.IsNullOrWhiteSpace(_familyPath) && File.Exists(_familyPath))
                app.OpenAndActivateDocument(_familyPath);
        }

        public string GetName()
        {
            return "FamilyOpener";
        }

        public void SetPath(string path)
        {
            _familyPath = path;
        }
    }
}