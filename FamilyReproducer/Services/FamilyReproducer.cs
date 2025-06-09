#region

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

#endregion

namespace FamilyReproducer.Models
{
    /// <summary>
    ///     Класс для воспроизведения (воссоздания) семейства Revit на основе данных FamilyData.
    /// </summary>
    public class FamilyReproducer
    {
        private readonly Application _app;
        private readonly FamilyData _data;
        private readonly Document _doc;
        private readonly UIDocument _uiDoc;

        /// <summary>
        ///     Конструктор.
        /// </summary>
        /// <param name="uiApp">Объект UIApplication.</param>
        /// <param name="data">Данные семейства для воссоздания.</param>
        public FamilyReproducer(UIApplication uiApp, FamilyData data)
        {
            _uiDoc = uiApp.ActiveUIDocument;
            _doc = _uiDoc.Document;
            _app = uiApp.Application;
            _data = data;
        }

        /// <summary>
        ///     Событие вызывается после успешного сохранения семейства.
        /// </summary>
        public Action<string> OnFamilySaved { get; set; }

        /// <summary>
        ///     Основной метод для запуска процесса воссоздания семейства.
        /// </summary>
        public void Execute()
        {
            try
            {
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                var outputPath = Path.Combine(assemblyDir, "Temp");
                Directory.CreateDirectory(outputPath);

                var jsonPath = Path.Combine(outputPath, $"{_data.FamilyName}_Data.json");
                File.WriteAllText(jsonPath,
                    JsonConvert.SerializeObject(_data, Formatting.Indented));

                // Чтение и десериализация данных из JSON
                var deserializedData = JsonConvert.DeserializeObject<FamilyData>(File.ReadAllText(jsonPath));

                // Воссоздание семейства на основе десериализованных данных
                RecreateFamily(deserializedData);

                TaskDialog.Show("Успех", "Воссоздание семейства выполнено успешно!");
            }
            catch (Exception ex)
            {
                var detailed = $"Не удалось воспроизвести семейство: {ex.Message}\n{ex.StackTrace}";
                TaskDialog.Show("Ошибка", detailed);
            }
        }

        /// <summary>
        ///     Проверяет, существует ли параметр с заданным именем в менеджере параметров семейства.
        /// </summary>
        private bool ParameterExists(FamilyManager familyManager, string paramName)
        {
            return familyManager.Parameters.Cast<FamilyParameter>()
                .Any(p => p.Definition.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Основной метод воссоздания семейства из переданных данных.
        /// </summary>
        /// <param name="familyData">Данные семейства для воссоздания.</param>
        private void RecreateFamily(FamilyData familyData)
        {
            // Создание нового документа семейства на основе шаблона
            var familyDoc = _app.NewFamilyDocument(
                _app.FamilyTemplatePath + @"\Metric Generic Model.rft"); // Нужно взять свой

            using (var trans = new Transaction(familyDoc, "Воссоздание семейства"))
            {
                trans.Start();

                // Установка или создание типа семейства по умолчанию
                if (!familyDoc.FamilyManager.Types.Cast<FamilyType>().Any())
                {
                    var newType = familyDoc.FamilyManager.NewType("DefaultType");
                    familyDoc.FamilyManager.CurrentType = newType;
                }
                else
                {
                    familyDoc.FamilyManager.CurrentType = familyDoc.FamilyManager.Types.Cast<FamilyType>().First();
                }

                // Добавление параметров в семейство, если их ещё нет
                // Обязательно убедитесь, что категория семейства задана
                if (familyDoc.OwnerFamily.FamilyCategory == null)
                {
                    familyDoc.OwnerFamily.FamilyCategory =
                        familyDoc.Settings.Categories.get_Item(BuiltInCategory.OST_GenericModel);
                }

                // Добавление параметров
                foreach (var paramData in familyData.Parameters)
                {
                    if (string.IsNullOrWhiteSpace(paramData.Name))
                        continue;

                    if (!ParameterExists(familyDoc.FamilyManager, paramData.Name))
                    {
                        try
                        {
                            var fp = familyDoc.FamilyManager.AddParameter(
                                paramData.Name,
                                groupTypeId: GroupTypeId.Constraints,
                                specTypeId: SpecTypeId.Length,
                                paramData.IsInstance);

                            // Установка значения или формулы
                            if (!string.IsNullOrEmpty(paramData.Formula))
                            {
                                familyDoc.FamilyManager.SetFormula(fp, paramData.Formula);
                            }
                            else if (paramData.Value != null)
                            {
                                switch (fp.StorageType)
                                {
                                    case StorageType.Double:
                                        familyDoc.FamilyManager.Set(fp, Convert.ToDouble(paramData.Value));
                                        break;
                                    case StorageType.Integer:
                                        familyDoc.FamilyManager.Set(fp, Convert.ToInt32(paramData.Value));
                                        break;
                                    case StorageType.String:
                                        familyDoc.FamilyManager.Set(fp, paramData.Value.ToString());
                                        break;
                                    case StorageType.ElementId:
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Error",
                                $"Ошибка при добавлении параметра {paramData.Name}:\n{ex.Message}");
                        }
                    }
                }

                // Сбор уже существующих опорных плоскостей по имени
                    var refPlanes = new Dictionary<string, ReferencePlane>();
                var collector = new FilteredElementCollector(familyDoc).OfClass(typeof(ReferencePlane));
                foreach (ReferencePlane rp in collector)
                    if (!refPlanes.ContainsKey(rp.Name))
                        refPlanes[rp.Name] = rp;

                // Создание новых опорных плоскостей, если их нет
                foreach (var planeData in familyData.ReferencePlanes)
                    if (!refPlanes.ContainsKey(planeData.Name))
                    {
                        var origin = new XYZ(planeData.Origin.X, planeData.Origin.Y, planeData.Origin.Z);
                        var normal = new XYZ(planeData.Normal.X, planeData.Normal.Y, planeData.Normal.Z);
                        var up = new XYZ(0, 0, 1);

                        var newPlane = CreateReferencePlane(familyDoc, normal, origin, up, planeData.Name);
                        refPlanes[planeData.Name] = newPlane;
                    }

                // Воссоздание геометрии (выдавливание)
                foreach (var geomData in familyData.Geometries)
                {
                    var normal = new XYZ(
                        geomData.SketchPlaneNormal.X,
                        geomData.SketchPlaneNormal.Y,
                        geomData.SketchPlaneNormal.Z);

                    var origin = new XYZ(
                        geomData.SketchPlaneOrigin.X,
                        geomData.SketchPlaneOrigin.Y,
                        geomData.SketchPlaneOrigin.Z);

                    var plane = Plane.CreateByNormalAndOrigin(normal, origin);
                    var sketchPlane = SketchPlane.Create(familyDoc, plane);

                    var profile = new CurveArrArray();
                    var curveArray = new CurveArray();

                    // Формирование контура выдавливания
                    foreach (var curveData in geomData.Profile)
                    {
                        var rawStart = new XYZ(curveData.StartPoint.X, curveData.StartPoint.Y, curveData.StartPoint.Z);
                        var rawEnd = new XYZ(curveData.EndPoint.X, curveData.EndPoint.Y, curveData.EndPoint.Z);

                        var start = ProjectOntoPlane(rawStart, plane);
                        var end = ProjectOntoPlane(rawEnd, plane);

                        if (start.DistanceTo(end) < _app.ShortCurveTolerance)
                            continue;

                        curveArray.Append(Line.CreateBound(start, end));
                    }

                    if (curveArray.Size == 0)
                        throw new Exception("Контур выдавливания пуст. Невозможно создать геометрию.");

                    // Замыкаем контур, если не замкнут
                    var first = curveArray.get_Item(0).GetEndPoint(0);
                    var last = curveArray.get_Item(curveArray.Size - 1).GetEndPoint(1);
                    if (!first.IsAlmostEqualTo(last)) curveArray.Append(Line.CreateBound(last, first));

                    profile.Append(curveArray);

                    var exstart = geomData.ExtrusionStart;
                    var exend = geomData.ExtrusionEnd;
                    var extrusionHeight = exend - exstart;

                    if (extrusionHeight <= 0)
                        throw new Exception("Высота выдавливания должна быть больше нуля.");

                    // Сдвигаем плоскость для начала выдавливания
                    var moveVec = normal.Multiply(exstart);
                    ElementTransformUtils.MoveElement(familyDoc, sketchPlane.Id, moveVec);

                    var extrusion = familyDoc.FamilyCreate.NewExtrusion(
                        geomData.IsSolid, profile, sketchPlane, extrusionHeight);

                    // Привязка параметров начала выдавливания к параметрам семейства
                    if (!string.IsNullOrEmpty(geomData.ExtrusionStartParam))
                    {
                        var startParam = familyDoc.FamilyManager.Parameters
                            .Cast<FamilyParameter>()
                            .FirstOrDefault(p => p.Definition.Name == geomData.ExtrusionStartParam);

                        var start = extrusion.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM);

                        if (start != null && startParam != null)
                        {
                            if (start.StorageType == StorageType.Double)
                                start.Set(geomData.ExtrusionStart);

                            if (familyDoc.FamilyManager.CanElementParameterBeAssociated(start))
                                familyDoc.FamilyManager.AssociateElementParameterToFamilyParameter(start, startParam);
                        }
                    }

                    // Привязка параметров конца выдавливания к параметрам семейства
                    if (!string.IsNullOrEmpty(geomData.ExtrusionEndParam))
                    {
                        var endParam = familyDoc.FamilyManager.Parameters
                            .Cast<FamilyParameter>()
                            .FirstOrDefault(p => p.Definition.Name == geomData.ExtrusionEndParam);

                        var end = extrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM);

                        if (end != null && endParam != null)
                        {
                            if (end.StorageType == StorageType.Double)
                                end.Set(geomData.ExtrusionEnd);

                            if (familyDoc.FamilyManager.CanElementParameterBeAssociated(end))
                                familyDoc.FamilyManager.AssociateElementParameterToFamilyParameter(end, endParam);
                        }
                    }
                }

                familyDoc.Regenerate();

                // Воссоздание размеров, если есть данные
                if (familyData.Dimensions != null && familyData.Dimensions.Count > 0)
                    RecreateDimensions(familyDoc, familyData.Dimensions);

                trans.Commit();
            }

            // Генерация уникального имени и сохранение семейства
            var uniqueName =
                $"Воссозданное семейство_{Guid.NewGuid().ToString().Substring(0, 8)}_{familyData.FamilyName}.rfa";
            var familyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                uniqueName);

            familyDoc.SaveAs(familyPath, new SaveAsOptions { OverwriteExistingFile = true });

            OnFamilySaved?.Invoke(familyPath);

            // Закрытие открытых документов с исходным именем семейства, чтобы избежать конфликтов
            foreach (Document openDoc in _app.Documents)
            {
                if (!openDoc.IsModifiable || openDoc.IsLinked) continue;

                if (openDoc.Title.Equals($"{familyData.FamilyName}", StringComparison.OrdinalIgnoreCase))
                    openDoc.Close(false);
            }
        }

        /// <summary>
        ///     Воссоздаёт размеры семейства на основе переданных данных.
        /// </summary>
        /// <param name="familyDoc">Документ семейства.</param>
        /// <param name="dimensions">Список данных размеров.</param>
        private void RecreateDimensions(Document familyDoc, List<DimensionData> dimensions)
        {
            const double tolerance = 0.001;

            // Получаем первый план этажа (FloorPlan)
            var view = new FilteredElementCollector(familyDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate);

            if (view == null) return;

            foreach (var dimensionData in dimensions)
                try
                {
                    if (dimensionData.References == null || dimensionData.References.Count < 2) continue;

                    ReferencePlane centerPlane = null;
                    var centerPlaneRefData = dimensionData.References
                        .FirstOrDefault(r =>
                            r.GeometryType == "ReferencePlane" && !string.IsNullOrEmpty(r.ReferencePlaneName));

                    if (centerPlaneRefData != null)
                        centerPlane = new FilteredElementCollector(familyDoc)
                            .OfClass(typeof(ReferencePlane))
                            .Cast<ReferencePlane>()
                            .FirstOrDefault(rp => rp.Name == centerPlaneRefData.ReferencePlaneName);

                    var allModelCurves = new FilteredElementCollector(familyDoc)
                        .OfClass(typeof(CurveElement))
                        .Cast<CurveElement>()
                        .Where(ce => ce is ModelCurve)
                        .Cast<ModelCurve>()
                        .ToList();

                    var matchedRefs = new List<(Reference Ref, XYZ MidPoint)>();

                    // Поиск кривых, соответствующих данным ребрам
                    foreach (var refData in dimensionData.References)
                        if (refData.GeometryType == "Edge" && refData.StartPoint != null && refData.EndPoint != null)
                        {
                            ModelCurve matchedCurve = null;

                            foreach (var mc in allModelCurves)
                                if (mc.GeometryCurve is Line line)
                                {
                                    var matchNormal =
                                        line.GetEndPoint(0).IsAlmostEqualTo(
                                            new XYZ(refData.StartPoint.X, refData.StartPoint.Y, refData.StartPoint.Z),
                                            tolerance)
                                        && line.GetEndPoint(1).IsAlmostEqualTo(
                                            new XYZ(refData.EndPoint.X, refData.EndPoint.Y, refData.EndPoint.Z),
                                            tolerance);

                                    var matchReversed =
                                        line.GetEndPoint(1).IsAlmostEqualTo(
                                            new XYZ(refData.StartPoint.X, refData.StartPoint.Y, refData.StartPoint.Z),
                                            tolerance)
                                        && line.GetEndPoint(0).IsAlmostEqualTo(
                                            new XYZ(refData.EndPoint.X, refData.EndPoint.Y, refData.EndPoint.Z),
                                            tolerance);

                                    if (matchNormal || matchReversed)
                                    {
                                        matchedCurve = mc;
                                        break;
                                    }
                                }

                            if (matchedCurve != null)
                            {
                                var mid = (matchedCurve.GeometryCurve.GetEndPoint(0) +
                                           matchedCurve.GeometryCurve.GetEndPoint(1)) / 2;
                                matchedRefs.Add((matchedCurve.GeometryCurve.Reference, mid));
                            }
                        }

                    if (matchedRefs.Count < 2) continue;

                    var centerOrigin = centerPlane != null ? centerPlane.GetPlane().Origin : new XYZ(0, 0, 0);
                    var sorted = centerPlane != null
                        ? matchedRefs.OrderBy(r => r.MidPoint.DistanceTo(centerOrigin)).ToList()
                        : matchedRefs;

                    var planeZ = centerPlane != null ? centerOrigin.Z : sorted[0].MidPoint.Z;

                    if (centerPlane == null)
                    {
                        // Создаем обычный размер без равных сегментов
                        var refArrayLabeled = new ReferenceArray();
                        refArrayLabeled.Append(sorted[0].Ref);
                        refArrayLabeled.Append(sorted[1].Ref);

                        var startPt = sorted[0].MidPoint;
                        var endPt = sorted[1].MidPoint;

                        var dimLineLabeled = Line.CreateBound(
                            new XYZ(startPt.X, startPt.Y, planeZ),
                            new XYZ(endPt.X, endPt.Y, planeZ));

                        var dimLabeled = familyDoc.FamilyCreate.NewDimension(view, dimLineLabeled, refArrayLabeled);

                        if (!string.IsNullOrEmpty(dimensionData.Label))
                        {
                            var labelParam = familyDoc.FamilyManager.Parameters
                                .Cast<FamilyParameter>()
                                .FirstOrDefault(p => p.Definition.Name == dimensionData.Label);

                            if (labelParam != null)
                                try
                                {
                                    dimLabeled.FamilyLabel = labelParam;
                                }
                                catch
                                {
                                }
                        }
                    }
                    else
                    {
                        // Создаем размер с равеными сегментов относительно базовой плоскости
                        var refArrayEqual = new ReferenceArray();
                        refArrayEqual.Append(centerPlane.GetReference());
                        refArrayEqual.Append(sorted[0].Ref);
                        refArrayEqual.Append(sorted[1].Ref);

                        var startPt = sorted[0].MidPoint;
                        var endPt = sorted[1].MidPoint;

                        var dimLineEqual = Line.CreateBound(
                            new XYZ(startPt.X, startPt.Y, planeZ),
                            new XYZ(endPt.X, endPt.Y, planeZ));

                        var dimEqual = familyDoc.FamilyCreate.NewDimension(view, dimLineEqual, refArrayEqual);

                        if (dimEqual.Segments != null && dimEqual.Segments.Size == 2)
                            dimEqual.AreSegmentsEqual = dimensionData.AreSegmentsEqual;

                        if (!string.IsNullOrEmpty(dimensionData.Label))
                        {
                            var refArrayLabeled = new ReferenceArray();
                            refArrayLabeled.Append(sorted[0].Ref);
                            refArrayLabeled.Append(sorted[1].Ref);

                            var dimLineLabeled = Line.CreateBound(
                                new XYZ(startPt.X, startPt.Y, planeZ),
                                new XYZ(endPt.X, endPt.Y, planeZ));

                            var dimLabeled = familyDoc.FamilyCreate.NewDimension(view, dimLineLabeled, refArrayLabeled);

                            var labelParam = familyDoc.FamilyManager.Parameters
                                .Cast<FamilyParameter>()
                                .FirstOrDefault(p => p.Definition.Name == dimensionData.Label);

                            if (labelParam != null)
                                try
                                {
                                    dimLabeled.FamilyLabel = labelParam;
                                }
                                catch
                                {
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // При ошибках при воссоздании размерных линий выводим сообщение в консоль
                    Debug.WriteLine($"Ошибка при создании размерной линии: {ex.Message}");
                }
        }

        /// <summary>
        ///     Создаёт новую базовую плоскость с заданными параметрами.
        /// </summary>
        /// <param name="doc">Документ семейства.</param>
        /// <param name="normal">Нормаль плоскости.</param>
        /// <param name="origin">Точка начала плоскости.</param>
        /// <param name="bubbleEnd">Направление пузырька (визуального индикатора).</param>
        /// <param name="name">Имя плоскости.</param>
        /// <returns>Созданная базовая плоскость.</returns>
        private ReferencePlane CreateReferencePlane(Document doc, XYZ normal, XYZ origin, XYZ bubbleEnd, string name)
        {
            using (var tr = new Transaction(doc, "Создание базовой плоскости"))
            {
                tr.Start();
                var rp = doc.FamilyCreate.NewReferencePlane(origin, origin + normal, bubbleEnd, doc.ActiveView);
                rp.Name = name;
                tr.Commit();
                return rp;
            }
        }

        /// <summary>
        ///     Проецирует точку на плоскость.
        /// </summary>
        /// <param name="point">Исходная точка.</param>
        /// <param name="plane">Плоскость.</param>
        /// <returns>Проекция точки на плоскость.</returns>
        private XYZ ProjectOntoPlane(XYZ point, Plane plane)
        {
            var v = point - plane.Origin;
            var dist = v.DotProduct(plane.Normal);
            return point - plane.Normal.Multiply(dist);
        }
    }
}