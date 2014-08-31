using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DataAccess;
using DomainModel;
using DomainModel.Exceptions;
using GeoAPI.Geometries;
//using NLog;
using TileLibrary;

namespace TileServer
{
	/// <summary>
	/// Создаватель тайлов.
	/// В качестве источника данных служит база данных с указанной строкой подключения.
	/// </summary>
	public class TileGenerator
	{
		//private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		private readonly IFeatureClassRepository _featureClassRepository;
		private readonly IFeatureReadonlyRepository _featureRepository;
		private readonly IPresentationRepository _presentationRepository;

		private readonly ILabelingStyleRepository _labelingStyleRepository;

		private readonly IGeometryForTilePreparer _preparer;

		/// <summary>
		/// Инициализирует новый экземпляр генератора тайлов.
		/// </summary>
		/// <param name="featureClassRepository">Репозиторий фичеклассов.</param>
		/// <param name="featureRepository">Источник данных для тайлов</param>
		/// <param name="presentationRepository">Репозиторий представлений (для атрибутивных тайлов)</param>
		/// <param name="labelingStyleRepository">Репозиторий стилей лейблинга слоёв (для атрибутивных тайлов)</param>
		/// <param name="preparer">Обрезает геометрию по тайлу, упрощает и т.п.</param>
		public TileGenerator(
			IFeatureClassRepository featureClassRepository,
			IFeatureReadonlyRepository featureRepository,
			IPresentationRepository presentationRepository,
			ILabelingStyleRepository labelingStyleRepository,
			IGeometryForTilePreparer preparer)
		{
			if (featureClassRepository == null)
				throw new ArgumentNullException("featureClassRepository");

			if (featureRepository == null)
				throw new ArgumentNullException("featureRepository");

			if (presentationRepository == null)
				throw new ArgumentNullException("presentationRepository");

			if (labelingStyleRepository == null)
				throw new ArgumentNullException("labelingStyleRepository");

			if (preparer == null)
				throw new ArgumentNullException("preparer");

			_featureClassRepository = featureClassRepository;
			_featureRepository = featureRepository;
			_presentationRepository = presentationRepository;
			_labelingStyleRepository = labelingStyleRepository;
			_preparer = preparer;
		}

		/// <summary>
		/// Сгенерировать тайлы заданных классов фич для заданного экстента карты.
		/// Генерирует только геометрические тайлы (+атрибуты компонент).
		/// </summary>
		/// <param name="mapExtent">Экстент карты</param>
		/// <param name="featureClassNames">Классы карты</param>
		/// <param name="fromScaleLevel">Уровень масштаба, с которого начинается генерация.</param>
		/// <param name="toScaleLevel">Уровень масштаба, до которого генерируются тайлы.</param>
		/// <returns>Ленивый перечислитель по тайлам</returns>
		public IEnumerable<GeometryAndAttributeTiles> GenerateTiles(IEnvelope mapExtent, IList<string> featureClassNames, int fromScaleLevel, int toScaleLevel)
		{
			return GenerateTiles(mapExtent, featureClassNames, fromScaleLevel, toScaleLevel, null);
		}

		/// <summary>
		/// Сгенерировать тайлы заданных классов фич для заданного проекта.
		/// Генерирует геометрические и атрибутивные тайлы (те, которые используются в слоях представлений, привязанных к проекту).
		/// </summary>
		/// <param name="projectSyscode">Сискод проекта (бранча)</param>
		/// <param name="featureClassNames">Классы карты</param>
		/// <param name="fromScaleLevel">Уровень масштаба, с которого начинается генерация.</param>
		/// <param name="toScaleLevel">Уровень масштаба, до которого генерируются тайлы.</param>
		/// <returns>Ленивый перечислитель по тайлам</returns>
		public IEnumerable<GeometryAndAttributeTiles> GenerateTiles(long projectSyscode, IList<string> featureClassNames, int fromScaleLevel, int toScaleLevel)
		{
			var presentations = _presentationRepository.LoadPresentations(projectSyscode);

			var featureClassCollection = _featureClassRepository.LoadFeatureClasses(false);
			var mapProject = _featureRepository.GetFeatures(featureClassCollection.GetFeatureClassByName(Names.MapProjectFeatureClass), 0, 1000)
				.FirstOrDefault(x => ((DirectoryValue)x.GetAttribute(Names.BranchDirectoryClass).Value).SysCode == projectSyscode);
			if (mapProject == null)
				throw new ArgumentException("projectSyscode");

			return GenerateTiles(mapProject.Geom.EnvelopeInternal, featureClassNames, fromScaleLevel, toScaleLevel, presentations);
		}

		/// <summary>
		/// Создает геометрический тайл с заданным адресом.
		/// Геометрия обрезается по границам окружающего прямоугольника тайла.
		/// В результате разрезания геометрия может стать невалидной, это нормально.
		/// Но нужно это учитывать при дальнейшей работе с ней.
		/// </summary>
		/// <param name="tileAddress">Адрес создаваемого тайла.</param>
		/// <returns>Созданный геометрический тайл.</returns>
		public GeometryAndAttributeTiles CreateGeometryTile(TileAddress tileAddress)
		{
			if (tileAddress == null)
				throw new ArgumentNullException("tileAddress");

			if (!tileAddress.IsGeom)
				throw new ArgumentException("Tile is not a geometry tile");

			var featureClass = _featureClassRepository.LoadFeatureClasses(false).GetFeatureClassByName(tileAddress.FeatureClassName);
			if (featureClass == null)
				throw new FeatureClassNotFoundException();

			var newTileAddress = new TileAddress(
				tileAddress.ScaleLevel, tileAddress.Col, tileAddress.Row, featureClass.Name, Feature.GeomAttrName, TileAddress.NullHash);

			return CreateGeometryTile(newTileAddress, featureClass);
		}

		/// <summary>
		/// Создает атрибутивный тайл с указанным адресом.
		/// </summary>
		/// <param name="tileAddress">Адрес создаваемого тайла.</param>
		/// <returns>Созданный атрибутивный тайл.</returns>
		public AttributeTile CreateAttributeTile(TileAddress tileAddress)
		{
			if (tileAddress == null)
				throw new ArgumentNullException("tileAddress");

			if (tileAddress.IsGeom)
				throw new ArgumentException("Tile is not an attribute tile");

			var featureClass = _featureClassRepository.LoadFeatureClasses(false).GetFeatureClassByName(tileAddress.FeatureClassName);
			if (featureClass == null)
				throw new FeatureClassNotFoundException();

			return CreateAttributeTile(tileAddress, featureClass);
		}

		private IEnumerable<GeometryAndAttributeTiles> GenerateTiles(
			IEnvelope mapExtent, IList<string> featureClassNames, int fromScaleLevel, int toScaleLevel, PresentationCollection presentations)
		{
			if (mapExtent == null)
				throw new ArgumentNullException("mapExtent");

			if (featureClassNames == null)
				throw new ArgumentNullException("featureClassNames");

			var featureClassCollection = _featureClassRepository.LoadFeatureClasses(false);

			var featureClasses = featureClassCollection
				.GetFeatureClassesByNames(featureClassNames)
				.Select(x => featureClassCollection.GetParentFeatureClassById(x.Id))
				.ToList();

			if (featureClasses.Count != featureClassNames.Count())
				throw new ArgumentException("featureClassNames");

		//	Log.Info("Generating tiles...");

			foreach (var featureClass in featureClasses)
			{
		//		Log.Info("FeatureClass {0}:", featureClass.Name);

				string[] attributes = null;
				if (presentations != null)
					attributes = GetAttrs(presentations, featureClass, featureClassCollection).Distinct().ToArray();

				for (int scaleLevel = fromScaleLevel; scaleLevel <= toScaleLevel; scaleLevel++)
				{
			//		Log.Info("Scale level: {0}", scaleLevel);
					var tiledMapExtent = TileOperations.Filter(featureClass.MinVisibleLevel, mapExtent, scaleLevel);
					if (tiledMapExtent == null)
						continue;

					for (int row = tiledMapExtent.MinRow; row <= tiledMapExtent.MaxRow; row++)
					{
			//			Log.Info("Processing row {0}", row);

						int minCol = tiledMapExtent.MinCol;
						int maxCol = tiledMapExtent.MaxCol;
						for (int col = minCol; col <= maxCol; col++)
						{
							var geomTileAddress = new TileAddress(scaleLevel, col, row, featureClass.Name, Feature.GeomAttrName, TileAddress.NullHash);
							var tile = CreateGeometryTile(geomTileAddress, featureClass);

							if (attributes != null)
							{
								foreach (var attribute in attributes)
								{
									var attrTileAddress = new TileAddress(scaleLevel, col, row, featureClass.Name, attribute, TileAddress.NullHash);
									var attrTile = CreateAttributeTile(attrTileAddress, featureClass);
									tile.AttributeTiles.Add(attrTile);
								}
							}

							yield return tile;
						}
					}
				}
			}

		//	Log.Info("Done.");
		}

		private IEnumerable<string> GetAttrs(IEnumerable<Presentation> presentations, FeatureClass featureClass, IEnumerable<FeatureClass> featureClasses)
		{
			return presentations.SelectMany(presentation => GetAttrs(presentation, featureClass, featureClasses));
		}

		private List<string> GetAttrs(Presentation presentation, FeatureClass featureClass, IEnumerable<FeatureClass> featureClasses)
		{
			var layers = presentation.Layers.GetByFeatureClassId(featureClass.Id);
			var childClasses = featureClasses.Where(x => x.ParentId == featureClass.Id);
			foreach (var childClass in childClasses)
			{
				layers = layers.Union(presentation.Layers.GetByFeatureClassId(childClass.Id));
			}

			var layersArray = layers.ToArray();

			// свяжем слои со стилями лейблинга
			var labelingStyles = _labelingStyleRepository.LoadLabelingStyles();
			TileOperations.ResolveLayerStylesAndLabeling(layersArray, labelingStyles);

			return TileOperations.GetFilterExpressionAttributes(layersArray, featureClass);
		}

		private GeometryAndAttributeTiles CreateGeometryTile(TileAddress tileAddress, FeatureClass featureClass)
		{
			var stopwatch = Stopwatch.StartNew();
			var tileExtent = tileAddress.GetEnvelope();
			var shapes = _featureRepository.GetGeometry(tileExtent, featureClass);
			stopwatch.Stop();
		//	Log.Debug("GetGeometry({0}): {1} shapes received in {2:0.000}.", tileAddress, shapes.Item1.Count, stopwatch.Elapsed.TotalSeconds);

			stopwatch = Stopwatch.StartNew();
			var componentAttrTiles = new List<TileAttribute>();
			var resultShapes = new Dictionary<int, List<TileShape>>();
			foreach (var shapeList in shapes.Item1)
			{
				var result = _preparer.GeometryToTile(tileAddress, featureClass, shapeList.Value);
				resultShapes.Add(shapeList.Key, result.Item1);
				componentAttrTiles.AddRange(result.Item2);
			}

			stopwatch.Stop();
	//		Log.Debug("CreateGeometryTile({0}): tile created in {1:0.000}.", tileAddress, stopwatch.Elapsed.TotalSeconds);

			var coordinateSize = CoordinateConverter.BytesPerCoordinate(tileAddress.ScaleLevel, tileAddress.ScaleLevel < featureClass.MinNongeneralizedLevel);
			var geomTile = new GeometryTile(tileAddress, resultShapes, coordinateSize, shapes.Item2);
			AttributeTile attrTile = null;
			if (componentAttrTiles.Count > 0)
			{
				var attrTileAddress = new TileAddress(
					tileAddress.ScaleLevel,
					tileAddress.Col,
					tileAddress.Row,
					tileAddress.FeatureClassName,
					Names.ComponentAttrs,
					TileAddress.NullHash);
				attrTile = new AttributeTile(attrTileAddress, componentAttrTiles.OrderBy(x => x.Id).ToList());
			}

			var resultTile = new GeometryAndAttributeTiles(geomTile);
			if (attrTile != null)
				resultTile.AttributeTiles.Add(attrTile);

			return resultTile;
		}

		// TODO как быть с упрощением в атрибутивных тайлах? Сейчас в тайле храним все атрибуты.
		// Можно удалять из них по идентификатору фич те атрибуты, которые отсутствуют в соответствующем геометрическом тайле.
		private AttributeTile CreateAttributeTile(TileAddress tileAddress, FeatureClass featureClass)
		{
			var stopwatch = Stopwatch.StartNew();
			var tileExtent = tileAddress.GetEnvelope();
			var attrs = _featureRepository.GetAttribute(tileExtent, featureClass, tileAddress.AttrName);

			stopwatch.Stop();
		//	Log.Debug("CreateAttributeTile({0}): {1} tile created in {2:0.000}.", tileAddress, attrs.Count, stopwatch.Elapsed.TotalSeconds);

			return new AttributeTile(tileAddress, attrs);
		}
	}
}
