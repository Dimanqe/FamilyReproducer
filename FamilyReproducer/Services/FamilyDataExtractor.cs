#region

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamilyReproducer.Models;

#endregion

namespace FamilyReproducer.Services
{
    /// <summary>
    ///     Класс для извлечения данных из семейства Revit:
    ///     параметров, геометрии и размерных линий.
    /// </summary>
    public class FamilyDataExtractor
    {
        private readonly UIApplication _uiApp;

        /// <summary>
        ///     Конструктор с передачей UIApplication.
        /// </summary>
        public FamilyDataExtractor(UIApplication uiApp)
        {
            _uiApp = uiApp;
        }

        /// <summary>
        ///     Открывает файл семейства и извлекает из него данные.
        /// </summary>
        /// <param name="familyFilePath">Путь к файлу семейства (*.rfa).</param>
        /// <returns>Объект FamilyData с параметрами, геометрией и размерами.</returns>
        public FamilyData ExtractParameters(string familyFilePath)
        {
            Document familyDoc = null;

            try
            {
                familyDoc = _uiApp.Application.OpenDocumentFile(familyFilePath);

                if (!familyDoc.IsFamilyDocument)
                    throw new Exception("Выбранный файл не является файлом семейства.");

                return ExtractFamilyDataFromDoc(familyDoc);
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось извлечь параметры: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Основной метод извлечения данных из документа семейства.
        /// </summary>
        private FamilyData ExtractFamilyDataFromDoc(Document familyDoc)
        {
            return new FamilyData
            {
                FamilyName = familyDoc.Title,
                Parameters = ExtractParametersFromDoc(familyDoc),
                Geometries = ExtractGeometriesFromDoc(familyDoc),
                Dimensions = ExtractDimensionsFromDoc(familyDoc)
            };
        }

        /// <summary>
        ///     Извлекает параметры семейства.
        /// </summary>
        private List<ParameterData> ExtractParametersFromDoc(Document familyDoc)
        {
            var paramList = new List<ParameterData>();
            var fm = familyDoc.FamilyManager;

            foreach (FamilyParameter param in fm.Parameters)
            {
                var dataType = param.Definition.GetDataType();
                var typeId = dataType != null ? dataType.TypeId : null;

                paramList.Add(new ParameterData
                {
                    Name = param.Definition.Name,
                    Type = typeId,
                    IsInstance = param.IsInstance,
                    Group = param.Definition.ParameterGroup,
                    Value = GetParameterValue(fm, param)
                });
            }

            return paramList;
        }

        /// <summary>
        ///     Извлекает геометрию (экструзии) из семейства.
        /// </summary>
        private List<GeometryData> ExtractGeometriesFromDoc(Document familyDoc)
        {
            var geometries = new List<GeometryData>();

            var extrusions = new FilteredElementCollector(familyDoc)
                .OfClass(typeof(Extrusion))
                .Cast<Extrusion>();

            foreach (var extrusion in extrusions)
                try
                {
                    var sketch = extrusion.Sketch;
                    if (sketch == null || sketch.Profile == null)
                        continue;

                    var plane = sketch.SketchPlane?.GetPlane();
                    var normal = plane?.Normal ?? XYZ.BasisZ;
                    var origin = plane?.Origin ?? XYZ.Zero;

                    var geometry = new GeometryData
                    {
                        SketchPlane = sketch.SketchPlane?.Name ?? "Unnamed",
                        SketchPlaneOrigin = new PointData { X = origin.X, Y = origin.Y, Z = origin.Z },
                        SketchPlaneNormal = new PointData { X = normal.X, Y = normal.Y, Z = normal.Z },
                        ExtrusionDirection = new PointData { X = normal.X, Y = normal.Y, Z = normal.Z },
                        ExtrusionStart = extrusion.StartOffset,
                        ExtrusionEnd = extrusion.EndOffset,
                        ExtrusionStartParam =
                            GetAssociatedFamilyParameterName(
                                extrusion.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM), familyDoc) ?? "",
                        ExtrusionEndParam =
                            GetAssociatedFamilyParameterName(
                                extrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM), familyDoc) ?? "",
                        BooleanOperation = extrusion.IsSolid ? "Join" : "Cut",
                        Profile = ExtractProfile(sketch),
                        IsSolid = extrusion.IsSolid
                    };

                    geometries.Add(geometry);
                }
                catch (Exception)
                {
                }

            return geometries;
        }

        /// <summary>
        ///     Извлекает профиль (контуры) экструзии из Sketch.
        /// </summary>
        private List<CurveData> ExtractProfile(Sketch sketch)
        {
            var profile = new List<CurveData>();
            const double minLength = 1e-6;

            foreach (CurveArray curveArray in sketch.Profile)
            foreach (Curve curve in curveArray)
                if (curve is Line line)
                {
                    var start = line.GetEndPoint(0);
                    var end = line.GetEndPoint(1);

                    if (!start.IsAlmostEqualTo(end) && line.Length > minLength)
                        profile.Add(new CurveData
                        {
                            StartPoint = new PointData { X = start.X, Y = start.Y, Z = start.Z },
                            EndPoint = new PointData { X = end.X, Y = end.Y, Z = end.Z },
                            IsReferenceLine = false
                        });
                }

            return profile;
        }

        /// <summary>
        ///     Получает имя параметра семейства, связанного с данным параметром.
        /// </summary>
        private string GetAssociatedFamilyParameterName(Parameter param, Document doc)
        {
            if (param == null) return null;

            var fm = doc.FamilyManager;
            var associated = fm.GetAssociatedFamilyParameter(param);
            return associated?.Definition?.Name;
        }

        /// <summary>
        ///     Получает значение параметра для текущего типа семейства.
        /// </summary>
        private object GetParameterValue(FamilyManager fm, FamilyParameter param)
        {
            if (param == null) return null;

            var familyType = fm.CurrentType ?? fm.Types.Cast<FamilyType>().FirstOrDefault();
            if (familyType == null) return null;

            try
            {
                switch (param.StorageType)
                {
                    case StorageType.Double:
                        return familyType.AsDouble(param);
                    case StorageType.Integer:
                        return familyType.AsInteger(param);
                    case StorageType.String:
                        return familyType.AsString(param);
                    case StorageType.ElementId:
                        return familyType.AsElementId(param)?.IntegerValue;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        /// <summary>
        ///     Извлекает размерные линии и их ссылки из семейства.
        /// </summary>
        private List<DimensionData> ExtractDimensionsFromDoc(Document familyDoc)
        {
            var dimensions = new List<DimensionData>();

            var dimCollector = new FilteredElementCollector(familyDoc)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>();

            foreach (var dim in dimCollector)
                try
                {
                    var references = new List<ReferenceData>();

                    foreach (Reference reference in dim.References)
                    {
                        var refElem = familyDoc.GetElement(reference.ElementId);
                        GeometryObject geoObj = null;
                        var geometryType = "Unknown";
                        var planeName = "";

                        if (refElem is ReferencePlane refPlane)
                        {
                            geometryType = "ReferencePlane";
                            planeName = refPlane.Name;
                        }

                        if (refElem is ModelCurve modelCurve)
                        {
                            geoObj = modelCurve.GeometryCurve;
                            geometryType = "Edge";
                        }
                        else
                        {
                            geoObj = refElem?.GetGeometryObjectFromReference(reference);

                            if (geoObj is Edge) geometryType = "Edge";
                            else if (geoObj is Face) geometryType = "Face";
                        }

                        var refData = new ReferenceData
                        {
                            ReferencePlaneName = planeName,
                            SketchOwnerId = refElem?.Id?.ToString(),
                            GeometryType = geometryType
                        };

                        if (geoObj is Curve curve)
                        {
                            var start = curve.GetEndPoint(0);
                            var end = curve.GetEndPoint(1);

                            refData.StartPoint = new PointData { X = start.X, Y = start.Y, Z = start.Z };
                            refData.EndPoint = new PointData { X = end.X, Y = end.Y, Z = end.Z };
                        }
                        else if (geoObj is Face face)
                        {
                            var bb = face.GetBoundingBox();
                            var uvCenter = (bb.Min + bb.Max) / 2;
                            var center = face.Evaluate(uvCenter);

                            refData.FaceCenter = new PointData { X = center.X, Y = center.Y, Z = center.Z };
                        }

                        references.Add(refData);
                    }

                    dimensions.Add(new DimensionData
                    {
                        Label = dim.FamilyLabel?.Definition?.Name ?? "",
                        Value = dim.ValueString,
                        AreSegmentsEqual = dim.AreSegmentsEqual,
                        References = references
                    });
                }
                catch (Exception)
                {
                }

            return dimensions;
        }
    }
}