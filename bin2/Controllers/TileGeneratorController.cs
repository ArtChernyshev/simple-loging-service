using System;
using System.Collections.Generic;
using System.Net;
using System.Web.Http;
using CommonLibrary;
using DataAccess;
using DomainModel.Exceptions;
using ServiceStack.Mvc;
using TileLibrary;

namespace TileServer.Controllers
{
	public class TileGeneratorController : ServiceStackController
	{
		private readonly ITileStorage _tileStorage;
		private readonly Lazy<TileGenerator> _tileGenerator;

		public TileGeneratorController(Lazy<TileGenerator> tileGenerator, ITileStorage tileStorage)
		{
			_tileGenerator = tileGenerator;
			_tileStorage = tileStorage;
		}

		// GET api/tilegenerator
		public IEnumerable<string> Get()
		{
			// тут можно возвращать текущее состояние тайлогенератора
			throw new HttpResponseException(HttpStatusCode.NotImplemented);
		}

		/// <summary>
		/// Сгенерировать тайлы
		/// </summary>
		/// <param name="extent">Экстент (в географических координатах)</param>
		/// <param name="featureClasses">Список классов карты</param>
		//// TODO можно добавить кастомный modelbinder для Envelope, чтобы объявить параметры более типизировано.
		public void Post(string extent, [FromUri]string[] featureClasses)
		{
			if (string.IsNullOrEmpty(extent) || featureClasses == null)
				throw new HttpResponseException(HttpStatusCode.BadRequest);

			try
			{
				var geoExtent = NtsExtensions.ParseEnvelope(extent);
				var projectionConverter = new ProjectionConverter(ProjectionId.Geographic, ProjectionId.WorldMercator);
				var projectedExtent = projectionConverter.Convert(geoExtent);

				var tileSequence = _tileGenerator.Value.GenerateTiles(projectedExtent, featureClasses, MercatorTileGrid.MinScaleLevel, MercatorTileGrid.MaxScaleLevel);
				SaveTiles(tileSequence);
			}
			catch (ArgumentException)
			{
				throw new HttpResponseException(HttpStatusCode.BadRequest);
			}
			catch (FormatException)
			{
				throw new HttpResponseException(HttpStatusCode.BadRequest);
			}
			catch (FeatureClassNotFoundException)
			{
				throw new HttpResponseException(HttpStatusCode.NotFound);
			}
		}

		/// <summary>
		/// Сгенерировать тайлы
		/// </summary>
		/// <param name="projectSyscode">Сискод проекта (бранча)</param>
		/// <param name="featureClasses">Список классов карты</param>
		public void Post(long projectSyscode, [FromUri]string[] featureClasses)
		{
			if (featureClasses == null)
				throw new HttpResponseException(HttpStatusCode.BadRequest);

			try
			{
				var tileSequence = _tileGenerator.Value.GenerateTiles(projectSyscode, featureClasses, MercatorTileGrid.MinScaleLevel, MercatorTileGrid.MaxScaleLevel);
				SaveTiles(tileSequence);
			}
			catch (ArgumentException)
			{
				throw new HttpResponseException(HttpStatusCode.BadRequest);
			}
			catch (FormatException)
			{
				throw new HttpResponseException(HttpStatusCode.BadRequest);
			}
			catch (FeatureClassNotFoundException)
			{
				throw new HttpResponseException(HttpStatusCode.NotFound);
			}
		}

		private void SaveTiles(IEnumerable<GeometryAndAttributeTiles> tileSequence)
		{
			foreach (var tiles in tileSequence)
			{
				_tileStorage.SaveTile(tiles.GeometryTile);
				if (tiles.AttributeTiles != null)
				{
					foreach (var attributeTile in tiles.AttributeTiles)
					{
						_tileStorage.SaveTile(attributeTile);
					}
				}
			}
		}
	}
}
