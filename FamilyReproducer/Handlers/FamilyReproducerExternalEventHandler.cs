#region

using System;
using Autodesk.Revit.UI;
using FamilyReproducer.Models;

#endregion

namespace FamilyReproducer.Handlers
{
    public class FamilyReproducerExternalEventHandler : IExternalEventHandler
    {
        public FamilyData Data { get; set; }

        public void Execute(UIApplication app)
        {
            if (Data == null)
                throw new InvalidOperationException("Данные семейства не установлены");

            var reproducer = new Models.FamilyReproducer(app, Data);

            reproducer.OnFamilySaved = savedPath =>
            {
                var uiApp = new UIApplication(app.Application);
                uiApp.OpenAndActivateDocument(savedPath);
            };

            reproducer.Execute();
        }

        public string GetName()
        {
            return "Внешнее событие для воспроизведения семейства";
        }
    }
}