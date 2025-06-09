#region

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamilyReproducer.Handlers;
using FamilyReproducer.ViewModels;

#endregion

namespace FamilyReproducer
{
    [Transaction(TransactionMode.Manual)]
    public class FamilyReproduceCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var eventHandler = new FamilyReproducerExternalEventHandler();
                var externalEvent = ExternalEvent.Create(eventHandler);
                var viewModel = new MainWindowViewModel(externalEvent, eventHandler);
                var mainWindow = new MainWindow(viewModel, commandData.Application.ActiveUIDocument.Application);
                mainWindow.Show();
                return Result.Succeeded;
            }
            catch (Exception e)
            {
                return Result.Failed;
            }
        }
    }
}