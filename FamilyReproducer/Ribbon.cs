#region

using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using FamilyReproducer.Properties;

#endregion

namespace FamilyReproducer
{
    public class Ribbon : IExternalApplication
    {
        private const string TabName = "Воспроизведение семейства";

        public Result OnStartup(UIControlledApplication application)
        {
            if (!ComponentManager.Ribbon.Tabs.Any(i => i.Name == TabName)) application.CreateRibbonTab(TabName);
            var RibbonName = "Инструменты";
            var RibbonPanel = application.GetRibbonPanels(TabName).FirstOrDefault(i => i.Name == RibbonName);
            if (RibbonPanel == null) RibbonPanel = application.CreateRibbonPanel(TabName, RibbonName);

            var DllPath = Assembly.GetExecutingAssembly().Location;

            var PushButtonData1 = new PushButtonData("FamilyReproducer", "Воспроизведение семейства", DllPath,
                "FamilyReproducer.FamilyReproduceCommand");
            PushButtonData1.LargeImage = ImgToSource(Resources.FamilyReproducer);
            PushButtonData1.Image = ImgToSource(Resources.FamilyReproducer);
            PushButtonData1.ToolTip = "Плагин для воспроизведения семейства";
            var pushButton1 = (PushButton)RibbonPanel.AddItem(PushButtonData1);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private BitmapSource ImgToSource(Bitmap source)
        {
            return Imaging.CreateBitmapSourceFromHBitmap(source.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
    }
}