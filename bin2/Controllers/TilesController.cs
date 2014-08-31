using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using DataAccess;
using DomainModel;
using DomainModel.Exceptions;

//using NLog;
using ServiceStack.Mvc;
using TileLibrary;

// TODO может возникнуть ситуация гонки при генерации тайлов, когда по запросам нескольких клиентов
// начнуть генериться тайлы для одной области. Вообщем-то не сильно страшно.
// Решается очередью запросов на генерациию.
namespace TileServer.Controllers
{
	public class TilesController : ServiceStackController
	{
	//	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		private readonly ITileStorage _tileStorage;
		private readonly Lazy<TileGenerator> _tileGenerator;

		public TilesController(Lazy<TileGenerator> tileGenerator, ITileStorage tileStorage)
		{
			_tileGenerator = tileGenerator;
			_tileStorage = tileStorage;
		}

		// GET api/tiles
		public IEnumerable<string> Get()
		{
			throw new HttpResponseException(HttpStatusCode.NotImplemented);
		}

		public HttpResponseMessage Get(TileAddress address)
		{
			if (address == null)
				throw new HttpResponseException(HttpStatusCode.BadRequest);

			Tile tile;

			try
			{
				tile = _tileStorage.LoadTile(address);
				if (tile == null)
				{
					// Хэш проставляется после сохранения тайла в хранилище
					// TODO тут проблема: по запросу на геометрический тайл генерится и 
					// сохраняется в хранилище два тайла: собственно геом-тайл и компонент-аттр-тайл.
					// Если запросы на геом и комп-аттр тайлы приходят параллельно, то комп-аттр тайл
					// может вернуться пустой, т.к. к этому моменту он может быть не создан.
					// Нужно комп-аттр-тайл генерить независимо от геом-тайла.
					if (address.IsGeom)
					{
						var geomTile = _tileGenerator.Value.CreateGeometryTile(address);
						tile = _tileStorage.SaveTile(geomTile.GeometryTile);
						if (geomTile.AttributeTiles != null)
						{
							foreach (var attrTile in geomTile.AttributeTiles)
							{
								_tileStorage.SaveTile(attrTile);
							}
						}
					}
					else
					{
						var attrTile = _tileGenerator.Value.CreateAttributeTile(address);
						tile = _tileStorage.SaveTile(attrTile);
					}
				}
			}
			catch (FeatureClassNotFoundException)
			{
				throw new HttpResponseException(HttpStatusCode.BadRequest);
			}
			catch (AttributeNotFoundException)
			{
				throw new HttpResponseException(HttpStatusCode.BadRequest);
			}

			// Реализуем условный get.
			var response = new HttpResponseMessage();

			// Это нужно только на asp.net development server, поскольку
			// он подавляет etag из-за отсутствия этого заголовка. На IIS ок.
			response.Headers.CacheControl = new CacheControlHeaderValue
			{
				Public = true,
				MaxAge = TimeSpan.FromSeconds(0)
			};

			var etagValue = string.Concat("\"", tile.Address.Hash, "\"");

			//if (Request.Headers.IfNoneMatch.Any(x => string.CompareOrdinal(x.Tag, etagValue) == 0))
			//{
			//	response.StatusCode = HttpStatusCode.NotModified;
			//}
			//else
			//{
				response.StatusCode = HttpStatusCode.OK;
				response.Content = new ByteArrayContent(tile.Content);
				response.Headers.ETag = new EntityTagHeaderValue(etagValue);
			//}

			return response;
		}
	}
}
